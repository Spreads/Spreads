// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

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
        private Func<T> _factory;
        private readonly bool _allocateOnEmpty;
        internal readonly T[] _objects;
        private SpinLock _lock; // do not make this readonly; it's a mutable struct
        private int _index;
        internal bool TraceLowCapacityAllocation;

        internal LockedObjectPool(int numberOfObjects, Func<T> factory, bool allocateOnEmpty = true)
        {
            _factory = factory;
            _allocateOnEmpty = allocateOnEmpty;
            _lock = new SpinLock(Debugger.IsAttached);
            _objects = new T[numberOfObjects];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            var objects = _objects;
            T obj = null;

            bool lockTaken = false, allocate = false;
#if !NETCOREAPP
            try
#endif
            {
                _lock.Enter(ref lockTaken);

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
                if (lockTaken) _lock.Exit(false);
            }

            if (allocate || (obj == null && _allocateOnEmpty))
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
        internal bool Return(T obj)
        {
            var lockTaken = false;
            bool pooled;
#if !NETCOREAPP
            try
#endif
            {
                _lock.Enter(ref lockTaken);
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
                if (lockTaken)
                {
                    _lock.Exit(false);
                }
            }

            return pooled;
        }

        public void Dispose()
        {
            _factory = null;
            T obj;
            while ((obj = Rent()) != null)
            {
                if (obj is IDisposable idisp)
                {
                    idisp.Dispose();
                }
            }
        }
    }
}
