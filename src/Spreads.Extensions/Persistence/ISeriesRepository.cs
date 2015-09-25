using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads;
using Spreads.Collections;
using System.Runtime.CompilerServices;

namespace Spreads.Persistence {

    // TODO! in CWT we should store lockreleaser with a finlizer that sends release command
    // not recommended way to use finalizer, but we could potentially leverage GC in a good way, together with CWT

    public interface ISeriesRepository
    {
        /// <summary>
        /// 
        /// </summary>
        IOrderedMap<K, V> WriteSeries<K, V>(string seriesId);

        /// <summary>
        /// Read-only series
        /// </summary>
        ISeries<K, V> ReadSeries<K, V>(string seriesId);
    }


    public class SeriesRepository : ISeriesRepository
    {
        private readonly IPersistentStore _store;
        private readonly ISeriesNode _node;
        private readonly ConditionalWeakTable<object, object> _seriesLocks = new ConditionalWeakTable<object, object>();

        public SeriesRepository(IPersistentStore store, ISeriesNode node)
        {
            var cwt = new ConditionalWeakTable<string, string>();
            _store = store;
            _node = node;
        }

        public IOrderedMap<K, V> WriteSeries<K, V>(string seriesId)
        {
            lock (_seriesLocks)
            {
                var writeSeries = _store.GetOrderedMap<K, V>(seriesId);
                // TODO acquire exclusive global write lock
                _seriesLocks.[seriesId] = writeSeries.SyncRoot;
                return writeSeries;
            }
        }

        public ISeries<K, V> ReadSeries<K, V>(string seriesId)
        {
            lock (_series)
            {
                if (_series.ContainsKey(seriesId))
                {
                    var readOnlySeries = (_series[seriesId] as IOrderedMap<K, V>).ReadOnly();
                    return readOnlySeries;
                }
                var writeSeries = _store.GetOrderedMap<K, V>(seriesId);
                _series[seriesId] = writeSeries;
                return writeSeries.ReadOnly();
            }
        }
    }
}
