// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Utils
{
    public unsafe class TimeService : IDisposable
    {
        private static TimeService _default = new TimeService();
        /// <summary>
        /// 
        /// </summary>
        public static TimeService Default
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _default;
            set
            {
                _default?.Dispose();
                _default = value;
            }
        }

        private IntPtr _lastUpdatedPtr;
        private bool _allocated;

        private Timer _timer;
        private Thread _spinnerThread;
        

        public TimeService() : this(IntPtr.Zero)
        {
        }

        public TimeService(IntPtr ptr, int intervalMilliseconds = 1)
        {
            Start(ptr, intervalMilliseconds);
        }

        private void Start(IntPtr ptr, int intervalMilliseconds)
        {
            if (ptr == IntPtr.Zero)
            {
                ptr = Marshal.AllocHGlobal(8);
                _allocated = true;
            }

            _lastUpdatedPtr = ptr;

            *(long*) _lastUpdatedPtr = 0;

            UpdateTime();

            _timer = new Timer(o =>
            {
                UpdateTime();
            }, null, 0, intervalMilliseconds);
        }

        public void Dispose()
        {
            if (_lastUpdatedPtr != IntPtr.Zero)
            {
                _timer.Dispose();
                if (_allocated)
                {
                    Marshal.FreeHGlobal(_lastUpdatedPtr);
                }
                _lastUpdatedPtr = IntPtr.Zero;
            }
        }

        public Timestamp CurrentTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Timestamp)Interlocked.Add(ref *(long*)_lastUpdatedPtr, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long UpdateTime()
        {
            if (_spinnerThread != null || _lastUpdatedPtr == IntPtr.Zero)
            {
                return (long)(Timestamp)DateTime.UtcNow;
            }
            while (true)
            {
                var last = Volatile.Read(ref *(long*)_lastUpdatedPtr);
                var current = (long)(Timestamp)DateTime.UtcNow;
                if (current > last)
                {
                    if (last == Interlocked.CompareExchange(ref *(long*)_lastUpdatedPtr, current, last))
                    {
                        return current;
                    }
                }
                else
                {
                    // Tight loop with Interlocked.Add cannot keep up with nanos
                    // This branch could happen if we update too often and DateTime.UtcNow has not changed.
                    // Strictly monotonic is important:
                    // just ignore non-monotomic updates and CurrentTime will keep
                    // incrementing on every access.
                    return last;
                }
            }
        }

        /// <summary>
        /// Dedicate a core to update time in a hot loop that will occupy 100% of one CPU core.
        /// </summary>
        /// <param name="ct"></param>
        public void StartSpinUpdate(CancellationToken ct)
        {
            Console.WriteLine("Starting spinner thread");
            if (!ct.CanBeCanceled)
            {
                ThrowHelper.ThrowInvalidOperationException("Must provide cancellable token to TimeService.SpinUpdate, otherwise a thread will spin and consume 100% of a core without a way to stop it.");
            }

            if (_spinnerThread != null)
            {
                ThrowHelper.ThrowInvalidOperationException("Spin Update is already started.");
            }

            object started = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Console.WriteLine("Started TimeSerive spinner thread");
                    Interlocked.Exchange(ref started, new object());
                    while (!ct.IsCancellationRequested)
                    {
                        UpdateTime();
                    }
                }
                finally
                {
                    _spinnerThread = null;
                }
            });
            thread.Priority = ThreadPriority.AboveNormal;
            thread.IsBackground = true;
            thread.Name = "TimeService_spinner";
            thread.Start();
            _spinnerThread = thread;

            while (started == null)
            {
                Thread.Sleep(0);
            }
        }
    }
}
