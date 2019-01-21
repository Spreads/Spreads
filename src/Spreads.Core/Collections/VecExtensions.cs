// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Algorithms;
using Spreads.Native;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    public static class VecExtensions
    {
        /// <summary>
        /// Performs standard binary serach and returns index of the value or its negative binary complement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinarySearch<T>(
            this ref Vec<T> vec, int start, int length,
            T value, KeyComparer<T> comparer)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return VectorSearch.BinarySearch(
                ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// Find value using binary searach according to the lookup direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookup<T>(
            this ref Vec<T> vec, int start, int length,
            T value, KeyComparer<T> comparer, Lookup lookup)
        {
            if ((uint)start > (uint)vec._length || (uint)length > (uint)(vec._length - start))

            { VecThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start); }

            return VectorSearch.BinaryLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer, lookup);
        }

        /// <summary>
        /// Performs <see cref="BinarySearch{T}(ref Vec{T},int,int,T,KeyComparer{T})"/> without bound checks.
        /// </summary>
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DangerousBinarySearch<T>(
            this ref Vec<T> vec, int start, int length, T value, KeyComparer<T> comparer)
        {
            return VectorSearch.BinarySearch(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer);
        }

        /// <summary>
        /// <see cref="BinaryLookup{T}"/> without bound checks.
        [Obsolete("Dangerous, justify usage in mute warning comment")]
        public static int DangerousBinaryLookup<T>(
            this ref Vec<T> vec, int start, int length,
            T value, KeyComparer<T> comparer, Lookup lookup)
        {
            return VectorSearch.BinaryLookup(ref Unsafe.Add(ref vec.DangerousGetPinnableReference(), start), vec.Length, value, comparer, lookup);
        }
    }
}