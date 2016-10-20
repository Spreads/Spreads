using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


// ReSharper disable once CheckNamespace
namespace Spreads {
    // internal concrete implenetations of shared functionality
    internal static class Impl {
        public static class ArrayPool<T> {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static T[] Rent(int minLength) {
                return System.Buffers.ArrayPool<T>.Shared.Rent(minLength);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Return(T[] array, bool clearArray = true) {
                System.Buffers.ArrayPool<T>.Shared.Return(array, clearArray);
            }
        }
    }
}
