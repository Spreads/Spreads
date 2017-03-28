// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using Spreads.Serialization;
using Spreads.Utils;

namespace Spreads.Buffers
{
    public static class BufferPool<T>
    {
        private static ArrayPool<T> PoolImpl = new DefaultArrayPool<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Rent(int minLength, bool requireExact = true)
        {
            // temp fix while SM doesn't support unequal keys/values
            var buffer = PoolImpl.Rent(minLength);
            if (requireExact && buffer.Length != minLength)
            {
                Return(buffer, false);
                return new T[minLength];
            }
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T[] array, bool clearArray = true)
        {
            PoolImpl.Return(array, clearArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OwnedBuffer<T> RentBuffer(int minLength, bool requireExact = true)
        {
            var array = Rent(minLength, requireExact);
            return OwnedPooledArray<T>.Create(array);
        }
    }

    internal static class BufferPool
    {
        // max pooled array size
        private const int SharedBufferSize = 4096;

        [ThreadStatic]
        private static OwnedBuffer<byte> _sharedBuffer;

        [ThreadStatic]
        private static int _sharedBufferOffset;

        /// <summary>
        /// Return a contiguous segment of memory backed by a pooled array
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PreservedBuffer<byte> PreserveMemory(int length)
        {
            // https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/DefaultArrayPool.cs#L35
            // DefaultArrayPool has a minimum size of 16
            const int smallTreshhold = 16;
            if (length <= smallTreshhold)
            {
                if (_sharedBuffer == null)
                {
                    _sharedBuffer = BufferPool<byte>.RentBuffer(SharedBufferSize, false);
                    _sharedBufferOffset = 0;
                }
                var bufferSize = _sharedBuffer.Length;
                var newOffset = _sharedBufferOffset + length;
                if (newOffset > bufferSize)
                {
                    // replace shared buffer, the old one will be disposed
                    // when all ReservedMemory views on it are disposed
                    _sharedBuffer = BufferPool<byte>.RentBuffer(SharedBufferSize, false);
                    _sharedBufferOffset = 0;
                    newOffset = length;
                }
                var buffer = _sharedBuffer.Buffer.Slice(_sharedBufferOffset, length);

                _sharedBufferOffset = BitUtil.Align(newOffset, IntPtr.Size);
                return new PreservedBuffer<byte>(buffer);
            }
            var ownedMemory = BufferPool<byte>.RentBuffer(length, false);
            var buffer2 = ownedMemory.Buffer.Slice(0, length);
            return new PreservedBuffer<byte>(buffer2);
        }
    }

    /// <summary>
    /// A memory pool that allows to get preserved buffers backed by pooled arrays.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PreservedBufferPool<T>
    {
        private static int _sizeOfT = BinarySerializer.Size<T>();

        /// <summary>
        /// Constructs a new PreservedBufferPool instance.
        /// Keep in mind that every thread using this pool will have a thread-static
        /// buffer of the size `sharedBufferSize * SizeOf(T)` for fast allocation
        /// of preserved buffers of size smaller or equal to smallTreshhold.
        /// </summary>
        /// <param name="sharedBufferSize">Size of thread-static buffers in number of T elements</param>
        /// <param name="smallTreshhold"></param>
        public PreservedBufferPool(int sharedBufferSize = 0)
        {
            if (_sizeOfT <= 0)
            {
                throw new NotSupportedException("PreservedBufferPool only supports blittable types");
            }
            if (sharedBufferSize <= 0)
            {
                sharedBufferSize = 4096 / BinarySerializer.Size<T>();
            }
            else
            {
                var bytesLength = BitUtil.FindNextPositivePowerOfTwo(sharedBufferSize * _sizeOfT);
                sharedBufferSize = bytesLength / _sizeOfT;
            }
            _sharedBufferSize = sharedBufferSize;
            _smallTreshhold = 16;
        }

        // max pooled array size
        private int _sharedBufferSize;

        // https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/DefaultArrayPool.cs#L35
        // DefaultArrayPool has a minimum size of 16
        private int _smallTreshhold;

        [ThreadStatic]
        private static OwnedBuffer<T> _sharedBuffer;

        [ThreadStatic]
        private static int _sharedBufferOffset;

        /// <summary>
        /// Return a contiguous segment of memory backed by a pooled array
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PreservedBuffer<T> PreserveBuffer(int length)
        {
            if (length <= _smallTreshhold)
            {
                if (_sharedBuffer == null)
                {
                    _sharedBuffer = BufferPool<T>.RentBuffer(this._sharedBufferSize, false);
                    _sharedBufferOffset = 0;
                }
                var bufferSize = _sharedBuffer.Length;
                var newOffset = _sharedBufferOffset + length;
                if (newOffset > bufferSize)
                {
                    // replace shared buffer, the old one will be disposed
                    // when all ReservedMemory views on it are disposed
                    _sharedBuffer = BufferPool<T>.RentBuffer(_sharedBufferSize, false);
                    _sharedBufferOffset = 0;
                    newOffset = length;
                }
                var buffer = _sharedBuffer.Buffer.Slice(_sharedBufferOffset, length);

                _sharedBufferOffset = newOffset;
                return new PreservedBuffer<T>(buffer);
            }
            var ownedMemory = BufferPool<T>.RentBuffer(length, false);
            var buffer2 = ownedMemory.Buffer.Slice(0, length);
            return new PreservedBuffer<T>(buffer2);
        }
    }
}