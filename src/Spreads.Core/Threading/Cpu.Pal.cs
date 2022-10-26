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
        private interface ICpuNumberGetter
        {
            int GetCpuNumber();
        }

        private sealed class WindowsCpuNumberGetter : ICpuNumberGetter
        {
            private static readonly int _coreCount = Environment.ProcessorCount;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetCpuNumber()
            {
                if (_coreCount > 64)
                {
                    ProcessorNumber procNum = default;
                    GetCurrentProcessorNumberEx(ref procNum);
                    return (procNum.Group << 6) | procNum.Number;
                }

                return (int)GetCurrentProcessorNumber();
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
        }

        private sealed class LinuxCpuNumberGetter : ICpuNumberGetter
        {
            public int GetCpuNumber() => sched_getcpu();

#if HAS_SUPPRESS_GC_TRANSITION
            [SuppressGCTransition]
#endif
            [SuppressUnmanagedCodeSecurity]
            [DllImport("libc.so.6", SetLastError = true)]
            private static extern int sched_getcpu();
        }

        private sealed class FallbackCpuNumberGetter : ICpuNumberGetter
        {
            public int GetCpuNumber() => -1;
        }

        /// <summary>
        /// Platform abstraction layer
        /// </summary>
        public static class Pal
        {
            // Hope that it's devirtualized
            private static readonly ICpuNumberGetter _cpuNumberGetterGetter = InitIGetCpuNumber();

            private static ICpuNumberGetter InitIGetCpuNumber()
            {
                try
                {
                    ICpuNumberGetter cpuGetter = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? new WindowsCpuNumberGetter()
                        : new LinuxCpuNumberGetter();

                    if (cpuGetter.GetCpuNumber() < 0)
                        throw new Exception("Native CPU number getter is not working");
                    return cpuGetter;
                }
                catch
                {
                    return new FallbackCpuNumberGetter();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static int GetCurrentCpuNumber() => _cpuNumberGetterGetter.GetCpuNumber();
        }
    }
}
