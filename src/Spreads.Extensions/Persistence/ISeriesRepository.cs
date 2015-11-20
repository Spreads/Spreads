using System.Threading.Tasks;

namespace Spreads.Persistence
{

    public interface ISeriesRepository {
        /// <summary>
        /// Acquire global write lock on the series and return series as a writable series
        /// </summary>
        Task<IPersistentOrderedMap<K, V>> WriteSeries<K, V>(string seriesId);

        /// <summary>
        /// Get read-only series
        /// </summary>
        Task<ISeries<K, V>> ReadSeries<K, V>(string seriesId);
    }
}