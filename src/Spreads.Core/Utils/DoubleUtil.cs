// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.Utils
{
    public static class DoubleUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOppositeSignNaive(this double first, double second)
        {
            var sign1 = Math.Sign(first);
            var sign2 = Math.Sign(second);
            return sign1 * sign2 == -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOppositeSign(this double first, double second)
        {
            return first * second < 0.0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool IsOppositeSignBitwise(this double first, double second)
        {
            const ulong mask = (1UL << 63) - 1UL;
            var value1 = *((ulong*)(&first));
            var value2 = *((ulong*)(&second));
            //var signBitDiffers = (value1 >> 63) != (value2 >> 63);
            //var firstIsNonZero = (mask & value1) != 0UL;
            //var secondIsNonZero = (mask & value2) != 0UL;
            return ((value1 >> 63) != (value2 >> 63)) && ((mask & value1) != 0UL) && ((mask & value2) != 0UL);
        }
    }
}