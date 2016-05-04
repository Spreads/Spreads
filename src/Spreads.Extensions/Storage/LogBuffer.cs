using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Storage {

    public delegate void OnAppend(ArraySegment<byte> message);

    public interface ILogBuffer {
        void Append(ArraySegment<byte> message);
        event OnAppend OnAppend;
    }

    public class LogBuffer : ILogBuffer, IDisposable {
        private const int HeaderSize = 256;
        private const int NumberOfTerms = 3;

        private DirectFile _df;
        private readonly Task _reader;

        private int _writerTerm;
        /// <summary>
        /// Where value of current tail is stored
        /// </summary>
        private IntPtr _writerTailPtr;
        /// <summary>
        /// Start of the current term
        /// </summary>
        private IntPtr _writerTermPtr;

        private int _readerTerm;
        private int _readerTail;
        private IntPtr _readerTermPtr;

        private readonly CancellationTokenSource _cts;

        /// <summary>
        /// Total size of the log file
        /// </summary>
        public int LogSize => HeaderSize + TermSize * NumberOfTerms;

        private unsafe int ActiveTermId
        {
            get { return Volatile.Read(ref *(int*)(_df._buffer._data)); }
            set { Volatile.Write(ref *(int*)(_df._buffer._data), value); }
        }

        public string Filename { get; }

        public int TermSize { get; }

        private IntPtr Tail(int termId) {
            return _df._buffer._data + 4 + termId * 4;
        }

        public unsafe LogBuffer(string filename, int termSizeMb = 5) {
            Filename = filename;
            TermSize = termSizeMb * 1024 * 1024;

            _df = new DirectFile(filename, LogSize);
            _writerTerm = ActiveTermId;
            _writerTailPtr = Tail(_writerTerm);
            _writerTermPtr = _df._buffer._data + HeaderSize + TermSize * _writerTerm;

            // init at the last known place
            _readerTerm = _writerTerm;
            _readerTail = *(int*)_writerTailPtr;
            _readerTermPtr = _writerTermPtr;

            _cts = new CancellationTokenSource();

            _reader = Task.Factory.StartNew(() => {
                var sw = new SpinWait();
                while (!_cts.Token.IsCancellationRequested)
                {
                    var tail = _readerTail; // Volatile.Read(ref *(int*)_readerTailPtr);
                    if (tail >= TermSize || *(int*)(_readerTermPtr + tail) == -1) {
                        // switch term
                        _readerTerm = (_readerTerm + 1) % NumberOfTerms;
                        _readerTail = 0;
                        _readerTermPtr = _df._buffer._data + HeaderSize + TermSize * _readerTerm;
                    } else {
                        var len = *(int*)(_readerTermPtr + tail);
                        if (len > 0)
                        {
                            // could read value
                            byte[] bytes = new byte[len];
                            Marshal.Copy((_readerTermPtr + tail + 4), bytes, 0, len);
                            OnAppend?.Invoke(new ArraySegment<byte>(bytes));
                            _readerTail = _readerTail + len + 4;
                        }
                    }
                    // TODO? implement signaling via WaitHandle?
                    if (sw.NextSpinWillYield) sw.Reset();
                    sw.SpinOnce();
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }


        /// <summary>
        /// Zero all bytes of the term
        /// </summary>
        private void ClearTerm(int termId) {
            checked {
                unsafe
                {
                    var start = _df._buffer._data + HeaderSize + termId * TermSize;
                    for (int i = 0; i < TermSize / 8; i++) {
                        *(long*)(start + i) = 0L;
                    }
                    *(int*)Tail(termId) = 0;
                }

            }
        }

        public unsafe void Append(ArraySegment<byte> message) {
            var len = message.Count;
            var nextTail = Interlocked.Add(ref *(int*)_writerTailPtr, len + 4);
            // have enough space after this claim
            if (nextTail <= TermSize) {
                // write message body
                Marshal.Copy(message.Array, message.Offset, _writerTermPtr + (nextTail - len), len);
                // write message length
                Volatile.Write(ref *(int*)(_writerTermPtr + (nextTail - len - 4)), len);
                return;
            }
            
            if (nextTail - (len + 4) < TermSize) {
                // we are the first to read the end of the term, it is our responsibility
                // to clean after ourselves

                //var oldLenPtr = (_writerTermPtr + (nextTail - len - 4));


                var newTerm = (_writerTerm + 1) % NumberOfTerms;
                ClearTerm(newTerm);

                *(int*)Tail(newTerm) = 0;

                _writerTerm = newTerm;
                _writerTailPtr = Tail(_writerTerm);
                _writerTermPtr = _df._buffer._data + HeaderSize + TermSize * _writerTerm;

                ActiveTermId = newTerm;

                // signal to readers about term overflow
                Volatile.Write(ref *(int*)_writerTermPtr, -1);

                Append(message);
                //return;

            } else {
                Trace.Assert(nextTail - (len + 4) >= TermSize);
                // we claimed space after someone first reached the limit.
                // must wait until ActiveTermId is incremented
                var sw = new SpinWait();
                while (ActiveTermId == _writerTerm) {
                    // TODO the switcher could die
                    sw.SpinOnce();
                }
                _writerTerm = ActiveTermId;
                _writerTailPtr = Tail(_writerTerm);
                _writerTermPtr = _df._buffer._data + HeaderSize + TermSize * _writerTerm;

                Append(message);
                //return;
            }
            Console.WriteLine(nextTail);
            throw new ApplicationException("Should never be there");
        }

        public event OnAppend OnAppend;

        public void Dispose() {
            _cts.Cancel();
            _reader.Wait();
            GC.SuppressFinalize(this);
        }

        ~LogBuffer() {
            _cts.Cancel();
            _reader.Wait();
        }
    }
}
