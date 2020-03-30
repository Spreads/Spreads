// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Algorithms;
using Spreads.Native;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    public static class VectorExtensions
    {
        #region Vec<T> BinarySearch

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ref Vec<T> vec, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)vec._length || (uint)length > (uint)(vec._length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinarySearch(ref vec, offset, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// Performs <see cref="BinarySearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this ref Vec<T> vec, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            //if (length == 0)
            //{
            //    return -1;
            //}
            return VectorSearch.BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), offset), vec.Length, value, comparer);
        }

        #endregion Vec<T> BinarySearch

        #region T[] BinarySearch

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this T[] array, T value, KeyComparer<T> comparer = default)
        {
#pragma warning disable 618
            return DangerousBinarySearch(array, 0, array.Length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// Performs binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)array.Length || (uint)length > (uint)(array.Length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinarySearch(array, offset, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(T[],int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.BinarySearch(ref array[0], offset, length, value, comparer);
        }
        
        #endregion T[] BinarySearch

        #region Vec<T> BinaryLookup

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this ref Vec<T> vec, int offset, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)vec._length || (uint)length > (uint)(vec._length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinaryLookup(ref vec, offset, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}(ref Vec{T},int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousBinaryLookup<T>(this ref Vec<T> vec, int offset, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            // not needed
            //if (length == 0)
            //{
            //    return -1;
            //}
            return VectorSearch.BinaryLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), offset), vec.Length, ref value, lookup, comparer);
        }

        #endregion Vec<T> BinaryLookup

        #region T[] BinaryLookup

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this T[] array, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
#pragma warning disable 618
            return DangerousBinaryLookup(array, 0, array.Length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this T[] array, int offset, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)array.Length || (uint)length > (uint)(array.Length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinaryLookup(array, offset, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}(T[],int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousBinaryLookup<T>(this T[] array, int offset, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.BinaryLookup(ref array[0], offset, length, ref value, lookup, comparer);
        }

        #endregion T[] BinaryLookup

        #region Vec<T> InterpolationSearch

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this ref Vec<T> vec, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)vec._length || (uint)length > (uint)(vec._length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationSearch(ref vec, offset, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this ref Vec<T> vec, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.InterpolationSearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), offset), vec.Length, value, comparer);
        }

        #endregion Vec<T> InterpolationSearch

        #region T[] InterpolationSearch

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this T[] array, T value, KeyComparer<T> comparer = default)
        {
#pragma warning disable 618
            return DangerousInterpolationSearch(array, 0, array.Length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)array.Length || (uint)length > (uint)(array.Length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationSearch(array, offset, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(T[],int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.InterpolationSearch(ref array[0], offset, length, value, comparer);
        }

        #endregion T[] InterpolationSearch

        #region Vec<T> InterpolationLookup

        /// <summary>
        /// Find value using interpolation search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this ref Vec<T> vec, int offset, int length, ref T value,
            Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)vec._length || (uint)length > (uint)(vec._length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationLookup(ref vec, offset, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationLookup{T}(ref Spreads.Native.Vec{T},int,int,ref T,Spreads.Lookup,Spreads.KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousInterpolationLookup<T>(this ref Vec<T> vec, int offset, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), offset), length, ref value, lookup, comparer);
        }

        #endregion Vec<T> InterpolationLookup

        #region T[] InterpolationLookup

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this T[] array, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
#pragma warning disable 618
            return DangerousInterpolationLookup(array, 0, array.Length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this T[] array, int offset, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)offset > (uint)array.Length || (uint)length > (uint)(array.Length - offset))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationLookup(array, offset, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationLookup{T}(T[],int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousInterpolationLookup<T>(this T[] array, int offset, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.InterpolationLookup(ref array[0], offset, length, ref value, lookup, comparer);
        }

        #endregion T[] InterpolationLookup
    }
}