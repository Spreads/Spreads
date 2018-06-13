using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// A cursor used as an input to <see cref="Series{TKey,TValue,TCursor}"/>.
    /// The members are used by the corresponding members in <see cref="Series{TKey,TValue,TCursor}"/>.
    /// </summary>
    public interface ICursorSeries<TKey, TValue, TCursor> : ISpecializedCursor<TKey, TValue, TCursor>
        where TCursor : ICursor<TKey, TValue>
    {
        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.Updated"/>
        /// </summary>
        ValueTask<bool> Updated { get; }

        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.IsIndexed"/>
        /// </summary>
        bool IsIndexed { get; }

        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.IsCompleted"/>
        /// </summary>
        bool IsCompleted { get; }

    }
}