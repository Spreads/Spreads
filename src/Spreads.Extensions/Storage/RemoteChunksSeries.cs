/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Collections;

namespace Spreads.Storage {


    // TODO (!!!) even though this "just works" on MySQL, the code was never reviewed after first working POC version

    /// <summary>
    /// Boilerplate to implement persistent series
    /// </summary>
    public class RemoteChunksSeries<K, V, TMapId> : Series<K, SortedMap<K, V>>, IOrderedMap<K, SortedMap<K, V>> {
        private readonly TMapId _mapId;
        private readonly IKeyComparer<K> _comparer;
        private readonly Func<TMapId, long, Task<SortedMap<long, int>>> _remoteKeysLoader;
        private readonly Func<TMapId, long, Task<SortedMap<K, V>>> _remoteLoader;
        private readonly Func<TMapId, long, SortedMap<K, V>, Task<long>> _remoteSaver;
        private readonly Func<TMapId, long, Lookup, Task<long>> _remoteRemover;
        private readonly Func<TMapId, long, Task<IDisposable>> _remoteLocker;
        private readonly long _localVersion;
        private IOrderedMap<long, int> _localKeysCache;
        private readonly Func<TMapId, long, SortedMap<K, V>> _localChunksCacheGet;
        private readonly Action<TMapId, long, SortedMap<K, V>> _localChunksCacheSet;
        //private Int64 _version;
        private readonly object _syncRoot = new object();

        public object SyncRoot
        {
            get { return _syncRoot; }
        }

        public long Count
        {
            get
            {
                return _localKeysCache.Count;
            }
        }

        public long Version
        {
            get { return _localKeysCache.Version; }
            set { _localKeysCache.Version = value; }
        }

        SortedMap<K, V> this[K key]
        {
            get
            {
                return (this as IReadOnlyOrderedMap<K, SortedMap<K, V>>)[key];
            }

            set
            {
                var v = value;
                var k = ToInt64(key);
                using (var locker = _remoteLocker(_mapId, k).Result) {
                    _remoteSaver(_mapId, k, v).Wait();
                    _localChunksCacheSet(_mapId, k, v);
                    _localKeysCache[k] = v.Version;
                }
            }
        }

        new public bool IsEmpty
        {
            get { return _localKeysCache.IsEmpty; }
        }

        bool IReadOnlyOrderedMap<K, SortedMap<K, V>>.IsEmpty
        {
            get { return _localKeysCache.IsEmpty; }
        }

        SortedMap<K, V> IOrderedMap<K, SortedMap<K, V>>.this[K key]
        {
            get
            {
                return (this as IReadOnlyOrderedMap<K, SortedMap<K, V>>)[key];
            }

            set
            {
                var v = value;
                var k = ToInt64(key);
                using (var locker = _remoteLocker(_mapId, k).Result) {
                    _remoteSaver(_mapId, k, v).Wait();
                    _localChunksCacheSet(_mapId, k, v);
                    _localKeysCache[k] = v.Version;
                }
            }
        }

        bool ISeries<K, SortedMap<K, V>>.IsIndexed
        {
            get
            {
                return false;
            }
        }


        public RemoteChunksSeries(TMapId mapId,
            IKeyComparer<K> comparer,
            // mapId, current map version => map with chunk keys and versions
            Func<TMapId, long, Task<SortedMap<long, int>>> remoteKeysLoader,
            // mapId, chunkKey => deserialied chunk
            Func<TMapId, long, Task<SortedMap<K, V>>> remoteLoader,
            // mapId, chunkKey, deserialied chunk => whole map version
            Func<TMapId, long, SortedMap<K, V>, Task<long>> remoteSaver, // TODO corresponding updates
                                                                         // mapId, chunkKey, direction => whole map version
            Func<TMapId, long, Lookup, Task<long>> remoteRemover, // TODO corresponding updates
                                                                  // mapId, chunkKey => lock releaser
            Func<TMapId, long, Task<IDisposable>> remoteLocker,
            long localVersion,
            // chunk key -> chunk version
            IOrderedMap<long, int> localKeysCache,
            // chunk key -> deserialized chunk version
            Func<TMapId, long, SortedMap<K, V>> localChunksCacheGet,
            Action<TMapId, long, SortedMap<K, V>> localChunksCacheSet) {
            _mapId = mapId;
            //_prefix = "RemoteInt64Map_" + _mapId.ToString("D");
            _comparer = comparer;
            _remoteKeysLoader = remoteKeysLoader;
            _remoteLoader = remoteLoader;
            _remoteSaver = remoteSaver;
            _remoteRemover = remoteRemover;
            _remoteLocker = remoteLocker;
            _localVersion = localVersion;
            _localKeysCache = localKeysCache;
            _localChunksCacheGet = localChunksCacheGet;
            _localChunksCacheSet = localChunksCacheSet;


            // TODO constructor must just start synch process on local caches, Series must work with updating source normally
            // TODO don't wait on it
            UpdateLocalKeysCacheAsync().Wait();

            // TODO! invalidate _localChunksCache, delete all existing keys that are not in renewedKeys
            // and when versions differ

        }


