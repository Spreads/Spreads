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

        /// <summary>
        /// Default OffHeap pool has capacity of 4. This static field could be changed to a new instance.
        /// Please know what you are doing. Large buffers are bad.
        /// </summary>
        internal static BufferPool OffHeap = new OffHeapBufferPool(4);

        // max pooled array size
        internal const int SharedBufferSize = 4096;

        internal static readonly int StaticBufferSize = Settings.ThreadStaticPinnedBufferSize;

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
            _threadStaticBuffer = OwnedPooledArray<byte>.Create(new byte[StaticBufferSize]);
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
                    var _ = StaticBuffer; // access getter
                }
                return _threadStaticMemory;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual RetainedMemory<byte> RetainMemory(int length, bool requireExact = true)
        {
            // https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/DefaultArrayPool.cs#L35
            // DefaultArrayPool has a minimum size of 16

            if (length <= _smallTreshhold)
            {
                if (_sharedBuffer == null)
                {
                    _sharedBuffer = OwnedPooledArray<byte>.Create(SharedBufferSize);
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
                    _sharedBuffer = OwnedPooledArray<byte>.Create(SharedBufferSize);

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
            var ownedPooledArray = OwnedPooledArray<byte>.Create(length);
            return requireExact ? ownedPooledArray.Retain(length) : ownedPooledArray.Retain();
        }

        /// <summary>
        /// Return a contiguous segment of memory backed by a pooled memory from a shares array pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RetainedMemory<byte> Retain(int length, bool requireExact = true)
        {
            return Shared.RetainMemory(length, requireExact);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RetainedMemory<byte> RetainNoLoh(int length, bool requireExact = true)
        {
            if (length >= Settings.LOH_LIMIT)
            {
                if (OffHeap != null)
                {
                    return OffHeap.RetainMemory(length, requireExact);
                }
                ThrowHelper.ThrowInvalidOperationException("BufferPool.OffHeap is null while requesting RetainNoLoh");
            }
            return Shared.RetainMemory(length, requireExact);
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
