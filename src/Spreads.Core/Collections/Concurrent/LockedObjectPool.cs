// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Threading;

namespace Spreads.Collections.Concurrent
{
    public sealed class LockedObjectPool<T> : PerCoreObjectPool<T, LockedObjectPoolCore<T>, LockedObjetPoolCoreWrapper<T>> where T : class
    {
        public LockedObjectPool(Func<T> factory, int perCoreSize, bool allocateOnEmpty = true, bool unbounded = false)
            : base(() => new RightPaddedLockedObjectPoolCore(factory, perCoreSize, allocateOnEmpty: false), allocateOnEmpty ? factory : () => null, unbounded)
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

    public struct LockedObjetPoolCoreWrapper<T> : IObjectPoolWrapper<T, LockedObjectPoolCore<T>> where T : class
    {
        public LockedObjectPoolCore<T> Pool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            set;
        }

        public void Dispose()
        {
            ((IDisposable) Pool).Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Rent()
        {
            return Pool.Rent();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(T obj)
        {
            return Pool.Return(obj);
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

        public LockedObjectPoolCore(Func<T> factory, int size, bool allocateOnEmpty = true)
            : base(size, factory ?? throw new ArgumentNullException(nameof(factory)))
        {
            AllocateOnEmpty = allocateOnEmpty;
        }

        public int Index => _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? Rent()
        {
            var items = _items;
            T? obj = null;

            var allocate = false;
#if !NETCOREAPP
            RuntimeHelpers.PrepareConstrainedRegions();
            try
#endif
            {
                var spinner = new SpinWait();
                while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                {
                    spinner.SpinOnce();
                    Cpu.FlushCurrentCpuId();
                }

                if (_index < items.Length)
                {
                    ref var item = ref items[_index];
                    obj = item.Value;
                    item = default;
                    _index++;
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
        private T? CreateNewObject() => Factory?.Invoke();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoTrace()
        {
            Trace.TraceWarning("Allocating new object in LockedObjectPool due to low capacity");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(T obj)
        {
            bool pooled;
#if !NETCOREAPP
            RuntimeHelpers.PrepareConstrainedRegions();
            try
#endif
            {
                var spinner = new SpinWait();
                while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                {
                    spinner.SpinOnce();
                    Cpu.FlushCurrentCpuId();
                }

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
