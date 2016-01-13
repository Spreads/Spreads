using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {
    public static class MemoryCache {
        private static System.Runtime.Caching.MemoryCache _cache = new System.Runtime.Caching.MemoryCache("Spreads.DefaultCache");
        // this is to avoid allocation of objects that will live several seconds, even if they are small they could be promoted and add needless GC overhead
        private static Dictionary<int, CacheItemPolicy> _policies = new Dictionary<int, CacheItemPolicy>();

        public static void Set(string key, object value, int seconds) {
            CacheItemPolicy policy;
            if (!_policies.TryGetValue(seconds, out policy)) {
                policy = new CacheItemPolicy {
                    RemovedCallback = RemovedCallback,
                    SlidingExpiration = TimeSpan.FromSeconds(seconds)
                };
                _policies.Add(seconds, policy);
            }
            _cache.Set(key, value, policy);
        }

        public static object Get(string key) {
            return _cache.Get(key);
        }

        public static object Remove(string key) {
            return _cache.Remove(key);
        }

        private static void RemovedCallback(CacheEntryRemovedArguments arg) {
            if (arg.RemovedReason == CacheEntryRemovedReason.Evicted) {
                var item = arg.CacheItem.Value as IDisposable;
                if (item != null) item.Dispose();
            }
        }
    }
}
