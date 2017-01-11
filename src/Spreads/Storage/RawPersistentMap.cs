// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Storage {

    public class RawPersistentMap : Series<long, RawPanelChunk>, IPersistentOrderedMap<long, RawPanelChunk> {
        private readonly int _panelId;
        private long _firstKey;
        private long _lastKey;
        // TODO add prefetch logic only after simple implementation works, must ensure that prefetching will improve performance

        public RawPersistentMap(StorageProvider provider, int panelId) {
            _panelId = panelId;
            Provider = provider;
            var range = Provider.GetRange(_panelId);
            _firstKey = range.Value1;
            _lastKey = range.Value2;
        }

        public void Add(long key, RawPanelChunk value) {
            if (value.ChunkKey != key) throw new InvalidOperationException("Key and ChunkKey must be equal");
            if (value.LastKey > _lastKey) _lastKey = value.LastKey;
            if (key < _firstKey) _firstKey = key;
            Provider.Add(value, false);
        }

        public void AddLast(long key, RawPanelChunk value) {
            if (key <= _lastKey) {
                throw new OutOfOrderKeyException<long>(_lastKey, key,
                    "RawPersistentMap.AddLast: New key is smaller or equal to the largest existing key");
            }
            Add(key, value);
        }

        public void AddFirst(long key, RawPanelChunk value) {
            if (key >= _firstKey) {
                throw new OutOfOrderKeyException<long>(_lastKey, key,
                    "RawPersistentMap.AddLast: New key is larger or equal to the smallest existing key");
            }
            Add(key, value);
        }

        public bool Remove(long key) {
            return Provider.Remove(_panelId, _firstKey, Lookup.EQ);
        }

        public bool RemoveLast(out KeyValuePair<long, RawPanelChunk> kvp)
        {
            kvp = this.Last;
            Trace.Assert(kvp.Key == _lastKey);
            _lastKey = this.Last.Key;
            return Remove(kvp.Key);
        }

        public bool RemoveFirst(out KeyValuePair<long, RawPanelChunk> kvp) {
            kvp = this.First;
            Trace.Assert(kvp.Key == _firstKey);
            _lastKey = this.First.Key;
            return Remove(kvp.Key);
        }

        public bool RemoveMany(long key, Lookup direction) {
            return Provider.Remove(_panelId, key, direction);
        }

        public int Append(IReadOnlyOrderedMap<long, RawPanelChunk> appendMap, AppendOption option) {
            throw new NotImplementedException();
        }

        public void Complete() {
            throw new NotImplementedException();
        }

        public long Count { get; }
        public long Version { get; set; }

        public new RawPanelChunk this[long key]
        {
            get
            {
                var value = new RawPanelChunk[1];
                if (1 == Provider.TryGetChunksAt(_panelId, key, Lookup.EQ, ref value)) {
                    return value[0];
                }
                throw new KeyNotFoundException();
            }
            set { Provider.Add(value, true); }
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        public void Flush() {
            Provider.Flush(_panelId);
        }

        public string Id { get; }

        internal StorageProvider Provider { get; }

        public class RawPersistentMapCursor : ICursor<long, RawPanelChunk> {
            private readonly RawPersistentMap _source;
            private readonly StorageProvider _provider;
            private int _state = -1;
            private RawPanelChunk[] _values = new RawPanelChunk[1];

            public RawPersistentMapCursor(RawPersistentMap source) {
                _source = source;
                _provider = _source.Provider;
            }

            public Task<bool> MoveNext(CancellationToken cancellationToken) {
                if (_state == -1) {
                    return Task.FromResult(this.MoveNext());
                }
                if (_provider.TryGetChunksAt(_source._panelId, CurrentKey, Lookup.GT, ref _values) > 0) {
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }

            public bool MoveNext() {
                if (_state == -1) {
                    return this.MoveFirst();
                }
                if (_provider.TryGetChunksAt(_source._panelId, CurrentKey, Lookup.GT, ref _values) > 0) {
                    return true;
                }
                return false;
            }

            public bool MoveAt(long key, Lookup direction) {
                if (_provider.TryGetChunksAt(_source._panelId, key, direction, ref _values) > 0) {
                    return true;
                }
                return false;
            }

            public bool MoveFirst() {
                if (_provider.TryGetChunksAt(_source._panelId, long.MinValue, Lookup.GE, ref _values) > 0) {
                    _state = 1;
                    return true;
                }
                return false;
            }

            public bool MoveLast() {
                if (_provider.TryGetChunksAt(_source._panelId, long.MaxValue, Lookup.LE, ref _values) > 0) {
                    _state = 1;
                    return true;
                }
                return false;
            }

            public bool MovePrevious() {
                if (_state == -1) {
                    return this.MoveLast();
                }
                if (_provider.TryGetChunksAt(_source._panelId, CurrentKey, Lookup.LT, ref _values) > 0) {
                    return true;
                }
                return false;
            }

            public KeyValuePair<long, RawPanelChunk> Current
                => new KeyValuePair<long, RawPanelChunk>(CurrentKey, CurrentValue);

            object IEnumerator.Current => Current;

            public Task<bool> MoveNextBatch(CancellationToken cancellationToken) {
                return Task.FromResult(false);
            }

            public ICursor<long, RawPanelChunk> Clone() {
                var newCursor = new RawPersistentMapCursor(_source);
                if (_state != -1) {
                    newCursor.MoveAt(CurrentKey, Lookup.EQ);
                }
                return newCursor;
            }

            public bool TryGetValue(long key, out RawPanelChunk value) {
                RawPanelChunk[] tmp = new RawPanelChunk[1];
                if (_provider.TryGetChunksAt(_source._panelId, key, Lookup.GT, ref _values) > 0) {
                    value = tmp[0];
                    return true;
                }
                value = null;
                return false;
            }

            public IComparer<long> Comparer { get; } = KeyComparer.GetDefault<long>();

            public long CurrentKey => _values[0].ChunkKey;
            public RawPanelChunk CurrentValue => _values[0];

            public ISeries<long, RawPanelChunk> CurrentBatch => null;

            public ISeries<long, RawPanelChunk> Source => _source;
            public bool IsContinuous => false;

            public void Dispose() {
                Reset();
            }

            public void Reset() {
                _state = -1;
                foreach (var rawPanelChunk in _values) {
                    rawPanelChunk?.Dispose();
                }
                Array.Clear(_values, 0, _values.Length);
            }
        }
    }
}
