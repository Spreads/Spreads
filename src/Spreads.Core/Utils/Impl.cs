// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Spreads {

    // internal concrete implenetations of shared functionality
    internal static class Impl {

        public static class ArrayPool<T> {

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T[] Rent(int minLength, bool requireExact = true) {
                // temp fix while SM doesn't support unequal keys/values
                var buffer = System.Buffers.ArrayPool<T>.Shared.Rent(minLength);
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
                    System.Buffers.ArrayPool<T>.Shared.Return(array, clearArray);
                } catch {
                }
            }
        }
    }
}