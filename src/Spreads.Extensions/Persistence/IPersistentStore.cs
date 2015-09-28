using System.Runtime.Caching;
using Spreads.Collections;

namespace Spreads.Persistence {

    public interface IPersistentStore {
        /// <summary>
        /// Get writable series that persist changes locally
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
            public PersistentSortedMap(string seriesId)
            {
                Id = seriesId;
            }

            public void Flush() { }
            public string Id { get; }
            public void Dispose() { }
        }

        private readonly MemoryCache _cache = MemoryCache.Default;
        public IPersistentOrderedMap<K, V> GetPersistentOrderedMap<K, V>(string seriesId)
        {
            var key = "InMemoryPersistentStore:" + _prefix + ":" + seriesId;
            var exitsing = _cache[key] as IPersistentOrderedMap<K, V>;
            if (exitsing != null) return exitsing;
            exitsing = new PersistentSortedMap<K, V>(seriesId) {IsSynchronized = true};
            _cache[key] = exitsing;
            return exitsing;
        }
    }
}
