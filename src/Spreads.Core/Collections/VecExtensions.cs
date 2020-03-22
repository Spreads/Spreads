// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Algorithms;
using Spreads.Native;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    public readonly struct ArrayVector<T> : IVector<T>
    {
        private readonly T[] _array;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayVector(T[] array)
        {
            _array = array;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (_array as IEnumerable<T>).GetEnumerator();
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Length;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array[index];
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _array.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetItem(int index)
        {
            return _array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGetItem(int index)
        {
            // TODO without BC
            return _array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ArrayVector<T>(T[] array)
        {
            return new ArrayVector<T>(array);
        }
    }

    public static class VectorExtensions
    {
        #region Vec<T> BinarySearch

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinarySearch(ref vec, start, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// Performs <see cref="BinarySearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            //if (length == 0)
            //{
            //    return -1;
            //}
            return VectorSearch.BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
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
        public static int BinarySearch<T>(this T[] array, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinarySearch(array, start, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(T[],int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this T[] array, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.BinarySearch(ref array[start], length, value, comparer);
        }
        
        #endregion T[] BinarySearch

        #region TVector : IVector<T> BinarySearch

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ArrayVector<T> vec, T value, KeyComparer<T> comparer = default)
        {
            return VectorSearch.BinarySearch(ref vec, 0, vec.Length, value, comparer);
        }

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ArrayVector<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            BoundCheck<T, ArrayVector<T>>(vec, start, length);
#pragma warning disable 618
            return DangerousBinarySearch(vec, start, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="BinarySearch{T}(ArrayVector{T} ,int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this ArrayVector<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            // not needed
            //if (length == 0)
            //{
            //    return -1;
            //}
            return VectorSearch.BinarySearch(ref vec, start, length, value, comparer);
        }

        #endregion TVector : IVector<T> BinarySearch

        #region Vec<T> BinaryLookup

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this ref Vec<T> vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinaryLookup(ref vec, start, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}(ref Vec{T},int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousBinaryLookup<T>(this ref Vec<T> vec, int start, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            // not needed
            //if (length == 0)
            //{
            //    return -1;
            //}
            return VectorSearch.BinaryLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, ref value, lookup, comparer);
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
        public static int BinaryLookup<T>(this T[] array, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousBinaryLookup(array, start, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}(T[],int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousBinaryLookup<T>(this T[] array, int start, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.BinaryLookup(ref array[start], length, ref value, lookup, comparer);
        }

        #endregion T[] BinaryLookup

        #region TVector : IVector<T> BinaryLookup

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this ArrayVector<T> vec, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.BinaryLookup(ref vec, 0, vec.Length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this ArrayVector<T> vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            BoundCheck<T, ArrayVector<T>>(vec, start, length);
            return VectorSearch.BinaryLookup(ref vec, start, length, ref value, lookup, comparer);
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}(ArrayVector{T} ,int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinaryLookup<T>(this ArrayVector<T> vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            // not needed
            //if (length == 0)
            //{
            //    return -1;
            //}
            return VectorSearch.BinaryLookup(ref vec, start, length, ref value, lookup, comparer);
        }

        #endregion TVector : IVector<T> BinaryLookup

        #region Vec<T> InterpolationSearch

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationSearch(ref vec, start, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.InterpolationSearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
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
        public static int InterpolationSearch<T>(this T[] array, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationSearch(array, start, length, value, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(T[],int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this T[] array, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.InterpolationSearch(ref array[start], length, value, comparer);
        }

        #endregion T[] InterpolationSearch

        #region TVector : IVector<T> InterpolationSearch

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this ArrayVector<T> vec, T value, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationSearch(ref vec, 0, vec.Length, value, comparer);
        }

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this ArrayVector<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            BoundCheck<T, ArrayVector<T>>(vec, start, length);
            return VectorSearch.InterpolationSearch(ref vec, start, length, value, comparer);
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(ArrayVector{T} ,int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this ArrayVector<T> vec, int start, int length, T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationSearch(ref vec, start, length, value, comparer);
        }

        #endregion TVector : IVector<T> InterpolationSearch

        #region Vec<T> InterpolationLookup

        /// <summary>
        /// Find value using interpolation search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this ref Vec<T> vec, int start, int length, ref T value,
            Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationLookup(ref vec, start, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationLookup{T}(ref Spreads.Native.Vec{T},int,int,ref T,Spreads.Lookup,Spreads.KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousInterpolationLookup<T>(this ref Vec<T> vec, int start, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, ref value, lookup, comparer);
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
        public static int InterpolationLookup<T>(this T[] array, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

#pragma warning disable 618
            return DangerousInterpolationLookup(array, start, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="InterpolationLookup{T}(T[],int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousInterpolationLookup<T>(this T[] array, int start, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (length == 0)
            {
                return -1;
            }
            return VectorSearch.InterpolationLookup(ref array[start], length, ref value, lookup, comparer);
        }

        #endregion T[] InterpolationLookup

        #region TVector : IVector<T> InterpolationLookup

        /// <summary>
        /// Find value using interpolation search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this ArrayVector<T> vec, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationLookup(ref vec, 0, vec.Length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Find value using interpolation search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this ArrayVector<T> vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            BoundCheck<T, ArrayVector<T>>(vec, start, length);
            return VectorSearch.InterpolationLookup(ref vec, start, length, ref value, lookup, comparer);
        }

        /// <summary>
        /// <see cref="InterpolationLookup{T}(ArrayVector{T},int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationLookup<T>(this ArrayVector<T> vec, int start, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationLookup(ref vec, start, length, ref value, lookup, comparer);
        }

        #endregion TVector : IVector<T> InterpolationLookup

        #region Bound check

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BoundCheck<T, TVector>(TVector vec, int start, int length) where TVector : IVector<T>
        {
            if ((uint)start > (uint)vec.Length || (uint)length > (uint)(vec.Length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }
        }

        #endregion Bound check
    }
}