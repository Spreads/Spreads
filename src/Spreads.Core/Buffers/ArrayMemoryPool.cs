// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    public sealed class ArrayMemoryPool<T> : MemoryPool<T>
    {
        public new static ArrayMemoryPool<T> Shared = new ArrayMemoryPool<T>();

        private ArrayMemoryPool()
        { }

        // ReSharper disable once InconsistentNaming
        private const int s_maxBufferSize = int.MaxValue;

        public override int MaxBufferSize => s_maxBufferSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal OwnedPooledArray<T> RentCore(int minimumBufferSize = -1)
        {
            if (minimumBufferSize == -1)
            {
                minimumBufferSize = 1 + (4095 / Unsafe.SizeOf<T>());
            }
            else if (((uint)minimumBufferSize) > s_maxBufferSize)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(minimumBufferSize));
            }

            return OwnedPooledArray<T>.Create(minimumBufferSize);
        }

        public override IMemoryOwner<T> Rent(int minimumBufferSize = -1)
        {
            return RentCore(minimumBufferSize);
        }

        protected override void Dispose(bool disposing)
        {
        }  // ArrayMemoryPool is a shared pool so Dispose() would be a nop even if there were native resources to dispose.
    }
}