        private long ToInt64(K key) {
            return _comparer.Diff(key, default(K));
        }

        private K FromInt64(long value) {
            return _comparer.Add(default(K), value);
        }


        private async Task UpdateLocalKeysCacheAsync() {
            // TODO update as a task and work with _localKeysCache normally assuming it could be 
            // all keys that are modified after this version
            var renewedKeys = await _remoteKeysLoader(_mapId, _localVersion);
            // TODO int to Int64 for SM version
            //_version = renewedKeys.Version;
            // rewrite all local keys cache with new keys
            // TODO use append
            _localKeysCache = renewedKeys; //.Append(renewedKeys, AppendOption.DropOldOverlap);
            return;
        }

        private async Task<SortedMap<K, V>> LoadChunkAsync(long chunkKey) {
            var cached = _localChunksCacheGet(_mapId, chunkKey);
            if (cached != null) {
                return cached;
            }
            using (var locker = await _remoteLocker(_mapId, chunkKey)) {
                return await _remoteLoader(_mapId, chunkKey);
            }
        }

        public override ICursor<K, SortedMap<K, V>> GetCursor() {
            return new RemoteCursor(this);
        }

        public void Add(K k, SortedMap<K, V> v) {
            var chunkKey = ToInt64(k);
            lock (_syncRoot) {
                using (var locker = _remoteLocker(_mapId, chunkKey).Result) {

                }
            }

        }

        public void AddLast(K k, SortedMap<K, V> v) {
            throw new NotImplementedException();
        }

        public void AddFirst(K k, SortedMap<K, V> v) {
            throw new NotImplementedException();
        }

        public bool Remove(K k) {
            return RemoveMany(k, Lookup.EQ);
        }

        public bool RemoveLast(out KeyValuePair<K, SortedMap<K, V>> value) {
            value = this.Last;
            return RemoveMany(value.Key, Lookup.EQ);
        }

        public bool RemoveFirst(out KeyValuePair<K, SortedMap<K, V>> value) {
            value = this.First;
            return RemoveMany(value.Key, Lookup.EQ);
        }

        public bool RemoveMany(K k, Lookup direction) {
            var intKey = ToInt64(k);
            var r1 = _localKeysCache.RemoveMany(intKey, direction);
            return r1 && _remoteRemover(_mapId, intKey, direction).Result > 0;
        }

        public int Append(IReadOnlyOrderedMap<K, SortedMap<K, V>> appendMap, AppendOption option) {
            throw new NotImplementedException();
        }

        public void Complete() {
            throw new NotImplementedException();
        }

        class RemoteCursor : ICursor<K, SortedMap<K, V>> {
            private readonly RemoteChunksSeries<K, V, TMapId> _source;
            private ICursor<long, int> _keysCursor;
            private bool _isReset = true;

            public RemoteCursor(RemoteChunksSeries<K, V, TMapId> source) {
                _source = source;
                _keysCursor = source._localKeysCache.GetCursor();
            }

            public IComparer<K> Comparer
            {
                get
                {
                    return _source._comparer;
                }
            }

            public KeyValuePair<K, SortedMap<K, V>> Current
            {
                get
                {
                    return new KeyValuePair<K, SortedMap<K, V>>(this.CurrentKey, this.CurrentValue);
                }
            }

            public IReadOnlyOrderedMap<K, SortedMap<K, V>> CurrentBatch
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public K CurrentKey
            {
                get
                {
                    Debug.Assert(!_isReset, "Should not access current key of reset cursor");

                    return _isReset ? default(K) : _source.FromInt64(_keysCursor.CurrentKey);
                }
            }

            public SortedMap<K, V> CurrentValue
            {
                get
                {
                    Debug.Assert(!_isReset, "Should not access current value of reset cursor");
                    return _isReset ? null : _source.LoadChunkAsync(_keysCursor.CurrentKey).Result;
                }
            }

            public bool IsContinuous
            {
                get
                {
                    return false;
                }
            }

            public ISeries<K, SortedMap<K, V>> Source
            {
                get
                {
                    return _source;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return this.Current;
                }
            }

