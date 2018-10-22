// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    // HAA0301/HAA0302 only happens once per pool

    public class OffHeapBufferPool<T> where T : struct
    {
        /// <summary>
        /// Internal for tests only, do not use in other places.
        /// </summary>
        internal readonly LockedObjectPool<OffHeapMemory<T>> _pool;

        private readonly int _maxLength;

#pragma warning disable HAA0302 // Display class allocation to capture closure

        /// <param name="maxBuffersCount">Max number of buffers to pool.</param>
        /// <param name="maxLength">Max size of a buffer.</param>
        public OffHeapBufferPool(int maxBuffersCount, int maxLength = 16 * 1024 * 1024)
#pragma warning restore HAA0302 // Display class allocation to capture closure
        {
            // First Pow2 in LOH
            if (maxLength < 128 * 1024) // for other cases Array-based is OK
            {
                maxLength = 128 * 1024;
            }

            if (maxLength > 100 * 1024 * 1024) // currently 8Mb + 4032 is max use case
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(maxLength));
            }

            _maxLength = maxLength;

            _pool = new LockedObjectPool<OffHeapMemory<T>>(maxBuffersCount,
#pragma warning disable HAA0301 // Closure Allocation Source
                () => new OffHeapMemory<T>(this));
#pragma warning restore HAA0301 // Closure Allocation Source
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> RetainMemory(int length, bool requireExact = true)
        {
            // for large buffers we prefer small number of them but increase the size on-demand

            var mem = Rent(length);
            if (requireExact)
            {
                return mem.Retain(length);
            }
            return mem.Retain();
        }

        // Rent -> OHM -> OHM.Dispose/Finalize/OnNoRef return to the pool, if cannot pool then destroy obj & counter

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OffHeapMemory<T> Rent(int minimumCapacity)
        {
            if (unchecked((uint)minimumCapacity) > _maxLength)
            {
                RentThrowArgumentOutOfRange();
            }
            var offHeapMemory = _pool.Rent();
            offHeapMemory._pool = this;
            // mem.Counter.Revive();
            offHeapMemory.Init(minimumCapacity);
            Debug.Assert(!offHeapMemory.IsDisposed);
            return offHeapMemory;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RentThrowArgumentOutOfRange()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException("minimumCapacity");
        }

        [Obsolete("Should only be used from OnNoRefs in OffHeapMemory or in tests")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Return(OffHeapMemory<T> offHeapMemory)
        {
            if (offHeapMemory.IsDisposed)
            {
                ReturnThrowDisposed();
            }

            // offHeapMemory must be in the same state as it was after Rent so that Rent/Return work without any other requirements
            if (offHeapMemory.IsRetained)
            {
                ReturnThrowRetained();
            }

            // offHeapMemory.Counter.Dispose();

            if (offHeapMemory._pool == null)
            {
                ReturnThrowAlienOrAlreadyPooled();
            }

            // OffHeapMemory finalizer knows what to do in the case the object is not
            // disposed after false return here
            var pooled = _pool.Return(offHeapMemory);
            offHeapMemory._pool = null;
            return pooled;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReturnThrowAlienOrAlreadyPooled()
        {
            ThrowHelper.ThrowObjectDisposedException("Cannot return to pool alien or already pooled buffer");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReturnThrowRetained()
        {
            ThrowHelper.ThrowInvalidOperationException("Cannot return to pool retained buffer");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReturnThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException("OffHeapMemory");
        }
    }
}
