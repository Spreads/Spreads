// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System.Buffers;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers {

    public static class BufferPool<T> {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Rent(int minLength, bool requireExact = true) {
            // temp fix while SM doesn't support unequal keys/values
            var buffer = ArrayPool<T>.Shared.Rent(minLength);
            if (requireExact && buffer.Length != minLength) {
                Return(buffer, false);
                return new T[minLength];
            } else {
                return buffer;
            }
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

}