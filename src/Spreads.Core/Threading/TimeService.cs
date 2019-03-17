// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Threading
{
    /// <summary>
    /// A time service that updates time in background and increments time by one nanosecond on every read, thus guaranteeing unique increasing time sequence.
    /// Could work with shared memory so that multiple processes see the same time.
    /// Supports high-precision mode when a dedicated thread polls time in a hot loop.
    /// </summary>
    public unsafe class TimeService : IDisposable
    {
        private static TimeService _default = new TimeService();

        /// <summary>
        /// Default instance that uses in-process
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

        /// <summary>
        /// A shortcut to <see cref="CurrentTime"/> value of <see cref="TimeService.Default"/> default time service.
        /// </summary>
        public static Timestamp Now => Default.CurrentTime;

        private IntPtr _lastUpdatedPtr;
        private bool _allocated;

        private Timer _timer;
        private Thread _spinnerThread;

        /// <summary>
        /// Creates a new time service with update interval of 1 millisecond.
        /// </summary>
        public TimeService() : this(IntPtr.Zero)
        {
        }

        /// <summary>
        /// Creates a new time service.
        /// </summary>
        /// <param name="ptr">Memory location to store time.</param>
        /// <param name="intervalMilliseconds">Background update interval.</param>
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

            *(long*)_lastUpdatedPtr = 0;

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
            if (_lastUpdatedPtr == IntPtr.Zero)
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

        // https://github.com/dotnet/coreclr/pull/9736

        /// <summary>
        /// Dedicate a thread to update time in a hot loop. Modern desktops even with Window OS
        /// could provide near microsecond time precision via <see cref="DateTime.UtcNow"/>, but
        /// the call is rather expensive. A dedicated thread could do this call and other threads
        /// just read the updated value and increment it by 1 nanosecond on every read.
        ///
        /// <para />
        ///
        /// By default thread priority is set to <see cref="ThreadPriority.Normal"/>. Setting this lower does
        /// not make a lot of sense, instead you could just use TimeService with default updates based on a timer.
        /// Low-priority thread will often be preempted by other threads and time precision will be bad.
        /// To lessen CPU consumption you could provide a positive <paramref name="spinCount"/>.
        /// On i7-8700 a value between 100-200 gives better precision. This is probably due to the expensive call
        /// to DateTime.UtcNow that takes just less than 1/2 of the system timer precision.
        /// Keep it at default unless measured a better precision with a different value.
        /// </summary>
        /// <param name="ct"><see cref="CancellationToken.CanBeCanceled"/> must be true, i.e. default one will not work.</param>
        /// <param name="priority">Thread priority. .</param>
        /// <param name="spinCount">Number of times to call <see cref="Thread.SpinWait"/> after each time update.</param>
        public void StartSpinUpdate(CancellationToken ct, ThreadPriority priority = ThreadPriority.Normal, int spinCount = 150)
        {
            Trace.TraceInformation("Starting TimeService spinner thread");
            if (!ct.CanBeCanceled)
            {
                ThrowHelper.ThrowInvalidOperationException("Must provide cancellable token to TimeService.StartSpinUpdate, otherwise a thread will spin and consume 100% of a core without a way to stop it.");
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
                        if (spinCount > 0)
                        {
                            Thread.SpinWait(spinCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Exception in TimeService spinner thread: " + ex);
                }
                finally
                {
                    _spinnerThread = null;
                }
            });
            thread.Priority = priority;
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
