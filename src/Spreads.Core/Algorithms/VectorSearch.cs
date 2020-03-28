// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//

using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Native;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Spreads.Utils;
#if HAS_INTRINSICS
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;

#endif

namespace Spreads.Algorithms
{
    /// <summary>
    /// Algorithms to find values in contiguous data (memory region or a data structure with an indexer), e.g. <see cref="Vec{T}"/>.
    /// WARNING: Methods in this static class do not perform bound checks and are intended to be used
    /// as building blocks in other parts that calculate bounds correctly and
    /// do performs required checks on external input.
    /// </summary>
    public static partial class VectorSearch
    {
        /// <summary>
        /// Performs classic binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchClassic<T>(ref T vecStart, int offset, int length, T value, KeyComparer<T> comparer = default)
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
                    int i = (int) (((uint) hi + (uint) lo) >> 1);

                    int c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i)));

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
        /// Performs classic binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        [SuppressMessage("ReSharper", "HeapView.BoxingAllocation")] // false warnings for (type)(object)value pattern
        public static int BinarySearch<T>(ref T vecStart, int length, T value, KeyComparer<T> comparer = default)
        {
#if HAS_INTRINSICS
            if (Avx2.IsSupported)
            {
                if (typeof(T) == typeof(sbyte))
                    return BinarySearchAvx(ref Unsafe.As<T, sbyte>(ref vecStart), length, (sbyte) (object) value);

                if (typeof(T) == typeof(short))
                    return BinarySearchAvx(ref Unsafe.As<T, short>(ref vecStart), length, (short) (object) value);

                if (typeof(T) == typeof(int))
                    return BinarySearchAvx(ref Unsafe.As<T, int>(ref vecStart), length, (int) (object) value);

                if (typeof(T) == typeof(long)
                    || typeof(T) == typeof(Timestamp)
                    || typeof(T) == typeof(DateTime)
                )
                    return BinarySearchAvx(ref Unsafe.As<T, long>(ref vecStart), length, Unsafe.As<T, long>(ref value));
            }
#endif

            // This one is actually very hard to beat in general case
            // because of memory access (cache miss) costs. In the classic
            // algorithm every memory access is useful, i.e. it halves the
            // search space. K-ary search has K-2 useless memory accesses.
            // E.g. for SIMD-ized search with K = 4 we do 4 memory accesses
            // but reduce the search space to the same size as 2 accesses
            // in the classic algorithm. SIMD doesn't speedup memory access,
            // which is the main cost for high number of items.
            return BinarySearchClassic(ref vecStart, 0, length, value, comparer);
        }

#if HAS_INTRINSICS
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int BinarySearchAvx2(ref long vecStart, int offset, int length, long value)
        {
            unchecked
            {
                int lo = offset;
                int hi = offset + length - 1;
                int mask;

                long vLo;

                if (lo > hi)
                    return ~lo;

                if (hi - lo < Vector256<long>.Count)
                    goto LINEAR;

                Vector256<long> vecI;
                Vector256<long> gt;
                var valVec = Vector256.Create(value);
                while (hi - lo >= Vector256<long>.Count << 2) // TODO 2x but index==4 should work, irregular test is reproducible
                {
                    var i = (int) (((uint) hi + (uint) lo - Vector256<long>.Count) >> 1);

                    vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<long>();
                            lo = i + index;
                            vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                            goto RETURN;
                        }

                        // val is not greater than all in vec
                        // not i-1, i could equal;
                        hi = i;
                    }
                    else
                    {
                        // val is larger than all in vec
                        lo = i + Vector256<long>.Count;
                    }
                }

                do
                {
                    vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, lo)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI); // _mm256_cmpgt_epi64
                    mask = Avx2.MoveMask(gt.AsByte());

                    var clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                    var index = (32 - clz) / Unsafe.SizeOf<long>();
                    lo += index;
                    if (lo > hi)
                        return ~lo;
                } while (mask == -1 & hi - lo >= Vector256<long>.Count);

                LINEAR:

                while (value > (vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo)))
                       && ++lo <= hi
                )
                {
                }

                RETURN:
                var ceq1 = -UnsafeEx.Ceq(value, vLo);
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

