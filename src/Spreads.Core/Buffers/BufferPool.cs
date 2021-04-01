// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    // TODO review it once again
    // * why two pools, not RMP.Default? What config is not good for default but good for data? Is it upper size and buckets to try?

    public static class BufferPool<T>
    {
        /// <summary>
        /// Retrieves an array that is at least the requested length.
        /// </summary>
        /// <param name="minLength"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Rent(int minLength)
        {
            return ArrayPool<T>.Shared.Rent(minLength);
        }

        /// <summary>
        /// Return an array to the pool.
        /// </summary>
        /// <param name="array">An array to return.</param>
        /// <param name="clearArray">Force clear of arrays of blittable types. Arrays that could have references are always cleared.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T[] array, bool clearArray = false)
        {
            ArrayPool<T>.Shared.Return(array, clearArray);
        }

        /// <summary>
        /// This is entry/exit point for data buffers. All in-memory data containers use this pool.
        /// </summary>
        public static RetainableMemoryPool<T> MemoryPool = new RetainableMemoryPool<T>(
                factory: RetainableMemoryPool<T>.DefaultFactory,
                minLength: Settings.MIN_POOLED_BUFFER_LEN,
                maxLength: Math.Max(Settings.MIN_POOLED_BUFFER_LEN, Settings.LARGE_BUFFER_LIMIT / Unsafe.SizeOf<T>()),
                maxBuffersPerBucketPerCore: 16,
                maxBucketsToTry: 2); // TODO review params
    }

    public class BufferPool
    {
        private static readonly BufferPool Shared = new BufferPool();

        // [Obsolete("Main RMP is already pinned for all blittables")]
        // internal static RetainableMemoryPool<byte> PinnedArrayMemoryPool = RetainableMemoryPool<byte>.Default;

        internal BufferPool()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RetainedMemory<byte> RetainMemory(int length, bool requireExact = true)
        {
            var arrayMemory = RetainableMemoryPool<byte>.Default.RentMemory(length, requireExact);
            if (AdditionalCorrectnessChecks.Enabled && arrayMemory.IsDisposed)
                BuffersThrowHelper.ThrowDisposed<RetainedMemory<byte>>();
            return requireExact ? arrayMemory.Retain(0, length) : arrayMemory.Retain(0, arrayMemory.Length);
        }

        /// <summary>
        /// Return a contiguous segment of memory backed by a pooled memory from a shares array pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RetainedMemory<byte> Retain(int length, bool requireExact = true)
        {
            return Shared.RetainMemory(length, requireExact);
        }

        /// <summary>
        /// Retains memory for temporary usage. Actual length could be larger than requested.
        /// When requested length is above 64kb then off-heap memory is used.
        /// </summary>
        internal static RetainedMemory<byte> RetainTemp(int length)
        {
            var rm = Shared.RetainMemory(length, false);
            ThrowHelper.DebugAssert(rm.IsPinned);
            return rm;
        }
    }

    internal static class BufferPoolRetainedMemoryHelper<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsRetainedMemory = IsRetainedMemoryInit();

        public static void DisposeRetainedMemory(T[] array, int offset, int len)
        {
            // TODO assert here
            for (int i = offset; i < offset + len; i++)
            {
#if SPREADS
                // TODO test it!
                UnsafeEx.DisposeConstrained(ref array[i]); // ((IDisposable)array[i]).Dispose();
#else
                (array[i] as IDisposable)?.Dispose();
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRetainedMemoryInit()
        {
            var ti = typeof(T).GetTypeInfo();
            if (ti.IsGenericTypeDefinition)
            {
                return ti.GetGenericTypeDefinition() == typeof(RetainedMemory<>);
            }
            else
            {
                return false;
            }
        }
    }
}
