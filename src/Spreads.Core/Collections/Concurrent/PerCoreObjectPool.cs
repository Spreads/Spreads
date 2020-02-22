using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Native;

namespace Spreads.Collections.Concurrent
{
    public class PerCoreObjectPool<T, TPoolImpl> : IObjectPool<T> where TPoolImpl : IObjectPool<T> where T : class
    {
        private readonly Func<T> _objFactory;
        private const int MaxPools = 128;

        private readonly PoolEntry[] _perCorePoolEntries;

        private readonly ConcurrentQueue<T> _unboundedPool;
        private volatile bool _disposed;

        protected PerCoreObjectPool(Func<TPoolImpl> perCorePoolFactory, Func<T> objFactory, bool unbounded)
        {
            var perCorePools = new PoolEntry[Math.Min(Environment.ProcessorCount, MaxPools)];
            for (int i = 0; i < perCorePools.Length; i++)
            {
                perCorePools[i] = new PoolEntry(perCorePoolFactory());
            }

            _perCorePoolEntries = perCorePools;

            _objFactory = objFactory;

            if (unbounded)
            {
                _unboundedPool = new ConcurrentQueue<T>();
            }
        }

        public T Rent()
        {
            return Rent(CpuIdCache.GetCurrentCpuId());
        }

        public bool Return(T obj)
        {
            return Return(obj, CpuIdCache.GetCurrentCpuId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T Rent(int cpuId)
        {
            if (_disposed)
            {
                BuffersThrowHelper.ThrowDisposed<LockedObjectPool<T>>();
            }

            var poolEntries = _perCorePoolEntries;
            var index = cpuId % poolEntries.Length;
            T? obj;
            
            for (int i = 0; i <= poolEntries.Length; i++)
            {
                ref var entry = ref poolEntries[index];
                if ((obj = entry.Pool.Rent()) != null)
                {
                    return obj;
                }
                
                if (++index == poolEntries.Length) index = 0;
            }

            if (_unboundedPool != null && _unboundedPool.TryDequeue(out obj))
            {
                return obj;
            }

            return _objFactory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Return(T obj, int cpuId)
        {
            if (_disposed)
            {
                return false;
            }

            var poolEntries = _perCorePoolEntries;
            int index = cpuId % poolEntries.Length;
            
            for (int i = 0; i <= poolEntries.Length; i++)
            {
                ref var entry = ref poolEntries[index];
                if (entry.Pool.Return(obj))
                {
                    return true;
                }

                if (++index == poolEntries.Length) index = 0;
            }

            if (_unboundedPool != null)
            {
                _unboundedPool.Enqueue(obj);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            lock (_perCorePoolEntries)
            {
                if (_disposed)
                    return;
                _disposed = true;

                foreach (var entry in _perCorePoolEntries)
                {
                    entry.Pool.Dispose();
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

        private struct PoolEntry
        {
            public TPoolImpl Pool;

            public PoolEntry(TPoolImpl pool)
            {
                Pool = pool;
            }
        }
    }
}