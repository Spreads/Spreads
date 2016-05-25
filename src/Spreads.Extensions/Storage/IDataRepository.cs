using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage {

    public interface IDataRepository {
        /// <summary>
        /// Acquire a write lock on the series and return series as a writable series
        /// </summary>
        /// <param name="seriesId">Unique series id</param>
        /// <param name="allowBatches">If true, writes are replicated in chunks and after calling Flush().
        /// This gives significantly higher throughput but higher latency.</param>
        Task<PersistentSeries<K, V>> WriteSeries<K, V>(string seriesId, bool allowBatches = false);

        /// <summary>
        /// Get read-only series
        /// </summary>
        Task<Series<K, V>> ReadSeries<K, V>(string seriesId);

        // TODO remove interfaces
        /// <summary>
        /// Get writeable persistent IDictionary
        /// </summary>
        Task<IDictionary<K,V>> WriteMap<K, V>(string mapId, int initialCapacity);

    }
}
