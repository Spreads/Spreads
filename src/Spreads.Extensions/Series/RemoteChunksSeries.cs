using System;
using System.Collections;
using System.Collections.Generic;
using Spreads.Collections;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Runtime.Caching;

namespace Spreads {


	public class RemoteChunksSeries<K, V> : Series<K, SortedMap<K, V>>, IOrderedMap<K, SortedMap<K, V>> {
		private readonly Guid _mapId;
		private readonly IKeyComparer<K> _comparer;
		private readonly Func<Guid, Int64, Task<SortedMap<Int64, int>>> _remoteKeysLoader;
		private readonly Func<Guid, Int64, Task<SortedMap<K, V>>> _remoteLoader;
		private readonly Func<Guid, Int64, SortedMap<K, V>, Task<Int64>> _remoteSaver;
		private readonly Func<Guid, Int64, Lookup, Task<Int64>> _remoteRemover;
		private readonly Func<Guid, Int64, Task<IDisposable>> _remoteLocker;
		private readonly long _localVersion;
		private IOrderedMap<Int64, int> _localKeysCache;
		private readonly Func<Guid, Int64, SortedMap<K, V>> _localChunksCacheGet;
		private readonly Action<Guid, Int64, SortedMap<K, V>> _localChunksCacheSet;
		private Int64 _version;
		private readonly object _syncRoot = new object();

		public object SyncRoot {
			get { return _syncRoot; }
		}

		public long Count {
			get {
				return _localKeysCache.Count;
			}
		}

        SortedMap<K, V> this[K key] {
            get {
                return (this as IReadOnlyOrderedMap<K, SortedMap<K, V>>)[key];
            }

            set {
                var v = value;
                var k = ToInt64(key);
                using (var locker = _remoteLocker(_mapId, k).Result) {
                    _remoteSaver(_mapId, k, v).Wait();
                    _localChunksCacheSet(_mapId, k, v);
                    _localKeysCache[k] = v.Version;
                }
            }
        }

        SortedMap<K, V> IOrderedMap<K, SortedMap<K, V>>.this[K key] {
			get {
				return (this as IReadOnlyOrderedMap<K, SortedMap<K, V>>)[key];
			}

			set {
				var v = value;
				var k = ToInt64(key);
				using (var locker = _remoteLocker(_mapId, k).Result) {
					_remoteSaver(_mapId, k, v).Wait();
					_localChunksCacheSet(_mapId, k, v);
					_localKeysCache[k] = v.Version;
				}
			}
		}

		bool ISeries<K, SortedMap<K, V>>.IsIndexed {
			get {
				return false;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="env">Opened SDB environment</param>
		/// <param name="dbName">Name of a database where the map is stored</param>
		/// <param name="mapId">Map id must be 16 bytes (enough to pack series id and Guid, Md5 for everything else)</param>
		/// <param name="comparer">ISpreadsComparer for keys.PersistentSortedMap could be used for varsioned key-value storage, then mapId is the key and K is TimePeriod for versions.</param>
		/// <param name="writeAck"></param>
		public RemoteChunksSeries(Guid mapId,
			IKeyComparer<K> comparer,
			// mapId, current map version => map with chunk keys and versions
			Func<Guid, Int64, Task<SortedMap<Int64, int>>> remoteKeysLoader,
			// mapId, chunkKey => deserialied chunk
			Func<Guid, Int64, Task<SortedMap<K, V>>> remoteLoader,
			// mapId, chunkKey, deserialied chunk => whole map version
			Func<Guid, Int64, SortedMap<K, V>, Task<Int64>> remoteSaver, // TODO corresponding updates
																		 // mapId, chunkKey, direction => whole map version
			Func<Guid, Int64, Lookup, Task<Int64>> remoteRemover, // TODO corresponding updates
																  // mapId, chunkKey => lock releaser
			Func<Guid, Int64, Task<IDisposable>> remoteLocker,
			Int64 localVersion,
			// chunk key -> chunk version
			IOrderedMap<Int64, int> localKeysCache,
			// chunk key -> deserialized chunk version
			Func<Guid, Int64, SortedMap<K, V>> localChunksCacheGet,
			Action<Guid, Int64, SortedMap<K, V>> localChunksCacheSet) {
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


		private Int64 ToInt64(K key) {
			return _comparer.Diff(key, default(K));
		}

		private K FromInt64(Int64 value) {
			return _comparer.Add(default(K), value);
		}


		private async Task UpdateLocalKeysCacheAsync() {
			// TODO update as a task and work with _localKeysCache normally assuming it could be 
			// all keys that are modified after this version
			var renewedKeys = await _remoteKeysLoader(_mapId, _localVersion);
			// TODO int to Int64 for SM version
			_version = renewedKeys.Version;
			// rewrite all local keys cache with new keys
			// TODO use append
			_localKeysCache = renewedKeys; //.Append(renewedKeys, AppendOption.DropOldOverlap);
			return;
		}

		private async Task<SortedMap<K, V>> LoadChunkAsync(Int64 chunkKey) {
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

		class RemoteCursor : ICursor<K, SortedMap<K, V>> {
			private readonly RemoteChunksSeries<K, V> _source;
			private ICursor<long, int> _keysCursor;
			private bool _isReset = true;

			public RemoteCursor(RemoteChunksSeries<K, V> source) {
				_source = source;
				_keysCursor = source._localKeysCache.GetCursor();
			}

			public IComparer<K> Comparer {
				get {
					return _source._comparer;
				}
			}

			public KeyValuePair<K, SortedMap<K, V>> Current {
				get {
					return new KeyValuePair<K, SortedMap<K, V>>(this.CurrentKey, this.CurrentValue);
				}
			}

			public IReadOnlyOrderedMap<K, SortedMap<K, V>> CurrentBatch {
				get {
					throw new NotImplementedException();
				}
			}

			public K CurrentKey {
				get {
					Debug.Assert(!_isReset, "Should not access current key of reset cursor");

					return _isReset ? default(K) : _source.FromInt64(_keysCursor.CurrentKey);
				}
			}

			public SortedMap<K, V> CurrentValue {
				get {
					Debug.Assert(!_isReset, "Should not access current value of reset cursor");
					return _isReset ? null : _source.LoadChunkAsync(_keysCursor.CurrentKey).Result;
				}
			}

			public bool IsContinuous {
				get {
					return false;
				}
			}

			public ISeries<K, SortedMap<K, V>> Source {
				get {
					return _source;
				}
			}

			object IEnumerator.Current {
				get {
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
            private Int64 _key;
            private RemoteChunksSeries<K, V> _source;
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

            public LazyValue(Int64 key, RemoteChunksSeries<K, V> source) {
                _key = key;
                _source = source;

            }

            public SortedMap<K, V> Value {
                get {
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
                set {
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

