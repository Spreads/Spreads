// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Native;
using System.Diagnostics;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(
            this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer, Lookup lookup)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// <see cref="BinarySearch{T}(ref Spreads.Native.Vec{T},int,int,T,Spreads.KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(
            this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer)
        {
            return BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// <see cref="BinarySearch{T}(ref Vec{T},int,int,T,KeyComparer{T},Lookup)"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(
            this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer, Lookup lookup)
        {
            return BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer, lookup);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(
            ref T vecStart, int length, T value, KeyComparer<T> comparer, Lookup lookup)
        {
            if (length == 0)
            {
                return ~0;
            }

            bool eqOk = lookup.IsEqualityOK();
            int lo = 0;
            int hi = length - 1;
            int i;

            // optimization: return last item if requesting LE with key >= the last one
            // or if key == last and equality is acceptable
            int c = comparer.Compare(value, Unsafe.Add(ref vecStart, hi));
            if (c >= 0)
            {
                if (lookup == Lookup.LE || (c == 0 && eqOk))
                {
                    return hi;
                }
            }

            // If length == 0, hi == -1, and loop will not be entered
            while (lo <= hi)
            {
                // PERF: `lo` or `hi` will never be negative inside the loop,
                //       so computing median using uints is safe since we know
                //       `length <= int.MaxValue`, and indices are >= 0
                //       and thus cannot overflow an uint.
                //       Saves one subtraction per loop compared to
                //       `int i = lo + ((hi - lo) >> 1);`

                i = (int)(((uint)hi + (uint)lo) >> 1);

                c = comparer.Compare(value, Unsafe.Add(ref vecStart, i));
                if (c == 0)
                {
                    goto FOUND;
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

            // complement = ~lo;

            // If none found, then a negative number that is the bitwise complement
            // of the index of the next element that is larger than or, if there is
            // no larger element, the bitwise complement of `length`, which
            // is `lo` at this point.
            // i = ~lo;

            // GE/GT will be at lo
            // LE/LT will be at lo-1
            // EQ must return ~lo

            if (lookup == Lookup.EQ)
            {
                return ~lo;
            }

            i = lo - lookup.ComplementAdjustment();

            goto HAS_I_CANDIDATE;

        FOUND:
            Debug.Assert(i >= 0);
            if (eqOk)
            {
                return i;
            }
            // -1 or +1
            i = i + lookup.NeqOffset();

        HAS_I_CANDIDATE:
            if (unchecked((uint)i) >= unchecked((uint)length))
            {
                // out of range
                return ~lo;
            }

            return i;
        }
    }
}