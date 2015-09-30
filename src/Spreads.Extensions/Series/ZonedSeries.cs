using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spreads;


namespace Spreads {

    // Map is in UTC, we are converting from UTC on each read

    public class ZonedCursor<V> : ICursor<DateTime, V> //MapCursor<DateTime, V>,
    {
        private ICursor<DateTime, V> _cursor;
        private readonly string _timeZone;
        //public ZonedCursor(IReadOnlyOrderedMap<DateTime, V> map) : base(map)
        //{

        //}

        public ZonedCursor(ICursor<DateTime, V> cursor, string timeZone) {
            _cursor = cursor;
            _timeZone = timeZone;
        }

        public IComparer<DateTime> Comparer {
            get {
                return _cursor.Comparer;
            }
        }

        public KeyValuePair<DateTime, V> Current {
            get {
                return new KeyValuePair<DateTime, V>(_cursor.CurrentKey.ConvertToZoneWithUncpecifiedKind(_timeZone), _cursor.CurrentValue);
            }
        }

        public IReadOnlyOrderedMap<DateTime, V> CurrentBatch {
            get
            {
                //return new CursorSeries<DateTime, V>(() => new ZonedCursor(_cursor.Clone, _timeZone));
                throw new NotImplementedException();
            }
        }

        public DateTime CurrentKey {
            get {
                return _cursor.CurrentKey.ConvertToZoneWithUncpecifiedKind(_timeZone);
            }
        }

        public V CurrentValue {
            get {
                return _cursor.CurrentValue;
            }
        }

        public bool IsContinuous {
            get {
                return _cursor.IsContinuous;
            }
        }

        public ISeries<DateTime, V> Source {
            get {
                return new ZonedSeries<V>(_cursor.Source as IOrderedMap<DateTime, V>, _timeZone);
            }
        }

        object IEnumerator.Current {
            get {
                return this.Current;
            }
        }

        public ICursor<DateTime, V> Clone() {
            return new ZonedCursor<V>(_cursor.Clone(), _timeZone);
        }

        public void Dispose() {
            _cursor.Dispose();
        }

        public bool MoveAt(DateTime key, Lookup direction) {
            return _cursor.MoveAt(key.ConvertToUtcWithUncpecifiedKind(_timeZone), direction);
        }

        public bool MoveFirst() {
            return _cursor.MoveFirst();
        }

        public bool MoveLast() {
            return _cursor.MoveLast();
        }

        public bool MoveNext() {
            return _cursor.MoveNext();
        }

        public Task<bool> MoveNext(CancellationToken cancellationToken) {
            return _cursor.MoveNext(cancellationToken);
        }

        public Task<bool> MoveNextBatch(CancellationToken cancellationToken) {
            return _cursor.MoveNextBatch(cancellationToken);
        }

        public bool MovePrevious() {
            return _cursor.MovePrevious();
        }

        public void Reset() {
            _cursor.Reset();
        }

        public bool TryGetValue(DateTime key, out V value) {
            return _cursor.TryGetValue(key.ConvertToUtcWithUncpecifiedKind(_timeZone), out value);
        }

    }



    /// <summary>
    /// All keys are transformed into UTC on writes and back on reads
    /// </summary>
    /// <typeparam name="V"></typeparam>
    public class ZonedSeries<V> : Series<DateTime, V>, IPersistentOrderedMap<DateTime, V> {
        private readonly IOrderedMap<DateTime, V> _map;
        private readonly string _tz;

        public ZonedSeries(IOrderedMap<DateTime, V> map, string originalTimeZone) {
            _map = map;
            _tz = originalTimeZone;
        }

        public override ICursor<DateTime, V> GetCursor() {
            return new ZonedCursor<V>(_map.GetCursor(), _tz);
        }

        public V this[DateTime key] {
            get {
                return _map[key.ConvertToUtcWithUncpecifiedKind(_tz)];
            }

            set { _map[key.ConvertToUtcWithUncpecifiedKind(_tz)] = value; }
        }

        public long Count {
            get { return _map.Count; }
        }

        public long Version {
            get { return _map.Version; }
        }

        public string Id {
            get {
                throw new NotImplementedException();
            }
        }

        public void Add(DateTime k, V v) {
            _map.Add(k.ConvertToUtcWithUncpecifiedKind(_tz), v);
        }

        public void AddFirst(DateTime k, V v) {
            _map.AddFirst(k.ConvertToUtcWithUncpecifiedKind(_tz), v);
        }

        public void AddLast(DateTime k, V v) {
            _map.AddLast(k.ConvertToUtcWithUncpecifiedKind(_tz), v);
        }

        public int Append(IReadOnlyOrderedMap<DateTime, V> appendMap, AppendOption option) {
            throw new NotImplementedException();
        }

        public bool Remove(DateTime k) {
            return _map.Remove(k.ConvertToUtcWithUncpecifiedKind(_tz));
        }

        public bool RemoveFirst(out KeyValuePair<DateTime, V> value) {
            throw new NotImplementedException();
        }

        public bool RemoveLast(out KeyValuePair<DateTime, V> value) {
            throw new NotImplementedException();
        }

        public bool RemoveMany(DateTime k, Lookup direction) {
            return _map.RemoveMany(k.ConvertToUtcWithUncpecifiedKind(_tz), direction);
        }

        public void Flush()
        {
            var persistentSource = _map as IPersistentOrderedMap<DateTime, V>;
            persistentSource?.Flush();
        }

        public void Dispose()
        {
            this.Flush();
        }
    }
}
