// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//

using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Native;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vector = System.Numerics.Vector;

#if NETCOREAPP3_0

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

#endif

namespace Spreads.Algorithms
{
    /// <summary>
    /// Algorithms to find values in contiguous data (memory region or a data structure with an indexer), e.g. <see cref="Vec{T}"/>.
    /// </summary>
    /// <remarks>
    /// WARNING: Methods in this static class do not perform bound checks and are intended to be used
    /// as building blocks in other parts that calculate bounds correctly and
    /// do performs required checks on external input.
    /// </remarks>
    public static class VectorSearch
    {
        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(
            ref T vecStart, int length, T value, KeyComparer<T> comparer)
        {
            unchecked
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

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T, TVec>(ref TVec vec, T value, KeyComparer<T> comparer)
            where TVec : IVec<T>
        {
            unchecked
            {
                int lo = 0;
                int hi = vec.Length - 1;
                while (lo <= hi)
                {
                    int i = (int)(((uint)hi + (uint)lo) >> 1);

                    int c = comparer.Compare(value, vec.DangerousGet(i));
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
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(ref T vecStart, int length, T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return ~0;
            }

            int i = BinarySearch(ref vecStart, length, value, comparer);

            if (i >= 0)
            {
                if (lookup.IsEqualityOK())
                {
                    return i;
                }

                if (lookup == Lookup.LT)
                {
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else // depends on if (eqOk) above
                {
                    Debug.Assert(lookup == Lookup.GT);
                    if (i == length - 1)
                    {
                        return ~length;
                    }

                    i = i + 1;
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
                if (((uint)lookup & (uint)Lookup.LT) != 0)
                {
                    // i is idx of element that is larger, nothing here for LE/LT
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else
                {
                    Debug.Assert(((uint)lookup & (uint)Lookup.GT) != 0);
                    Debug.Assert(i <= length);
                    // if was negative, if it was ~length then there are no more elements for GE/GT
                    if (i == length)
                    {
                        return ~length;
                    }

                    // i is the same, ~i is idx of element that is GT the value
                }
            }

            Debug.Assert(unchecked((uint)i) < unchecked((uint)length));
            return i;
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T, TVec>(ref TVec vec, T value, Lookup lookup, KeyComparer<T> comparer = default)
            where TVec : IVec<T>
        {
            var length = vec.Length;
            if (length == 0)
            {
                return ~0;
            }

            int i = BinarySearch(ref vec, value, comparer);

            if (i >= 0)
            {
                if (lookup.IsEqualityOK())
                {
                    return i;
                }

                if (lookup == Lookup.LT)
                {
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else // depends on if (eqOk) above
                {
                    Debug.Assert(lookup == Lookup.GT);
                    if (i == length - 1)
                    {
                        return ~length;
                    }

                    i = i + 1;
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
                if (((uint)lookup & (uint)Lookup.LT) != 0)
                {
                    // i is idx of element that is larger, nothing here for LE/LT
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else
                {
                    Debug.Assert(((uint)lookup & (uint)Lookup.GT) != 0);
                    Debug.Assert(i <= length);
                    // if was negative, if it was ~length then there are no more elements for GE/GT
                    if (i == length)
                    {
                        return ~length;
                    }

                    // i is the same, ~i is idx of element that is GT the value
                }
            }

            Debug.Assert(unchecked((uint)i) < unchecked((uint)length));
            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(ref T vecStart, int length, T value, KeyComparer<T> comparer = default)
        {
            if (typeof(T) == typeof(Timestamp))
            {
                return InterpolationSearch(ref Unsafe.As<T, long>(ref vecStart), length, Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(long))
            {
                return InterpolationSearch(ref Unsafe.As<T, long>(ref vecStart), length, Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(int))
            {
                return InterpolationSearch(ref Unsafe.As<T, int>(ref vecStart), length, Unsafe.As<T, int>(ref value));
            }

            if (!KeyComparer<T>.Default.IsDiffable)
            {
                ThrowNonDiffable<T>();
            }
            return InterpolationSearchGeneric(ref vecStart, length, value, KeyComparer<T>.Default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T, TVec>(ref TVec vec, T value, KeyComparer<T> comparer = default)
            where TVec : IVec<T>
        {
            // TODO test if this works and is inlined well

            if (typeof(TVec) == typeof(Vector<Timestamp>))
            {
                return InterpolationSearch(ref Unsafe.As<TVec, Vector<long>>(ref vec), Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(Vector<long>))
            {
                return InterpolationSearch(ref Unsafe.As<TVec, Vector<long>>(ref vec), Unsafe.As<T, long>(ref value));
            }

            if (typeof(T) == typeof(Vector<int>))
            {
                return InterpolationSearch(ref Unsafe.As<TVec, Vector<int>>(ref vec), Unsafe.As<T, int>(ref value));
            }

            if (!KeyComparer<T>.Default.IsDiffable)
            {
                ThrowNonDiffable<T>();
            }
            return InterpolationSearchGeneric(ref vec, value, comparer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNonDiffable<T>()
        {
            ThrowHelper.ThrowNotSupportedException("KeyComparer<T>.Default.IsDiffable must be true for interpolation search: T is " + typeof(T).FullName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearchYY(ref long vecStart, int length, long value)
        {
            unchecked
            {
                int lo = 0;
                int hi = length - 1;

                if (lo < hi)
                {
                    var vlo = vecStart;
                    var vhi = Unsafe.Add(ref vecStart, hi);
                    var range = vhi - vlo;

                    // this is the power itself so that we could shift by it
                    var rangeNextPow2 = 64 - IntUtil.NumberOfLeadingZeros(range);

                    var nominator = ((hi - lo) * (value - vlo));

                    // this is lower estimate because rangeNextPow2 is higher that actual
                    var iLeft = (int)(nominator >> rangeNextPow2);
                    var vLeft = Unsafe.Add(ref vecStart, iLeft);

                    if (value == vLeft)
                    {
                        return iLeft;
                    }

                    // we missed the very happy case
                    if (value < vLeft)
                    {
                        hi = iLeft - 1;
                    }
                    else
                    {
                        var iRight = Math.Min(hi, (int)(nominator >> (rangeNextPow2 - 1)));
                        var vRight = Unsafe.Add(ref vecStart, iRight);

                        if (value < vRight)
                        {
                            lo = iLeft + 1;
                            hi = iRight - 1;
                        }
                        else if (value == vRight)
                        {
                            return length;
                        }
                        // value in (iLeft, i)
                        lo = iLeft + 1;
                    }
                }
                return BinarySearch(ref Unsafe.Add(ref vecStart, lo), 1 + hi - lo, value, default);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch(ref long vecStart, int length, long value)
        {
            unchecked
            {
                int lo = 0;
                int hi = length - 1;
                if (lo < hi)
                {
                    long vlo = vecStart;
                    long vhi = Unsafe.Add(ref vecStart, hi);
                    long range = vhi - vlo;

                    var nominator = (hi - lo) * (value - vlo);

                    var i = Math.Min(hi, (int)((double)nominator / range));

                    var vi = Unsafe.Add(ref vecStart, i);

                    if (value == vi)
                    {
                        return i;
                    }

                    // we missed the very happy case

                    // this is the power itself so that we could shift by it
                    var rangeNextPow2 = 64 - IntUtil.NumberOfLeadingZeros(range);

                    if (value < vi)
                    {
                        // this is lower estimate because rangeNextPow2 is higher that actual
                        var iLeft = (int)(nominator >> rangeNextPow2);
                        var vLeft = Unsafe.Add(ref vecStart, iLeft);

                        if (value < vLeft)
                        {
                            // value in [0, vLeft)
                            hi = iLeft - 1;
                        }
                        else if (value == vLeft)
                        {
                            return iLeft;
                        }
                        else
                        {
                            // value in (iLeft, i)
                            lo = iLeft + 1;
                            hi = i - 1;
                        }
                    }
                    else
                    {
                        var iRight = Math.Min(hi, (int)(nominator >> (rangeNextPow2 - 1)));
                        var vRight = Unsafe.Add(ref vecStart, iRight);

                        if (value < vRight)
                        {
                            // value in (i, iRight)
                            lo = i + 1;
                            hi = iRight - 1;
                        }
                        else if (value == vRight)
                        {
                            return iRight;
                        }
                        else
                        {
                            // value in (iRight, hi]
                            lo = iRight + 1;
                        }
                    }
                }

                var iBs = BinarySearch(ref Unsafe.Add(ref vecStart, lo), 1 + hi - lo, value, default);

                if (iBs >= 0)
                {
                    return lo + iBs;
                }
                else
                {
                    return ~(lo + ~iBs);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearchXX(ref long vecStart, int length, long value)
        {
            unchecked
            {
                int lo = 0;
                int hi = length - 1;
                int hl;
                var c = 0;
                // If length == 0, hi == -1, and loop will not be entered
                while ((hl = hi - lo) >= 0)
                {
                    c++;
                    if (c > 2)
                    {
                        return BinarySearch(ref Unsafe.Add(ref vecStart, lo), 1 + hi - lo, value, default);
                    }

                    int i;
                    if (hl > 0)
                    {
                        var vlo = (ulong)Unsafe.Add(ref vecStart, lo);
                        var totalRange = (ulong)Unsafe.Add(ref vecStart, hi) - vlo;
                        var valueRange = (ulong)value - vlo;

                        if (valueRange > totalRange)
                        {
                            valueRange = 0;
                        }

                        i = lo +
                            (valueRange > totalRange
                            ? (int)(((uint)hi + (uint)lo) >> 1)
                            : (int)(hl * ((double)valueRange / totalRange)));
                        // division via double is much faster
                    }
                    else
                    {
                        i = lo;
                    }

                    var vi = Unsafe.Add(ref vecStart, i);
                    if (value == vi)
                    {
                        return i;
                    }
                    else if (value > vi)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearchX(ref long vecStart, int length, long value)
        {
            unchecked
            {
                int lo = 0;
                int hi = length - 1;
                int hl;

                // If length == 0, hi == -1, and loop will not be entered
                if ((hl = hi - lo) >= 0)
                {
                    int i;
                    if (hl > 0)
                    {
                        var vlo = (ulong)Unsafe.Add(ref vecStart, lo);
                        var totalRange = (ulong)Unsafe.Add(ref vecStart, hi) - vlo;
                        var valueRange = (ulong)value - vlo;

                        if (valueRange > totalRange)
                        {
                            valueRange = 0;
                        }

                        i = lo +
                            (valueRange > totalRange
                                ? (int)(((uint)hi + (uint)lo) >> 1)
                                : (int)(hl * ((double)valueRange / totalRange)));
                        // division via double is much faster
                    }
                    else
                    {
                        i = lo;
                    }

                    var vi = Unsafe.Add(ref vecStart, i);
                    if (value == vi)
                    {
                        return i;
                    }
                    else if (value > vi)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        hi = i - 1;
                    }

                    return BinarySearch(ref Unsafe.Add(ref vecStart, lo), 1 + hi - lo, value, default);
                }

                return ~lo;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<TVec>(ref TVec vec, long value)
            where TVec : IVec<long>
        {
            unchecked
            {
                int lo = 0;
                int hi = vec.Length - 1;
                int hl;
                // If length == 0, hi == -1, and loop will not be entered
                while ((hl = hi - lo) >= 0)
                {
                    int i;
                    if (hl > 0)
                    {
                        var vlo = (ulong)vec.DangerousGet(lo);
                        var totalRange = (ulong)vec.DangerousGet(hi) - vlo;
                        var valueRange = (ulong)value - vlo;

                        if (valueRange > totalRange)
                        {
                            valueRange = totalRange >> 1;
                        }

                        // division via double is much faster

                        i = lo + (int)(hl * ((double)valueRange / totalRange));
                    }
                    else
                    {
                        i = lo;
                    }

                    var vi = vec.DangerousGet(i);
                    if (value == vi)
                    {
                        return i;
                    }
                    else if (value > vi)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch(ref int vecStart, int length, int value)
        {
            unchecked
            {
                int lo = 0;
                int hi = length - 1;
                int hl;
                // If length == 0, hi == -1, and loop will not be entered
                while ((hl = hi - lo) >= 0)
                {
                    int i;
                    if (hl > 0)
                    {
                        var vlo = (ulong)Unsafe.Add(ref vecStart, lo);
                        var totalRange = (ulong)Unsafe.Add(ref vecStart, hi) - vlo;
                        var valueRange = (ulong)value - vlo;

                        if (valueRange > totalRange)
                        {
                            valueRange = totalRange >> 1;
                        }

                        // division via double is much faster

                        i = lo + (int)(hl * ((double)valueRange / totalRange));
                    }
                    else
                    {
                        i = lo;
                    }

                    var vi = Unsafe.Add(ref vecStart, i);
                    if (value == vi)
                    {
                        return i;
                    }
                    else if (value > vi)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<TVec>(ref TVec vec, int value)
            where TVec : IVec<int>
        {
            unchecked
            {
                int lo = 0;
                int hi = vec.Length - 1;
                int hl;
                // If length == 0, hi == -1, and loop will not be entered
                while ((hl = hi - lo) >= 0)
                {
                    int i;
                    if (hl > 0)
                    {
                        var vlo = (ulong)vec.DangerousGet(lo);
                        var totalRange = (ulong)vec.DangerousGet(hi) - vlo;
                        var valueRange = (ulong)value - vlo;

                        if (valueRange > totalRange)
                        {
                            valueRange = totalRange >> 1;
                        }

                        // division via double is much faster

                        i = lo + (int)(hl * ((double)valueRange / totalRange));
                    }
                    else
                    {
                        i = lo;
                    }

                    var vi = vec.DangerousGet(i);
                    if (value == vi)
                    {
                        return i;
                    }
                    else if (value > vi)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int InterpolationSearchGeneric<T>(ref T vecStart, int length, T value, KeyComparer<T> comparer = default)
        {
            unchecked
            {
                int lo = 0;
                int hi = length - 1;
                int hl;
                // If length == 0, hi == -1, and loop will not be entered
                while ((hl = hi - lo) >= 0)
                {
                    int i;
                    if (hl > 0)
                    {
                        var vlo = Unsafe.Add(ref vecStart, lo);
                        var totalRange = (ulong)comparer.Diff(Unsafe.Add(ref vecStart, hi), vlo);
                        var valueRange = (ulong)comparer.Diff(value, vlo);

                        if (valueRange > totalRange)
                        {
                            valueRange = totalRange >> 1;
                        }

                        // division via double is much faster

                        i = lo + (int)(hl * ((double)valueRange / totalRange));
                    }
                    else
                    {
                        i = lo;
                    }

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

                return ~lo;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int InterpolationSearchGeneric<T, TVec>(ref TVec vec, T value, KeyComparer<T> comparer = default)
             where TVec : IVec<T>
        {
            unchecked
            {
                int length = vec.Length;
                int lo = 0;
                int hi = length - 1;
                int hl;
                // If length == 0, hi == -1, and loop will not be entered
                while ((hl = hi - lo) >= 0)
                {
                    int i;
                    if (hl > 0)
                    {
                        var vlo = vec.DangerousGet(lo);
                        var totalRange = (ulong)comparer.Diff(vec.DangerousGet(hi), vlo);
                        var valueRange = (ulong)comparer.Diff(value, vlo);

                        if (valueRange > totalRange)
                        {
                            valueRange = totalRange >> 1;
                        }

                        // division via double is much faster

                        i = lo + (int)(hl * ((double)valueRange / totalRange));
                    }
                    else
                    {
                        i = lo;
                    }

                    int c = comparer.Compare(value, vec.DangerousGet(i));
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
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(ref T vecStart, int length, T value, Lookup lookup,
            KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return ~0;
            }

            int i = InterpolationSearch(ref vecStart, length, value, comparer);

            if (i >= 0)
            {
                if (lookup.IsEqualityOK())
                {
                    return i;
                }

                if (lookup == Lookup.LT)
                {
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else // depends on if (eqOk) above
                {
                    Debug.Assert(lookup == Lookup.GT);
                    if (i == length - 1)
                    {
                        return ~length;
                    }

                    i = i + 1;
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
                if (((uint)lookup & (uint)Lookup.LT) != 0)
                {
                    // i is idx of element that is larger, nothing here for LE/LT
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else
                {
                    Debug.Assert(((uint)lookup & (uint)Lookup.GT) != 0);
                    Debug.Assert(i <= length);
                    // if was negative, if it was ~length then there are no more elements for GE/GT
                    if (i == length)
                    {
                        return ~length;
                    }

                    // i is the same, ~i is idx of element that is GT the value
                }
            }

            Debug.Assert(unchecked((uint)i) < unchecked((uint)length));
            return i;
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T, TVec>(ref TVec vec, T value, Lookup lookup, KeyComparer<T> comparer = default)
            where TVec : IVec<T>
        {
            var length = vec.Length;
            if (length == 0)
            {
                return ~0;
            }

            int i = InterpolationSearch(ref vec, value, comparer);

            if (i >= 0)
            {
                if (lookup.IsEqualityOK())
                {
                    return i;
                }

                if (lookup == Lookup.LT)
                {
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else // depends on if (eqOk) above
                {
                    Debug.Assert(lookup == Lookup.GT);
                    if (i == length - 1)
                    {
                        return ~length;
                    }

                    i = i + 1;
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
                if (((uint)lookup & (uint)Lookup.LT) != 0)
                {
                    // i is idx of element that is larger, nothing here for LE/LT
                    if (i == 0)
                    {
                        return -1;
                    }

                    i = i - 1;
                }
                else
                {
                    Debug.Assert(((uint)lookup & (uint)Lookup.GT) != 0);
                    Debug.Assert(i <= length);
                    // if was negative, if it was ~length then there are no more elements for GE/GT
                    if (i == length)
                    {
                        return ~length;
                    }

                    // i is the same, ~i is idx of element that is GT the value
                }
            }

            Debug.Assert(unchecked((uint)i) < unchecked((uint)length));
            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOf<T>(ref T vecStart, T value, int length)
        {
            Debug.Assert(length >= 0);

            if (Vector.IsHardwareAccelerated)
            {
                if (typeof(T) == typeof(byte))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, byte>(ref vecStart), Unsafe.As<T, byte>(ref value), length);
                }
                if (typeof(T) == typeof(sbyte))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, sbyte>(ref vecStart), Unsafe.As<T, sbyte>(ref value), length);
                }
                if (typeof(T) == typeof(ushort))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, ushort>(ref vecStart), Unsafe.As<T, ushort>(ref value), length);
                }
                if (typeof(T) == typeof(short))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, short>(ref vecStart), Unsafe.As<T, short>(ref value), length);
                }
                if (typeof(T) == typeof(uint))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, uint>(ref vecStart), Unsafe.As<T, uint>(ref value), length);
                }
                if (typeof(T) == typeof(int))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, int>(ref vecStart), Unsafe.As<T, int>(ref value), length);
                }
                if (typeof(T) == typeof(ulong))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, ulong>(ref vecStart), Unsafe.As<T, ulong>(ref value), length);
                }
                if (typeof(T) == typeof(long))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, long>(ref vecStart), Unsafe.As<T, long>(ref value), length);
                }
                if (typeof(T) == typeof(float))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, float>(ref vecStart), Unsafe.As<T, float>(ref value), length);
                }
                if (typeof(T) == typeof(double))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, double>(ref vecStart), Unsafe.As<T, double>(ref value), length);
                }

                // non-standard
                // Treat Timestamp as long because it is a single long internally
                if (typeof(T) == typeof(Timestamp))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, long>(ref vecStart), Unsafe.As<T, long>(ref value), length);
                }

                // Treat DateTime as ulong because it is a single ulong internally
                if (typeof(T) == typeof(DateTime))
                {
                    return IndexOfVectorized(ref Unsafe.As<T, ulong>(ref vecStart), Unsafe.As<T, ulong>(ref value), length);
                }
            }

            return IndexOfSimple(ref vecStart, value, length);
        }

        internal static unsafe int IndexOfVectorized<T>(ref T searchSpace, T value, int length) where T : struct
        {
            unchecked
            {
                Debug.Assert(length >= 0);

                IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
                IntPtr nLength = (IntPtr)length;

                if (Vector.IsHardwareAccelerated && length >= System.Numerics.Vector<T>.Count * 2)
                {
                    int unaligned = (int)Unsafe.AsPointer(ref searchSpace) & (System.Numerics.Vector<T>.Count - 1);
                    nLength = (IntPtr)((System.Numerics.Vector<T>.Count - unaligned) & (System.Numerics.Vector<T>.Count - 1));
                }

            SequentialScan:
                while ((byte*)nLength >= (byte*)8)
                {
                    nLength -= 8;

                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset)))
                        goto Found;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 1)))
                        goto Found1;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 2)))
                        goto Found2;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 3)))
                        goto Found3;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 4)))
                        goto Found4;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 5)))
                        goto Found5;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 6)))
                        goto Found6;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 7)))
                        goto Found7;

                    offset += 8;
                }

                if ((byte*)nLength >= (byte*)4)
                {
                    nLength -= 4;

                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset)))
                        goto Found;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 1)))
                        goto Found1;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 2)))
                        goto Found2;
                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset + 3)))
                        goto Found3;

                    offset += 4;
                }

                while ((byte*)nLength > (byte*)0)
                {
                    nLength -= 1;

                    if (UnsafeEx.EqualsConstrained(ref value, ref Unsafe.Add(ref searchSpace, offset)))
                        goto Found;

                    offset += 1;
                }

                if (Vector.IsHardwareAccelerated && ((int)(byte*)offset < length))
                {
                    nLength = (IntPtr)((length - (int)(byte*)offset) & ~(System.Numerics.Vector<T>.Count - 1));

                    // Get comparison Vector
                    System.Numerics.Vector<T> vComparison = new System.Numerics.Vector<T>(value);

                    while ((byte*)nLength > (byte*)offset)
                    {
                        var vMatches = Vector.Equals(vComparison,
                            Unsafe.ReadUnaligned<System.Numerics.Vector<T>>(
                                ref Unsafe.As<T, byte>(ref Unsafe.Add(ref searchSpace, offset))));
                        if (System.Numerics.Vector<T>.Zero.Equals(vMatches))
                        {
                            offset += System.Numerics.Vector<T>.Count;
                            continue;
                        }

                        // Find offset of first match
                        return IndexOfSimple(ref Unsafe.Add(ref searchSpace, offset), value, System.Numerics.Vector<T>.Count);
                        // return (int)(byte*)offset + LocateFirstFoundByte(vMatches);
                    }

                    if ((int)(byte*)offset < length)
                    {
                        nLength = (IntPtr)(length - (int)(byte*)offset);
                        goto SequentialScan;
                    }
                }

                return -1;
            Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
                return (int)(byte*)offset;
            Found1:
                return (int)(byte*)(offset + 1);
            Found2:
                return (int)(byte*)(offset + 2);
            Found3:
                return (int)(byte*)(offset + 3);
            Found4:
                return (int)(byte*)(offset + 4);
            Found5:
                return (int)(byte*)(offset + 5);
            Found6:
                return (int)(byte*)(offset + 6);
            Found7:
                return (int)(byte*)(offset + 7);
            }
        }

        /// <summary>
        /// Simple implementation using for loop.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoOptimization)]
        internal static int IndexOfSimple<T>(ref T vecStart, T value, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (UnsafeEx.EqualsConstrained(ref Unsafe.Add(ref vecStart, i), ref value))
                {
                    return i;
                }
            }
            return -1;
        }

