// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spreads
{
    // NB Interfaces in a single file because
    // current order is logical from the most primitive to complex interfaces

    // See also
    // https://github.com/dotnet/csharplang/blob/master/proposals/async-streams.md
    // https://github.com/dotnet/csharplang/issues/43
    // Pattern-based Compilation
    // The compiler will bind to the pattern-based APIs if they exist, preferring those over using the interface
    // (the pattern may be satisfied with instance methods or extension methods). The requirements for the pattern are:

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
    /// Extends <see cref="IEnumerator{T}"/> to support asynchronous MoveNextAsync.
    /// </summary>
    /// <remarks>
    /// Contract: when MoveNext() returns false it means that there are no more elements
    /// *right now*, and a consumer should call <see cref="MoveNextAsync()"/> to await for a new element, or spin
    /// and repeatedly call <see cref="IEnumerator.MoveNext"/> when a new element is expected very soon. Repeated calls to MoveNext()
    /// could eventually return true. Changes to the underlying sequence, which do not affect enumeration,
    /// do not invalidate the enumerator.
    ///
    /// False move from a valid state keeps a cursor/enumerator at the previous valid state.
    /// </remarks>
    public interface IAsyncEnumerator<out T> : IEnumerator<T>, IAsyncDisposable
    {
        /// <summary>
        /// Async move next.
        /// </summary>
        /// <remarks>
        /// We often refer to this method as <c>MoveNextAsync</c> when it is used with <c>CancellationToken.None</c>
        /// or cancellation token doesn't matter in the context.
        /// </remarks>
        /// <returns>true when there is a next element in the sequence, false if the sequence is complete and there will be no more elements ever.</returns>
        ValueTask<bool> MoveNextAsync();
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

    [Obsolete]
    public interface IAsyncNotifier
    {
        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsCompleted in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// A ValueTask that is completed when underlying data is changed after the task is accessed.
        /// Internally used for signaling to async cursors.
        /// This is a signal to try MoveNext, which gives a definite answer, this task could complete
        /// when data is not changed (false positive), consumers should not rely on this task
        /// but spin on it. It means "likely updated or some condition where it is easier to retry moving on consumer side"
        /// </summary>
        ValueTask Updated { get; }
    }

    [Obsolete]
    public interface IAsyncNotifier<T>
    {
        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsCompleted in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// A ValueTask that is completed when underlying data is changed after the task is accessed.
        /// Internally used for signaling to async cursors.
        /// This is a signal to try MoveNext, which gives a definite answer, this task could complete
        /// when data is not changed (false positive), consumers should not rely on this task
        /// but spin on it. It means "likely updated or some condition where it is easier to retry moving on consumer side"
        /// </summary>
        ValueTask<T> Updated { get; }
    }

    /// <summary>
    /// Main interface for data series.
    /// </summary>
    public interface ISeries<TKey, TValue> : IAsyncEnumerable<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsCompleted in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// A ValueTask that is completed when underlying data is changed after the task is accessed.
        /// Internally used for signaling to async cursors.
        /// This is a signal to try MoveNext, which gives a definite answer, this task could complete
        /// when data is not changed (false positive), consumers should not rely on this task
        /// but spin on it. It means "likely updated or some condition where it is easier to retry moving on consumer side"
        /// </summary>
        ValueTask<bool> Updated { get; }

        /// <summary>
        /// If true then elements are placed by some custom order (e.g. order of addition, index) and not sorted by keys.
        /// </summary>
        bool IsIndexed { get; }

        /// <summary>
        /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact
        /// positions and relative LT/LE/GT/GE moves.
        /// </summary>
        ICursor<TKey, TValue> GetCursor();       

        KeyComparer<TKey> Comparer { get; }

        /// <summary>
        /// First element option.
        /// </summary>
        Opt<KeyValuePair<TKey, TValue>> First { get; }

        /// <summary>
        /// Last element option.
        /// </summary>
        Opt<KeyValuePair<TKey, TValue>> Last { get; }

        /// <summary>
        /// See ICursor.TryGetValue docs.
        /// </summary>
        TValue this[TKey key] { get; }

        bool TryGetValue(TKey key, out TValue value);

        ///// <summary>
        ///// Value at index (offset). Implemented efficiently for indexed series and SortedMap, but default implementation
        ///// is LINQ's <code>[series].Skip(idx-1).Take(1).Value</code> .
        ///// </summary>
        bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp); // TODO support negative moves in all implementations, -1 is last

        /// <summary>
        /// The method finds value according to direction, returns false if it could not find such a value
        /// For indexed series LE/GE directions are invalid (throws InvalidOperationException), while
        /// LT/GT search is done by index rather than by key and possible only when a key exists.
        /// TryFindAt works only with existing keys and is an equivalent of ICursor.MoveAt.
        ///
        /// Check IsMissing property of returned value - it's equivalent to false return of TryXXX pattern.
        /// </summary>
        bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp);

        // NB: Not async ones. Sometimes it's useful for optimization when we check underlying type.

        // TODO when default interfaces are implemented in C# 7.3/8 then Keys/Values should just redirect
        // to cursor.CurrentKey/CurrentValue (important not to Current.Key/Current.Value - slower)
        // Current SortedMap implementation of those members is overkill

        /// <summary>
        /// Keys enumerable.
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        /// <summary>
        /// Values enumerable.
        /// </summary>
        IEnumerable<TValue> Values { get; }
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
    public interface ICursor<TKey, TValue> : IAsyncEnumerator<KeyValuePair<TKey, TValue>>
    {
        CursorState State { get; }

        KeyComparer<TKey> Comparer { get; }

        /// <summary>
        /// Puts the cursor to the position according to LookupDirection
        /// </summary>
        bool MoveAt(TKey key, Lookup direction);

        bool MoveFirst();

        bool MoveLast();

        // MoveNext is a part of IEnumerable

        // NB returning zero is the same as false, no need for TryXXX/Opt<>
        // if we moved by zero steps then we at the same position as before.
        // Zero means we are by stride or less close to the end. If allowPartial = true or stride = 1
        // then we are at the end of series on zero return value.

        long MoveNext(long stride, bool allowPartial);

        bool MovePrevious();

        long MovePrevious(long stride, bool allowPartial);

        // NB even if we could

        TKey CurrentKey { get; }

        TValue CurrentValue { get; }

        /// <summary>
        /// Optional (used for batch/SIMD optimization where gains are visible), MUST NOT throw NotImplementedException()
        /// Returns true when a batch is available immediately (async for IO, not for waiting for new values),
        /// returns false when batching is not supported or there are no more immediate values and a consumer should switch to MoveNextAsync().
        /// </summary>
        Task<bool> MoveNextBatch();

        // NB Using KeyValueReadOnlySpan because batching is only profitable if
        // we could get spans from source or if reduce operation over span is so
        // fast that accumulating a batch in a buffer is cheaper (e.g. SIMD sum, but
        // couldn't find such case in benchmarks)

        /// <summary>
        /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
        /// The actual implementation of the batch could be mutable and could reference a part of the original series, therefore consumer
        /// should never try to mutate the batch directly even if type check reveals that this is possible, e.g. it is a SortedMap
        /// </summary>
        ISeries<TKey, TValue> CurrentBatch { get; }

        /// <summary>
        /// Original series. Note that .Source.GetCursor() is equivalent to .Clone() called on not started cursor
        /// </summary>
        ISeries<TKey, TValue> Source { get; }

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
        bool TryGetValue(TKey key, out TValue value);
    }

    /// <summary>
    /// An <see cref="ICursor{TKey, TValue}"/> with a known implementation type.
    /// </summary>
    // ReSharper disable once TypeParameterCanBeVariant
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
    /// An untyped <see cref="ISeries{TKey, TValue}"/> interface with both keys and values as <see cref="Variant"/> types.
    /// </summary>
    public interface IVariantSeries : ISeries<Variant, Variant>
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
    /// DataStream has incrementing keys.
    /// </summary>
    public interface IDataStream<T> : ISeries<ulong, T>
    { }

    public interface IMutableDataStream<T> : IDataStream<T>
    {
        Task<bool> TryAddLast(T value);
    }

    public class DataStream
    {
        private DataStream()
        {
        }

        /// <summary>
        /// 2**48 ought to be enough for anybody. It's 8.9 years of microseconds.
        /// </summary>
        public static ulong MaxVersion = (1UL << 48) - 1UL;
    }

    ///// <summary>
    ///// An untyped <see cref="ISeries{TKey, TValue}"/> interface with both keys and values as <see cref="Variant"/> types.
    ///// </summary>
    //public interface ISeries : ISeries, ISeries<Variant, Variant>
    //{
    //}

    /// <summary>
    /// Mutable series
    /// </summary>
    public interface IMutableSeries<TKey, TValue> : ISeries<TKey, TValue> //, IDictionary<TKey, TValue>
    {
        // NB even if Async methods add some overhead in sync case, it is small due to caching if Task<bool> return values
        // In persistence layer is used to be a PITA to deal with sync methods with async IO

        long Count { get; }

        /// <summary>
        /// Incremented after any change to data, including setting of the same value to the same key
        /// </summary>
        long Version { get; }

        bool IsAppendOnly { get; }

        /// <summary>
        /// Set value at key. Returns true if key did not exist before.
        /// </summary>
        Task<bool> Set(TKey key, TValue value);

        /// <summary>
        /// Attempts to add new key and value to map.
        /// </summary>
        Task<bool> TryAdd(TKey key, TValue value);

        /// <summary>
        /// Checked addition, checks that new element's key is larger/later than the Last element's key
        /// and adds element to this map
        /// throws ArgumentOutOfRangeException if new key is smaller than the last
        /// </summary>
        Task<bool> TryAddLast(TKey key, TValue value);

        /// <summary>
        /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
        /// and adds element to this map
        /// throws ArgumentOutOfRangeException if new key is larger than the first
        /// </summary>
        Task<bool> TryAddFirst(TKey key, TValue value);

        /// <summary>
        /// Returns Value deleted at the given key on success.
        /// </summary>
        ValueTask<Opt<TValue>> TryRemove(TKey key);

        /// <summary>
        /// Returns KeyValue deleted at the first key.
        /// </summary>
        ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveFirst();

        /// <summary>
        /// Returns KeyValue deleted at the last key (with version before deletion)
        /// </summary>
        ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveLast();

        /// <summary>
        /// Removes all keys from the given key towards the given direction and return the nearest removed key.
        /// </summary>
        ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction);

        /// <summary>
        /// Update value at key and remove other elements according to direction.
        /// Value of updatedAtKey could be invalid (e.g. null for reference types) and ignored,
        /// i.e. there is no guarantee that the given key is present after calling this method.
        /// This method is only used for atomic RemoveMany operation in SCM and could n
        /// </summary>
        Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction);

        /// <summary>
        /// And values from appendMap to the end of this map.
        /// </summary>
        ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap, AppendOption option = AppendOption.RejectOnOverlap);

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

    //// TODO review signature. Why chunk is mutable? Why not just Specialized mutable series?
    //internal interface IMutableChunksSeries<TKey, TValue, TContainer> : ISeries<TKey, TContainer>, IPersistentObject
    //    where TContainer : IMutableSeries<TKey, TValue>
    //{
    //    /// <summary>
    //    /// Keep the key chunk if it is not empty, remove all other chunks to the direction side, update version from the key chunk
    //    /// </summary>
    //    Task<bool> RemoveMany(TKey key, TContainer keyChunk, Lookup direction);

    //    Task<bool> Set(TKey key, TContainer value);

    //    long Version { get; }
    //}
}
