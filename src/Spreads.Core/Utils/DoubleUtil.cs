// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetDouble<TValue>(TValue value)
        {
            if (typeof(TValue) == typeof(double))
            {
                return ((double)(object)value);
            }

            if (typeof(TValue) == typeof(float))
            {
                return ((float)(object)value);
            }

            if (typeof(TValue) == typeof(int))
            {
                return (double)((int)(object)value);
            }

            if (typeof(TValue) == typeof(long))
            {
                return (double)((long)(object)value);
            }

            if (typeof(TValue) == typeof(uint))
            {
                return (double)((uint)(object)value);
            }

            if (typeof(TValue) == typeof(ulong))
            {
                return (double)((ulong)(object)value);
            }

            if (typeof(TValue) == typeof(decimal))
            {
                return (double)((decimal)(object)value);
            }

            if (typeof(TValue) == typeof(SmallDecimal))
            {
                return (double)((SmallDecimal)(object)value);
            }

            return GetDoubleDynamic(value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double GetDoubleDynamic<TValue>(TValue value)
        {
            if (value is double)
            {
                return ((double)(object)value);
            }

            if (value is float)
            {
                return ((float)(object)value);
            }

            if (value is int)
            {
                return (double)((int)(object)value);
            }

            if (value is long)
            {
                return (double)((long)(object)value);
            }

            if (value is uint)
            {
                return (double)((uint)(object)value);
            }

            if (value is ulong)
            {
                return (double)((ulong)(object)value);
            }

            if (value is decimal)
            {
                return (double)((decimal)(object)value);
            }

            if (value is SmallDecimal)
            {
                return (double)((SmallDecimal)(object)value);
            }

            // TODO Review why this method is needed and where it's used. If it's really needed, review if null should return double.NaN.
            return (double)(object)(value);
        }
    }
}
