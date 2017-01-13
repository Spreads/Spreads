// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Spreads {
    /// <summary>
    /// Projects values from source to destination and back
    /// </summary>
    public class ProjectValuesWrapper<K, Vsrc, Vdest> : Series<K, Vdest>, IPersistentSeries<K, Vdest> {
        private readonly IMutableSeries<K, Vsrc> _innerMap;
        private readonly Func<Vsrc, Vdest> _srcToDest;
        private readonly Func<Vdest, Vsrc> _destToSrc;


        public ProjectValuesWrapper(IMutableSeries<K, Vsrc> innerMap,
            Func<Vsrc, Vdest> srcToDest, Func<Vdest, Vsrc> destToSrc) {
            _innerMap = innerMap;
            _srcToDest = srcToDest;
            _destToSrc = destToSrc;
        }


        public Vdest this[K key]
        {
            get
            {
                return _srcToDest(_innerMap[key]);
            }

            set
            {
                _innerMap[key] = _destToSrc(value);
            }
        }


        public void Add(K k, Vdest v) {
            _innerMap.Add(k, _destToSrc(v));

        }

        public void AddFirst(K k, Vdest v) {
            _innerMap.AddFirst(k, _destToSrc(v));
        }

        public void AddLast(K k, Vdest v) {
            _innerMap.AddLast(k, _destToSrc(v));
        }

        public int Append(IReadOnlySeries<K, Vdest> appendMap, AppendOption option) {
            var count = _innerMap.Append(appendMap.Map(_destToSrc), option);
            return count;
        }

        public bool Remove(K k) {
            var res = _innerMap.Remove(k);
            return res;
        }

        public bool RemoveFirst(out KeyValuePair<K, Vdest> value) {
            KeyValuePair<K, Vsrc> srcVal;
            var res = _innerMap.RemoveFirst(out srcVal);
            if (res) {
                value = new KeyValuePair<K, Vdest>(srcVal.Key, _srcToDest(srcVal.Value));
            } else {
                value = default(KeyValuePair<K, Vdest>);
            }
            return res;
        }

        public bool RemoveLast(out KeyValuePair<K, Vdest> value) {
            KeyValuePair<K, Vsrc> srcVal;
            var res = _innerMap.RemoveLast(out srcVal);
            if (res) {
                value = new KeyValuePair<K, Vdest>(srcVal.Key, _srcToDest(srcVal.Value));
            } else {
                value = default(KeyValuePair<K, Vdest>);
            }
            return res;
        }

        public bool RemoveMany(K k, Lookup direction) {
            var res = _innerMap.RemoveMany(k, direction);
            return res;
        }


        //////////////////////////////////// READ METHODS BELOW  ////////////////////////////////////////////


        Vdest IReadOnlySeries<K, Vdest>.this[K key] => _srcToDest(_innerMap[key]);

        public IComparer<K> Comparer => _innerMap.Comparer;

        public long Version
        {
            get { return _innerMap.Version; }
            set { _innerMap.Version = value; }
        }

        public long Count => _innerMap.Count;

        public KeyValuePair<K, Vdest> First
        {
            get
            {
                var srcFirst = _innerMap.First;
                return new KeyValuePair<K, Vdest>(srcFirst.Key, _srcToDest(srcFirst.Value));
            }
        }

        public KeyValuePair<K, Vdest> Last
        {
            get
            {
                var srcFirst = _innerMap.Last;
                return new KeyValuePair<K, Vdest>(srcFirst.Key, _srcToDest(srcFirst.Value));
            }
        }

        public string Id
        {
            get
            {
                var pom = _innerMap as IPersistentSeries<K, Vsrc>;
                return pom != null ? pom.Id : "";
            }
        }

        public bool IsEmpty => _innerMap.IsEmpty;

        public bool IsIndexed => _innerMap.IsIndexed;

        public bool IsReadOnly => _innerMap.IsReadOnly;
        public void Complete() => _innerMap.Complete();

        public IEnumerable<K> Keys => _innerMap.Keys;

        public object SyncRoot => _innerMap.SyncRoot;

        public IEnumerable<Vdest> Values => _innerMap.Values.Select(_srcToDest);

        public void Dispose() {
            var pom = _innerMap as IPersistentSeries<K, Vsrc>;
            pom?.Dispose();
        }

        public void Flush() {
            var pom = _innerMap as IPersistentSeries<K, Vsrc>;
            pom?.Flush();
        }

        public Vdest GetAt(int idx) {
            return _srcToDest(_innerMap.GetAt(idx));
        }

        public override ICursor<K, Vdest> GetCursor() {
            return new BatchMapValuesCursor<K, Vsrc, Vdest>(_innerMap.GetCursor, _srcToDest);
        }

        public IAsyncEnumerator<KeyValuePair<K, Vdest>> GetEnumerator() {
            return (_innerMap as IAsyncEnumerable<KeyValuePair<K, Vdest>>).GetEnumerator();
        }

        public bool TryFind(K key, Lookup direction, out KeyValuePair<K, Vdest> value) {
            KeyValuePair<K, Vsrc> srcVal;
            var res = _innerMap.TryFind(key, direction, out srcVal);
            if (res) {
                value = new KeyValuePair<K, Vdest>(srcVal.Key, _srcToDest(srcVal.Value));
            } else {
                value = default(KeyValuePair<K, Vdest>);
            }
            return res;
        }

        public bool TryGetFirst(out KeyValuePair<K, Vdest> value) {
            KeyValuePair<K, Vsrc> srcVal;
            var res = _innerMap.TryGetFirst(out srcVal);
            if (res) {
                value = new KeyValuePair<K, Vdest>(srcVal.Key, _srcToDest(srcVal.Value));
            } else {
                value = default(KeyValuePair<K, Vdest>);
            }
            return res;
        }

        public bool TryGetLast(out KeyValuePair<K, Vdest> value) {
            KeyValuePair<K, Vsrc> srcVal;
            var res = _innerMap.TryGetLast(out srcVal);
            if (res) {
                value = new KeyValuePair<K, Vdest>(srcVal.Key, _srcToDest(srcVal.Value));
            } else {
                value = default(KeyValuePair<K, Vdest>);
            }
            return res;
        }

        public bool TryGetValue(K key, out Vdest value) {
            Vsrc srcVal;
            var res = _innerMap.TryGetValue(key, out srcVal);
            if (res) {
                value = _srcToDest(srcVal);
            } else {
                value = default(Vdest);
            }
            return res;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetCursor();
        }

        IEnumerator<KeyValuePair<K, Vdest>> IEnumerable<KeyValuePair<K, Vdest>>.GetEnumerator() {
            return GetCursor();
        }

    }


}
