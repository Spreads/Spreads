// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads
{
    // NB Interfaces in a single file because
    // current order is logical from the most primitive to complex interfaces

    // See also
    // https://github.com/dotnet/csharplang/blob/master/proposals/async-streams.md
    // https://github.com/dotnet/csharplang/issues/43
    //Pattern-based Compilation
    //The compiler will bind to the pattern-based APIs if they exist, preferring those over using the interface
    //(the pattern may be satisfied with instance methods or extension methods). The requirements for the pattern are:

    //* The enumerable must expose a GetAsyncEnumerator method that may be called with no arguments and that returns an enumerator
    //  that meets the relevant pattern.
    //* The enumerator must expose a MoveNextAsync method that may be called with no arguments and that returns something which may
    //  be awaited and whose GetResult() returns a bool.
    //* The enumerator must also expose Current property whose getter returns a T representing the kind of data being enumerated.
    //* The enumerator may optionally expose a DisposeAsync method that may be invoked with no arguments and that returns something
    //  that can be awaited and whose GetResult() returns void.

    public interface IAsyncDisposable
    {
        Task DisposeAsync();
    }

    /// <summary>
    /// Extends <see cref="IEnumerator{T}"/> to support asynchronous MoveNextAsync with cancellation.
    /// </summary>
    /// <remarks>
    /// Contract: when MoveNextAsync() returns false it means that there are no more elements
    /// right now, and a consumer should call MoveNextAsync() to await for a new element, or spin
    /// and repeatedly call MoveNextAsync() when a new element is expected very soon. Repeated calls to MoveNextAsync()
    /// could eventually return true. Changes to the underlying sequence, which do not affect enumeration,
    /// do not invalidate the enumerator.
    ///
    /// <c>Current</c> property follows the parent contracts as described here: https://msdn.microsoft.com/en-us/library/58e146b7(v=vs.110).aspx
    /// Some implementations guarantee that <c>Current</c> keeps its last value from successful MoveNextAsync(),
    /// but that must be explicitly stated in a data structure documentation (e.g. SortedMap).
    /// </remarks>
    public interface IAsyncEnumerator<out T> : IEnumerator<T>, IAsyncDisposable
    {
        // TODO (docs) last part of the remarks above should always be true and the contract must be documented
        // False move from a valid state must keep a cursor/enumerator at the previous valid state

        /// <summary>
        /// Async move next.
        /// </summary>
        /// <remarks>
        /// We often refer to this method as <c>MoveNextAsync</c> when it is used with <c>CancellationToken.None</c>
        /// or cancellation token doesn't matter in the context.
        /// </remarks>
        /// <param name="cancellationToken">Use <c>CancellationToken.None</c> as default token</param>
        /// <returns>true when there is a next element in the sequence, false if the sequence is complete and there will be no more elements ever</returns>
        Task<bool> MoveNextAsync(CancellationToken cancellationToken);

        Task<bool> MoveNextAsync(); // F#... doesn't like optional params from C# :/
    }

    /// <summary>
    /// Exposes the <see cref="IAsyncEnumerator{T}"/> async enumerator, which supports a sync and async iteration over a collection of a specified type.
    /// </summary>
    public interface IAsyncEnumerable<out T> : IEnumerable<T>
    {
        /// <summary>
        /// Returns an async enumerator.
        /// </summary>
        IAsyncEnumerator<T> GetAsyncEnumerator();
    }

    // TODO see issue https://github.com/Spreads/Spreads/issues/99
    // DataStream is Series<long, TValue>. Event consecutive keys are not required in general but could be required by some protocol.

    ///// <summary>
    ///// An <see cref="IAsyncEnumerable{KeyValuePair}"/> and <see cref="IPublisher{KeyValuePair}"/> with additional guarantee
    ///// that items are ordered by <typeparamref name="TKey"/>.
    ///// </summary>
    //public interface IDataStream<TKey, TValue> : IAsyncEnumerable<KeyValuePair<TKey, TValue>>
    //{
    //    /// <summary>
    //    /// Key comparer.
    //    /// </summary>
    //    KeyComparer<TKey> Comparer { get; }
    //}

    /// <summary>
    /// Main interface for data series.
    /// </summary>
    public interface ISeries<TKey, TValue> : IAsyncEnumerable<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// If true then elements are placed by some custom order (e.g. order of addition, index) and not sorted by keys.
        /// </summary>
        bool IsIndexed { get; }

        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsCompleted in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact
        /// positions and relative LT/LE/GT/GE moves.
        /// </summary>
        ICursor<TKey, TValue> GetCursor();

        /// <summary>
        /// A Task that is completed with True whenever underlying data is changed.
        /// Internally used for signaling to async cursors.
        /// After getting the Task one should check if any changes happened (version change or cursor move) before awaiting the task.
        /// If the task is completed with false then the series is read-only, immutable or complete.
        /// </summary>
        Task<bool> Updated { get; }
    }

    /// <summary>
    /// A series with a known strongly typed cursor type.
    /// </summary>
    public interface ISpecializedSeries<TKey, TValue, out TCursor> : ISeries<TKey, TValue>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        /// <summary>
        /// Get a strongly typed cursor that implements the <see cref="ISpecializedCursor{TKey,TValue,TCursor}"/> interface.
        /// </summary>
        /// <returns></returns>
        new TCursor GetCursor();
    }

    // TODO (docs) review the contract (together with IAsynEnumerable above) and format the xml doc

    /// <summary>
    /// ICursor is an advanced enumerator that supports moves to first, last, previous, next, next batch, exact
    /// positions and relative LT/LE/GT/GE moves.
    /// </summary>
    /// <remarks>
    /// Cursor is resilient to changes in an underlying sequence during movements, e.g. the
    /// sequence could grow during move next. (See documentation for out of order behavior.)
    ///
    /// Supports batches with MoveNextBatchAsync() and CurrentBatch members. Accessing current key
    /// after MoveNextBatchAsync or CurrentBatch after any single key movement results in InvalidOperationException.
    /// IsBatch property indicates whether the cursor is positioned on a single value or a batch.
    ///
    /// Contracts:
    /// 1. At the beginning a cursor consumer could call any single move method or MoveNextBatch. MoveNextBatch could
    ///    be called only on the initial move or after a previous MoveNextBatch() call that returned true. It MUST NOT
    ///    be called in any other situation, ICursor implementations MUST return false on any such wrong call.
    /// 2. CurrentBatch contains a batch only after a call to MoveNextBatch() returns true. CurrentBatch is undefined
    ///    in all other cases.
    /// 3. After a call to MoveNextBatch() returns false, the consumer MUST use only single calls. ICursor implementations MUST
    ///    ensure that the relative moves MoveNextAsync/Previous start from the last position of the previous batch.
    /// 4. Synchronous moves return true if data is instantly available, e.g. in a map data structure in memory or on fast disk DB.
    ///    ICursor implementations should not block threads, e.g. if a map is IUpdateable, synchronous MoveNextAsync should not wait for
    ///    an update but return false if there is no data right now.
    /// 5. When synchronous MoveNextAsync or MoveLast return false, the consumer should call async overload of MoveNextAsync. Inside the async
    ///    implementation of MoveNextAsync, a cursor must check if the source is IUpdateable and return Task.FromResult(false) immediately if it is not.
    /// 6. When any move returns false, cursor stays at the position before that move (TODO now this is ensured only for SM MN/MP and for Bind(ex.MA) )
    /// _. TODO If the source is updated during a lifetime of a cursor, cursor must recreate its state at its current position
    ///    Rewind logic only for async? Throw in all cases other than MoveNextAsync, MoveAt? Or at least on MovePrevious.
    ///    Or special behavior of MoveNextAsync only on appends or changing the last value? On other changes must throw invalidOp (locks are there!)
    ///    So if update is before the current position of a cursor, then throw. If after - then this doesn't affect the cursor in any way.
    ///    TODO cursor could implement IUpdateable when source does, or pass through to CursorSeries
    ///
    /// </remarks>
    public interface ICursor<TKey, TValue>
        : IAsyncEnumerator<KeyValuePair<TKey, TValue>>
    {
        CursorState State { get; }

        KeyComparer<TKey> Comparer { get; }

        /// <summary>
        /// Puts the cursor to the position according to LookupDirection
        /// </summary>
        bool MoveAt(TKey key, Lookup direction);

        bool MoveFirst();

        bool MoveLast();

        bool MovePrevious();

        ref readonly TKey CurrentKey { get; }

        ref readonly TValue CurrentValue { get; }

        ref readonly KeyValueRef<TKey, TValue> CurrentRef { get; }

        /// <summary>
        /// Optional (used for batch/SIMD optimization where gains are visible), MUST NOT throw NotImplementedException()
        /// Returns true when a batch is available immediately (async for IO, not for waiting for new values),
        /// returns false when there is no more immediate values and a consumer should switch to MoveNextAsync().
        /// </summary>
        Task<bool> MoveNextBatch(CancellationToken cancellationToken = default);

        // NB Using ReadOnlyKeyValueSpan because batching is only profitable if
        // we could get spans from source or if reduce operation over span is so
        // fast that accumulating a batch in a buffer is cheaper (e.g. SIMD sum, but
        // couldn't find such case in benchmarks)

        /// <summary>
        /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
        /// The actual implementation of the batch could be mutable and could reference a part of the original series, therefore consumer
        /// should never try to mutate the batch directly even if type check reveals that this is possible, e.g. it is a SortedMap
        /// </summary>
        ReadOnlyKeyValueSpan<TKey, TValue> CurrentBatch { get; }

        /// <summary>
        /// Original series. Note that .Source.GetCursor() is equivalent to .Clone() called on not started cursor
        /// </summary>
        IReadOnlySeries<TKey, TValue> Source { get; }

        /// <summary>
        /// If true then TryGetValue could return values for any keys, not only for existing keys.
        /// E.g. previous value, interpolated value, etc.
        /// </summary>
        bool IsContinuous { get; }

        /// <summary>
        /// Copy this cursor and position the copy at the same place as this cursor.
        /// </summary>
        ICursor<TKey, TValue> Clone();

        /// <summary>
        /// Gets a calculated value for continuous series without moving the cursor position.
        /// E.g. a continuous cursor for Repeat() will check if current state allows to get previous value,
        /// and if not then .Source.GetCursor().MoveAt(key, LE).
        /// </summary>
        /// <remarks>
        /// The TryGetValue method should be optimized
        /// for sort join case using enumerator, e.g. for repeat it should keep previous value and check if
        /// the requested key is between the previous and the current keys, and then return the previous one.
        /// NB This is not thread safe. ICursors must be used from a single thread.
        /// </remarks>
        bool TryGetValue(in TKey key, out TValue value);
    }

    /// <summary>
    /// An <see cref="ICursor{TKey, TValue}"/> with a known implementation type.
    /// </summary>
    public interface ISpecializedCursor<TKey, TValue, TCursor> : ICursor<TKey, TValue>
        where TCursor : ICursor<TKey, TValue>
    {
        /// <summary>
        /// Returns an initialized (ready to move) instance of <typeparamref name="TCursor"/>.
        /// It could be the same instance for <see cref="Series{TKey,TValue,TCursor}"/>.
        /// It is the equivalent to calling the method <see cref="ISeries{TKey,TValue}.GetCursor"/> on <see cref="ICursor{TKey,TValue}.Source"/> for the non-specialized ICursor.
        /// </summary>
        /// <remarks>
        /// This method must work on disposed instances of <see cref="ISpecializedCursor{TKey, TValue, TCursor}"/>.
        /// </remarks>
        TCursor Initialize();

        /// <summary>
        /// Copy this cursor and position the copy at the same place as this cursor.
        /// </summary>
        new TCursor Clone();
    }

    /// <summary>
    /// NB! 'Read-only' doesn't mean that the object is immutable or not changing. It only means
    /// that there is no methods to change the map *from* this interface, without any assumptions about
    /// the implementation. Underlying sequence could be mutable and rapidly changing; to prevent any
    /// changes use lock (Monitor.Enter) on the SyncRoot property. Doing so will block any changes for
    /// mutable implementations and won't affect immutable implementations.
    /// </summary>
    public interface IReadOnlySeries<TKey, TValue> : ISeries<TKey, TValue>
    {
        /// <summary>
        /// True if a series is empty.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// First element, throws InvalidOperationException if empty
        /// </summary>
        KeyValueRef<TKey, TValue> First { get; }

        /// <summary>
        /// Last element, throws InvalidOperationException if empty
        /// </summary>
        KeyValueRef<TKey, TValue> Last { get; }

        /// <summary>
        /// Value at key, throws KeyNotFoundException if key is not present in the series (even for continuous series).
        /// Use TryGetValue to get a value between existing keys for continuous series.
        /// </summary>
        ref readonly TValue this[in TKey key] { get; }

        ///// <summary>
        ///// Value at index (offset). Implemented efficiently for indexed series and SortedMap, but default implementation
        ///// is LINQ's <code>[series].Skip(idx-1).Take(1).Value</code> .
        ///// </summary>
        KeyValueRef<TKey, TValue> GetAt(long idx);

        // Not async ones. Sometimes it's useful for optimization
        /// <summary>
        /// Keys enumerable.
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        /// <summary>
        /// Values enumerable.
        /// </summary>
        IEnumerable<TValue> Values { get; }

        /// <summary>
        /// The method finds value according to direction, returns false if it could not find such a value
        /// For indexed series LE/GE directions are invalid (throws InvalidOperationException), while
        /// LT/GT search is done by index rather than by key and possible only when a key exists.
        /// TryFind works only with existing keys and is an equivalent of ICursor.MoveAt.
        /// </summary>
        bool TryFind(in TKey key, Lookup direction, out KeyValueRef<TKey, TValue> value);
    }

    public static class ReadOnlySeriesExtensions
    {
        /// <summary>
        /// Try get first element.
        /// </summary>
        public static bool TryGetFirst<TKey, TValue, TSeries>(this TSeries series,
            out KeyValuePair<TKey, TValue> value)
            where TSeries : IReadOnlySeries<TKey, TValue>
        {
            if (series.IsEmpty)
            {
                value = default;
                return false;
            }
            value = series.First;
            return true;
        }

        /// <summary>
        /// Try get last element.
        /// </summary>
        public static bool TryGetLast<TKey, TValue, TSeries>(this TSeries series,
            out KeyValuePair<TKey, TValue> value)
            where TSeries : IReadOnlySeries<TKey, TValue>
        {
            if (series.IsEmpty)
            {
                value = default;
                return false;
            }
            value = series.Last;
            return true;
        }
    }

    /// <summary>
    /// An untyped <see cref="ISeries{TKey, TValue}"/> interface with both keys and values as <see cref="Variant"/> types.
    /// </summary>
    public interface ISeries : ISeries<Variant, Variant>
    {
        /// <summary>
        /// <see cref="TypeEnum"/> for the keys type.
        /// </summary>
        TypeEnum KeyType { get; }

        /// <summary>
        /// <see cref="TypeEnum"/> for the values type.
        /// </summary>
        TypeEnum ValueType { get; }
    }

    /// <summary>
    /// An untyped <see cref="IReadOnlySeries{TKey, TValue}"/> interface with both keys and values as <see cref="Variant"/> types.
    /// </summary>
    public interface IReadOnlySeries : ISeries, IReadOnlySeries<Variant, Variant>
    {
    }

    /// <summary>
    /// Mutable series
    /// </summary>
    public interface IMutableSeries<TKey, TValue> : IReadOnlySeries<TKey, TValue> //, IDictionary<TKey, TValue>
    {
        // NB even if Async methods add some overhead in sync case, it is small due to caching if Task<bool> return values
        // In persistence layer is used to be a PITA to deal with sync methods with async IO

        long Count { get; }

        /// <summary>
        /// Incremented after any change to data, including setting of the same value to the same key
        /// </summary>
        long Version { get; }

        bool IsAppendOnly { get; }

        Task<bool> Set(TKey key, TValue value);

        /// <summary>
        /// Adds new key and value to map, throws if the key already exists
        /// </summary>
        Task Add(TKey key, TValue value);

        /// <summary>
        /// Checked addition, checks that new element's key is larger/later than the Last element's key
        /// and adds element to this map
        /// throws ArgumentOutOfRangeException if new key is smaller than the last
        /// </summary>
        Task AddLast(TKey key, TValue value);

        /// <summary>
        /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
        /// and adds element to this map
        /// throws ArgumentOutOfRangeException if new key is larger than the first
        /// </summary>
        Task AddFirst(TKey key, TValue value);

        Task<bool> Remove(TKey key);

        Task<bool> RemoveLast(out KeyValuePair<TKey, TValue> kvp);

        Task<bool> RemoveFirst(out KeyValuePair<TKey, TValue> kvp);

        Task<bool> RemoveMany(TKey key, Lookup direction);

        /// <summary>
        /// And values from appendMap to the end of this map
        /// </summary>
        Task<long> Append(ReadOnlyKeyValueSpan<TKey, TValue> appendMap, AppendOption option = AppendOption.ThrowOnOverlap);

        /// <summary>
        /// Make the map read-only and disable all Add/Remove/Set methods (they will throw)
        /// </summary>
        Task Complete();
    }

    /// <summary>
    /// `Flush` has a standard meaning, e.g. as in Stream, and saves all changes. `Dispose` calls `Flush`. `Id` is globally unique.
    /// </summary>
    public interface IPersistentObject : IDisposable
    {
        /// <summary>
        /// Persist any cached data.
        /// </summary>
        Task Flush();

        /// <summary>
        /// Unique string identifier.
        /// </summary>
        string Id { get; }
    }

    /// <summary>
    /// ISeries backed by some persistent storage.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public interface IPersistentSeries<TKey, TValue> : IMutableSeries<TKey, TValue>, IPersistentObject
    {
    }

    // TODO review signature. Why chunk is mutable? Why not just Specialized mutable series?
    internal interface IMutableChunksSeries<TKey, TValue, TContainer> : IReadOnlySeries<TKey, TContainer>, IPersistentObject
        where TContainer : IMutableSeries<TKey, TValue>
    {
        /// <summary>
        /// Keep the key chunk if it is not empty, remove all other chunks to the direction side, update version from the key chunk
        /// </summary>
        Task<bool> RemoveMany(TKey key, TContainer keyChunk, Lookup direction);

        new ref readonly TContainer this[in TKey key] { get; }

        Task<bool> Set(TKey key, TContainer value);

        long Version { get; }
    }
}