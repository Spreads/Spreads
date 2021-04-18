// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace Spreads.Threading
{
    public static partial class Cpu
    {
        /// <summary>
        /// Platform abstraction layer
        /// </summary>
        public static class Pal
        {
            private static readonly int _coreCount = Environment.ProcessorCount;

            private static readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            private static readonly bool _schedGetCpuWorks = TestSchedGetCpu();

            private static bool TestSchedGetCpu()
            {
                if (_isWindows) return false;

                try
                {
                    if (sched_getcpu() >= 0)
                        return true;
                }
                catch
                {
                    // ignored
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static int GetCurrentCpuNumber()
            {
                // Static readonly fields are JIT constants,
                // GetCurrentProcessorNumber is faster than it's Ex variant
                if (_isWindows)
                {
                    if (_coreCount > 64)
                    {
                        ProcessorNumber procNum = default;
                        GetCurrentProcessorNumberEx(ref procNum);
                        return (procNum.Group << 6) | procNum.Number;
                    }

                    return (int)GetCurrentProcessorNumber();
                }

                if (_schedGetCpuWorks)
                    return sched_getcpu();

                return -1;
            }

#if HAS_SUPPRESS_GC_TRANSITION
            [SuppressGCTransition]
#endif
            [SuppressUnmanagedCodeSecurity]
            [DllImport("kernel32.dll")]
            private static extern uint GetCurrentProcessorNumber();

#pragma warning disable 649
            /// <summary>
            /// Represents a logical processor in a processor group.
            /// </summary>
            [SuppressMessage("ReSharper", "UnassignedField.Global")]
            private struct ProcessorNumber
            {
                /// <summary>
                /// The processor group to which the logical processor is assigned.
                /// </summary>
                public ushort Group;

                /// <summary>
                /// The number of the logical processor relative to the group.
                /// </summary>
                public byte Number;

                /// <summary>
                /// This parameter is reserved.
                /// </summary>
                public byte Reserved;
            }

#pragma warning restore 649

#if HAS_SUPPRESS_GC_TRANSITION
            [SuppressGCTransition]
#endif
            [SuppressUnmanagedCodeSecurity]
            [DllImport("kernel32.dll")]
            private static extern void GetCurrentProcessorNumberEx(ref ProcessorNumber processorNumber);

#if HAS_SUPPRESS_GC_TRANSITION
            [SuppressGCTransition]
#endif
            [SuppressUnmanagedCodeSecurity]
            [DllImport("libc.so.6", SetLastError = true)]
            public static extern int sched_getcpu();
        }
    }
}
