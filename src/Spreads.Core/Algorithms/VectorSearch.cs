// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if HAS_INTRINSICS
using Spreads.Utils;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using Spreads.DataTypes;
using Spreads.Utils;
using static Spreads.Utils.Constants;

namespace Spreads.Algorithms
{
    /// <summary>
    /// Algorithms to find values in contiguous memory region.
    /// WARNING: Methods in this static class do not perform bound checks and are intended to be used
    /// as building blocks in other parts that calculate bounds correctly and
    /// do performs required checks on external input.
    /// </summary>
    public static partial class VectorSearch
    {
        /// <summary>
        /// Optimized binary search that returns the same value as the classic algorithm.
        /// </summary>
        /// <returns>Returns index of the value (if present) or its negative binary complement.</returns>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int BinarySearch<T>(ref T searchSpace, int length, T value, KeyComparer<T> comparer = default)
            => BinarySearch(ref searchSpace, 0, length, value, comparer);

        /// <summary>
        /// Optimized binary search that returns the same value as the classic algorithm.
        /// </summary>
        /// <returns>Returns index of the value (if present) or its negative binary complement.
        /// The index is relative to <paramref name="searchSpace"/>, not to <paramref name="offset"/>.</returns>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int BinarySearch<T>(ref T searchSpace, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            return BinarySearchLoHi(ref searchSpace, offset, offset + length - 1, value, comparer);
        }

        /// <summary>
        /// Optimized binary search that returns the same value as the classic algorithm.
        /// </summary>
        /// <returns>Returns index of the value (if present) or its negative binary complement.
        /// The index is relative to <paramref name="searchSpace"/>, not to <paramref name="lo"/>.</returns>
        [MethodImpl(MethodImplAggressiveAll)]
        internal static int BinarySearchLoHi<T>(ref T searchSpace, int lo, int hi, T value, KeyComparer<T> comparer = default)
        {
#if HAS_INTRINSICS
            if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
            {
                if (typeof(T) == typeof(sbyte))
                    return BinarySearchAvx2LoHi(ref Unsafe.As<T, sbyte>(ref searchSpace), lo, hi, Unsafe.As<T, sbyte>(ref value));

                if (typeof(T) == typeof(short))
                    return BinarySearchAvx2LoHi(ref Unsafe.As<T, short>(ref searchSpace), lo, hi, Unsafe.As<T, short>(ref value));

                if (typeof(T) == typeof(int))
                    return BinarySearchAvx2LoHi(ref Unsafe.As<T, int>(ref searchSpace), lo, hi, Unsafe.As<T, int>(ref value));

                if (typeof(T) == typeof(long)
                    || typeof(T) == typeof(Timestamp)
                   )
                    return BinarySearchAvx2LoHi(ref Unsafe.As<T, long>(ref searchSpace), lo, hi, Unsafe.As<T, long>(ref value));

                if (typeof(T) == typeof(float))
                    return BinarySearchAvx2LoHi(ref Unsafe.As<T, float>(ref searchSpace), lo, hi, Unsafe.As<T, float>(ref value));

                if (typeof(T) == typeof(double))
                    return BinarySearchAvx2LoHi(ref Unsafe.As<T, double>(ref searchSpace), lo, hi, Unsafe.As<T, double>(ref value));

            }

            if (System.Runtime.Intrinsics.X86.Sse42.IsSupported)
            {
                if (typeof(T) == typeof(sbyte))
                    return BinarySearchSse42LoHi(ref Unsafe.As<T, sbyte>(ref searchSpace), lo, hi, Unsafe.As<T, sbyte>(ref value));

                if (typeof(T) == typeof(short))
                    return BinarySearchSse42LoHi(ref Unsafe.As<T, short>(ref searchSpace), lo, hi, Unsafe.As<T, short>(ref value));

                if (typeof(T) == typeof(int))
                    return BinarySearchSse42LoHi(ref Unsafe.As<T, int>(ref searchSpace), lo, hi, Unsafe.As<T, int>(ref value));

                if (typeof(T) == typeof(float))
                    return BinarySearchSse42LoHi(ref Unsafe.As<T, float>(ref searchSpace), lo, hi, Unsafe.As<T, float>(ref value));

                if (typeof(T) == typeof(double))
                    return BinarySearchSse42LoHi(ref Unsafe.As<T, double>(ref searchSpace), lo, hi, Unsafe.As<T, double>(ref value));

                if (typeof(T) == typeof(long)
                    || typeof(T) == typeof(Timestamp)
                   )
                    return BinarySearchSse42LoHi(ref Unsafe.As<T, long>(ref searchSpace), lo, hi, Unsafe.As<T, long>(ref value));
            }
#endif
            return BinarySearchHybridLoHi(ref searchSpace, lo, hi, value, comparer);
        }

