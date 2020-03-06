// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Native;

namespace Spreads.Collections.Concurrent
{
    public class PerCoreObjectPool<T, TPoolImpl, TWrapper> : IObjectPool<T>
        where T : class
        where TPoolImpl : IObjectPool<T>
        where TWrapper : struct, IObjectPoolWrapper<T, TPoolImpl>
    {
        private readonly Func<T>? _objFactory;

        protected readonly TWrapper[] _perCorePools;

        private readonly ConcurrentQueue<T> _unboundedPool;

        private volatile bool _disposed;

        protected PerCoreObjectPool(Func<TPoolImpl> perCorePoolFactory, Func<T> objFactory, bool unbounded)
        {
            _perCorePools = new TWrapper[Cpu.CoreCount];
            for (int i = 0; i < _perCorePools.Length; i++)
            {
                _perCorePools[i] = new TWrapper {Pool = perCorePoolFactory()};
            }

            _objFactory = objFactory;

            if (unbounded)
                _unboundedPool = new ConcurrentQueue<T>();
        }

        public T? Rent()
        {
            return Rent(Cpu.GetCurrentCoreId());
        }

        public bool Return(T obj)
        {
            return Return(obj, Cpu.GetCurrentCoreId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T? Rent(int cpuId)
        {
            if (_disposed)
                BuffersThrowHelper.ThrowDisposed<LockedObjectPool<T>>();

            T? obj;

            for (int i = 0; i <= Cpu.CoreCount; i++)
            {
                if ((obj = _perCorePools[cpuId].Rent()) != null)
                    return obj;

                if (++cpuId == Cpu.CoreCount)
                    cpuId = 0;
            }

            return RentSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T? RentSlow()
        {
            if (_unboundedPool != null && _unboundedPool.TryDequeue(out var obj))
                return obj;

            return _objFactory?.Invoke();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Return(T obj, int cpuId)
        {
            if (_disposed)
                return false;

            for (int i = 0; i <= Cpu.CoreCount; i++)
            {
                if (_perCorePools[cpuId].Return(obj))
                    return true;

                if (++cpuId == Cpu.CoreCount)
                    cpuId = 0;
            }

            return ReturnSlow(obj);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ReturnSlow(T obj)
        {
            if (_unboundedPool != null)
            {
                _unboundedPool.Enqueue(obj);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            lock (_perCorePools)
            {
                if (_disposed)
                    return;
                _disposed = true;

                foreach (var pool in _perCorePools)
                {
                    pool.Dispose();
                }

                if (_unboundedPool != null)
                {
                    while (_unboundedPool.TryDequeue(out var item) && item is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// For diagnostics only.
        /// </summary>
        internal IEnumerable<T> EnumerateItems()
        {
            foreach (var poolImpl in _perCorePools)
            {
                if (poolImpl.Pool is ObjectPoolCoreBase<T> pool)
                {
                    // ReSharper disable once HeapView.ObjectAllocation.Possible
                    // ReSharper disable once HeapView.ObjectAllocation
                    foreach (var enumerateItem in pool.EnumerateItems())
                    {
                        yield return enumerateItem;
                    }
                }
            }
        }
    }
}