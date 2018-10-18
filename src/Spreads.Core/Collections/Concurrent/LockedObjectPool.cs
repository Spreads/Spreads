// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
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
        private readonly T[] _objects;
        private SpinLock _lock; // do not make this readonly; it's a mutable struct
        private int _index;

        internal LockedObjectPool(int numberOfObjects, Func<T> factory)
        {
            _factory = factory;
            _lock = new SpinLock(Debugger.IsAttached);
            _objects = new T[numberOfObjects];
        }

        public T Rent()
        {
            var objects = _objects;
            T obj = null;

            bool lockTaken = false, allocate = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_index < objects.Length)
                {
                    obj = objects[_index];
                    objects[_index++] = null;
                    allocate = obj == null;
                }
            }
            finally
            {
                if (lockTaken) _lock.Exit(false);
            }

            if (allocate)
            {
                obj = _factory?.Invoke();
            }

            return obj;
        }

        internal bool Return(T obj)
        {
            var lockTaken = false;
            bool pooled;
            try
            {
                _lock.Enter(ref lockTaken);
                pooled = _index != 0;
                if (pooled)
                {
                    _objects[--_index] = obj;
                }
            }
            finally
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