            public ICursor<K, SortedMap<K, V>> Clone() {
                return new RemoteCursor(_source);
            }

            public void Dispose() {
                _keysCursor.Dispose();
            }

            public bool MoveAt(K index, Lookup direction) {
                var moved = _keysCursor.MoveAt(_source.ToInt64(index), direction);
                if (moved) {
                    _isReset = false;
                } else {
                    _isReset = true;
                }
                return moved;
            }

            public bool MoveFirst() {
                var moved = _keysCursor.MoveFirst();
                if (moved) {
                    _isReset = false;
                } else {
                    _isReset = true;
                }
                return moved;
            }

            public bool MoveLast() {
                var moved = _keysCursor.MoveLast();
                if (moved) {
                    _isReset = false;
                } else {
                    _isReset = true;
                }
                return moved;
            }

            public bool MoveNext() {
                var moved = _keysCursor.MoveNext();
                if (moved) {
                    _isReset = false;
                }
                return moved;
            }

            public async Task<bool> MoveNext(CancellationToken cancellationToken) {
                var moved = await _keysCursor.MoveNext(cancellationToken);
                if (moved) {
                    _isReset = false;
                }
                return moved;
            }

            public async Task<bool> MoveNextBatch(CancellationToken cancellationToken) {
                var moved = await _keysCursor.MoveNextBatch(cancellationToken);
                if (moved) {
                    _isReset = false;
                }
                return moved;
            }

            public bool MovePrevious() {
                var moved = _keysCursor.MovePrevious();
                if (moved) {
                    _isReset = false;
                }
                return moved;
            }

            public void Reset() {
                _isReset = true;
                _keysCursor.Reset();
            }

            public bool TryGetValue(K key, out SortedMap<K, V> value) {
                value = default(SortedMap<K, V>);
                int version;
                if (_keysCursor.TryGetValue(_source.ToInt64(key), out version)) {
                    value = _source.LoadChunkAsync(_keysCursor.CurrentKey).Result;
                    return true;
                }
                return false;
            }
        }



        internal class LazyValue : IDisposable {
            //internal static MemoryCache Cache = MemoryCache.Default; // new MemoryCache("MySQLDateTimeMap");
            private long _key;
            private RemoteChunksSeries<K, V, TMapId> _source;
            private WeakReference<SortedMap<K, V>> _wr = new WeakReference<SortedMap<K, V>>(null);
            private static CacheItemPolicy _policy;
            private int lastSavedVersion = 0;

            static LazyValue() {
                _policy = new CacheItemPolicy();
                _policy.SlidingExpiration = TimeSpan.FromMinutes(1);
                //_policy.RemovedCallback = (CacheEntryRemovedCallback) ((args) =>
                //{
                //	if (args.RemovedReason != CacheEntryRemovedReason.Removed)
                //	{
                //		var item = args.CacheItem.Value as SortedMap<DateTime, V>;
                //		Debug.Assert(item != null);
                //                }
                //            });
            }

            public LazyValue(long key, RemoteChunksSeries<K, V, TMapId> source) {
                _key = key;
                _source = source;

            }

            public SortedMap<K, V> Value
            {
                get
                {
                    SortedMap<K, V> target;
                    if (_wr.TryGetTarget(out target)) {
                        // _wr must be alive while target is in cache, do not check cache here
                        return target;
                    } else {
                        var v = _source.LoadChunkAsync(_key).Result;
                        //Cache.Set(_key, v, _policy);
                        _wr.SetTarget(v);
                        lastSavedVersion = v.Version;
                        return v;
                    }
                }
                set
                {
                    var v = value;
                    if (true) // || lastSavedVersion != v.Version) 
                    {
                        var key = _source.FromInt64(_key);
                        _source[key] = v;
                        //Cache.Set(_key, v, _policy);
                        _wr.SetTarget(v);
                        lastSavedVersion = v.Version;
                    }
                }
            }

            ~LazyValue() {
                Dispose(false);
            }

            //Implement IDisposable.
            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing) {
                if (disposing) {
                    // Free other state (managed objects).
                }
                SortedMap<K, V> v;
                //var cached = Cache[_key];
                if (_wr.TryGetTarget(out v)) // || cached != null) {
                    {
                    //if (v == null) { v = cached as SortedMap<DateTime, V>; }
                    if (true) // || lastSavedVersion != v.Version)
                    {
                        var key = _source.FromInt64(_key);
                        _source[key] = v;
                        //Cache.Remove(_key);
                        _wr = null;
                        lastSavedVersion = 0;
                    }
                } else {
                    //Debug.Assert(!Cache.Contains(_key));
                }
            }
        }


    }


}

