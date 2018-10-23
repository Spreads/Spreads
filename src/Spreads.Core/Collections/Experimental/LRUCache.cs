using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Spreads.Collections.Experimental
{
    public interface WithId
    {
        long Id { get; }
    }

    [Obsolete("Delete this. Main idea is in LockedWeakDictionary.")]
    class LRUCache<T> where T : class, WithId
    {
        private int count;
        private T[] _items;
        private ConcurrentDictionary<long, T> _dictionary = new ConcurrentDictionary<long, T>();

        public LRUCache(int cacheSize = 37)
        {
            count = 37;
            _items = new T[count];
        }


        public T this[long key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var candidate = _items[key % count];
                if (candidate == null || candidate.Id != key)
                {
                    candidate = _dictionary[key];
                    _items[key % count] = candidate;
                }
                return candidate;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _items[key % count] = value;
                _dictionary[key] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T Find(long id)
        {
            var candidate = _items[id % count];
            if (candidate == null || candidate.Id != id)
            {
                return null;
            }

            return candidate;
        }
    }
}
