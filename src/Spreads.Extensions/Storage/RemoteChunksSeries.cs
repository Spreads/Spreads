/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Serialization;

namespace Spreads.Storage {


    // TODO disposable, release lock

    /// <summary>
    /// Boilerplate to implement persistent series
    /// </summary>
    public class RemoteChunksSeries<K, V> : IPersistentOrderedMap<K, SortedMap<K, V>> { // Series<K, SortedMap<K, V>>,

        static RemoteChunksSeries()
        {
            // when used as a generic type argument, static ctor is not called
            SortedMap<K, V>.Init();
        }
        public delegate void ChunkSaveHandler(SeriesChunk chunk);

        private readonly long _mapId;
        private readonly IKeyComparer<K> _comparer;

        private KeyValuePair<K, SortedMap<K, V>> _lastAccessedElement;

        private readonly Func<long, long, Task<SortedMap<long, SeriesChunk>>> _remoteKeysLoader;

        private readonly Func<long, long, Task<SeriesChunk>> _remoteLoader;
        private readonly Func<SeriesChunk, Task<long>> _remoteSaver;

        private readonly Func<long, long, long, Lookup, Task<long>> _remoteRemover;
        private bool _readOnly;

        internal IOrderedMap<long, LazyValue> _chunksCache;

        private readonly object _syncRoot = new object();

        public object SyncRoot => _syncRoot;

        public long Count => _chunksCache.Count;

        public long Version
        {
            get { return _chunksCache.Version; }
            set { _chunksCache.Version = value; }
        }

        public bool IsEmpty => _chunksCache.IsEmpty;


        bool ISeries<K, SortedMap<K, V>>.IsIndexed
        {
            get
            {
                return false;
            }
        }

        public KeyValuePair<K, SortedMap<K, V>> First
        {
            get
            {
                lock (_syncRoot) {
                    return new KeyValuePair<K, SortedMap<K, V>>(FromInt64(_chunksCache.First.Key), _chunksCache.First.Value.Value);
                }
            }
        }

        public KeyValuePair<K, SortedMap<K, V>> Last
        {
            get
            {
                lock (_syncRoot) {
                    return new KeyValuePair<K, SortedMap<K, V>>(FromInt64(_chunksCache.Last.Key),
                        _chunksCache.Last.Value.Value);
                }
            }
        }

        public IEnumerable<K> Keys
        {
            get
            {
                lock (_syncRoot) {
                    return _chunksCache.Keys.Select(FromInt64);
                }
            }
        }

        public IEnumerable<SortedMap<K, V>> Values
        {
            get
            {
                lock (_syncRoot) {
                    return _chunksCache.Values.Select(lv => lv.Value);
                }
            }
        }

