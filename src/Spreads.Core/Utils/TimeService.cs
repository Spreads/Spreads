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
    public static unsafe class TimeService
    {
        private static IntPtr _lastUpdatedPtr;
        private static bool _allocated;

        private static Timer _timer;

        static TimeService()
        {
            // Could stop and start with another pointer if needed
            Start();
        }

        public static void Start()
        {
            Start(IntPtr.Zero);
        }

        public static void Start(IntPtr ptr)
        {
            if (_lastUpdatedPtr != IntPtr.Zero)
            {
                Stop();
            }

            if (ptr == IntPtr.Zero)
            {
                ptr = Marshal.AllocHGlobal(8);
                _allocated = true;
            }

            _lastUpdatedPtr = ptr;

            _timer = new Timer(o =>
            {
                UpdateTime();
            }, null, 0, 1);
            UpdateTime();
        }

        public static void Stop()
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

        public static Timestamp CurrentTime
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (Timestamp)Interlocked.Add(ref *(long*)_lastUpdatedPtr, 1); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateTime()
        {
            Interlocked.Exchange(ref *(long*)_lastUpdatedPtr, (long)(Timestamp)DateTime.UtcNow);
        }
    }
}
