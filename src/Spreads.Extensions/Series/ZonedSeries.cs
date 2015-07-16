using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spreads;


namespace Spreads {

    /// <summary>
    /// All keys are transformed into UTC on writes and back on reads
    /// </summary>
    /// <typeparam name="V"></typeparam>
    public class ZonedSeries<V> : Series<DateTime, V>, IOrderedMap<DateTime, V> {
        private readonly IOrderedMap<DateTime, V> _map;
        private readonly string _tz;

        public ZonedSeries(IOrderedMap<DateTime, V> map, string originalTimeZone) {
            _map = map;
            _tz = originalTimeZone;
        }

        public override ICursor<DateTime, V> GetCursor()
        {
            throw new NotImplementedException();
            // TODO two-way key map is possible for cursor
            // just map keys
            //return new MapKeysCursor<DateTime, V>(_map.GetCursor, dt => dt.ConvertToZoneWithUncpecifiedKind(_tz));
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
    }
}