#endif

        /// <summary>
        /// Performs standard binary search and returns index of the value from the beginning of <paramref name="vec"/> (not from <paramref name="start"/>) or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T, TVec>(ref TVec vec, int start, int length, T value, KeyComparer<T> comparer = default)
            where TVec : IVector<T>
        {
            Debug.Assert(unchecked((uint) start + (uint) length) <= vec.Length);
            unchecked
            {
                int lo = start;
                int hi = start + length - 1;
                while (lo <= hi)
                {
                    int i = (int) (((uint) hi + (uint) lo) >> 1);

                    int c = comparer.Compare(value, vec.DangerousGetItem(i));
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

                return ~lo;
            }
        }

        /// <summary>
        /// Converts a result of a sorted search to result of directional search with direction of <see cref="Lookup"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SearchToLookup(int start, int length, Lookup lookup, int i)
        {
            if (i >= start)
            {
                if (lookup.IsEqualityOK())
                {
                    return i;
                }

                if (lookup == Lookup.LT)
                {
                    if (i == start)
                    {
                        return ~start;
                    }

                    i--;
                }
                else // depends on if (eqOk) above
                {
                    Debug.Assert(lookup == Lookup.GT);
                    if (i == start + length - 1)
                    {
                        return ~(start + length);
                    }

                    i++;
                }
            }
            else
            {
                if (lookup == Lookup.EQ)
                {
                    return i;
                }

                i = ~i;

                // LT or LE
                if (((uint) lookup & (uint) Lookup.LT) != 0)
                {
                    // i is idx of element that is larger, nothing here for LE/LT
                    if (i == start)
                    {
                        return ~start;
                    }

                    i--;
                }
                else
                {
                    Debug.Assert(((uint) lookup & (uint) Lookup.GT) != 0);
                    Debug.Assert(i <= start + length);
                    // if was negative, if it was ~length then there are no more elements for GE/GT
                    if (i == start + length)
                    {
                        return ~(start + length);
                    }

                    // i is the same, ~i is idx of element that is GT the value
                }
            }

            Debug.Assert(unchecked((uint) i) - start < unchecked((uint) length));
            return i;
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(ref T vecStart, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            Debug.Assert(length >= 0);

            var i = BinarySearch(ref vecStart, length, value, comparer);

            var li = SearchToLookup(0, length, lookup, i);

            if (li != i & li >= 0) // not &&
            {
                value = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, li));
            }

            return SearchToLookup(0, length, lookup, i);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T, TVec>(ref TVec vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
            where TVec : IVector<T>
        {
            Debug.Assert(unchecked((uint) start + (uint) length) <= vec.Length);

            int i = BinarySearch(ref vec, start, length, value, comparer);

            var li = SearchToLookup(start, length, lookup, i);

            if (li != i & li >= 0) // not &&
            {
                value = vec.DangerousGetItem(li);
            }

            return li;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(ref T vecStart, int length, T value, KeyComparer<T> comparer = default)
        {
            if (typeof(T) == typeof(Timestamp))
            {
                return InterpolationSearch(ref Unsafe.As<T, long>(ref vecStart), 0, length, Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(long))
            {
                return InterpolationSearch(ref Unsafe.As<T, long>(ref vecStart), 0, length, Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(int))
            {
                return InterpolationSearch(ref Unsafe.As<T, int>(ref vecStart), length, Unsafe.As<T, int>(ref value));
            }

            if (!KeyComparer<T>.Default.IsDiffable)
            {
                ThrowNonDiffable<T>();
            }

            return InterpolationSearchGeneric(ref vecStart, length, value, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T, TVec>(ref TVec vec, int start, int length, T value, KeyComparer<T> comparer = default)
            where TVec : IVector<T>
        {
            // TODO test if this works and is inlined well

            // TODO this doesn't look right and/or needed, review. Vector is not used anywhere so far, but the intent was to use it with strides

            if (typeof(TVec) == typeof(Vector<Timestamp>))
            {
                return InterpolationSearch(ref Unsafe.As<TVec, Vector<long>>(ref vec), start, length,
                    Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(Vector<long>))
            {
                return InterpolationSearch(ref Unsafe.As<TVec, Vector<long>>(ref vec), start, length,
                    Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(Vector<int>))
            {
                return InterpolationSearch(ref Unsafe.As<TVec, Vector<int>>(ref vec), start, length,
                    Unsafe.As<T, int>(ref value));
            }

            if (!KeyComparer<T>.Default.IsDiffable)
            {
                ThrowNonDiffable<T>();
            }

            return InterpolationSearchGeneric(ref vec, start, length, value, comparer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNonDiffable<T>()
        {
            ThrowHelper.ThrowNotSupportedException("KeyComparer<T>.Default.IsDiffable must be true for interpolation search: T is " + typeof(T).FullName);
        }

        /// <summary>
        /// Exponential
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch(ref long vecStart, int offset, int length, long value)
        {
            unchecked
            {
                int i;
                int lo = offset;
                int hi = offset + length - 1;
                if (lo < hi) // TODO review the limit when BS is faster
                {
                    long vlo = UnsafeEx.ReadUnaligned(ref vecStart);
                    long vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    long vRange = vhi - vlo;
                    int range = hi - lo;

                    Debug.Assert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = range * (double) (value - vlo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to vecStart
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                    if (value == vi)
                    {
                        goto FOUND;
                    }

                    var step = 1;

                    if (value < vi)
                    {
                        // lo = i - 1, but could be < hi
                        while ((i = i - step) >= offset)
                        {
                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value > vi)
                            {
                                lo = i + 1;
                                // vi(i + offset) was > value, so we could exclude this position for hi
                                hi = i + step - 1;
                                goto BIN_SEARCH;
                            }

                            // x2
                            step <<= 1;
                        }

                        Debug.Assert(i < offset);

                        // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + step;

                        Debug.Assert(lo == offset);
                        Debug.Assert(hi >= lo);
                    }
                    else // value > vi
                    {
                        // lo = i + 1, but could be > hi

                        while ((i = i + step) <= hi)
                        {
                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value < vi)
                            {
                                hi = i - 1;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - step + 1;
                                goto BIN_SEARCH;
                            }

                            step <<= 1;
                        }

                        Debug.Assert(i > hi);

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - step;

                        Debug.Assert(hi == offset + length - 1);
                        Debug.Assert(hi >= lo);
                    }
                }

                BIN_SEARCH:
#if HAS_INTRINSICS
                return BinarySearchClassic(ref vecStart, lo, 1 + hi - lo, value);

                // return BinarySearchAvx2(ref vecStart, lo, 1 + hi - lo, value);
#else
                return BinarySearchClassic(ref vecStart, lo, 1 + hi - lo, value);
#endif

                FOUND:
                return i;
            }
        }

#if HAS_INTRINSICS
        /// <summary>
        /// Exponential
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearchAvx(ref long vecStart, int offset, int length, long value)
        {
            unchecked
            {
                int i;
                int lo = offset;
                int hi = offset + length - 1;
                int mask;

                if (hi - lo > Vector256<long>.Count * 8) // TODO review the limit when BS is faster
                {
                    Vector256<long> vecI;
                    Vector256<long> gt;
                    var valVec = Vector256.Create(value);

                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                    long vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    var range = hi - lo;
                    long vRange = vhi - vLo;

                    ThrowHelper.DebugAssert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange) - (Vector256<long>.Count >> 1);

                    var maxI = range - (Vector256<long>.Count - 1);
                    if ((uint) i > maxI)
                        i = i < 0 ? 0 : maxI;
                    // make i relative to vecStart
                    i += lo;

                    vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    var step = Vector256<long>.Count;

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                            int index = (32 - clz) / Unsafe.SizeOf<long>();
                            lo = i + index;
                            // vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                            goto RETURN;
                        }

                        // mask == 0, val is not greater than all in vec
                        // not i-1, i could equal;
                        // hi = i;

                        // lo = i - 1, but could be < hi
                        while ((i -= step) >= offset)
                        {
                            // vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));
                            gt = Avx2.CompareGreaterThan(valVec, vecI); // _mm256_cmpgt_epi64
                            mask = Avx2.MoveMask(gt.AsByte());

                            if (mask != -1)
                            {
                                if (mask != 0)
                                {
                                    int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                                    int index = (32 - clz) / Unsafe.SizeOf<long>();
                                    lo = i + index;
                                    // vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                                    goto RETURN;
                                }

                                // val is not greater than all in vec
                                // not i-1, i could equal;
                                // hi = i;
                                // x2
                                step <<= 1;
                            }
                            else
                            {
                                // val is larger than all in vec
                                lo = i + Vector256<long>.Count;
                                hi = i + step; // not -1, that value could be equal
                                goto BIN_SEARCH;
                            }
                        }

                        // ThrowHelper.DebugAssert(i < offset);
                        //
                        // // // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + step;
                        //
                        // ThrowHelper.DebugAssert(lo == offset);
                        // ThrowHelper.DebugAssert(hi >= lo);
                    }
                    else
                    {
                        // mask == -1, val is larger than all in vec
                        // lo = i + Vector256<long>.Count;

                        while ((i = i + step) <= hi - (Vector256<long>.Count - 1))
                        {
                            // vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));
                            gt = Avx2.CompareGreaterThan(valVec, vecI); // _mm256_cmpgt_epi64
                            mask = Avx2.MoveMask(gt.AsByte());

                            if (mask != -1)
                            {
                                if (mask != 0)
                                {
                                    int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                                    int index = (32 - clz) / Unsafe.SizeOf<long>();
                                    lo = i + index;
                                    // vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                                    goto RETURN;
                                }

                                // val is not greater than all in vec
                                // not i-1, i could equal;
                                hi = i;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - step + 1;
                                goto BIN_SEARCH;
                            }

                            // val is larger than all in vec
                            // lo = i + Vector256<long>.Count;
                            step <<= 1;

                            // if (value == vi)
                            // {
                            //     goto FOUND;
                            // }
                            //
                            // if (value < vi)
                            // {
                            //     hi = i - 1;
                            //     // vi(i - offset) was > value, so we could exclude this position for hi
                            //     lo = i - offset + 1;
                            //     goto BIN_SEARCH;
                            // }

                            // offset <<= 1;
                        }

                        ThrowHelper.DebugAssert(i > hi - (Vector256<long>.Count - 1));

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - step;

                        ThrowHelper.DebugAssert(hi - offset == length - 1);
                        ThrowHelper.DebugAssert(hi >= lo - (Vector256<long>.Count - 1));
                    }
                }

                BIN_SEARCH:
                return BinarySearchAvx2(ref vecStart, lo, 1 + hi - lo, value);

                RETURN:
                // ThrowHelper.DebugAssert(lo < offset + length);
                var ceq1 = -UnsafeEx.Ceq(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo)));
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

        /// <summary>
        /// Exponential
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearchAvx2(ref long vecStart, int offset, int length, long value)
        {
            unchecked
            {
                int i;
                int lo = offset;
                int hi = offset + length - 1;
                int mask;

                if (hi - lo > Vector256<long>.Count * 16) // TODO review the limit when BS is faster
                {
                    Vector256<long> vecI;
                    Vector256<long> gt;
                    var valVec = Vector256.Create(value);

                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                    long vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    var range = hi - lo;
                    long vRange = vhi - vLo;

                    ThrowHelper.DebugAssert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange) - (Vector256<long>.Count >> 1);

                    var maxI = range - (Vector256<long>.Count - 1);
                    if ((uint) i > maxI)
                        i = i < 0 ? 0 : maxI;
                    // make i relative to vecStart
                    i += lo;

                    vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));
                    gt = Avx2.CompareGreaterThan(valVec, vecI);
                    mask = Avx2.MoveMask(gt.AsByte());

                    var step = Vector256<long>.Count;

                    if (mask != -1)
                    {
                        if (mask != 0)
                        {
                            // int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                            // int index = (32 - clz) / Unsafe.SizeOf<long>();
                            // lo = i + index;
                            // vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                            goto RETURNX;
                        }

                        // mask == 0, val is not greater than all in vec !!!!!!!!!!!!!!!!!!!!!!!!!!
                        // not i-1, i could equal;
                        // hi = i;

                        // lo = i - 1, but could be < hi
                        while ((i -= step) >= offset)
                        {
                            // vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));
                            gt = Avx2.CompareGreaterThan(valVec, vecI); // _mm256_cmpgt_epi64
                            mask = Avx2.MoveMask(gt.AsByte());

                            if (mask != -1)
                            {
                                if (mask != 0)
                                {
                                    // int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                                    // int index = (32 - clz) / Unsafe.SizeOf<long>();
                                    // lo = i + index;
                                    // vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                                    goto RETURNX;
                                }

                                // val is not greater than all in vec
                                // not i-1, i could equal;
                                // hi = i;
                                // x2
                                step <<= 1;
                            }
                            else
                            {
                                // val is larger than all in vec
                                lo = i + Vector256<long>.Count;
                                hi = i + step; // not -1, that value could be equal
                                goto BIN_SEARCH;
                            }
                        }

                        // ThrowHelper.DebugAssert(i < offset);
                        //
                        // // // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + step;
                        //
                        // ThrowHelper.DebugAssert(lo == offset);
                        // ThrowHelper.DebugAssert(hi >= lo);
                    }
                    else
                    {
                        // mask == -1, val is larger than all in vec
                        // lo = i + Vector256<long>.Count;

                        while ((i = i + step) <= hi - (Vector256<long>.Count - 1))
                        {
                            // vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            vecI = Unsafe.ReadUnaligned<Vector256<long>>(ref Unsafe.As<long, byte>(ref Unsafe.Add(ref vecStart, i)));
                            gt = Avx2.CompareGreaterThan(valVec, vecI); // _mm256_cmpgt_epi64
                            mask = Avx2.MoveMask(gt.AsByte());

                            if (mask != -1)
                            {
                                if (mask != 0)
                                {
                                    // int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                                    // int index = (32 - clz) / Unsafe.SizeOf<long>();
                                    // lo = i + index;
                                    // vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                                    goto RETURNX;
                                }

                                // val is not greater than all in vec
                                // not i-1, i could equal;
                                hi = i;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - step + 1;
                                goto BIN_SEARCH;
                            }

                            // val is larger than all in vec
                            // lo = i + Vector256<long>.Count;
                            step <<= 1;

                            // if (value == vi)
                            // {
                            //     goto FOUND;
                            // }
                            //
                            // if (value < vi)
                            // {
                            //     hi = i - 1;
                            //     // vi(i - offset) was > value, so we could exclude this position for hi
                            //     lo = i - offset + 1;
                            //     goto BIN_SEARCH;
                            // }

                            // offset <<= 1;
                        }

                        ThrowHelper.DebugAssert(i > hi - (Vector256<long>.Count - 1));

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - step;

                        ThrowHelper.DebugAssert(hi - offset == length - 1);
                        ThrowHelper.DebugAssert(hi >= lo - (Vector256<long>.Count - 1));
                    }
                }

                BIN_SEARCH:
                return BinarySearchAvx2(ref vecStart, lo, 1 + hi - lo, value);

                RETURNX:
                int clz = (int) Lzcnt.LeadingZeroCount((uint) mask);
                int index = (32 - clz) / Unsafe.SizeOf<long>();
                lo = i + index;
                // ThrowHelper.DebugAssert(lo < offset + length);
                var ceq1 = -UnsafeEx.Ceq(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo)));
                return (ceq1 & lo) | (~ceq1 & ~lo);
            }
        }

        /// <summary>
        /// Exponential
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearchAvx3(ref long vecStart, int offset, int length, long value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches 

            unchecked
            {
                int i;
                int lo = offset;
                int hi = offset + length - 1;

                if (hi - lo > Vector256<long>.Count * 4) // TODO review the limit when BS is faster
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    var range = hi - lo;
                    long vRange = vhi - vLo;

                    ThrowHelper.DebugAssert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = range * (double) (value - vLo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > range)
                        i = i < 0 ? 0 : range;
                    // make i relative to vecStart
                    i += lo;

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));
                    if (value == vi)
                        goto FOUND;

                    var step = 1;

                    if (value > vi)
                    {
                        while (true)
                        {
                            i += step;
                            if (i > hi)
                            {
                                break;
                            }

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value <= vi)
                            {
                                if (value == vi)
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
                            {
                                break;
                            }

                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value >= vi)
                            {
                                if (value == vi)
                                    goto FOUND;

                                lo = i + 1;
                                break;
                            }

                            step <<= 1;
                        }

                        hi = i + step - 1;
                    }
                }

                return BinarySearchAvx2(ref vecStart, lo, 1 + hi - lo, value);

                FOUND:
                return i;
            }
        }

        /// <summary>
        /// Exponential
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int InterpolationSearchAvx4(ref long vecStart, int offset, int length, long value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches 

            unchecked
            {
                int i;
                int lo = offset;
                int hi = offset + length - 1;

                if (hi - lo > Vector256<long>.Count * 8) // TODO review the limit when BS is faster
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    var range = hi - lo;
                    long vRange = vhi - vLo;

                    ThrowHelper.DebugAssert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = range * (double) (value - vLo);

                    i = lo + (int) (nominator / vRange);

                    var step = 0;
                    var inRange = 0;
                    while ((uint) (i - lo) < range)
                    {
                        var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                        var c = value.CompareTo(vi); // (UnsafeEx.Cgt(value, vi) << 1) - 1;

                        if (c * step < 0)
                        {
                            // on first iteration direction is zero and this condition is never hit

                            // changed direction, need to calculate lo/hi
                            // goto XXX;
                            // var iLessStep = i - step;
                            // if (step > 0)
                            // {
                            //     lo = iLessStep + 1;
                            //     hi = i - 1;
                            // }
                            // else
                            // {
                            //     lo = i + 1;
                            //     hi = iLessStep - 1;
                            // }
                            //
                            inRange = -1;
                            break;
                        }

                        if (c == 0)
                            return i;

                        // if(direction == 0)
                        // direction = c;
                        var stepIsZero = UnsafeEx.Ceq(step, 0);
                        step = (step + stepIsZero * c) << (1 - stepIsZero);
                        i += step;
                        // iter++;
                        // else
                        // {
                        //     break;
                        // }
                    }

                    var iLessStep = i - step;
                    // if (step != 0)
                    // {
                    //     if (step > 0)
                    //     {
                    //         lo = iLessStep + 1;
                    //     }
                    //     else
                    //     {
                    //         hi = iLessStep - 1;
                    //     }
                    // }

                    // XXX:
                    if (step != 0)
                    {
                        if (step > 0)
                        {
                            lo = iLessStep + 1;
                            hi = (inRange & (i - 1)) | (~inRange & hi);
                        }
                        else
                        {
                            lo = (inRange & (i + 1)) | (~inRange & lo);
                            hi = iLessStep - 1;
                        }
                    }
                }

                return BinarySearchAvx2(ref vecStart, lo, 1 + hi - lo, value);
            }
        }

        /// <summary>
        /// Exponential
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int InterpolationSearchAvx5(ref long vecStart, int offset, int length, long value)
        {
            // Try interpolation only for big-enough lengths and do minimal job,
            // just find the range with exponential search with minimal branches 

            unchecked
            {
                int i;
                int lo = offset;
                int hi = offset + length - 1;

                if (hi - lo > Vector256<long>.Count * 8) // TODO review the limit when BS is faster
                {
                    var vLo = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, lo));
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    int range = hi - lo;
                    long vRange = vhi - vLo;

                    ThrowHelper.DebugAssert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = range * (double) (value - vLo);

                    i = lo + (int) (nominator / vRange);

                    // if ((uint) i > range)
                    //     i = i < 0 ? 0 : (int)range;
                    // // make i relative to vecStart
                    // i += lo;
                    //
                    // var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));
                    //
                    // var c =  value.CompareTo(vi); // (UnsafeEx.Cgt(value, vi) << 1) - 1;
                    // if (c == 0)
                    //     goto FOUND;

                    // var c = 0;
                    var step = 0; // ((c >> 31) << 1) + 1;

                    while ((uint) (i - lo) < range)
                    {
                        var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));
                        
                        if (value < vi)
                        {
                            if (step > 0)
                            {
                                // lo = i - step + 1;
                                hi = i - 1;
                                break;
                            }

                            step = (step << 1) - 1;
                        }
                        else
                        {
                            if (value == vi)
                                goto FOUND;
                            
                            if (step < 0)
                            {
                                // hi = i - step - 1;
                                lo = i + 1;
                                break;
                            }

                            step = (step << 1) + 1;
                        }

                        i += step;
                    }

                    if (step != 0)
                    {
                        var iLessStep = i - step;
                        if (step > 0)
                        {
                            lo = iLessStep + 1;
                        }
                        else
                        {
                            hi = iLessStep - 1;
                        }
                    }
                }

                BINARY:
                return BinarySearchAvx2(ref vecStart, lo, 1 + hi - lo, value);

                FOUND:
                return i;
            }
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch2(ref long vecStart, int length, long value)
        {
            const int start = 0;

            unchecked
            {
                // this is definitive version, all other overloads must do exactly the same operations

                int i;
                int lo = start;
                int hi = start + length - 1;
                if (lo < hi)
                {
                    long vlo = UnsafeEx.ReadUnaligned(ref vecStart);
                    long vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    long vRange = vhi - vlo;

                    Debug.Assert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = (hi - lo) * (double) (value - vlo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > hi)
                    {
                        i = i > hi ? hi : lo;
                    }

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                    if (value == vi)
                    {
                        goto FOUND;
                    }

                    var offset = 1;

                    if (value < vi)
                    {
                        // lo = i - 1, but could be < hi
                        while ((i = i - offset) >= start)
                        {
                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value > vi)
                            {
                                lo = i + 1;
                                // vi(i + offset) was > value, so we could exclude this position for hi
                                hi = i + offset - 1;
                                goto BIN_SEARCH;
                            }

                            // x2
                            offset <<= 1;
                        }

                        Debug.Assert(i < start);

                        // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + offset;

                        Debug.Assert(lo == start);
                        Debug.Assert(hi >= lo);
                    }
                    else // value > vi
                    {
                        // lo = i + 1, but could be > hi

                        while ((i = i + offset) <= hi)
                        {
                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value < vi)
                            {
                                hi = i - 1;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - offset + 1;
                                goto BIN_SEARCH;
                            }

                            offset <<= 1;
                        }

                        Debug.Assert(i > hi);

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - offset;

                        Debug.Assert(hi == start + length - 1);
                        Debug.Assert(hi >= lo);
                    }
                }

                BIN_SEARCH:

                // This returns index from lo to hi, so we need to adjust for the narrowed range
                var iBs = BinarySearch(ref Unsafe.Add(ref vecStart, lo), 1 + hi - lo, value, default);

                if (iBs >= 0)
                {
                    return lo + iBs;
                }
                else
                {
                    return ~(lo + ~iBs);
                }

                FOUND:
                return i;
            }
        }

        /// <summary>
        /// Returns index from the beginning of the vector (not from the <paramref name="start"/> parameter).
        /// </summary>
        /// <param name="vec"></param>
        /// <param name="start">Start of the search range.</param>
        /// <param name="length">Length of the search range.</param>
        /// <param name="value">Value to search.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<TVec>(ref TVec vec, int start, int length, long value)
            where TVec : IVector<long>
        {
            unchecked
            {
                // adjusted copy of long

                int i;
                int lo = start;
                int hi = start + length - 1;
                if (lo < hi)
                {
                    long vlo = vec.DangerousGetItem(lo);
                    long vhi = vec.DangerousGetItem(hi);
                    long vRange = vhi - vlo;

                    Debug.Assert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = (hi - lo) * (double) (value - vlo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > hi)
                    {
                        i = i > hi ? hi : lo;
                    }

                    var vi = vec.DangerousGetItem(i);

                    if (value == vi)
                    {
                        goto FOUND;
                    }

                    var offset = 1;

                    if (value < vi)
                    {
                        // lo = i - 1, but could be < hi
                        while ((i = i - offset) >= start)
                        {
                            vi = vec.DangerousGetItem(i);

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value > vi)
                            {
                                lo = i + 1;
                                // vi(i + offset) was > value, so we could exclude this position for hi
                                hi = i + offset - 1;
                                goto BIN_SEARCH;
                            }

                            // x2
                            offset <<= 1;
                        }

                        Debug.Assert(i < start);

                        // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + offset;

                        Debug.Assert(lo == start);
                        Debug.Assert(hi >= lo);
                    }
                    else // value > vi
                    {
                        // lo = i + 1, but could be > hi

                        while ((i = i + offset) <= hi)
                        {
                            vi = vec.DangerousGetItem(i);

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value < vi)
                            {
                                hi = i - 1;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - offset + 1;
                                goto BIN_SEARCH;
                            }

                            offset <<= 1;
                        }

                        Debug.Assert(i > hi);

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - offset;

                        Debug.Assert(hi == start + length - 1);
                        Debug.Assert(hi >= lo);
                    }
                }

                BIN_SEARCH:
                return BinarySearch(ref vec, lo, 1 + hi - lo, value, default);

                FOUND:
                return i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch(ref int vecStart, int length, int value)
        {
            const int start = 0;

            unchecked
            {
                // adjusted copy of long
                int i;
                int lo = start;
                int hi = start + length - 1;
                if (lo < hi)
                {
                    long vlo = UnsafeEx.ReadUnaligned(ref vecStart);
                    long vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    long vRange = vhi - vlo;

                    Debug.Assert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = (hi - lo) * (double) (value - vlo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > hi)
                    {
                        i = i > hi ? hi : lo;
                    }

                    var vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                    if (value == vi)
                    {
                        goto FOUND;
                    }

                    var offset = 1;

                    if (value < vi)
                    {
                        // lo = i - 1, but could be < hi
                        while ((i = i - offset) >= start)
                        {
                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value > vi)
                            {
                                lo = i + 1;
                                // vi(i + offset) was > value, so we could exclude this position for hi
                                hi = i + offset - 1;
                                goto BIN_SEARCH;
                            }

                            // x2
                            offset <<= 1;
                        }

                        Debug.Assert(i < start);

                        // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + offset;

                        Debug.Assert(lo == start);
                        Debug.Assert(hi >= lo);
                    }
                    else // value > vi
                    {
                        // lo = i + 1, but could be > hi

                        while ((i = i + offset) <= hi)
                        {
                            vi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i));

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value < vi)
                            {
                                hi = i - 1;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - offset + 1;
                                goto BIN_SEARCH;
                            }

                            offset <<= 1;
                        }

                        Debug.Assert(i > hi);

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - offset;

                        Debug.Assert(hi == start + length - 1);
                        Debug.Assert(hi >= lo);
                    }
                }

                BIN_SEARCH:

                var iBs = BinarySearch(ref Unsafe.Add(ref vecStart, lo), 1 + hi - lo, value, default);

                if (iBs >= 0)
                {
                    return lo + iBs;
                }
                else
                {
                    return ~(lo + ~iBs);
                }

                FOUND:
                return i;
            }
        }

        /// <summary>
        /// Returns index from the beginning of the vector (not from the <paramref name="start"/> parameter).
        /// </summary>
        /// <param name="vec"></param>
        /// <param name="start">Start of the search range.</param>
        /// <param name="length">Length of the search range.</param>
        /// <param name="value">Value to search.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<TVec>(ref TVec vec, int start, int length, int value)
            where TVec : IVector<int>
        {
            unchecked
            {
                // adjusted copy of long

                int i;
                int lo = start;
                int hi = start + length - 1;
                if (lo < hi)
                {
                    long vlo = vec.DangerousGetItem(lo);
                    long vhi = vec.DangerousGetItem(hi);
                    long vRange = vhi - vlo;

                    Debug.Assert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = (hi - lo) * (double) (value - vlo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > hi)
                    {
                        i = i > hi ? hi : lo;
                    }

                    var vi = vec.DangerousGetItem(i);

                    if (value == vi)
                    {
                        goto FOUND;
                    }

                    var offset = 1;

                    if (value < vi)
                    {
                        // lo = i - 1, but could be < hi
                        while ((i = i - offset) >= start)
                        {
                            vi = vec.DangerousGetItem(i);

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value > vi)
                            {
                                lo = i + 1;
                                // vi(i + offset) was > value, so we could exclude this position for hi
                                hi = i + offset - 1;
                                goto BIN_SEARCH;
                            }

                            // x2
                            offset <<= 1;
                        }

                        Debug.Assert(i < start);

                        // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + offset;

                        Debug.Assert(lo == start);
                        Debug.Assert(hi >= lo);
                    }
                    else // value > vi
                    {
                        // lo = i + 1, but could be > hi

                        while ((i = i + offset) <= hi)
                        {
                            vi = vec.DangerousGetItem(i);

                            if (value == vi)
                            {
                                goto FOUND;
                            }

                            if (value < vi)
                            {
                                hi = i - 1;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - offset + 1;
                                goto BIN_SEARCH;
                            }

                            offset <<= 1;
                        }

                        Debug.Assert(i > hi);

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - offset;

                        Debug.Assert(hi == start + length - 1);
                        Debug.Assert(hi >= lo);
                    }
                }

                BIN_SEARCH:
                return BinarySearch(ref vec, lo, 1 + hi - lo, value, default);

                FOUND:
                return i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int InterpolationSearchGeneric<T>(ref T vecStart, int length, T value, KeyComparer<T> comparer = default)
        {
            const int start = 0;
            unchecked
            {
                // adjusted copy of long

                int i;
                int lo = start;
                int hi = start + length - 1;
                if (lo < hi)
                {
                    var vlo = UnsafeEx.ReadUnaligned(ref vecStart);
                    var vhi = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, hi));
                    long vRange = comparer.Diff(vhi, vlo);

                    Debug.Assert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = (hi - lo) * (double) comparer.Diff(value, vlo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > hi) // likely false, edge cases with float division
                        i = i > hi ? hi : lo;

                    int c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i)));

                    if (c == 0)
                        goto FOUND;

                    var offset = 1;

                    if (c < 0)
                    {
                        // lo = i - 1, but could be < hi
                        while ((i = i - offset) >= start)
                        {
                            c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i)));

                            if (c == 0)
                            {
                                goto FOUND;
                            }

                            if (c > 0)
                            {
                                lo = i + 1;
                                // vi(i + offset) was > value, so we could exclude this position for hi
                                hi = i + offset - 1;
                                goto BIN_SEARCH;
                            }

                            // x2
                            offset <<= 1;
                        }

                        Debug.Assert(i < start);

                        // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + offset;

                        Debug.Assert(lo == start);
                        Debug.Assert(hi >= lo);
                    }
                    else // value > vi
                    {
                        // lo = i + 1, but could be > hi

                        while ((i = i + offset) <= hi)
                        {
                            c = comparer.Compare(value, UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, i)));

                            if (c == 0)
                            {
                                goto FOUND;
                            }

                            if (c < 0)
                            {
                                hi = i - 1;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - offset + 1;
                                goto BIN_SEARCH;
                            }

                            offset <<= 1;
                        }

                        Debug.Assert(i > hi);

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - offset;

                        Debug.Assert(hi == start + length - 1);
                        Debug.Assert(hi >= lo);
                    }
                }

                BIN_SEARCH:
                var iBs = BinarySearch(ref Unsafe.Add(ref vecStart, lo), 1 + hi - lo, value, default);

                var clt = iBs >> 31;
                return (clt & (~(lo + ~iBs))) | (~clt & (lo + iBs));

                // if (iBs >= 0)
                // {
                //     return lo + iBs;
                // }
                // else
                // {
                //     return ~(lo + ~iBs);
                // }

                FOUND:
                return i;
            }
        }

        /// <summary>
        /// Returns index from the beginning of the vector (not from the <paramref name="start"/> parameter).
        /// </summary>
        /// <param name="vec"></param>
        /// <param name="start">Start of the search range.</param>
        /// <param name="length">Length of the search range.</param>
        /// <param name="value">Value to search.</param>
        /// <param name="comparer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int InterpolationSearchGeneric<T, TVec>(ref TVec vec, int start, int length, T value, KeyComparer<T> comparer = default)
            where TVec : IVector<T>
        {
            unchecked
            {
                // adjusted copy of long

                int i;
                int lo = start;
                int hi = start + length - 1;
                if (lo < hi)
                {
                    var vlo = vec.DangerousGetItem(lo);
                    var vhi = vec.DangerousGetItem(hi);
                    long vRange = comparer.Diff(vhi, vlo);

                    Debug.Assert(vRange > 0);

                    // (hi - lo) <= int32.MaxValue
                    // vlo could be zero while value could easily be close to int64.MaxValue (nanos in unix time, we are now between 60 and 61 bit at 60.4)
                    // convert to double here to avoid overflow and for much faster calculations
                    // (only 4 cycles vs 25 cycles https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/)
                    double nominator = (hi - lo) * (double) comparer.Diff(value, vlo);

                    i = (int) (nominator / vRange);

                    if ((uint) i > hi)
                    {
                        i = i > hi ? hi : lo;
                    }

                    int c = comparer.Compare(value, vec.DangerousGetItem(i));

                    if (c == 0)
                    {
                        goto FOUND;
                    }

                    var offset = 1;

                    if (c < 0)
                    {
                        // lo = i - 1, but could be < hi
                        while ((i = i - offset) >= start)
                        {
                            c = comparer.Compare(value, vec.DangerousGetItem(i));

                            if (c == 0)
                            {
                                goto FOUND;
                            }

                            if (c > 0)
                            {
                                lo = i + 1;
                                // vi(i + offset) was > value, so we could exclude this position for hi
                                hi = i + offset - 1;
                                goto BIN_SEARCH;
                            }

                            // x2
                            offset <<= 1;
                        }

                        Debug.Assert(i < start);

                        // i was decremented by offset and became < start, restore it and search from zero
                        hi = i + offset;

                        Debug.Assert(lo == start);
                        Debug.Assert(hi >= lo);
                    }
                    else // value > vi
                    {
                        // lo = i + 1, but could be > hi

                        while ((i = i + offset) <= hi)
                        {
                            c = comparer.Compare(value, vec.DangerousGetItem(i));

                            if (c == 0)
                            {
                                goto FOUND;
                            }

                            if (c < 0)
                            {
                                hi = i - 1;
                                // vi(i - offset) was > value, so we could exclude this position for hi
                                lo = i - offset + 1;
                                goto BIN_SEARCH;
                            }

                            offset <<= 1;
                        }

                        Debug.Assert(i > hi);

                        // i was incremented by offset and became > hi, restore it and search to hi
                        lo = i - offset;

                        Debug.Assert(hi == start + length - 1);
                        Debug.Assert(hi >= lo);
                    }
                }

                BIN_SEARCH:
                return BinarySearch(ref vec, lo, 1 + hi - lo, value, default);

                FOUND:
                return i;
            }
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(ref T vecStart, int length, ref T value, Lookup lookup,
            KeyComparer<T> comparer = default)
        {
            Debug.Assert(length >= 0);

            var i = InterpolationSearch(ref vecStart, length, value, comparer);

            var li = SearchToLookup(0, length, lookup, i);

            if (li != i & li >= 0) // not &&
            {
                value = UnsafeEx.ReadUnaligned(ref Unsafe.Add(ref vecStart, li));
            }

            return li;
        }

        /// <summary>
        /// Returns index from the beginning of the vector (not from the <paramref name="start"/> parameter).
        /// </summary>
        /// <param name="vec"></param>
        /// <param name="start">Start of the search range.</param>
        /// <param name="length">Length of the search range.</param>
        /// <param name="value">Value to search.</param>
        /// <param name="lookup"></param>
        /// <param name="comparer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T, TVec>(ref TVec vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
            where TVec : IVector<T>
        {
            Debug.Assert(unchecked((uint) start + (uint) length) <= vec.Length);

            var i = InterpolationSearch(ref vec, start, length, value, comparer);

            var li = SearchToLookup(start, length, lookup, i);

            if (li != i & li >= 0) // not &&
            {
                value = vec.DangerousGetItem(li);
            }

            return li;
        }

        /// <summary>
        /// Performs interpolation search for well-known types and binary search for other types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SortedSearch<T>(ref T vecStart, int length, T value, KeyComparer<T> comparer = default)
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
            {
                return InterpolationSearch(ref vecStart, length, value, comparer);
            }

            return BinarySearch(ref vecStart, length, value, comparer);
        }

        /// <summary>
        /// Performs interpolation search for well-known types and binary search for other types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SortedSearch<T, TVec>(ref TVec vec, int start, int length, T value, KeyComparer<T> comparer = default)
            where TVec : IVector<T>
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
            {
                return InterpolationSearch(ref vec, start, length, value, comparer);
            }

            return BinarySearch(ref vec, start, length, value, comparer);
        }

        /// <summary>
        /// Performs interpolation lookup for well-known types and binary lookup for other types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SortedLookup<T>(ref T vecStart, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
            {
                return InterpolationLookup(ref vecStart, length, ref value, lookup, comparer);
            }

            return BinaryLookup(ref vecStart, length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Performs interpolation lookup for well-known types and binary lookup for other types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SortedLookup<T, TVec>(ref TVec vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
            where TVec : IVector<T>
        {
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (Settings.UseInterpolatedSearchForKnownTypes && KeyComparer<T>.IsDiffableSafe)
            {
                return InterpolationLookup(ref vec, start, length, ref value, lookup, comparer);
            }

            return BinaryLookup(ref vec, start, length, ref value, lookup, comparer);
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal static int IndexOf<T>(ref T vecStart, T value, int length)
        //{
        //    Debug.Assert(length >= 0);

        //    if (Vector.IsHardwareAccelerated)
        //    {
        //        if (typeof(T) == typeof(byte))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, byte>(ref vecStart), Unsafe.As<T, byte>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(sbyte))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, sbyte>(ref vecStart), Unsafe.As<T, sbyte>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(ushort))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, ushort>(ref vecStart), Unsafe.As<T, ushort>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(short))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, short>(ref vecStart), Unsafe.As<T, short>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(uint))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, uint>(ref vecStart), Unsafe.As<T, uint>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(int))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, int>(ref vecStart), Unsafe.As<T, int>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(ulong))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, ulong>(ref vecStart), Unsafe.As<T, ulong>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(long))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, long>(ref vecStart), Unsafe.As<T, long>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(float))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, float>(ref vecStart), Unsafe.As<T, float>(ref value), length);
        //        }
        //        if (typeof(T) == typeof(double))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, double>(ref vecStart), Unsafe.As<T, double>(ref value), length);
        //        }

        //        // non-standard
        //        // Treat Timestamp as long because it is a single long internally
        //        if (typeof(T) == typeof(Timestamp))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, long>(ref vecStart), Unsafe.As<T, long>(ref value), length);
        //        }

        //        // Treat DateTime as ulong because it is a single ulong internally
        //        if (typeof(T) == typeof(DateTime))
        //        {
        //            return IndexOfVectorized(ref Unsafe.As<T, ulong>(ref vecStart), Unsafe.As<T, ulong>(ref value), length);
        //        }
        //    }

        //    return IndexOfSimple(ref vecStart, value, length);
        //}

        //internal static unsafe int IndexOfVectorized<T>(ref T searchSpace, T value, int length) where T : struct
        //{
        //    unchecked
        //    {
        //        Debug.Assert(length >= 0);

        //        IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
        //        IntPtr nLength = (IntPtr)length;

        //        if (Vector.IsHardwareAccelerated && length >= System.Numerics.Vector<T>.Count * 2)
        //        {
        //            int unaligned = (int)Unsafe.AsPointer(ref searchSpace) & (System.Numerics.Vector<T>.Count - 1);
        //            nLength = (IntPtr)((System.Numerics.Vector<T>.Count - unaligned) & (System.Numerics.Vector<T>.Count - 1));
        //        }

        //    SequentialScan:
        //        while ((byte*)nLength >= (byte*)8)
        //        {
        //            nLength -= 8;

        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset)))
        //                goto Found;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 1)))
        //                goto Found1;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 2)))
        //                goto Found2;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 3)))
        //                goto Found3;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 4)))
        //                goto Found4;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 5)))
        //                goto Found5;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 6)))
        //                goto Found6;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 7)))
        //                goto Found7;

        //            offset += 8;
        //        }

        //        if ((byte*)nLength >= (byte*)4)
        //        {
        //            nLength -= 4;

        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset)))
        //                goto Found;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 1)))
        //                goto Found1;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 2)))
        //                goto Found2;
        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 3)))
        //                goto Found3;

        //            offset += 4;
        //        }

        //        while ((byte*)nLength > (byte*)0)
        //        {
        //            nLength -= 1;

        //            if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset)))
        //                goto Found;

        //            offset += 1;
        //        }

        //        if (Vector.IsHardwareAccelerated && ((int)(byte*)offset < length))
        //        {
        //            nLength = (IntPtr)((length - (int)(byte*)offset) & ~(System.Numerics.Vector<T>.Count - 1));

        //            // Get comparison Vector
        //            System.Numerics.Vector<T> vComparison = new System.Numerics.Vector<T>(value);

        //            while ((byte*)nLength > (byte*)offset)
        //            {
        //                var vMatches = Vector.Equals(vComparison,
        //                    Unsafe.ReadUnaligned<System.Numerics.Vector<T>>(
        //                        ref Unsafe.As<T, byte>(ref Unsafe.Add(ref searchSpace, offset))));
        //                if (System.Numerics.Vector<T>.Zero.Equals(vMatches))
        //                {
        //                    offset += System.Numerics.Vector<T>.Count;
        //                    continue;
        //                }

        //                // Find offset of first match
        //                return (int)(byte*)offset + IndexOfSimple(ref Unsafe.Add(ref searchSpace, offset), value, System.Numerics.Vector<T>.Count);
        //                // TODO copy that final step from corefx
        //                // return (int)(byte*)offset + LocateFirstFoundByte(vMatches);
        //            }

        //            if ((int)(byte*)offset < length)
        //            {
        //                nLength = (IntPtr)(length - (int)(byte*)offset);
        //                goto SequentialScan;
        //            }
        //        }

        //        return -1;
        //    Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
        //        return (int)(byte*)offset;
        //    Found1:
        //        return (int)(byte*)(offset + 1);
        //    Found2:
        //        return (int)(byte*)(offset + 2);
        //    Found3:
        //        return (int)(byte*)(offset + 3);
        //    Found4:
        //        return (int)(byte*)(offset + 4);
        //    Found5:
        //        return (int)(byte*)(offset + 5);
        //    Found6:
        //        return (int)(byte*)(offset + 6);
        //    Found7:
        //        return (int)(byte*)(offset + 7);
        //    }
        //}

        ///// <summary>
        ///// Simple implementation using for loop.
        ///// </summary>
        //[MethodImpl(MethodImplOptions.NoOptimization)]
        //internal static int IndexOfSimple<T>(ref T vecStart, T value, int length)
        //{
        //    for (int i = 0; i < length; i++)
        //    {
        //        if (UnsafeEx.EqualsConstrained(ref Unsafe.Add(ref vecStart, i), ref value))
        //        {
        //            return i;
        //        }
        //    }
        //    return -1;
        //}

        //// Taken from System.Text.Json.JsonReaderHelper.

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal static unsafe int IndexOfOrLessThan(ref byte searchSpace, byte value0, byte value1, byte lessThan, int length)
        //{
        //    unchecked
        //    {
        //        Debug.Assert(length >= 0);

        //        uint uValue0 = value0; // Use uint for comparisons to avoid unnecessary 8->32 extensions
        //        uint uValue1 = value1; // Use uint for comparisons to avoid unnecessary 8->32 extensions
        //        uint uLessThan = lessThan; // Use uint for comparisons to avoid unnecessary 8->32 extensions
        //        IntPtr index = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
        //        IntPtr nLength = (IntPtr)length;

        //        if (Vector.IsHardwareAccelerated && length >= System.Numerics.Vector<byte>.Count * 2)
        //        {
        //            int unaligned = (int)Unsafe.AsPointer(ref searchSpace) & (System.Numerics.Vector<byte>.Count - 1);
        //            nLength = (IntPtr)((System.Numerics.Vector<byte>.Count - unaligned) & (System.Numerics.Vector<byte>.Count - 1));
        //        }

        //    SequentialScan:
        //        uint lookUp;
        //        while ((byte*)nLength >= (byte*)8)
        //        {
        //            nLength -= 8;

        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 1);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found1;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 2);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found2;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 3);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found3;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 4);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found4;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 5);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found5;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 6);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found6;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 7);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found7;

        //            index += 8;
        //        }

        //        if ((byte*)nLength >= (byte*)4)
        //        {
        //            nLength -= 4;

        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 1);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found1;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 2);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found2;
        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 3);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found3;

        //            index += 4;
        //        }

        //        while ((byte*)nLength > (byte*)0)
        //        {
        //            nLength -= 1;

        //            lookUp = Unsafe.AddByteOffset(ref searchSpace, index);
        //            if (uValue0 == lookUp || uValue1 == lookUp || uLessThan > lookUp)
        //                goto Found;

        //            index += 1;
        //        }

        //        if (Vector.IsHardwareAccelerated && ((int)(byte*)index < length))
        //        {
        //            nLength = (IntPtr)((length - (int)(byte*)index) & ~(System.Numerics.Vector<byte>.Count - 1));

        //            // Get comparison Vector
        //            System.Numerics.Vector<byte> values0 = new System.Numerics.Vector<byte>(value0);
        //            System.Numerics.Vector<byte> values1 = new System.Numerics.Vector<byte>(value1);
        //            System.Numerics.Vector<byte> valuesLessThan = new System.Numerics.Vector<byte>(lessThan);

        //            while ((byte*)nLength > (byte*)index)
        //            {
        //                System.Numerics.Vector<byte> vData =
        //                    Unsafe.ReadUnaligned<System.Numerics.Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace, index));

        //                var vMatches = Vector.BitwiseOr(
        //                    Vector.BitwiseOr(
        //                        Vector.Equals(vData, values0),
        //                        Vector.Equals(vData, values1)),
        //                    Vector.LessThan(vData, valuesLessThan));

        //                if (System.Numerics.Vector<byte>.Zero.Equals(vMatches))
        //                {
        //                    index += System.Numerics.Vector<byte>.Count;
        //                    continue;
        //                }

        //                // Find offset of first match
        //                return (int)(byte*)index + LocateFirstFoundByte(vMatches);
        //            }

        //            if ((int)(byte*)index < length)
        //            {
        //                nLength = (IntPtr)(length - (int)(byte*)index);
        //                goto SequentialScan;
        //            }
        //        }

        //        return -1;
        //    Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
        //        return (int)(byte*)index;
        //    Found1:
        //        return (int)(byte*)(index + 1);
        //    Found2:
        //        return (int)(byte*)(index + 2);
        //    Found3:
        //        return (int)(byte*)(index + 3);
        //    Found4:
        //        return (int)(byte*)(index + 4);
        //    Found5:
        //        return (int)(byte*)(index + 5);
        //    Found6:
        //        return (int)(byte*)(index + 6);
        //    Found7:
        //        return (int)(byte*)(index + 7);
        //    }
        //}

        // Vector sub-search adapted from https://github.com/aspnet/KestrelHttpServer/pull/1138
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private static int LocateFirstFoundByte(System.Numerics.Vector<byte> match)
        // {
        //     var vector64 = Vector.AsVectorUInt64(match);
        //     ulong candidate = 0;
        //     int i = 0;
        //     // Pattern unrolled by jit https://github.com/dotnet/coreclr/pull/8001
        //     for (; i < System.Numerics.Vector<ulong>.Count; i++)
        //     {
        //         candidate = vector64[i];
        //         if (candidate != 0)
        //         {
        //             break;
        //         }
        //     }
        //
        //     // Single LEA instruction with jitted const (using function result)
        //     return i * 8 + LocateFirstFoundByte(candidate);
        // }
        //
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private static int LocateFirstFoundByte(ulong match)
        // {
        //     unchecked
        //     {
        //         // Flag least significant power of two bit
        //         var powerOfTwoFlag = match ^ (match - 1);
        //         // Shift all powers of two into the high byte and extract
        //         return (int) ((powerOfTwoFlag * XorPowerOfTwoToHighByte) >> 57);
        //     }
        // }
        //
        // private const ulong XorPowerOfTwoToHighByte = (0x07ul |
        //                                                0x06ul << 8 |
        //                                                0x05ul << 16 |
        //                                                0x04ul << 24 |
        //                                                0x03ul << 32 |
        //                                                0x02ul << 40 |
        //                                                0x01ul << 48) + 1;
    }
}