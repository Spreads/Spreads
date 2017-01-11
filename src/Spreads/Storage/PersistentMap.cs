// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Storage {

    public class PersistentMap<TKey, TValue> : Series<TKey, TValue>, IPersistentOrderedMap<TKey, TValue> {
        private readonly StorageProvider _provider;
        private readonly int _panelId;
        private readonly IKeyComparer<TKey> _comparer;
        private readonly RawPersistentMap _rawMap;

        public PersistentMap(StorageProvider provider, int panleId) {
            _provider = provider;
            _panelId = panleId;
            _rawMap = new RawPersistentMap(_provider, _panelId);
            _comparer = KeyComparer.GetDefault<TKey>() as IKeyComparer<TKey>;
            if (_comparer == null) throw new ArgumentException("Default TKey comparer must implement IKeyComparer<TKey> or TKey must be a know type");
        }

        internal long ToInt64(TKey key) {
            return _comparer.Diff(key, default(TKey));
        }

        internal TKey FromInt64(long key) {
            return _comparer.Add(default(TKey), key);
        }

        // TODO use variant layout. here we temporarily use existing BinarySerializer
        // to debug the implementation of storage, etc.
        internal PreservedMemory<byte> ToReservedMemory(TValue value) {
            MemoryStream tmp;
            var offset = 0;
            var size = BinarySerializer.SizeOf(value, out tmp);
            var memory = BufferPool.PreserveMemory(offset + size);
            BinarySerializer.Write(value, ref memory, (uint)offset, tmp);
            return memory;
        }

        internal TValue FromReservedMemory(PreservedMemory<byte> source) {
            var offset = 0;
            var value = default(TValue);
            BinarySerializer.Read<TValue>(source, (uint)offset, ref value);
            return value;
        }

        public void Add(TKey key, TValue value) {
            var longKey = ToInt64(key);
            var rawValue = ToReservedMemory(value);
            var rawPanelChunk = RawPanelChunk.Create();
            var rawColumnChunk = new RawColumnChunk(_panelId, 0, (long)longKey, (long)longKey, 0L, 1,
                default(PreservedMemory<byte>), rawValue);
            rawPanelChunk.Add(rawColumnChunk);
            _rawMap.Add(longKey, rawPanelChunk);
            rawPanelChunk.Dispose();
        }

        public void AddLast(TKey key, TValue value) {
            var longKey = ToInt64(key);
            throw new NotImplementedException();
        }

        public void AddFirst(TKey key, TValue value) {
            var longKey = ToInt64(key);
            throw new NotImplementedException();
        }

        public bool Remove(TKey key) {
            var longKey = ToInt64(key);
            throw new NotImplementedException();
        }

        public bool RemoveLast(out KeyValuePair<TKey, TValue> kvp) {
            throw new NotImplementedException();
        }

        public bool RemoveFirst(out KeyValuePair<TKey, TValue> kvp) {
            throw new NotImplementedException();
        }

        public bool RemoveMany(TKey key, Lookup direction) {
            var longKey = ToInt64(key);
            throw new NotImplementedException();
        }

        public int Append(IReadOnlyOrderedMap<TKey, TValue> appendMap, AppendOption option) {
            throw new NotImplementedException();
        }

        public void Complete() {
            throw new NotImplementedException();
        }

        public long Count { get; }
        public long Version { get; set; }

        public TValue this[TKey key]
        {
            get
            {
                var longKey = ToInt64(key);
                throw new NotImplementedException();
            }
            set
            {
                var longKey = ToInt64(key);
                throw new NotImplementedException();
            }
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        public void Flush() {
            throw new NotImplementedException();
        }

        public string Id { get; }

        public class PersistentMapCursor : ICursor<TKey, TValue> {

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

            public KeyValuePair<TKey, TValue> Current { get; }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveAt(TKey key, Lookup direction) {
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

            public ICursor<TKey, TValue> Clone() {
                throw new NotImplementedException();
            }

            public bool TryGetValue(TKey key, out TValue value) {
                throw new NotImplementedException();
            }

            public IComparer<TKey> Comparer { get; }
            public TKey CurrentKey { get; }
            public TValue CurrentValue { get; }
            public ISeries<TKey, TValue> CurrentBatch { get; }
            public ISeries<TKey, TValue> Source { get; }
            public bool IsContinuous { get; }
        }
    }
}
