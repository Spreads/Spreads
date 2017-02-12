// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Spreads.Utils;

namespace Spreads.Buffers {

    public static class BufferPool<T> {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Rent(int minLength, bool requireExact = true) {
            // temp fix while SM doesn't support unequal keys/values
            var buffer = ArrayPool<T>.Shared.Rent(minLength);
            if (requireExact && buffer.Length != minLength) {
                Return(buffer, false);
                return new T[minLength];
            }
            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T[] array, bool clearArray = true) {
            try {
                ArrayPool<T>.Shared.Return(array, clearArray);
            } catch {
                // ignored
                // NB temporarily, we ignore alien arrays instead of throwing. The method should return a bool, think about returning customized impl instead of relying on System.Buffer, it was just three small files
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OwnedMemory<T> RentMemory(int minLength, bool requireExact = true) {
            var array = Rent(minLength, requireExact);
            return OwnedPooledArray<T>.Create(array);
        }
    }

    internal static class BufferPool {

        // max pooled array size
        private const int SharedBufferSize = 4096; // 8 * 32; // NB must ensure that the upcoming safe disposal machinery won't allocate and we never exceed the initial bitmask capacity if every segment is used once

        [ThreadStatic]
        private static OwnedMemory<byte> _sharedBuffer;

        [ThreadStatic]
        private static int _sharedBufferOffset;

        /// <summary>
        /// Return a contiguous segment of memory backed by a pooled array
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PreservedMemory<byte> PreserveMemory(int length) {
            // https://github.com/dotnet/corefx/blob/master/src/System.Buffers/src/System/Buffers/DefaultArrayPool.cs#L35
            // DefaultArrayPool has a minimum size of 16
            const int smallTreshhold = 16;
            if (length <= smallTreshhold) {
                if (_sharedBuffer == null) {
                    _sharedBuffer = BufferPool<byte>.RentMemory(SharedBufferSize, false);
                    _sharedBufferOffset = 0;
                }
                var bufferSize = _sharedBuffer.Length;
                var newOffset = _sharedBufferOffset + length;
                if (newOffset > bufferSize) {
                    // replace shared buffer, the old one will be disposed
                    // when all ReservedMemory views on it are disposed
                    _sharedBuffer = BufferPool<byte>.RentMemory(SharedBufferSize, false);
                    _sharedBufferOffset = 0;
                }
                var memory = _sharedBuffer.Memory.Slice(_sharedBufferOffset, length);

                _sharedBufferOffset = BitUtil.Align(newOffset, IntPtr.Size);
                return memory.Preserve();
            }
            var ownedMemory = BufferPool<byte>.RentMemory(length, false);
            var memory2 = ownedMemory.Memory.Slice(0, length);
            return memory2.Preserve();
        }
    }
}