        public IComparer<K> Comparer
        {
            get { return _comparer; }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public RemoteChunksSeries(long mapId,
            IKeyComparer<K> comparer,
            // mapId, current map version => map with chunk keys and versions
            Func<long, long, Task<SortedMap<long, SeriesChunk>>> remoteKeysLoader,
            // mapId, chunkKey => deserialied chunk
            Func<long, long, Task<SeriesChunk>> remoteLoader,
            // mapId, chunkKey, deserialied chunk => whole map version
            Func<SeriesChunk, Task<long>> remoteSaver, // TODO corresponding updates
                                                       // mapId, chunkKey, direction => whole map version
            Func<long, long, long, Lookup, Task<long>> remoteRemover,
            long version,
            bool readOnly = false) {
            _mapId = mapId;
            _comparer = comparer;
            _remoteKeysLoader = remoteKeysLoader;
            _remoteLoader = remoteLoader;
            _remoteSaver = remoteSaver;
            _remoteRemover = remoteRemover;
            _readOnly = readOnly;

            var renewedKeys = _remoteKeysLoader(_mapId, 0).Result;
            var sm = renewedKeys.Map((k, ch) => new LazyValue(k, ch.Count, ch.Version, this)).ToSortedMap();
            sm.IsSynchronized = true;
            _chunksCache = sm;
            var maxChunkVersion = _chunksCache.Values.Max(x => x.ChunkVersion);
            if (maxChunkVersion != version) Trace.TraceWarning("Inconsistent series version in chunks and series definition.");
            _chunksCache.Version = Math.Max(version, maxChunkVersion);
        }


        internal long ToInt64(K key) {
            return _comparer.Diff(key, default(K));
        }

        internal K FromInt64(long value) {
            return _comparer.Add(default(K), value);
        }


        public SortedMap<K, V> this[K key]
        {
            get
            {
                lock (_syncRoot) {
                    var value = _chunksCache[ToInt64(key)].Value;
                    _lastAccessedElement = new KeyValuePair<K, SortedMap<K, V>>(key, value);
                    return value;
                }
            }

            set
            {
                lock (_syncRoot)
                {
                    MemoryStream stream;
                    var size = BinarySerializer.SizeOf(value, out stream);
                    // TODO pooled buffer
                    var buffer = new byte[size]; // ArrayPool<byte>.Shared.Rent(size);
                    BinarySerializer.Write(value, buffer, 0, stream);
                    var k = ToInt64(key);
                    var lv = new LazyValue(k, value.Count, _chunksCache.Version, this);
                    lv.Value = value;
                    // this line increments version, must go before _remoteSaver
                    _chunksCache[k] = lv;
                    if (!_readOnly) {
                        _remoteSaver(new SeriesChunk {
                            Id = _mapId,
                            ChunkKey = k,
                            Count = value.Count,
                            Version = _chunksCache.Version,
                            ChunkValue = buffer
                        }).Wait();
                    }
                    _lastAccessedElement = new KeyValuePair<K, SortedMap<K, V>>(key, value);
                }
            }
        }

        private async Task<SeriesChunk> LoadChunkAsync(long chunkKey) {
            return await _remoteLoader(_mapId, chunkKey);
        }

        public ICursor<K, SortedMap<K, V>> GetCursor() {
            lock (_syncRoot) {
                return new RemoteCursor(this);
            }
        }

        public void Add(K k, SortedMap<K, V> v) {
            throw new NotImplementedException();
        }

        public void AddLast(K k, SortedMap<K, V> v) {
            throw new NotImplementedException();
        }

        public void AddFirst(K k, SortedMap<K, V> v) {
            throw new NotImplementedException();
        }

        public bool Remove(K k) {
            lock (_syncRoot) {
                return RemoveMany(k, Lookup.EQ);
            }
        }

        public bool RemoveLast(out KeyValuePair<K, SortedMap<K, V>> value) {
            lock (_syncRoot) {
                value = this.Last;
                return RemoveMany(value.Key, Lookup.EQ);
            }
        }

        public bool RemoveFirst(out KeyValuePair<K, SortedMap<K, V>> value) {
            lock (_syncRoot) {
                value = this.First;
                return RemoveMany(value.Key, Lookup.EQ);
            }
        }

        public bool RemoveMany(K k, Lookup direction) {
            lock (_syncRoot) {
                var intKey = ToInt64(k);
                var r1 = _chunksCache.RemoveMany(intKey, direction);
                return r1 && (_readOnly || _remoteRemover(_mapId, intKey, _chunksCache.Version, direction).Result > 0);
            }
        }

        public int Append(IReadOnlyOrderedMap<K, SortedMap<K, V>> appendMap, AppendOption option) {
            throw new NotImplementedException();
        }

        public void Complete() {
            throw new NotSupportedException();
        }

        public SortedMap<K, V> GetAt(int idx) {
            lock (_syncRoot) {
                return this.Skip(idx).FirstOrDefault().Value;
            }
        }

        public bool TryFind(K key, Lookup direction, out KeyValuePair<K, SortedMap<K, V>> value) {
            lock (_syncRoot) {
                value = default(KeyValuePair<K, SortedMap<K, V>>);
                KeyValuePair<long, LazyValue> tmp;
                if (!_chunksCache.TryFind(ToInt64(key), direction, out tmp)) return false;
                value = new KeyValuePair<K, SortedMap<K, V>>(FromInt64(tmp.Key), tmp.Value.Value);
                _lastAccessedElement = value;
                return true;
            }
        }

        public bool TryGetFirst(out KeyValuePair<K, SortedMap<K, V>> value) {
            throw new NotImplementedException();
        }

        public bool TryGetLast(out KeyValuePair<K, SortedMap<K, V>> value) {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, out SortedMap<K, V> value) {
            lock (_syncRoot) {
                KeyValuePair<K, SortedMap<K, V>> tmp;
                if (TryFind(key, Lookup.EQ, out tmp)) {
                    value = tmp.Value;
                    _lastAccessedElement = new KeyValuePair<K, SortedMap<K, V>>(key, value);
                    return true;
                }
                value = null;
                return false;
            }
        }

        public IDisposable Subscribe(IObserver<KeyValuePair<K, SortedMap<K, V>>> observer) {
            throw new NotImplementedException();
        }

        public IAsyncEnumerator<KeyValuePair<K, SortedMap<K, V>>> GetEnumerator() {
            return new RemoteCursor(this);
        }

        IEnumerator<KeyValuePair<K, SortedMap<K, V>>> IEnumerable<KeyValuePair<K, SortedMap<K, V>>>.GetEnumerator() {
            return new RemoteCursor(this);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new RemoteCursor(this);
        }

        class RemoteCursor : ICursor<K, SortedMap<K, V>> {
            private readonly RemoteChunksSeries<K, V> _source;
            private ICursor<long, SortedMap<K, V>> _keysCursor;
            private bool _isReset = true;

            public RemoteCursor(RemoteChunksSeries<K, V> source) {
                _source = source;
                _keysCursor = source._chunksCache.Map(lv => lv.Value).GetCursor();

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
                    lock (_source._syncRoot) {
                        return new KeyValuePair<K, SortedMap<K, V>>(this.CurrentKey, this.CurrentValue);
                    }
                }
            }

            public IReadOnlyOrderedMap<K, SortedMap<K, V>> CurrentBatch
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public K CurrentKey
            {
                get
                {
                    lock (_source._syncRoot) {
                        Debug.Assert(!_isReset, "Should not access current chunkKey of reset cursor");
                        return _isReset ? default(K) : _source.FromInt64(_keysCursor.CurrentKey);
                    }
                }
            }

            public SortedMap<K, V> CurrentValue
            {
                get
                {
                    lock (_source._syncRoot) {
                        Debug.Assert(!_isReset, "Should not access current value of reset cursor");
                        return _isReset ? null : _keysCursor.CurrentValue;
                    }
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
                lock (_source._syncRoot) {
                    var moved = _keysCursor.MoveAt(_source.ToInt64(index), direction);
                    if (moved) {
                        _isReset = false;
                    } else {
                        _isReset = true;
                    }
                    return moved;
                }
            }

            public bool MoveFirst() {
                lock (_source._syncRoot) {
                    try {
                        var moved = _keysCursor.MoveFirst();
                        if (moved) {
                            _isReset = false;
                        } else {
                            _isReset = true;
                        }
                        return moved;
                    } catch {
                        return false;
                    }
                }
            }

            public bool MoveLast() {
                lock (_source._syncRoot) {
                    var moved = _keysCursor.MoveLast();
                    if (moved) {
                        _isReset = false;
                    } else {
                        _isReset = true;
                    }
                    return moved;
                }
            }

            public bool MoveNext() {
                lock (_source._syncRoot) {
                    var moved = _keysCursor.MoveNext();
                    if (moved) {
                        _isReset = false;
                    }
                    return moved;
                }
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
                lock (_source._syncRoot) {
                    var moved = _keysCursor.MovePrevious();
                    if (moved) {
                        _isReset = false;
                    }
                    return moved;
                }
            }

            public void Reset() {
                _isReset = true;
                _keysCursor.Reset();
            }

            public bool TryGetValue(K key, out SortedMap<K, V> value) {
                lock (_source._syncRoot) {
                    if (_keysCursor.TryGetValue(_source.ToInt64(key), out value)) {
                        return true;
                    }
                    return false;
                }
            }
        }


        internal struct LazyValue {
            private readonly long _chunkKey;
            private long _chunkSize;
            private long _chunkVersion;
            private readonly RemoteChunksSeries<K, V> _source;
            private readonly WeakReference<SortedMap<K, V>> _wr;


            public LazyValue(long chunkKey, long chunkSize, long chunkVersion, RemoteChunksSeries<K, V> source) {
                _chunkKey = chunkKey;
                _chunkSize = chunkSize;
                _chunkVersion = chunkVersion;
                _source = source;
                _wr = new WeakReference<SortedMap<K, V>>(null);
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
                        // TODO ... cache update
                        var chunkRow = _source.LoadChunkAsync(_chunkKey).Result;
                        if (chunkRow == null) {
                            throw new ApplicationException("chunkRow is null");
                            return SortedMap<K, V>.Empty;
                        }
                        _chunkSize = chunkRow.Count;
                        _chunkVersion = chunkRow.Version;
                        BinarySerializer.Read<SortedMap<K, V>>(chunkRow.ChunkValue, ref target);
                        _wr.SetTarget(target);
                        return target;
                    }
                }
                // NB use this method only when 
                internal set { _wr.SetTarget(value); }
            }