#if NETCOREAPP3_0

        [Obsolete("S.N.Vector always uses the widest available on the system")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfAvx(ref long searchSpace, long value, int length)
        {
            if (length < 1)
                return -1;

            if (length == 1)
                return 0;

            ref var position = ref searchSpace;
            if (length > Vector256<long>.Count)
            {
                var elementVec = Vector256.Create(value);
                while (length > Vector256<long>.Count)
                {
                    var curr = Unsafe.As<long, Vector256<long>>(ref position);

                    var mask = Avx2.CompareEqual(curr, elementVec);
                    if (!Avx.TestZ(mask, mask))
                    {
                        return IndexOfSimple(ref position, value, length);
                    }

                    position = ref Unsafe.Add<long>(ref position, Vector256<long>.Count);
                    length -= Vector256<long>.Count;
                }
            }

            return IndexOfSimple(ref position, value, length);
        }

        [Obsolete("S.N.Vector always uses the widest available on the system")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int IndexOfAvx(ref int vecStart, int value, int length)
        {
            if (length < 1)
                return -1;

            if (length == 1)
                return 0;

            ref int position = ref vecStart;
            if (length > Vector256<int>.Count)
            {
                var elementVec = Vector256.Create(value);
                do
                {
                    var curr = Unsafe.ReadUnaligned<Vector256<int>>(ref Unsafe.As<int, byte>(ref position));

                    var mask = Avx2.CompareEqual(curr, elementVec);
                    if (!Avx.TestZ(mask, mask))
                    {
                        return IndexOfSimple(ref position, value, length);
                    }

                    position = ref Unsafe.Add<int>(ref position, Vector256<int>.Count);
                    length -= Vector256<int>.Count;
                } while (length > Vector256<int>.Count);
            }

            return IndexOfSimple(ref position, value, length);
        }

#endif
    }
}
