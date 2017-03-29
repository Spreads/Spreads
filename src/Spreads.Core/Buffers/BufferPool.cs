// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;

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
        internal static OwnedBuffer<T> RentOwnedBuffer(int minLength, bool requireExact = true)
        {
            var array = Rent(minLength, requireExact);
            return OwnedPooledArray<T>.Create(array);
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
        internal static OwnedBuffer<byte> _sharedBuffer;

        [ThreadStatic]
        internal static int _sharedBufferOffset;

        [ThreadStatic]
        internal static OwnedBuffer<byte> _threadStaticBuffer;

        /// <summary>
        /// Thread-static OwnedBuffer<byte> with size of <see cref="StaticBufferSize"/>.
        /// </summary>
        internal static OwnedBuffer<byte> StaticBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _threadStaticBuffer ?? (_threadStaticBuffer = new StaticOwnedBuffer<byte>(StaticBufferSize));
            }
        }

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
                    _sharedBuffer = BufferPool<byte>.RentOwnedBuffer(SharedBufferSize, false);
                    // NB we must create a reference or the first PreservedBuffer could 
                    // dispose _sharedBuffer on PreservedBuffer disposal.
                    _sharedBuffer.AddReference();
                    _sharedBufferOffset = 0;
                }
                var bufferSize = _sharedBuffer.Length;
                var newOffset = _sharedBufferOffset + length;
                if (newOffset > bufferSize)
                {
                    // replace shared buffer, the old one will be disposed
                    // when all ReservedMemory views on it are disposed
                    var previous = _sharedBuffer;
                    _sharedBuffer = BufferPool<byte>.RentOwnedBuffer(SharedBufferSize, false);
                    _sharedBuffer.AddReference();
                    previous.Release();
                    _sharedBufferOffset = 0;
                    newOffset = length;
                }
                var buffer = _sharedBuffer.Buffer.Slice(_sharedBufferOffset, length);

                _sharedBufferOffset = BitUtil.Align(newOffset, IntPtr.Size);
                return new PreservedBuffer<byte>(buffer);
            }
            // NB here we exclusively own the buffer and disposal of PreservedBuffer will cause 
            // disposal and returning to pool of the ownedBuffer instance, unless references were added via
            // PreservedBuffer.Close() or PreservedBuffer.Buffer.Reserve()/Pin() methods
            var ownedBuffer = BufferPool<byte>.RentOwnedBuffer(length, false);
            var buffer2 = ownedBuffer.Buffer.Slice(0, length);
            return new PreservedBuffer<byte>(buffer2);
        }

        internal static void DisposePreservedBuffers<T>(T[] array, int offset, int len)
        {
            // TODO it is possible to this without boxing using reflection,
            // however boxing is Gen0 here and should be fast
            for (int i = offset; i < offset + len; i++)
            {
                ((IDisposable)array[i]).Dispose();
            }
        }

        internal static bool IsPreservedBuffer<T>()
        {
            var ti = typeof(T).GetTypeInfo();
            if (ti.IsGenericTypeDefinition)
            {
                return ti.GetGenericTypeDefinition() == typeof(PreservedBuffer<>);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Use a thread-static buffer as a temporary placeholder. One must only call this method and use the returned value
        /// from a single thread (no async/await, etc.).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OwnedBuffer<byte> UseTempBuffer(int minimumSize)
        {
            if (minimumSize <= StaticBufferSize)
            {
                return StaticBuffer;
            }
            return BufferPool<byte>.RentOwnedBuffer(minimumSize);
        }

        /// <summary>
        /// Prevent disposing of underlying array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal class StaticOwnedBuffer<T> : OwnedBuffer<T>
        {
            public StaticOwnedBuffer(int length) : base(new T[length], 0, length)
            {
            }

            protected override void Dispose(bool disposing)
            { }
        }
    }

    /// <summary>
    /// A memory pool that allows to get preserved buffers backed by pooled arrays.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PreservedBufferPool<T>
    {
        [ThreadStatic]
        internal static OwnedBuffer<T> _sharedBuffer;

        [ThreadStatic]
        internal static int _sharedBufferOffset;

        internal static int _sizeOfT = BinarySerializer.Size<T>();

        // max pooled array size
        internal int _sharedBufferSize;

        // https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/DefaultArrayPool.cs#L35
        // DefaultArrayPool has a minimum size of 16
        internal int _smallTreshhold;

        /// <summary>
        /// Constructs a new PreservedBufferPool instance.
        /// Keep in mind that every thread using this pool will have a thread-static
        /// buffer of the size `sharedBufferSize * SizeOf(T)` for fast allocation
        /// of preserved buffers of size smaller or equal to smallTreshhold.
        /// </summary>
        /// <param name="sharedBufferSize">Size of thread-static buffers in number of T elements</param>
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
                    _sharedBuffer = BufferPool<T>.RentOwnedBuffer(this._sharedBufferSize, false);
                    // NB we must create a reference or the first PreservedBuffer could 
                    // dispose _sharedBuffer on PreservedBuffer disposal.
                    _sharedBuffer.AddReference();
                    _sharedBufferOffset = 0;
                }
                var bufferSize = _sharedBuffer.Length;
                var newOffset = _sharedBufferOffset + length;
                if (newOffset > bufferSize)
                {
                    // replace shared buffer, the old one will be disposed
                    // when all ReservedMemory views on it are disposed
                    var previous = _sharedBuffer;
                    _sharedBuffer = BufferPool<T>.RentOwnedBuffer(_sharedBufferSize, false);
                    _sharedBuffer.AddReference();
                    previous.Release();
                    _sharedBufferOffset = 0;
                    newOffset = length;
                }
                var buffer = _sharedBuffer.Buffer.Slice(_sharedBufferOffset, length);

                _sharedBufferOffset = newOffset;
                return new PreservedBuffer<T>(buffer);
            }
            // NB here we exclusively own the buffer and disposal of PreservedBuffer will cause 
            // disposal and returning to pool of the ownedBuffer instance, unless references were added via
            // PreservedBuffer.Close() or PreservedBuffer.Buffer.Reserve()/Pin() methods
            var ownedBuffer = BufferPool<T>.RentOwnedBuffer(length, false);
            var buffer2 = ownedBuffer.Buffer.Slice(0, length);
            return new PreservedBuffer<T>(buffer2);
        }
    }
}