// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

#if HAS_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif

namespace Spreads.Algorithms
{
    public static class Intrinsics
    {
#if HAS_INTRINSICS
        public static readonly bool IsPopcountSupported = Popcnt.IsSupported && Environment.Is64BitProcess;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PopCount(ulong value)
        {
#if HAS_INTRINSICS
            if (IsPopcountSupported)
            {
                return Popcnt.X64.PopCount(value);
            }
#endif
            return PopcountManaged(value);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong PopcountManaged(ulong value)
        {
            ulong result = value - ((value >> 1) & 0x5555555555555555UL);
            result = (result & 0x3333333333333333UL) + ((result >> 2) & 0x3333333333333333UL);
            return (unchecked(((result + (result >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PopCount(uint value)
        {
#if HAS_INTRINSICS
            if (IsPopcountSupported)
            {
                return Popcnt.PopCount(value);
            }
#endif
            return PopcountManaged(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint PopcountManaged(uint value)
        {
            ThrowHelper.ThrowNotImplementedException();
            value = value - ((value >> 1) & 0x55555555);
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            value = ((value + (value >> 4) & 0xF0F0F0F) * 0x1010101) >> 24;
            return value;
        }
    }
}
