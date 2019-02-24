// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    /// <summary>
    /// Buffers throw helper
    /// </summary>
    internal static class BuffersThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowBadLength()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException("length");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNull<T>()
        {
            ThrowHelper.ThrowArgumentNullException(nameof(T));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRange()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException("index");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDisposed<T>()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(T));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDisposingRetained<T>()
        {
            ThrowHelper.ThrowInvalidOperationException("Cannot dispose retained " + nameof(T));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThowNegativeRefCount()
        {
            ThrowHelper.ThrowInvalidOperationException("Negative ref count");
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        //internal static void ThrowAlienOrAlreadyPooled<T>()
        //{
        //    ThrowHelper.ThrowInvalidOperationException("Cannot return to pool alien or already pooled " + nameof(T));
        //}

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowAlreadyPooled<T>()
        {
            ThrowHelper.ThrowObjectDisposedException("Cannot return to a pool an already pooled " + nameof(T));
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotFromPool<T>()
        {
            ThrowHelper.ThrowInvalidOperationException("Memory not from pool " + nameof(T));
        }
    }
}