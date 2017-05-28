// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    ///
    /// </summary>
    public class BoundedConcurrentQueue<T>
    {
        private readonly int _capacity;
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();

        public BoundedConcurrentQueue(int capacity = int.MaxValue)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public bool IsBounded => _capacity < int.MaxValue;
        public int Capacity => _capacity;
        public int Count => _queue.Count;

        public bool TryDequeue(out T result)
        {
            return _queue.TryDequeue(out result);
        }

        public bool TryEnqueue(T obj)
        {
            // hit Count property only when capacity is set
            // count is approximate
            if (_capacity < int.MaxValue)
            {
                if (_queue.Count < _capacity)
                {
                    _queue.Enqueue(obj);
                    return true;
                }
            }
            else
            {
                _queue.Enqueue(obj);
                return true;
            }
            return false;
        }
    }
}