        /// <summary>
        /// Performs classic binary search and returns index of the value or its negative binary complement.
        /// Used mostly for correctness check and benchmark baseline for other faster implementations.
        /// Use <see cref="SortedSearch{T}(ref T,int,T,Spreads.KeyComparer{T})"/> for the best performance.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        internal static int BinarySearchClassic<T>(ref T searchSpace, int length, T value, KeyComparer<T> comparer = default)
            => BinarySearchClassic(ref searchSpace, 0, length, value, comparer);

        /// <summary>
        /// Performs classic binary search and returns index of the value or its negative binary complement.
        /// Used mostly for correctness check and benchmark baseline for other faster implementations.
        /// Use <see cref="SortedSearch{T}(ref T,int,T,Spreads.KeyComparer{T})"/> for the best performance.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        internal static int BinarySearchClassic<T>(ref T searchSpace, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            unchecked
            {
                int lo = offset;
                int hi = offset + length - 1;
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

                    int c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i)));

                    if (c == 0)
                    {
                        return i;
                    }

                    if (c > 0)
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

        /// <summary>
        /// Classic binary search that falls back to linear search for the last small number of elements.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        internal static int BinarySearchHybridLoHi<T>(ref T searchSpace, int lo, int hi, T value, KeyComparer<T> comparer = default)
        {
            if (lo > hi)
                return ~lo;

            unchecked
            {
                int c;
                while (hi - lo > 7)
                {
                    int i = (int)(((uint)hi + (uint)lo) >> 1);

                    c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i)));

                    if (c > 0)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        if (c == 0)
                            return i;

                        hi = i - 1;
                    }
                }

                Debug.Assert(lo <= hi);
                while ((c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))) > 0
                       && ++lo <= hi)
                {
                }

                int ceq1 = -UnsafeEx.Ceq(c, 0);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

        /// <summary>
        /// Converts a result of a sorted search to result of directional search with direction of <see cref="Lookup"/>.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        internal static int SearchToLookupLoHi<T>(int lo, int hi, Lookup lookup, int i, ref T searchSpace, ref T value)
        {
            if (lookup == Lookup.EQ)
                return i;

            if (i >= lo)
            {
                if (lookup.IsEqualityOK())
                    goto RETURN;

                if (lookup == Lookup.LT)
                {
                    if (i == lo)
                        goto RETURN_O;

                    i--;
                }
                else // depends on if (eqOk) above
                {
                    Debug.Assert(lookup == Lookup.GT);
                    if (i == hi)
                        goto RETURN_OL;

                    i++;
                }
            }
            else
            {
                i = ~i;

                // LT or LE
                if (((uint)lookup & (uint)Lookup.LT) != 0)
                {
                    // i is idx of element that is larger, nothing here for LE/LT
                    if (i == lo)
                        goto RETURN_O;

                    i--;
                }
                else
                {
                    Debug.Assert(((uint)lookup & (uint)Lookup.GT) != 0);
                    Debug.Assert(i <= hi + 1);
                    // if was negative, if it was ~length then there are no more elements for GE/GT
                    if (i == hi + 1)
                        goto RETURN_OL;
                    // i is the same, ~i is idx of element that is GT the value
                }
            }

            value = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

            RETURN:
            Debug.Assert(unchecked((uint)i) - lo < unchecked((uint)(hi - lo + 1)));
            return i;

            RETURN_O:
            return ~lo;

            RETURN_OL:
            return ~(hi + 1);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int BinaryLookup<T>(ref T searchSpace, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return BinaryLookup(ref searchSpace, 0, length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int BinaryLookup<T>(ref T searchSpace, int offset, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            Debug.Assert(length >= 0);
            var lo = offset;
            int hi = offset + length - 1;
            var i = BinarySearchLoHi(ref searchSpace, lo, hi, value, comparer);
            return SearchToLookupLoHi<T>(lo, hi, lookup, i, ref searchSpace, ref value);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        internal static int BinaryLookupLoHi<T>(ref T searchSpace, int lo, int hi, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            var i = BinarySearchLoHi(ref searchSpace, lo, hi, value, comparer);
            return SearchToLookupLoHi<T>(hi, lo, lookup, i, ref searchSpace, ref value);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        public static int InterpolationSearch<T>(ref T searchSpace, int length, T value, KeyComparer<T> comparer = default)
        {
            return InterpolationSearch(ref searchSpace, offset: 0, length, value, comparer);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        public static int InterpolationSearch<T>(ref T searchSpace, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            var lo = offset;
            int hi = offset + length - 1;
            return InterpolationSearchLoHi(ref searchSpace, lo, hi, value, comparer);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        public static int InterpolationSearchLoHi<T>(ref T searchSpace, int lo, int hi, T value, KeyComparer<T> comparer = default)
        {
            if (typeof(T) == typeof(long)
                || typeof(T) == typeof(Timestamp))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, long>(ref searchSpace), lo, hi, Unsafe.As<T, long>(ref value));

            if (typeof(T) == typeof(ulong))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, ulong>(ref searchSpace), lo, hi, Unsafe.As<T, ulong>(ref value));

            if (typeof(T) == typeof(int))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, int>(ref searchSpace), lo, hi, Unsafe.As<T, int>(ref value));

            if (typeof(T) == typeof(uint))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, uint>(ref searchSpace), lo, hi, Unsafe.As<T, uint>(ref value));

            if (typeof(T) == typeof(short))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, short>(ref searchSpace), lo, hi, Unsafe.As<T, short>(ref value));

            if (typeof(T) == typeof(ushort))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, ushort>(ref searchSpace), lo, hi, Unsafe.As<T, ushort>(ref value));

            if (typeof(T) == typeof(char))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, char>(ref searchSpace), lo, hi, Unsafe.As<T, char>(ref value));

            if (typeof(T) == typeof(byte))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, byte>(ref searchSpace), lo, hi, Unsafe.As<T, byte>(ref value));

            if (typeof(T) == typeof(sbyte))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, sbyte>(ref searchSpace), lo, hi, Unsafe.As<T, sbyte>(ref value));

            if (typeof(T) == typeof(float))
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, float>(ref searchSpace), lo, hi, Unsafe.As<T, float>(ref value));

            if (typeof(T) == typeof(double))
            {
#if HAS_INTRINSICSXX
                return InterpolationSearchSpecializedLoHi2(ref Unsafe.As<T, double>(ref searchSpace), lo, hi, Unsafe.As<T, double>(ref value));
#endif
                return InterpolationSearchSpecializedLoHi(ref Unsafe.As<T, double>(ref searchSpace), lo, hi, Unsafe.As<T, double>(ref value));
            }

            if (!KeyComparer<T>.Default.IsDiffable)
                return BinarySearchLoHi(ref searchSpace, lo, hi, value, comparer);

            return InterpolationSearchGenericLoHi(ref searchSpace, lo, hi, value, comparer);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        internal static int InterpolationSearchGenericLoHi<T>(ref T searchSpace, int lo, int hi, T value, KeyComparer<T> comparer = default)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches
            // and switch to binary search.
            unchecked
            {
                int i;

                if (hi - lo > 16)
                {
                    var vlo = UnsafeEx.ReadUnaligned(ref searchSpace);
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi));
                    int range = hi - lo;
                    long vRange = comparer.Diff(vhi, vlo);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = (hi - lo) * (double)comparer.Diff(value, vlo);

                    i = (int)(nominator / vRange);

                    if ((uint)i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to vecStart
                    i += lo;

                    int c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i)));

                    if (c == 0)
                        goto FOUND;

                    var step = 1;

                    if (c > 0)
                    {
                        while (true)
                        {
                            i += step;

                            if (i > hi)
                                break;

                            c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i)));

                            if (c <= 0)
                            {
                                if (c == 0)
                                    goto FOUND;

                                hi = i - 1;
                                break;
                            }

                            step <<= 1;
                        }

                        lo = i - step + 1;
                    }
                    else
                    {
                        while (true)
                        {
                            i -= step;

                            if (i < lo)
                                break;

                            c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i)));

                            if (c >= 0)
                            {
                                if (c == 0)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchLoHi(ref searchSpace, lo, hi, value);

                FOUND:
                return i;
            }
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int InterpolationLookup<T>(ref T searchSpace, int length, ref T value, Lookup lookup,
            KeyComparer<T> comparer = default)
        {
            return InterpolationLookup(ref searchSpace, offset: 0, length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int InterpolationLookup<T>(ref T searchSpace, int offset, int length, ref T value, Lookup lookup,
            KeyComparer<T> comparer = default)
        {
            Debug.Assert(length >= 0);

            var lo = offset;
            int hi = offset + length - 1;

            return InterpolationLookupLoHi(ref searchSpace, lo, hi, ref value, lookup, comparer);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        internal static int InterpolationLookupLoHi<T>(ref T searchSpace, int lo, int hi, ref T value, Lookup lookup,
            KeyComparer<T> comparer = default)
        {
            var i = InterpolationSearchLoHi(ref searchSpace, lo, hi, value, comparer);
            return SearchToLookupLoHi(lo, hi, lookup, i, ref searchSpace, ref value);
        }

        /// <summary>
        /// Performs interpolation search for well-known types and binary search for other types.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int SortedSearch<T>(ref T searchSpace, int length, T value, KeyComparer<T> comparer = default)
        {
            return SortedSearch(ref searchSpace, offset: 0, length, value, comparer);
        }

        /// <summary>
        /// Performs interpolation search for well-known types and binary search for other types.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int SortedSearch<T>(ref T searchSpace, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
                return InterpolationSearch(ref searchSpace, offset, length, value, comparer);

            return BinarySearch(ref searchSpace, offset, length, value, comparer);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        internal static int SortedSearchLoHi<T>(ref T searchSpace, int lo, int hi, T value, KeyComparer<T> comparer = default)
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
                return InterpolationSearchLoHi(ref searchSpace, lo, hi, value, comparer);

            return BinarySearchLoHi(ref searchSpace, lo, hi, value, comparer);
        }

        /// <summary>
        /// Performs interpolation lookup for well-known types and binary lookup for other types.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int SortedLookup<T>(ref T searchSpace, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return SortedLookup(ref searchSpace, 0, length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Performs interpolation lookup for well-known types and binary lookup for other types.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        public static int SortedLookup<T>(ref T searchSpace, int offset, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
                return InterpolationLookup(ref searchSpace, offset, length, ref value, lookup, comparer);

            return BinaryLookup(ref searchSpace, offset, length, ref value, lookup, comparer);
        }

        [MethodImpl(MethodImplAggressiveAll)]
        public static int SortedLookupLoHi<T>(ref T searchSpace, int lo, int hi, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
                return InterpolationLookupLoHi(ref searchSpace, lo, hi, ref value, lookup, comparer);

            return BinaryLookupLoHi(ref searchSpace, lo, hi, ref value, lookup, comparer);
        }

#if HAS_INTRINSICS

        [MethodImpl(MethodImplAggressiveAll)]
        private static Vector256<T> ReadVector256<T>(ref T searchSpace, int i) where T : struct =>
            Unsafe.ReadUnaligned<Vector256<T>>(ref Unsafe.As<T, byte>(ref Unsafe.Add(ref searchSpace, i)));

        [MethodImpl(MethodImplAggressiveAll)]
        internal static int BinarySearchAvx2LoHi2(ref double searchSpace, int lo, int hi, double value)
        {
            unchecked
            {

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector256<double>.Count)
                    goto LINEAR;

                int mask;
                double vLo;

                // x3 is needed to safely fall into linear vectorized search:
                // after this loop all possible lo/hi are valid for linear vectorized search
                while (hi - lo >= Vector256<double>.Count * 3)
                {
                    var i = (int)(((uint)hi + (uint)lo - Vector256<double>.Count) >> 1);

                    mask = Avx2.MoveMask(Avx2.CompareGreaterThan(Vector256.Create(value), ReadVector256(ref searchSpace, i)).AsByte());

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int index = (32 - BitUtils.LeadingZeroCount(mask)) / Unsafe.SizeOf<double>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                            goto RETURN;
                        }

                        // val is not greater than all in vec
                        // not i-1, i could equal;
                        hi = i;
                    }
                    else
                    {
                        // val is larger than all in vec
                        lo = i + Vector256<double>.Count;
                    }
                }

                do
                {
                    mask = Avx2.MoveMask(Avx2.CompareGreaterThan(Vector256.Create(value), ReadVector256(ref searchSpace, lo)).AsByte());
                    var index = (32 - BitUtils.LeadingZeroCount(mask)) / Unsafe.SizeOf<double>();
                    lo += index;
                } while (mask == -1 & hi - lo >= Vector256<double>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo)))
                       && ++lo <= hi
                      )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

        [MethodImpl(MethodImplAggressiveAll)]
        internal static int InterpolationSearchSpecializedLoHi2(ref double searchSpace, int lo, int hi, double value)
        {
            // Try interpolation starting point only for big-enough lengths,
            // find the range with exponential search
            // and switch to binary search.
            unchecked
            {
                if (hi - lo > Settings.SAFE_CACHE_LINE / Unsafe.SizeOf<double>())
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));

                    var vRange = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, hi)) - vLo;

                    var middle = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, (int)(((uint)hi + (uint)lo) >> 1))) / vRange;
                    if (0.3 > middle || middle > 0.7)
                        goto BS;

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    // var iD = (range * (double) (value - vLo)) / vRange;

                    int i = lo + (int)(((hi - lo) * (value - vLo)) / vRange) - (Vector256<double>.Count / 2 - 1);

                    if (i < lo)
                        i = lo;

                    if (i + Vector256<double>.Count > hi)
                        i = hi - Vector256<double>.Count; // this is always true since cache line >> AVX vector size

                    var valueVec = Vector256.Create(value);
                    var mask = Avx2.MoveMask(Avx2.CompareGreaterThan(valueVec, ReadVector256(ref searchSpace, i)).AsByte());

                    // var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, i));

                    var step = 1;

                    if (mask == -1) // val is larger than all in vec
                    {
                        while (true)
                        {
                            i += step * Vector256<double>.Count;

                            if (i + Vector256<double>.Count > hi)
                                break;

                            var mask1 = Avx2.MoveMask(Avx2.CompareGreaterThan(valueVec, ReadVector256(ref searchSpace, i)).AsByte());

                            if (mask1 != -1)
                            {
                                if (mask1 != 0)
                                    goto FOUND;

                                // mask == 0, val is smaller than all in vec
                                hi = i - 1; // [. . . hi] [i . . .], no need to adjust for vec count
                                break;
                            }

                            // mask == -1, val is larger than all in vec
                            step <<= 1;
                        }

                        lo = i - (step - 1) * Vector256<double>.Count;
                        goto BS;
                    }

                    if (mask == 0) // val is smaller than all in vec
                    {
                        while (true)
                        {
                            i -= step * Vector256<double>.Count;

                            if (i < lo) // no need to adjust for vec count
                                break;

                            var mask1 = Avx2.MoveMask(Avx2.CompareGreaterThan(valueVec, ReadVector256(ref searchSpace, i)).AsByte());

                            if (mask1 != 0)
                            {
                                if (mask1 != -1)
                                    goto FOUND;

                                lo = i + Vector256<double>.Count;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step * Vector256<double>.Count - 1;
                        goto BS;
                    }

                    FOUND:
                    int index = (32 - BitUtils.LeadingZeroCount(mask)) / Unsafe.SizeOf<double>();
                    lo = i + index;
                    vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref searchSpace, lo));
                    var ceq1 = -UnsafeEx.Ceq(value, vLo);
                    return (ceq1 & lo) | (~ceq1 & ~lo);
                }

                BS:

                return BinarySearchAvx2LoHi(ref searchSpace, lo, hi, value);
            }
        }

#endif
    }
}
