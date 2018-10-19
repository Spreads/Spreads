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

    public class OffHeapBufferPool<T> where T : unmanaged
    {
        private readonly LockedObjectPool<OffHeapMemory<T>> _pool;

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

            _pool = new LockedObjectPool<OffHeapMemory<T>>(maxBuffersCount,
#pragma warning disable HAA0301 // Closure Allocation Source
                () => new OffHeapMemory<T>(new OffHeapBuffer<T>(), this));
#pragma warning restore HAA0301 // Closure Allocation Source
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> RetainMemory(int length, bool requireExact = true)
        {
            var mem = _pool.Rent();
            mem.EnsureCapacity(length);
            if (requireExact)
            {
                return mem.Retain(length);
            }
            return mem.Retain();
        }

        public OffHeapMemory<T> Rent(int minimumCapacity)
        {
            var mem = _pool.Rent();
            mem.EnsureCapacity(minimumCapacity);
            return mem;
        }

        public bool Return(OffHeapMemory<T> offHeapMemory)
        {
            var pooled = _pool.Return(offHeapMemory);
            if (!pooled)
            {
                ((IDisposable)offHeapMemory).Dispose();
            }

            return pooled;
        }
    }

    public class OffHeapBufferPool : BufferPool
    {
        private readonly int _maxLength;
        internal readonly LockedObjectPool<OffHeapMemory> _pool;
        private readonly Func<OffHeapMemory> _factory;
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

            _factory = () => new OffHeapMemory(new OffHeapBuffer<byte>(), this);
            _pool = new LockedObjectPool<OffHeapMemory>(maxBuffersCount, _factory);
            _pool.TraceLowCapacityAllocation = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override RetainedMemory<byte> RetainMemory(int length, bool requireExact = true)
        {
            var mem = Rent(length);
            mem.EnsureCapacity(length);
            if (requireExact)
            {
                return mem.Retain(length);
            }
            return mem.Retain();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OffHeapMemory Rent(int minimumCapacity)
        {
            OffHeapMemory mem;
            if (minimumCapacity <= _maxLength)
            {
                mem = _pool.Rent();
            }
            else
            {
                Trace.TraceWarning($"Allocating large OffHeapMemory with minimumCapacity: {minimumCapacity}");
                mem = _factory();
            }

            mem.EnsureCapacity(minimumCapacity);
            return mem;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(OffHeapMemory offHeapMemory)
        {
            if (offHeapMemory.IsRetained)
            {
                ThrowDisposingRetained();
            }

            if (offHeapMemory.IsDisposed)
            {
                ThrowDisposed();
            }
            bool pooled = false;
            if (offHeapMemory.Capacity <= _maxLength)
            {
                pooled = _pool.Return(offHeapMemory);
            }

            if (!pooled)
            {
                ((IDisposable)offHeapMemory).Dispose();
            }

            return pooled;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException("OffHeapMemory");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposingRetained()
        {
            ThrowHelper.ThrowInvalidOperationException("Cannot return retained OffHeapMemory");
        }
    }
}
