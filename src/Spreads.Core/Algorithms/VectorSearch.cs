// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//

using Spreads.DataTypes;
using Spreads.Native;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(
            ref T vecStart, int length, T value)
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
                ThrowHelper.ThrowNotSupportedException("KeyComparer<T>.Default.IsDiffable must be true for interpolation search");
            }
            return InterpolationSearchGeneric(ref vecStart, length, value, KeyComparer<T>.Default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch(
            ref long vecStart, int length, long value)
        {
            int lo = 0;
            int hi = length - 1;
            // If length == 0, hi == -1, and loop will not be entered
            while (lo <= hi)
            {
                unchecked
                {
                    var vlo = (ulong)Unsafe.Add(ref vecStart, lo);
                    var totalRange = (ulong)Unsafe.Add(ref vecStart, hi) - vlo;
                    var valueRange = (ulong)value - vlo;

                    // division via double is much faster
                    int i = lo + (int)((hi - lo) * (double)valueRange / totalRange);

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
            }
            return ~lo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch(
            ref int vecStart, int length, int value)
        {
            int lo = 0;
            int hi = length - 1;
            // If length == 0, hi == -1, and loop will not be entered
            while (lo <= hi)
            {
                unchecked
                {
                    var vlo = (ulong)Unsafe.Add(ref vecStart, lo);
                    var totalRange = (ulong)Unsafe.Add(ref vecStart, hi) - vlo;
                    var valueRange = (ulong)value - vlo;

                    // division via double is much faster
                    int i = lo + (int)((hi - lo) * (double)valueRange / totalRange);

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
            }
            return ~lo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int InterpolationSearchGeneric<T>(
            ref T vecStart, int length, T value, KeyComparer<T> comparer)
        {
            if (typeof(T) == typeof(Timestamp))
            {
                return InterpolationSearch(ref Unsafe.As<T, long>(ref vecStart), length, Unsafe.As<T, long>(ref value));
            }

            int lo = 0;
            int hi = length - 1;
            // If length == 0, hi == -1, and loop will not be entered
            while (lo <= hi)
            {
                var l = hi - lo;
                var totalRange = 1 + comparer.Diff(Unsafe.Add(ref vecStart, hi), Unsafe.Add(ref vecStart, lo));
                var valueRange = 1 + comparer.Diff(value, Unsafe.Add(ref vecStart, lo));

                // division via double is much faster
                int i = (int)(l * (double)valueRange / totalRange);

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

                if (Vector.IsHardwareAccelerated && length >= Vector<T>.Count * 2)
                {
                    int unaligned = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<T>.Count - 1);
                    nLength = (IntPtr)((Vector<T>.Count - unaligned) & (Vector<T>.Count - 1));
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
                    nLength = (IntPtr)((length - (int)(byte*)offset) & ~(Vector<T>.Count - 1));

                    // Get comparison Vector
                    Vector<T> vComparison = new Vector<T>(value);

                    while ((byte*)nLength > (byte*)offset)
                    {
                        var vMatches = Vector.Equals(vComparison,
                            Unsafe.ReadUnaligned<Vector<T>>(
                                ref Unsafe.As<T, byte>(ref Unsafe.Add(ref searchSpace, offset))));
                        if (Vector<T>.Zero.Equals(vMatches))
                        {
                            offset += Vector<T>.Count;
                            continue;
                        }

                        // Find offset of first match
                        return IndexOfSimple(ref Unsafe.Add(ref searchSpace, offset), value, Vector<T>.Count);
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
