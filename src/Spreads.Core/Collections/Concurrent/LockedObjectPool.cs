// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Threading;

namespace Spreads.Collections.Concurrent
{
    public sealed class LockedObjectPool<T> : PerCoreObjectPool<T, LockedObjectPoolCore<T>> where T : class
    {
        public LockedObjectPool(Func<T> factory, int perCoreSize, bool allocateOnEmpty = true, bool unbounded = false)
            : base(() => new RightPaddedLockedObjectPoolCore(factory, perCoreSize, allocateOnEmpty), factory, unbounded)
        {
        }

        internal sealed class RightPaddedLockedObjectPoolCore : LockedObjectPoolCore<T>
        {
#pragma warning disable 169
            private readonly Padding64 _padding64;
            private readonly Padding32 _padding32;
#pragma warning restore 169

            public RightPaddedLockedObjectPoolCore(Func<T> factory, int size, bool allocateOnEmpty = true) : base(
                factory, size, allocateOnEmpty)
            {
            }
        }
    }

    /// <summary>
    /// Thread-safe simple object pool.
    /// This pool is intended for storage and should not drop objects if there is space available.
    /// Good for native resources as opposed to <see cref="ObjectPool{T}"/>, which is only good for reducing managed objects allocations.
    /// </summary>
    public class LockedObjectPoolCore<T> : ObjectPoolCoreBase<T>, IObjectPool<T> where T : class
    {
        internal bool AllocateOnEmpty;

        private int _index;
        private int _locker;
#pragma warning disable 649
        internal bool TraceLowCapacityAllocation;
#pragma warning restore 649

        // TODO TypeLayout
        // In PerCoreObjectPool these objects are allocated sequentially
        // and the _locker field could be on the same cache line for 
        // multiple objects without padding. Perf difference almost 2x!
        // private long _padding0;
        // private long _padding1;

        public LockedObjectPoolCore(Func<T> factory, int size, bool allocateOnEmpty = true)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            AllocateOnEmpty = allocateOnEmpty;
            _items = new Element[size];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Rent()
        {
            if (_disposed)
                BuffersThrowHelper.ThrowDisposed<LockedObjectPool<T>>();

            var objects = _items;
            T obj = null;

            var allocate = false;
#if !NETCOREAPP
            RuntimeHelpers.PrepareConstrainedRegions();
            try
#endif
            {
                var spinner = new SpinWait();
                while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                    spinner.SpinOnce();

                if (_index < objects.Length)
                {
                    obj = objects[_index].Value;
                    objects[_index++] = default;
                    allocate = obj == null;
                }
            }
#if !NETCOREAPP
            finally
#endif
            {
                Volatile.Write(ref _locker, 0);
            }

            if (allocate || (obj == null && AllocateOnEmpty))
            {
                if (TraceLowCapacityAllocation && !allocate)
                    DoTrace();

                obj = CreateNewObject();
            }

            return obj;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T CreateNewObject()
        {
            return Factory();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoTrace()
        {
            Trace.TraceWarning("Allocating new object in LockedObjectPool due to low capacity");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(T obj)
        {
            if (_disposed)
                return false;

            bool pooled;
#if !NETCOREAPP
            RuntimeHelpers.PrepareConstrainedRegions();
            try
#endif
            {
                var spinner = new SpinWait();
                while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                    spinner.SpinOnce();

                pooled = _index != 0;
                if (pooled)
                    _items[--_index].Value = obj;
            }
#if !NETCOREAPP
            finally
#endif
            {
                Volatile.Write(ref _locker, 0);
            }

            return pooled;
        }
    }
}