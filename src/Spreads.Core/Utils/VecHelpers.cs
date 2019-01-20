// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Native;
using System.Runtime.CompilerServices;

namespace Spreads.Utils
{
    internal static class VecHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(
            this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// <see cref="BinarySearch{T}(ref Spreads.Native.Vec{T},int,int,T,Spreads.KeyComparer{T})"/> without bound checks.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vec"></param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(
            this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer)
        {
            return BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(
            ref T vecStart, int length, T value, KeyComparer<T> comparer)
        {
            int lo = 0;
            int hi = length - 1;
            // If length == 0, hi == -1, and loop will not be entered
            while (lo <= hi)
            {
                // PERF: `lo` or `hi` will never be negative inside the loop,
                //       so computing median using uints is safe since we know
                //       `length <= int.MaxValue`, and indices are >= 0
                //       and thus cannot overflow an uint.
                //       Saves one subtraction per loop compared to
                //       `int i = lo + ((hi - lo) >> 1);`
                int i = (int)(((uint)hi + (uint)lo) >> 1);

                int c = comparer.Compare(value, Unsafe.Add(ref vecStart, i));
                if (c == 0)
                {
                    return i;
                }
                else if (c > 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }
            // If none found, then a negative number that is the bitwise complement
            // of the index of the next element that is larger than or, if there is
            // no larger element, the bitwise complement of `length`, which
            // is `lo` at this point.
            return ~lo;
        }
    }
}