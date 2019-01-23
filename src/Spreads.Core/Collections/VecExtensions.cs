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
    public readonly struct ArrayVec<T> : IVec<T>
    {
        private readonly T[] _array;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayVec(T[] array)
        {
            _array = array;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (_array as IEnumerable<T>).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _array.GetEnumerator();
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
        public T Get(int index)
        {
            return _array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGet(int index)
        {
            // TODO without BC
            return _array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ArrayVec<T>(T[] array)
        {
            return new ArrayVec<T>(array);
        }
    }

    public static class VecExtensions
    {
        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return VectorSearch.BinarySearch(
                ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T, TVec>(this TVec vec, T value, KeyComparer<T> comparer = default)
            where TVec : IVec<T>
        {
            return VectorSearch.BinarySearch(ref vec, value, comparer);
        }

        /// <summary>
        /// Performs <see cref="BinarySearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            return VectorSearch.BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this ref Vec<T> vec, int start, int length, T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return VectorSearch.BinaryLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, lookup, comparer);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T, TVec>(this TVec vec, T value, Lookup lookup, KeyComparer<T> comparer = default)
            where TVec : IVec<T>
        {
            return VectorSearch.BinaryLookup(ref vec, value, lookup, comparer);
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousBinaryLookup<T>(this ref Vec<T> vec, int start, int length,
            T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.BinaryLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, lookup, comparer);
        }

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return VectorSearch.InterpolationSearch(
                ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T, TVec>(this TVec vec, T value, KeyComparer<T> comparer = default)
            where TVec : IVec<T>
        {
            return VectorSearch.InterpolationSearch(ref vec, value, comparer);
        }

        /// <summary>
        /// Performs <see cref="InterpolationSearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationSearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// Find value using interpolation search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this ref Vec<T> vec, int start, int length, T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return VectorSearch.InterpolationLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, lookup, comparer);
        }

        /// <summary>
        /// Find value using interpolation search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T, TVec>(this TVec vec, T value, Lookup lookup, KeyComparer<T> comparer = default)
            where TVec : IVec<T>
        {
            return VectorSearch.InterpolationLookup(ref vec, value, lookup, comparer);
        }

        /// <summary>
        /// <see cref="InterpolationLookup{T}"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousInterpolationLookup<T>(this ref Vec<T> vec, int start, int length,
            T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, lookup, comparer);
        }
    }
}