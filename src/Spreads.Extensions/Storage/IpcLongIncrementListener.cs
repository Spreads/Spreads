using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Storage {

    /// <summary>
    /// Read a value from mmaped file, then invoke an action every time a value increases
    /// </summary>
    public class IpcLongIncrementListener : IDisposable {
        private long _lastSeenValue;
        private DirectFile _df;
        private readonly EventWaitHandle _eh;
        private RegisteredWaitHandle _rwh;
        private readonly SemaphoreSlim _sem = new SemaphoreSlim(0, int.MaxValue);
        private readonly Action<long, long> _action;
        private bool _isRunning;
        private Task _task;
        public IpcLongIncrementListener(string filename, Action<long, long> action, long init = -1L) {
            _action = action;
            _df = new DirectFile(filename + ".ipclistener", 8);

            var handleName = Path.GetFileName(filename) + ".ipclistener";
            bool created;
            _eh = new EventWaitHandle(false, EventResetMode.ManualReset, handleName, out created);
            if (created && init != -1L) {
                _df.Buffer.VolatileWriteInt64(0, init);
                _lastSeenValue = init;
            } else {
                _lastSeenValue = _df.Buffer.VolatileReadInt64(0);
            }
        }

        public void Set(long newValue) {
            var current = _df.Buffer.VolatileReadInt64(0);
            if (newValue > current) {
                _df.Buffer.VolatileWriteInt64(0, newValue);
            }
            //while (_lastSeenValue != _df.Buffer.InterlockedCompareExchangeInt64(0, newValue, _lastSeenValue)) {
            //    _lastSeenValue = newValue;
            //    _df.Buffer.VolatileWriteInt64(0, newValue);
            //}
            _eh.Set();
            var sw = new SpinWait();
            for (int i = 0; i < 5; i++) { sw.SpinOnce(); }
            _eh.Reset();
        }

        public void Start() {
#if NET451
            _rwh = ThreadPool.UnsafeRegisterWaitForSingleObject(_eh, OnWait, null, 100, false);
#else
            _rwh = ThreadPool.RegisterWaitForSingleObject(_eh, OnWait, null, 100, false);
#endif

            _isRunning = true;
            _task = Task.Run(async () => {
                var sw = new SpinWait();
                while (_isRunning) {
                    var currentValue = _df.Buffer.VolatileReadInt64(0);
                    if (currentValue > _lastSeenValue) {
                        // nore that on next iteration we will see only the last value
                        _action.Invoke(_lastSeenValue, currentValue);
                        _lastSeenValue = currentValue;
                    } else {
                        if (!sw.NextSpinWillYield) {
                            sw.SpinOnce();
                        } else {
                            sw.Reset();
                            await _sem.WaitAsync(100);
                        }
                    }
                }
            });
        }

        public void Stop() {
            _isRunning = false;
            _sem.Release();
            _task.Wait();
            _rwh.Unregister(_eh);
        }

        private void OnWait(object state, bool timeout) {
            _sem.Release();
        }

        public void Dispose() {
            Stop();
            _sem.Dispose();
            _df.Dispose();
            _eh.Dispose();
        }
    }
}
