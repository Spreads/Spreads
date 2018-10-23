// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    // HAA0301/HAA0302 only happens once per pool

    public class OffHeapMemoryPool<T> : MemoryPool<T> //where T : struct
    {
        /// <summary>
        /// Internal for tests only, do not use in other places.
        /// </summary>
        internal readonly LockedObjectPool<OffHeapMemory<T>> _pool;

        private readonly int _maxBufferSize;

#pragma warning disable HAA0302 // Display class allocation to capture closure

        /// <param name="maxBuffersCount">Max number of buffers to pool.</param>
        /// <param name="maxBufferSize">Max size of a buffer.</param>
        public OffHeapMemoryPool(int maxBuffersCount, int maxBufferSize = 16 * 1024 * 1024)
#pragma warning restore HAA0302 // Display class allocation to capture closure
        {
            // First Pow2 in LOH
            if (maxBufferSize < 128 * 1024) // for other cases Array-based is OK
            {
                maxBufferSize = 128 * 1024;
            }

            if (maxBufferSize > 100 * 1024 * 1024) // currently 8Mb + 4032 is max use case
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(maxBufferSize));
            }

            _maxBufferSize = maxBufferSize;

            _pool = new LockedObjectPool<OffHeapMemory<T>>(maxBuffersCount,
#pragma warning disable HAA0301 // Closure Allocation Source
                () => new OffHeapMemory<T>(this));
#pragma warning restore HAA0301 // Closure Allocation Source
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> RetainMemory(int length, bool requireExact = true)
        {
            // for large buffers we prefer small number of them but increase the size on-demand

            var mem = RentMemory(length);
            if (requireExact)
            {
                return mem.Retain(length);
            }
            return mem.Retain();
        }

        // Rent -> OHM -> OHM.Dispose/Finalize/OnNoRef return to the pool, if cannot pool then destroy obj & counter

        protected override void Dispose(bool disposing)
        {
            _pool.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IMemoryOwner<T> Rent(int minimumCapacity = -1)
        {
            if (minimumCapacity == -1)
            {
                minimumCapacity = 4096;
            }

            return RentMemory(minimumCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OffHeapMemory<T> RentMemory(int minimumCapacity)
        {
            if (unchecked((uint)minimumCapacity) > _maxBufferSize)
            {
                RentThrowArgumentOutOfRange();
            }
            var offHeapMemory = _pool.Rent();
            offHeapMemory._pool = this;
            offHeapMemory.Init(minimumCapacity);
            Debug.Assert(!offHeapMemory.IsDisposed);
            return offHeapMemory;
        }

        public override int MaxBufferSize => _maxBufferSize;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RentThrowArgumentOutOfRange()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException("minimumCapacity");
        }

        [Obsolete("Should only be used from OnNoRefs in OffHeapMemory or in tests")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Return(OffHeapMemory<T> offHeapMemory)
        {
            // offHeapMemory must be in the same state as it was after Rent so that Rent/Return work without any other requirements

            if (offHeapMemory.IsDisposed)
            {
                ThrowDisposed<OffHeapMemory<T>>();
            }

            if (offHeapMemory.IsRetained)
            {
                ThrowDisposingRetained<OffHeapMemory<T>>();
            }

            if (offHeapMemory._pool != this)
            {
                ThrowAlienOrAlreadyPooled<OffHeapMemory<T>>();
            }

            var pooled = _pool.Return(offHeapMemory);
            offHeapMemory._pool = null;
            return pooled;
        }
    }
}
