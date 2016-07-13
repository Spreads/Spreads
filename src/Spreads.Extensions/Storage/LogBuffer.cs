using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Storage {
    internal interface ILogBuffer : IDisposable {
        void Append<T>(T message);
        IntPtr Claim(int length);
        event OnAppendHandlerOld OnAppend;
    }

    internal class LogBuffer : ILogBuffer {
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
                while (!_cts.Token.IsCancellationRequested) {
                    try {
                    } finally {
                        var tail = _readerTail; // Volatile.Read(ref *(int*)_readerTailPtr);
                        if (tail >= TermSize || *(int*)(_readerTermPtr + tail) == -1) {
                            while (ActiveTermId == _readerTerm) {
                                sw.SpinOnce();
                            }
                            // switch term
                            _readerTerm = (_readerTerm + 1) % NumberOfTerms;
                            _readerTail = 0;
                            _readerTermPtr = _df._buffer._data + HeaderSize + TermSize * _readerTerm;
                        } else {
                            var len = *(int*)(_readerTermPtr + tail);
                            if (len > 0) {
                                // could read value
                                byte[] bytes = new byte[len];
                                Marshal.Copy((_readerTermPtr + tail + 4), bytes, 0, len);
                                OnAppend?.Invoke((_readerTermPtr + tail));
                                _readerTail = _readerTail + len + 4;
                            }
                        }
                        //Thread.SpinWait(1);
                        // TODO? implement signaling via WaitHandle?
                        if (sw.NextSpinWillYield) sw.Reset();
                        sw.SpinOnce();
                    }
                }
                OptimizationSettings.TraceVerbose("LogBuffer invoke loop exited");
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .ContinueWith(task => {
                Console.WriteLine("LogBuffer OnAppendHandlerOld Invoke should never throw exceptions" + Environment.NewLine + task.Exception);
                Environment.FailFast("LogBuffer OnAppendHandlerOld Invoke should never throw exceptions", task.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
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

            var ptr = Claim(len);
            if (bytes == null) {
                TypeHelper<T>.StructureToPtr(message, ptr + 4);
            } else {
                Marshal.Copy(bytes, 0, ptr + 4, len);
            }
            Volatile.Write(ref *(int*)(ptr), len);
        }

        public unsafe IntPtr Claim(int length) {
            var nextTail = Interlocked.Add(ref *(int*)_writerTailPtr, length + 4);
            // have enough space after this claim
            if (nextTail <= TermSize) {
                return (_writerTermPtr + (nextTail - length - 4));
            }

            if (nextTail - (length + 4) <= TermSize) {
                // we are the first to read the end of the term, it is our responsibility
                // to clean after ourselves

                var oldLenPtr = (_writerTermPtr + (nextTail - length - 4));


                var newTerm = (_writerTerm + 1) % NumberOfTerms;
                ClearTerm(newTerm);

                *(int*)Tail(newTerm) = 0;

                _writerTerm = newTerm;
                _writerTailPtr = Tail(_writerTerm);
                _writerTermPtr = _df._buffer._data + HeaderSize + TermSize * _writerTerm;

                ActiveTermId = newTerm;

                // signal to readers about term overflow
                Volatile.Write(ref *(int*)oldLenPtr, -1);

                return Claim(length);

            } else {
                Trace.Assert(nextTail - (length + 4) > TermSize);
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

                return Claim(length); ;
            }
            Console.WriteLine(nextTail);
            throw new ApplicationException("Should never be there");
        }

        public event OnAppendHandlerOld OnAppend;

        private void Dispose(bool disposing) {
            _cts.Cancel();
            if (disposing) GC.SuppressFinalize(this);
        }

        public void Dispose() {
            Dispose(true);
        }

        ~LogBuffer() {
            Dispose(false);
        }
    }
}
