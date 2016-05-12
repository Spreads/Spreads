using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;
using Spreads.Storage.Aeron;
using Spreads.Storage.Aeron.Logbuffer;
using Spreads.Storage.Aeron.Protocol;
using static Spreads.Storage.Aeron.Logbuffer.LogBufferDescriptor;

namespace Spreads.Storage {

    // Append log is based on Aeron LogBuffers
    // LogBuffers are permament, we always start from current position
    // Spreads protocol ensures that if a slow-producing series
    // has non-flushed mutations that are cleared from the LogBuffers,
    // then on next subscription to the series will force flushing
    // of chunks, after which subscribers will reveive chunk commands
    // and then Flush event, after which they will have syncronized data.

    // TODO should track unflushed series (per partition probably) and flush when partition is cleared

    public delegate void OnAppendHandler(DirectBuffer buffer);

    public interface IAppendLog : IDisposable {
        void Append<T>(T message);
        long Claim(int length, out BufferClaim claim);
        event OnAppendHandler OnAppend;
    }

    internal class AppendLog : IAppendLog {
        private readonly LogBuffers _logBuffers;

        private long _subscriberPosition;
        private readonly HeaderWriter _headerWriter;

        private readonly TermAppender[] _termAppenders = new TermAppender[PARTITION_COUNT];
        private readonly int _initialTermId;
        private readonly int _positionBitsToShift;

        private readonly AsyncAutoResetEvent _cleanEvent = new AsyncAutoResetEvent();
        private readonly Task _cleaner;
        private readonly Task _poller;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _termLengthMask;


        public AppendLog(string filepath, int bufferSizeMb = 10) {
            var bufferSizeInBytes = BitUtil.FindNextPositivePowerOfTwo(bufferSizeMb * 1024 * 1024);

            _logBuffers = new LogBuffers(filepath, bufferSizeInBytes);

            for (int i = 0; i < PARTITION_COUNT; i++) {
                _termAppenders[i] = new TermAppender(_logBuffers.Buffers[i], _logBuffers.Buffers[i + PARTITION_COUNT]);
            }
            _termLengthMask = _logBuffers.TermLength - 1;
            _positionBitsToShift = BitUtil.NumberOfTrailingZeros(_logBuffers.TermLength);
            _initialTermId = InitialTermId(_logBuffers.LogMetaData);
            var defaultHeader = DefaultFrameHeader(_logBuffers.LogMetaData);
            _headerWriter = new HeaderWriter(defaultHeader);

            _subscriberPosition = Position;
            Trace.Assert(_subscriberPosition == Position);

            _cleaner = Task.Factory.StartNew(async () => {
                while (!_cts.IsCancellationRequested) {
                    // try to clean every second
                    await _cleanEvent.WaitAsync(1000);
                    CleanLogBuffer();
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

            _poller = Task.Factory.StartNew(() => {
                while (!_cts.IsCancellationRequested) {
                    Poll();
                    // TODO try waithandle as in IpcLongIncrementListener
                    Thread.SpinWait(1);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public event OnAppendHandler OnAppend;
        public event ErrorHandler OnError;

        public long Claim(int length, out BufferClaim claim)
        {
            while (true)
            {
                var partitionIndex = ActivePartitionIndex(_logBuffers.LogMetaData);
                var termAppender = _termAppenders[partitionIndex];
                long rawTail = termAppender.RawTailVolatile;
                long termOffset = rawTail & 0xFFFFFFFFL;

                long position = ComputeTermBeginPosition(TermId(rawTail), _positionBitsToShift, _initialTermId) + termOffset;

                long result = termAppender.Claim(_headerWriter, length, out claim);
                long newPosition = NewPosition(partitionIndex, (int) termOffset, position, result);

                if (newPosition < 0) continue;
                return newPosition;
            }
        }


        /// <summary>
        /// Get the current position to which the publication has advanced for this stream.
        /// </summary>
        /// <returns> the current position to which the publication has advanced for this stream. </returns>
        public long Position
        {
            get
            {
                long rawTail = _termAppenders[ActivePartitionIndex(_logBuffers.LogMetaData)].RawTailVolatile;
                int termOffset = TermOffset(rawTail, _logBuffers.TermLength);
                return ComputePosition(TermId(rawTail), termOffset, _positionBitsToShift, _initialTermId);
            }
        }


        private long NewPosition(int index, int currentTail, long position, long result) {
            long newPosition = TermAppender.TRIPPED;
            int termOffset = TermAppender.TermOffset(result);
            if (termOffset > 0) {
                newPosition = (position - currentTail) + termOffset;
            } else if (termOffset == TermAppender.TRIPPED) {
                //int nextIndex = NextPartitionIndex(index);
                //int nextNextIndex = NextPartitionIndex(nextIndex);
                //_termAppenders[nextIndex].TailTermId(TermAppender.TermId(result) + 1);
                //_termAppenders[nextNextIndex].StatusOrdered(LogBufferDescriptor.NEEDS_CLEANING);
                //ActivePartitionIndex(_logBuffers.LogMetaData, nextIndex);
                RotateLog(_logBuffers.Partitions, _logBuffers.LogMetaData, index, TermAppender.TermId(result) + 1);
                _cleanEvent.Set();
            }
            return newPosition;
        }

        public int CleanLogBuffer() {
            var workCount = 0;
            foreach (LogBufferPartition partition in _logBuffers.Partitions) {
                if (partition.Status == NEEDS_CLEANING) {
                    partition.Clean();
                    workCount = 1;
                }
            }
            return workCount;
        }

        /// <summary>
        /// Poll messages starting from _subscriberPosition and invoke OnAppendHandlerOld event
        /// </summary>
        private long Poll() {
            var subscriberIndex = IndexByPosition(_subscriberPosition, _positionBitsToShift);
            int termOffset = (int)_subscriberPosition & _termLengthMask;
            var termBuffer = _logBuffers.Buffers[subscriberIndex];

            long outcome = TermReader.Read(termBuffer, termOffset, OnAppend, 100, OnError);

            UpdatePosition(termOffset, TermReader.Offset(outcome));
            return outcome;
        }


        /// <summary>
        /// TODO inline
        /// </summary>
        /// <param name="offsetBefore"></param>
        /// <param name="offsetAfter"></param>
        private void UpdatePosition(int offsetBefore, int offsetAfter) {
            _subscriberPosition = _subscriberPosition + (offsetAfter - offsetBefore);
        }

        public void Dispose() {
            _cts.Cancel();
            _cleaner.Wait();
            _poller.Wait();
            _logBuffers.Dispose();
        }

        public unsafe void Append<T>(T message) {
            // NB Writing a generic type and its bytes after serialization should be indistinguishable
            byte[] bytes = null;
            if (TypeHelper<T>.Size == -1) {
                bytes = Serializer.Serialize(message);
                Append(bytes);
            }

            int len;
            if (typeof(T) == typeof(byte[])) {
                bytes = (byte[])(object)(message);
                len = bytes.Length;
            } else {
                len = TypeHelper<T>.Size;
            }

            BufferClaim claim;
            Claim(len, out claim);
            if (bytes == null) {
                TypeHelper<T>.StructureToPtr(message, claim.Buffer.Data + claim.Offset);
            } else {
                Marshal.Copy(bytes, 0, claim.Buffer.Data + claim.Offset, len);
            }
            claim.Commit();
        }
    }
}
