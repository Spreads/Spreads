// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Utils
{
    public unsafe class TimeService : IDisposable
    {
        public static TimeService Default = new TimeService();

        private IntPtr _lastUpdatedPtr;
        private bool _allocated;

        private Timer _timer;

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

            _timer = new Timer(o =>
            {
                UpdateTime();
            }, null, 0, intervalMilliseconds);
            UpdateTime();
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
            get { return (Timestamp)Interlocked.Add(ref *(long*)_lastUpdatedPtr, 1); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateTime()
        {
            Interlocked.Exchange(ref *(long*)_lastUpdatedPtr, (long)(Timestamp)DateTime.UtcNow);
        }
    }
}
