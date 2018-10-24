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
    }

    public class BufferPool
    {
        internal static BufferPool Shared = new BufferPool();

        internal static RetainableMemoryPool<byte, ArrayMemory<byte>> PinnedArrayMemoryPool =
            new RetainableMemoryPool<byte, ArrayMemory<byte>>(null,
                2048,
                1024 * 1024,
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
        /// Shared buffers are for slicing of small PreservedBuffers
        /// </summary>
        [ThreadStatic]
        private static ArrayMemory<byte> _sharedBuffer;

        [ThreadStatic]
        private static RetainedMemory<byte> _sharedBufferMemory;

        [ThreadStatic]
        internal static int SharedBufferOffset;

        /// <summary>
        /// Temp storage e.g. for serialization
        /// </summary>
        [ThreadStatic]
        private static ArrayMemory<byte> _threadStaticBuffer;

        [ThreadStatic]
        private static RetainedMemory<byte> _threadStaticMemory;

        private int _smallTreshhold = 16;

        internal BufferPool()
        { }

        protected internal int SmallTreshhold
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _smallTreshhold;
            set
            {
                if (value > SharedBufferSize)
                {
                    value = SharedBufferSize;
                }

                if (value < 16)
                {
                    value = 16;
                }
                _smallTreshhold = value;
            }
        }

        /// <summary>
        /// Thread-static <see cref="ArrayMemory{T}"/> with size of <see cref="StaticBufferSize"/>.
        /// Never dispose it!
        /// </summary>
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
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ArrayMemory<byte> CreateThreadStaticBuffer()
        {
            // TODO review this mess with externally owned
            _threadStaticBuffer = ArrayMemory<byte>.Create(new byte[StaticBufferSize], true);
            _threadStaticBuffer.Increment();
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

            //// https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/DefaultArrayPool.cs#L35
            //// DefaultArrayPool has a minimum size of 16

            //if (length <= _smallTreshhold)
            //{
            //    if (_sharedBuffer == null)
            //    {
            //        _sharedBuffer = ArrayMemory<byte>.Create(SharedBufferSize);
            //        // NB we must create a reference or the first RetainedMemory could
            //        // dispose _sharedBuffer on RetainedMemory disposal.

            //        // We are discarding RetainedMemory struct and will unpin below manually
            //        _sharedBufferMemory = _sharedBuffer.Retain();

            //        SharedBufferOffset = 0;
            //    }
            //    var bufferSize = _sharedBuffer.Memory.Length;
            //    var newOffset = SharedBufferOffset + length;
            //    if (newOffset > bufferSize)
            //    {
            //        // replace shared buffer, the old one will be disposed
            //        // when all ReservedMemory views on it are disposed
            //        var previous = _sharedBufferMemory;
            //        _sharedBuffer = ArrayMemory<byte>.Create(SharedBufferSize);

            //        _sharedBufferMemory = _sharedBuffer.Retain();
            //        previous.Dispose(); // unpinning manually, now the buffer is free and it's retainers determine when it goes back to the pool

            //        SharedBufferOffset = 0;
            //        newOffset = length;
            //    }

            //    var retainedMemory = _sharedBuffer.Retain(SharedBufferOffset, length);
            //    SharedBufferOffset = BitUtil.Align(newOffset, Settings.SliceMemoryAlignment);
            //    return retainedMemory;
            //}
            //// NB here we exclusively own the buffer and disposal of RetainedMemory will cause
            //// disposal and returning to pool of the ownedBuffer instance, unless references were added via
            //// RetainedMemory.Clone()
            //var arrayMemory = ArrayMemory<byte>.Create(length);
            //return requireExact ? arrayMemory.Retain(0, length) : arrayMemory.Retain();
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
        // JIT-time const
        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsRetainedMemory = IsRetainedMemoryInit();

        public static void DisposeRetainedMemory(T[] array, int offset, int len)
        {
            // TODO assert here
            for (int i = offset; i < offset + len; i++)
            {
                // TODO test it!
                UnsafeEx.DisposeConstrained(ref array[i]); // ((IDisposable)array[i]).Dispose();
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
