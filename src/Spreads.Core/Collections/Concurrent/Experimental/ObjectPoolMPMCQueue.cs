// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Collections.Concurrent.Experimental {
    /// <summary>
    /// Object pool based on MultipleProducerConsumerQueue
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [Obsolete("Not sure MPMC queue works correctly, sometimes it returns true but null object")]
    public class ObjectPoolMPMCQueue<T> : IObjectPool<T> where T : class, IPoolable<T>, new() {
        private readonly MultipleProducerConsumerQueue _pool;

        public ObjectPoolMPMCQueue(int capacity = int.MaxValue) {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            var capacity1 = Utils.BitUtil.FindNextPositivePowerOfTwo(capacity);
            _pool = new MultipleProducerConsumerQueue(capacity1);
        }

        public bool IsBounded => true;
        public int Capacity => _pool.Capacity;
        public int Count => _pool.Capacity;

        public T Allocate() {
            object result;
            if (!_pool.TryDequeue(out result) || result == null) {
                result = new T();
            }
            var asT = (T)(result);
            asT.Init();
            return asT;
        }

        public void Free(T obj) {
            obj.Release();
            _pool.TryEnqueue(obj);
        }
    }
}
