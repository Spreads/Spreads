// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Object pool based on ConcurrentBag
    /// </summary>
    public class ObjectPoolBag<T> : IObjectPool<T> where T : class, IPoolable<T>, new()
    {
        private readonly int _capacity;
        private int _count;
        private readonly ConcurrentBag<T> _pool = new ConcurrentBag<T>();

        public ObjectPoolBag(int capacity = int.MaxValue)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public bool IsBounded => _capacity < int.MaxValue;
        public int Capacity => _capacity;
        public int Count => _pool.Count;

        public T Allocate()
        {
            T result;
            if (!_pool.TryTake(out result))
            {
                result = new T();
            }
            else
            {
                if (_capacity < int.MaxValue)
                {
                    Interlocked.Decrement(ref _count);
                }
            }
            result.Init();
            return result;
        }

        public void Free(T obj)
        {
            obj.Release();
            if (_capacity < int.MaxValue)
            {
                var count = Volatile.Read(ref _count);
                if (count >= _capacity) return;
                Interlocked.Increment(ref count);
            }
            _pool.Add(obj);
        }
    }

    /// <summary>
    /// Object pool based on BoundedConcurrentQueue
    /// </summary>
    public class ObjectPoolQueue<T> : IObjectPool<T> where T : class, IPoolable<T>, new()
    {
        private readonly int _capacity;
        private readonly BoundedConcurrentQueue<T> _pool;

        public ObjectPoolQueue(int capacity = int.MaxValue)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _pool = new BoundedConcurrentQueue<T>(_capacity);
        }

        public bool IsBounded => _capacity < int.MaxValue;
        public int Capacity => _capacity;
        public int Count => _pool.Count;

        public T Allocate()
        {
            T result;
            if (!_pool.TryDequeue(out result))
            {
                result = new T();
            }
            result.Init();
            return result;
        }

        public void Free(T obj)
        {
            obj.Release();
            _pool.TryEnqueue(obj);
        }
    }
}