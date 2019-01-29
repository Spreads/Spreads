// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    public static class BufferPool<T>
    {
        private static readonly ArrayPool<T> ArrayPoolImpl = ArrayPool<T>.Shared;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Rent(int minLength)
        {
            return ArrayPoolImpl.Rent(minLength);
        }

        /// <summary>
        /// Return an array to the pool.
        /// </summary>
        /// <param name="array">An array to return.</param>
        /// <param name="clearArray">Force clear of arrays of blittable types. Arrays that could have references are always cleared.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T[] array, bool clearArray = false)
        {
            ArrayPoolImpl.Return(array, clearArray);
        }

        // TODO Review parameters. The goal is to work mostly with 128kb buffers, all smaller ones are just slices of 128kb ones
        // Those buffers are in LOH and always pinning them is OK and does not interfere with normal GC.
        public static RetainableMemoryPool<T> MemoryPool = new RetainableMemoryPool<T>(
                factory: null, 
                minLength: Settings.MIN_POOLED_BUFFER_LEN,
                maxLength: (Settings.LARGE_BUFFER_LIMIT * 4) / Unsafe.SizeOf<T>(),
                maxBuffersPerBucket: 64 + Environment.ProcessorCount * 16,
                maxBucketsToTry: 2);
    }

    public class BufferPool
    {
        internal static BufferPool Shared = new BufferPool();

        internal static RetainableMemoryPool<byte> PinnedArrayMemoryPool =
            new RetainableMemoryPool<byte>(null,
                2048,
                8 * 1024 * 1024,
                64, 2);

        /// <summary>
        /// Default OffHeap pool has capacity of 4. This static field could be changed to a new instance.
        /// Buffer never cleared automatically and user must clear them when needed. Zeroing is a big cost
        /// and even new[]-ing has to zero memory, this is why it is slow.
        /// Please know what you are doing.
        /// </summary>
        public static OffHeapMemoryPool<byte> OffHeapMemoryPool = new OffHeapMemoryPool<byte>(4);

        // max pooled array size
        internal const int SharedBufferSize = 4096;

        internal static readonly int StaticBufferSize = Settings.ThreadStaticPinnedBufferSize;

        /// <summary>
        /// Temp storage e.g. for serialization
        /// </summary>
        [ThreadStatic]
        private static ArrayMemory<byte> _threadStaticBuffer;

        [ThreadStatic]
        private static RetainedMemory<byte> _threadStaticMemory;

        internal BufferPool()
        { }

        /// <summary>
        /// Thread-static <see cref="ArrayMemory{T}"/> with size of <see cref="StaticBufferSize"/>.
        /// Never dispose it!
        /// </summary>
        [Obsolete("Will be removed soon")]
        internal static ArrayMemory<byte> StaticBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_threadStaticBuffer != null)
                {
                    return _threadStaticBuffer;
                }

                return CreateThreadStaticBuffer();

                ArrayMemory<byte> CreateThreadStaticBuffer()
                {
                    // TODO review this mess with externally owned
                    _threadStaticBuffer = ArrayMemory<byte>.Create(new byte[StaticBufferSize], true);
                    _threadStaticMemory = new RetainedMemory<byte>(_threadStaticBuffer, 0, _threadStaticBuffer.Memory.Length, false);
                    return _threadStaticBuffer;
                }
            }
        }

        [Obsolete("Will be removed soon")]
        internal static RetainedMemory<byte> StaticBufferMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_threadStaticBuffer == null)
                {
                    var _ = StaticBuffer; // access getter
                }
                return _threadStaticMemory;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<byte> RetainMemory(int length, bool requireExact = true)
        {
            var arrayMemory = PinnedArrayMemoryPool.RentMemory(length);
            return requireExact ? arrayMemory.Retain(0, length) : arrayMemory.Retain();
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
        /// Note that requireExact is false, this method is for temp buffers that could be very large.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RetainedMemory<byte> RetainTemp(int length)
        {
            if (length > Settings.LARGE_BUFFER_LIMIT)
            {
                if (OffHeapMemoryPool != null)
                {
                    return OffHeapMemoryPool.RentMemory(length).Retain();
                }

                ThrowOffHeapPoolIsNull();
            }
            return Shared.RetainMemory(length, false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowOffHeapPoolIsNull()
        {
            ThrowHelper.ThrowInvalidOperationException("BufferPool.OffHeap is null while requesting RetainNoLoh");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArrayMemory<T> RentArrayMemory<T>(int minLength)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OffHeapMemory<T> RentOffHeapMemory<T>(int minLength) where T : struct
        {
            throw new NotImplementedException();
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
                // TODO test it!
                Native.UnsafeEx.DisposeConstrained(ref array[i]); // ((IDisposable)array[i]).Dispose();
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
