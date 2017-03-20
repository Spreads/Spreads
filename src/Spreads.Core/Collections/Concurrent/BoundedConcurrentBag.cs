// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Object pool based on ConcurrentBag
    /// </summary>
    public class BoundedConcurrentBag<T>
    {
        private readonly int _capacity;
        private int _count;
        private readonly ConcurrentBag<T> _bag = new ConcurrentBag<T>();

        public BoundedConcurrentBag(int capacity = int.MaxValue)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public bool IsBounded => _capacity < int.MaxValue;
        public int Capacity => _capacity;
        public int Count => _count;

        public bool TryTake(out T result)
        {
            if (!_bag.TryTake(out result))
            {
                result = default(T);
                return false;
            }
            if (_capacity < int.MaxValue)
            {
                Interlocked.Decrement(ref _count);
            }
            return true;
        }

        public bool TryAdd(T obj)
        {
            if (_capacity < int.MaxValue)
            {
                var count = Volatile.Read(ref _count);
                if (count >= _capacity) return false;
                Interlocked.Increment(ref count);
            }
            _bag.Add(obj);
            return true;
        }
    }
}