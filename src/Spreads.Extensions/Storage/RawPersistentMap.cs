// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spreads;

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
            throw new NotImplementedException();
        }

        public void AddLast(long key, RawPanelChunk value) {
            if (key <= _lastKey) {
                throw new Spreads.OutOfOrderKeyException<long>(_lastKey, key,
                    "RawPersistentMap.AddLast: New key is smaller or equal to the largest existing key");
            }
            Add(key, value);
        }

        public void AddFirst(long key, RawPanelChunk value) {
            if (key >= _firstKey) {
                throw new Spreads.OutOfOrderKeyException<long>(_lastKey, key,
                    "RawPersistentMap.AddLast: New key is larger or equal to the smallest existing key");
            }
            Add(key, value);
        }

        public bool Remove(long key) {
            return Provider.Remove(_panelId, _firstKey, Lookup.EQ);
        }

        public bool RemoveLast(out KeyValuePair<long, RawPanelChunk> kvp) {
            throw new NotImplementedException();
        }

        public bool RemoveFirst(out KeyValuePair<long, RawPanelChunk> kvp) {
            throw new NotImplementedException();
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

        public RawPanelChunk this[long key]
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        public void Flush() {
            throw new NotImplementedException();
        }

        public string Id { get; }

        internal StorageProvider Provider { get; }


        public class RawPersistentMapCursor : ICursor<long, RawPanelChunk> {
            private readonly RawPersistentMap _source;

            public RawPersistentMapCursor(RawPersistentMap source) {
                _source = source;
            }

            public Task<bool> MoveNext(CancellationToken cancellationToken) {
                throw new NotImplementedException();
            }

            public void Dispose() {
                throw new NotImplementedException();
            }

            public bool MoveNext() {
                throw new NotImplementedException();
            }

            public void Reset() {
                throw new NotImplementedException();
            }

            public KeyValuePair<long, RawPanelChunk> Current { get; }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveAt(long key, Lookup direction) {
                throw new NotImplementedException();
            }

            public bool MoveFirst() {
                throw new NotImplementedException();
            }

            public bool MoveLast() {
                throw new NotImplementedException();
            }

            public bool MovePrevious() {
                throw new NotImplementedException();
            }

            public Task<bool> MoveNextBatch(CancellationToken cancellationToken) {
                throw new NotImplementedException();
            }

            public ICursor<long, RawPanelChunk> Clone() {
                throw new NotImplementedException();
            }

            public bool TryGetValue(long key, out RawPanelChunk value) {
                throw new NotImplementedException();
            }

            public IComparer<long> Comparer { get; } = KeyComparer.GetDefault<long>();

            public long CurrentKey { get; }
            public RawPanelChunk CurrentValue { get; }

            public ISeries<long, RawPanelChunk> CurrentBatch { get; }

            public ISeries<long, RawPanelChunk> Source => _source;
            public bool IsContinuous => false;
        }
    }
}