            public long ChunkKey => _chunkKey;

            public long ChunkSize => _chunkSize;

            public long ChunkVersion => _chunkVersion;
        }

        public void Dispose() {
            this.Flush();
        }

        public void Flush() {
            // this is what SCM Flush did before
            if (_lastAccessedElement.Value != null) {
                MemoryStream stream;
                var size = BinarySerializer.SizeOf(_lastAccessedElement.Value, out stream);
                // TODO pooled buffer
                var buffer = new byte[size]; // ArrayPool<byte>.Shared.Rent(size);
                BinarySerializer.Write(_lastAccessedElement.Value, buffer, 0, stream);
                var k = ToInt64(_lastAccessedElement.Key);
                var lv = new LazyValue(k, _lastAccessedElement.Value.Count, _chunksCache.Version, this);
                // this line increments version, must go before _remoteSaver
                if (!_readOnly) {
                    _remoteSaver(new SeriesChunk {
                        Id = _mapId,
                        ChunkKey = k,
                        Count = _lastAccessedElement.Value.Count,
                        Version = _chunksCache.Version,
                        ChunkValue = buffer
                    }).Wait();
                }
            }
            _lastAccessedElement = default(KeyValuePair<K, SortedMap<K, V>>);
        }

        public string Id { get; }

        internal bool ReadOnly
        {
            get { return _readOnly; }
            set { _readOnly = value; }
        }
    }


}

