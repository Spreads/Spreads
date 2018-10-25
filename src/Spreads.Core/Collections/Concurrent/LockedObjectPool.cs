// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Thread-safe simple object pool.
    /// This pool is intended for storage and should not drop objects if there is space available.
    /// Good for native resources as opposed to <see cref="ObjectPool{T}"/>, which is only good for reducing managed objects allocations.
    /// </summary>
    public sealed class LockedObjectPool<T> : IDisposable
        where T : class
    {
        internal bool AllocateOnEmpty;

        private Func<T> _factory;
        internal readonly T[] _objects;
        private int _index;
        private int _locker;
        internal bool TraceLowCapacityAllocation;
        private bool _disposed;

        public LockedObjectPool(int numberOfObjects, Func<T> factory, bool allocateOnEmpty = true)
        {
            _factory = factory;
            AllocateOnEmpty = allocateOnEmpty;
            _objects = new T[numberOfObjects];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            if (_disposed)
            {
                BuffersThrowHelper.ThrowDisposed<LockedObjectPool<T>>();
            }

            var objects = _objects;
            T obj = null;

            var allocate = false;
#if !NETCOREAPP
            try
#endif
            {
                var spinner = new SpinWait();
                while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                {
                    spinner.SpinOnce();
                }

                if (_index < objects.Length)
                {
                    obj = objects[_index];
                    objects[_index++] = null;
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
                {
                    DoTrace();
                }
                obj = CreateNewObject();
            }

            return obj;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T CreateNewObject()
        {
            return _factory?.Invoke();
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
            {
                return false;
            }
            bool pooled;
#if !NETCOREAPP
            try
#endif
            {
                var spinner = new SpinWait();
                while (0 != Interlocked.CompareExchange(ref _locker, 1, 0))
                {
                    spinner.SpinOnce();
                }

                pooled = _index != 0;
                if (pooled)
                {
                    _objects[--_index] = obj;
                }
            }
#if !NETCOREAPP
            finally
#endif
            {
                Volatile.Write(ref _locker, 0);
            }

            return pooled;
        }

        public void Dispose()
        {
            _disposed = true;
            _factory = null;
            foreach (var o in _objects)
            {
                if (o != null && o is IDisposable idisp)
                {
                    idisp.Dispose();
                }
            }
        }
    }
}
