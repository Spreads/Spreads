// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using Spreads.Algorithms;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    public static class VectorExtensions
    {
        /// <summary>
        /// Get an <paramref name="array"/> element at <paramref name="index"/> in a very unsafe way.
        /// There are no checks for null or bounds, the validity of the call must be ensured before using this method.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeGetAt<T>(this T[] array, int index)
        {
            Debug.Assert((uint)index < array.Length, "UnsafeGetAt: (uint)index < array.Length");
            return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.As<Box<T>>(array)!.Value, TypeHelper<T>.ArrayOffset), (uint)index);
        }

        /// <summary>
        /// Get an <paramref name="array"/> element at <paramref name="index"/> in a very unsafe way.
        /// There are no checks for null or bounds, the validity of the call must be ensured before using this method.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeGetAt<T>(this object array, int index)
        {
            Debug.Assert((uint)index < ((Array)array).Length, "UnsafeGetAt: (uint)index < array.Length");
            return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.As<Box<T>>(array)!.Value, TypeHelper<T>.ArrayOffset), (uint)index);
        }

        /// <summary>
        /// Get an <paramref name="array"/> element at <paramref name="index"/> in a very unsafe way.
        /// There are no checks for null or bounds, the validity of the call must be ensured before using this method.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeGetFirst<T>(this T[] array)
        {
            Debug.Assert(array.Length > 0, "UnsafeGetFirst: array.Length > 0");
            return ref Unsafe.AddByteOffset(ref Unsafe.As<Box<T>>(array)!.Value, TypeHelper<T>.ArrayOffset);
        }

        /// <summary>
        /// Get an <paramref name="array"/> element at <paramref name="index"/> in a very unsafe way.
        /// There are no checks for null or bounds, the validity of the call must be ensured before using this method.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T UnsafeGetFirst<T>(this object array)
        {
            Debug.Assert(((Array)array).Length > 0, "UnsafeGetFirst: array.Length > 0");
            return ref Unsafe.AddByteOffset(ref Unsafe.As<Box<T>>(array)!.Value, TypeHelper<T>.ArrayOffset);
        }

        /// <summary>
        /// Creates a new Vec over the portion of the target array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec<T> AsVec<T>(this T[] array, int start) => Vec<T>.Create(array, start);

        /// <summary>
        /// Creates a new Vec over the portion of the target array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec AsVec(this Array array, int start) => Vec.Create(array, start);

        /// <summary>
        /// Move a block of values inside vector. Source and destination could overlap.
        /// </summary>
        public static void MoveBlock<T>(in this Vec vec, int start, int length, int destination)
        {
            ThrowHelper.EnsureOffsetLength(start, length, vec.Length);
            // TODO MemoryMarshal.CreateReadOnlySpan and manual bound ckecks
            var span = vec.AsSpan<T>();
            span.Slice(start, length).CopyTo(span.Slice(destination, length));
        }

        /// <summary>
        /// Move a block of values inside vector. Source and destination could overlap.
        /// </summary>
        public static void MoveBlock<T>(in this Vec<T> vec, int start, int length, int destination)
        {
            var span = vec.Span;
            span.Slice(start, length).CopyTo(span.Slice(destination, length));
        }

        #region Vec<T> BinarySearch

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ref Vec<T> vec, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            ThrowHelper.EnsureOffsetLength(offset, length, vec.Length);
            return DangerousBinarySearch(ref vec, offset, length, value, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this ref Vec<T> vec, T value, KeyComparer<T> comparer = default)
        {
            return DangerousBinarySearch(ref vec, 0, vec.Length, value, comparer);
        }

        /// <summary>
        /// Performs <see cref="BinarySearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this ref Vec<T> vec, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            return VectorSearch.BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), offset), length, value, comparer);
        }

        #endregion Vec<T> BinarySearch

        #region T[] BinarySearch

        /// <summary>
        /// Performs standard binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this T[] array, T value, KeyComparer<T> comparer = default)
        {
            return DangerousBinarySearch(array, 0, array.Length, value, comparer);
        }

        /// <summary>
        /// Performs binary search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            ThrowHelper.EnsureOffsetLength(offset, length, array.Length);
            return DangerousBinarySearch(array, offset, length, value, comparer);
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(T[],int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
                return -1;
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
            ThrowHelper.EnsureOffsetLength(offset, length, vec.Length);
            return DangerousBinaryLookup(ref vec, offset, length, ref value, lookup, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this ref Vec<T> vec, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return DangerousBinaryLookup(ref vec, 0, vec.Length, ref value, lookup, comparer);
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}(ref Vec{T},int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinaryLookup<T>(this ref Vec<T> vec, int offset, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.BinaryLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), offset), length, ref value, lookup, comparer);
        }

        #endregion Vec<T> BinaryLookup

        #region T[] BinaryLookup

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this T[] array, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return DangerousBinaryLookup(array, 0, array.Length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(this T[] array, int offset, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            ThrowHelper.EnsureOffsetLength(offset, length, array.Length);

#pragma warning disable 618
            return DangerousBinaryLookup(array, offset, length, ref value, lookup, comparer);
#pragma warning restore 618
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}(T[],int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        public static int DangerousBinaryLookup<T>(this T[] array, int offset, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (length == 0)
                return -1;
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
            ThrowHelper.EnsureOffsetLength(offset, length, vec.Length);
            return DangerousInterpolationSearch(ref vec, offset, length, value, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this ref Vec<T> vec, T value, KeyComparer<T> comparer = default)
        {
            return DangerousInterpolationSearch(ref vec, 0, vec.Length, value, comparer);
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this ref Vec<T> vec, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            return VectorSearch.InterpolationSearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), offset), length, value, comparer);
        }

        #endregion Vec<T> InterpolationSearch

        #region T[] InterpolationSearch

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this T[] array, T value, KeyComparer<T> comparer = default)
        {
            return DangerousInterpolationSearch(array, 0, array.Length, value, comparer);
        }

        /// <summary>
        /// Performs interpolation search and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationSearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            ThrowHelper.EnsureOffsetLength(offset, length, array.Length);
            return DangerousInterpolationSearch(array, offset, length, value, comparer);
        }

        /// <summary>
        /// <see cref="InterpolationSearch{T}(T[],int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationSearch<T>(this T[] array, int offset, int length, T value, KeyComparer<T> comparer = default)
        {
            if (length == 0)
                return -1;
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
            ThrowHelper.EnsureOffsetLength(offset, length, vec.Length);
            return DangerousInterpolationLookup(ref vec, offset, length, ref value, lookup, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this ref Vec<T> vec, ref T value,
            Lookup lookup, KeyComparer<T> comparer = default)
        {
            return DangerousInterpolationLookup(ref vec, 0, vec.Length, ref value, lookup, comparer);
        }


        /// <summary>
        /// <see cref="InterpolationLookup{T}(ref Vec{T},int,int,ref T,Spreads.Lookup,Spreads.KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            return DangerousInterpolationLookup(array, 0, array.Length, ref value, lookup, comparer);
        }

        /// <summary>
        /// Find value using binary search according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int InterpolationLookup<T>(this T[] array, int offset, int length, ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            ThrowHelper.EnsureOffsetLength(offset, length, array.Length);
            return DangerousInterpolationLookup(array, offset, length, ref value, lookup, comparer);
        }

        /// <summary>
        /// <see cref="InterpolationLookup{T}(T[],int,int,ref T,Lookup,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousInterpolationLookup<T>(this T[] array, int offset, int length,
            ref T value, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (length == 0)
                return -1;
            return VectorSearch.InterpolationLookup(ref array[0], offset, length, ref value, lookup, comparer);
        }

        #endregion T[] InterpolationLookup
    }
}
