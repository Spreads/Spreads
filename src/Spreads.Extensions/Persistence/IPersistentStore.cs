using System;
using System.Runtime.Caching;
using Spreads.Collections;

namespace Spreads.Persistence {

    public interface IPersistentStore {
        /// <summary>
        /// Get writable series that persist changes locally. Always returns a reference to the same object for each seriesId
        /// </summary>
        IPersistentOrderedMap<K, V> GetPersistentOrderedMap<K, V>(string seriesId);
    }


    /// <summary>
    /// In-memory implementation of IPersistentStore. Useful for testing and for small amounts of data 
    /// that could be easily requested from a remote data source.
    /// </summary>
    public class InMemoryPersistentStore : IPersistentStore
    {
        private readonly string _prefix;

        public InMemoryPersistentStore(string prefix = "")
        {
            _prefix = prefix;
        }

        /// <summary>
        /// In-memory sorted map doesn't need a special flush/dispose
        /// </summary>
        private class PersistentSortedMap<K, V> : SortedMap<K, V>, IPersistentOrderedMap<K, V>
        {
            private readonly MemoryCache _cache;
            private readonly string _cacheKey;

            public PersistentSortedMap(string seriesId, MemoryCache cache, string cacheKey)
            {
                _cache = cache;
                _cacheKey = cacheKey;
                Id = seriesId;
            }

            public void Flush() { }
            public string Id { get; }

            public void Dispose()
            {
                _cache.Remove(_cacheKey);
            }
        }
        // TODO what is a series is evicted?
        private readonly MemoryCache _cache = MemoryCache.Default;
        public IPersistentOrderedMap<K, V> GetPersistentOrderedMap<K, V>(string seriesId)
        {
            var key = "InMemoryPersistentStore:" + _prefix + ":" + seriesId;
            var exitsing = _cache[key] as IPersistentOrderedMap<K, V>;
            if (exitsing != null) return exitsing;
            exitsing = new PersistentSortedMap<K, V>(seriesId, _cache, key) {IsSynchronized = true};
            _cache.Add(new CacheItem(key, exitsing), new CacheItemPolicy() {SlidingExpiration = TimeSpan.FromHours(1)}); // [key] = exitsing;
            return exitsing;
        }
    }
}
