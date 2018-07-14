// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    public static class BufferPool<T>
    {
        // private static readonly ArrayPool<T> PoolImpl = new DefaultArrayPool<T>();

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OwnedPooledArray<T> RentOwnedPooledArray(int minLength)
        {
            return ArrayMemoryPool<T>.Shared.RentCore(minLength);
        }
    }

    public static class BufferPool
    {
        // max pooled array size
        internal const int SharedBufferSize = 4096;

        internal const int StaticBufferSize = 16 * 1024;

        /// <summary>
        /// Shared buffers are for slicing of small PreservedBuffers
        /// </summary>
        [ThreadStatic]
        private static OwnedPooledArray<byte> _sharedBuffer;
        [ThreadStatic]
        private static RetainedMemory<byte> _sharedBufferMemory;

        [ThreadStatic]
        internal static int SharedBufferOffset;

        /// <summary>
        /// Temp storage e.g. for serialization
        /// </summary>
        [ThreadStatic]
        private static OwnedPooledArray<byte> _threadStaticBuffer;

        [ThreadStatic]
        private static RetainedMemory<byte> _threadStaticMemory;

        /// <summary>
        /// Thread-static <see cref="OwnedPooledArray{T}"/> with size of <see cref="StaticBufferSize"/>.
        /// Never dispose it!
        /// </summary>
        internal static OwnedPooledArray<byte> StaticBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_threadStaticBuffer != null)
                {
                    return _threadStaticBuffer;
                }

                return CreateThreadStaticBuffer();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static OwnedPooledArray<byte> CreateThreadStaticBuffer()
        {
            _threadStaticBuffer =
                OwnedPooledArray<byte>.Create(
                    new byte[Settings
                        .ThreadStaticPinnedBufferSize]); // BufferPool<byte>.RentOwnedPooledArray(StaticBufferSize);
            _threadStaticBuffer._referenceCount = int.MinValue;
            // NB Pin in LOH if ThreadStaticPinnedBufferSize > 85k, limit impact on compaction (see Slab in Kestrel)
            _threadStaticMemory = new RetainedMemory<byte>(_threadStaticBuffer.Memory);
            return _threadStaticBuffer;
        }

        internal static RetainedMemory<byte> StaticBufferMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_threadStaticBuffer == null)
                {
                    var x = StaticBuffer; // access getter
                }

                return _threadStaticMemory;
            }
        }

        /// <summary>
        /// Return a contiguous segment of memory backed by a pooled array
        /// </summary>
        /// <param name="length"></param>
        /// <param name="requireExact"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RetainedMemory<byte> Retain(int length, bool requireExact = true)
        {
            // https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/DefaultArrayPool.cs#L35
            // DefaultArrayPool has a minimum size of 16
            const int smallTreshhold = 16;
            if (length <= smallTreshhold)
            {
                if (_sharedBuffer == null)
                {
                    _sharedBuffer = BufferPool<byte>.RentOwnedPooledArray(SharedBufferSize);
                    // NB we must create a reference or the first RetainedMemory could
                    // dispose _sharedBuffer on RetainedMemory disposal.

                    // We are discarding RetainedMemory struct and will unpin below manually
                    _sharedBufferMemory = _sharedBuffer.Retain();

                    SharedBufferOffset = 0;
                }
                var bufferSize = _sharedBuffer.Memory.Length;
                var newOffset = SharedBufferOffset + length;
                if (newOffset > bufferSize)
                {
                    // replace shared buffer, the old one will be disposed
                    // when all ReservedMemory views on it are disposed
                    var previous = _sharedBufferMemory;
                    _sharedBuffer = BufferPool<byte>.RentOwnedPooledArray(SharedBufferSize);

                    _sharedBufferMemory = _sharedBuffer.Retain();
                    previous.Dispose(); // unpinning manually, now the buffer is free and it's retainers determine when it goes back to the pool

                    SharedBufferOffset = 0;
                    newOffset = length;
                }

                var retainedMemory = _sharedBuffer.Retain(SharedBufferOffset, length);
                SharedBufferOffset = BitUtil.Align(newOffset, Settings.SliceMemoryAlignment);
                return retainedMemory;
            }
            // NB here we exclusively own the buffer and disposal of RetainedMemory will cause
            // disposal and returning to pool of the ownedBuffer instance, unless references were added via
            // RetainedMemory.Clone()
            var ownedPooledArray = BufferPool<byte>.RentOwnedPooledArray(length);
            return requireExact ? ownedPooledArray.Retain(length) : ownedPooledArray.Retain();

        }



        /// <summary>
        /// Use a thread-static buffer as a temporary placeholder. One must only call this method and use the returned value
        /// from a single thread (no async/await, etc.).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete]
        internal static OwnedPooledArray<byte> UseTempBuffer(int minimumSize)
        {
            if (minimumSize <= StaticBufferSize)
            {
                return StaticBuffer;
            }
            return BufferPool<byte>.RentOwnedPooledArray(minimumSize);
        }
    }

    internal static class BufferPoolRetainedMemoryHelper<T>
    {
        // JIT-time const
        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsRetainedMemory = IsRetainedMemoryInit();

        public static void DisposeRetainedMemory(T[] array, int offset, int len)
        {
            for (int i = offset; i < offset + len; i++)
            {
                // TODO test it!
                Unsafe.DisposeConstrained(ref array[i]); // ((IDisposable)array[i]).Dispose();
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