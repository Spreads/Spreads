using System.Threading.Tasks;

namespace Spreads.Cursors
{
    // TODO (docs) explain why these members are needed, but first implement Range with this approach

    /// <summary>
    /// A cursor used as an input to <see cref="Series{TKey,TValue,TCursor}"/>.
    /// The members are used by the corresponding members in CursorSeries.
    /// </summary>
    public interface ICursorSeries<TKey, TValue, TCursor> : ISpecializedCursor<TKey, TValue, TCursor>
        where TCursor : ICursor<TKey, TValue>
    {
        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.Updated"/>
        /// </summary>
        Task<bool> Updated { get; }

        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.IsIndexed"/>
        /// </summary>
        bool IsIndexed { get; }

        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.IsReadOnly"/>
        /// </summary>
        bool IsReadOnly { get; }

    }
}