// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace Spreads
{
    // NB Interfaces in a single file because current order is logical from the most primitive to complex interfaces

    // This interfaces match pattern-based compilation of IEnumerables and async streams (introduced in C# 8.0)
    // The compiler will bind to the pattern-based APIs if they exist, preferring those over using the interface
    // (the pattern may be satisfied with instance methods or extension methods). The requirements for the pattern for async streams are:

    // * The enumerable must expose a GetAsyncEnumerator method that may be called with no arguments and that returns an enumerator
    //   that meets the relevant pattern.
    // * The enumerator must expose a MoveNextAsync method that may be called with no arguments and that returns something which may
    //   be awaited and whose GetResult() returns a bool.
    // * The enumerator must also expose Current property whose getter returns a T representing the kind of data being enumerated.
    // * The enumerator may optionally expose a DisposeAsync method that may be invoked with no arguments and that returns something
    //   that can be awaited and whose GetResult() returns void.

    // TODO Spreads follows the pattern of implementing unspecialized interfaces exlicitly and implementing specialized generic methods with the same name.

    public interface IAsyncDisposable
    {
        ValueTask DisposeAsync();
    }

    /// <summary>
    /// Extends <see cref="IEnumerator{T}"/> to support asynchronous MoveNextAsync.
    /// </summary>
    /// <remarks>
    /// Contract: when MoveNext() returns false it means that there are no more elements
    /// *right now*, and a consumer must call <see cref="MoveNextAsync()"/> and await a new element, or spin
    /// and repeatedly call <see cref="IEnumerator.MoveNext"/> when a new element is expected very soon.
    /// Repeated calls to MoveNext() could eventually return true. Changes to the underlying sequence that
    /// do not affect enumeration do not invalidate the enumerator.
    ///
    /// False move from a valid state keeps a cursor/enumerator at the previous valid state.
    /// </remarks>
    public interface IAsyncEnumerator<out T> : IEnumerator<T>, IAsyncDisposable
    //#if NETCOREAPP3_0
    //        ,System.IAsyncDisposable
    //#endif
    {
        /// <summary>
        /// Async move next.
        /// </summary>
        /// <returns>
        /// True when there is a next element in the sequence, false if the sequence is
        /// complete and there will be no more elements ever.
        /// </returns>
        ValueTask<bool> MoveNextAsync();
    }

    // A marker interface for optional batching feature
    [Obsolete]
    public interface IAsyncBatchEnumerator<T> // F# doesn't allow to implement this: IAsyncEnumerator<IEnumerable<T>>
    {
        // Same contract as in cursors:
        // if MoveNextBatchAsync(noAsync = true) returns false then there are no batches available synchronously
        // (e.g. some SIMD operations such as Sum() could benefit if a SortedMap
        // is available instantly, but they should not even try async call because normal Sum() will be faster )
        // For IO case MoveNextBatchAsync(noAsync = true) is a happy-path, but if it returns false then a consumer
        // must call and await MoveNextBatch(noAsync = false). Only after MNB(noAsync = false) returns false there
        // will be no batches ever and consumer must switch to per-item calls.
        ValueTask<bool> MoveNextBatch(bool noAsync);

        IEnumerable<T> CurrentBatch { get; }
    }

    /// <summary>
    /// Exposes the <see cref="IAsyncEnumerator{T}"/> async enumerator, which supports a sync and async
    /// iteration over a collection of a specified type.
    /// </summary>
    public interface IAsyncEnumerable<out T> : IEnumerable<T>
    {
        /// <summary>
        /// Returns an async enumerator.
        /// </summary>
        IAsyncEnumerator<T> GetAsyncEnumerator();
    }

    // TODO rename to INotifiable+Notify,

    /// <summary>
    /// An interface to an object that could have an outstanding job (e.g. is awaiting async completion).
    /// </summary>
    public interface IAsyncCompletable
    {
        /// <summary>
        /// Try complete an outstanding operation on a thread pool. The default case is to
        /// notify continuation of <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> when
        /// a data producer has a new value.
        /// </summary>
        /// <param name="cancel">Cancel completion. Causes OperationCancelledException in awaiters.</param>
        void TryComplete(bool cancel);
    }

    internal interface IAsyncSubscription : IDisposable
    {
        // Currently it is called with -1 after an async move completes
        // But notifiers could decrement themselves if we guarantee that
        // a single notification will succeed and there is no risk of missing
        // un update. However, this matters for a hot loop with several MOPS
        // For real-world data with so many updates we just spin and do not
        // use async machinery, this is for less frequent but important data
        // that we cannot miss but should not spin.
        void RequestNotification(int count);
    }

    /// <summary>
    /// An interface to pass a notification to waiting consumers (if any) when new data is available at a producer.
    /// </summary>
    public interface IAsyncCompleter
    {
        IDisposable Subscribe(IAsyncCompletable subscriber);
    }

    // TODO add it to all containers
    public interface IData
    {
        Mutability Mutability { get; }
    }

    // TODO merge/replace
    public interface ISeriesNew : IData
    {
        KeySorting KeySorting { get; }
    }

    public interface ISeriesNew<TKey, TValue> : ISeriesNew
    {
        // while rewriting keep existing, never commit in broken state
        // ?
        bool IsEmpty { get; }
        
        // TODO LastOrDefault - via Opt<> is terribly slow. Already implemented for DataSource.
    }

    /// <summary>
    /// Series are navigable ordered data streams of key-value pairs.
    /// </summary>
    public interface ISeries<TKey, TValue> : IAsyncEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
    {
        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsCompleted in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        [Obsolete("Use Mutability enum & Flags struct")]
        bool IsCompleted { get; }

        /// <summary>
        /// If true then elements are placed by some custom order (e.g. order of addition, index) and not sorted by keys.
        /// If false then the keys are sorted according to <see cref="Comparer"/>.
        /// </summary>
        [Obsolete("Use KeySorting enum & Flags struct")]
        bool IsIndexed { get; }

        /// <summary>
        /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, exact
        /// positions and relative LT/LE/GT/GE moves.
        /// </summary>
        ICursor<TKey, TValue> GetCursor();

        IAsyncCursor<TKey, TValue> GetAsyncCursor();

        /// <summary>
        /// An optimized <see cref="IComparer{T}"/> implementation with additional members to further optimize performance in certain cases.
        /// </summary>
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
        /// Optimized access to last value. For value types <see cref="IsEmpty"/> could be used to distinguish between a present default value and a missing value.
        /// </summary>
        /// <remarks>
        /// <see cref="Last"/> property is convenient but slowish in hot loops.
        /// </remarks>
        TValue LastValueOrDefault { get; }

        /// <summary>
        /// A throwing equivalent of <see cref="ICursor{TKey,TValue}.TryGetValue"/> and a series counterpart of <see cref="ICursor{TKey,TValue}.TryGetValue"/>.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Throws if key was not found in a series.</exception>
        TValue this[TKey key] { get; }

        /// <summary>
        /// Get a value at the given key. Evaluates continuous series at the key if there is no observed value at the key.
        /// </summary>
        bool TryGetValue(TKey key, out TValue value);

        /// <summary>
        /// Try get value at index (offset). The method is implemented efficiently for some containers (in-memory or immutable/append-only), but default implementation
        /// is LINQ's <code>[series].Skip(idx-1).Take(1).Value</code>.
        /// </summary>
        bool TryGetAt(long index, out KeyValuePair<TKey, TValue> kvp); // TODO support negative moves in all implementations, -1 is last

        /// <summary>
        /// The method finds value according to direction, returns false if it could not find such a value.
        /// For indexed series LE/GE directions are invalid and throw InvalidOperationException, while
        /// LT/GT search is done by index rather than by key and is possible only when a key exists.
        /// TryFindAt works only with observed keys and is a series counterpart of <see cref="ICursor{TKey,TValue}.MoveAt"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The <paramref name="direction">direction</paramref> is <see cref="Lookup.LE"/> or <see cref="Lookup.GE"/> for indexed series (<see cref="IsIndexed"/> = true). </exception>
        bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp);

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
    public interface ISpecializedSeries<TKey, TValue, TCursor> : ISeries<TKey, TValue>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        /// <summary>
        /// Get a strongly typed cursor that implements the <see cref="ISpecializedCursor{TKey,TValue,TCursor}"/> interface.
        /// </summary>
        /// <returns></returns>
        new TCursor GetCursor();

        new AsyncCursor<TKey, TValue, TCursor> GetAsyncCursor();
    }

    public interface ICursorNew<TKey, TValue> // TODO merge with existing
    {
        /// <summary>
        /// Move by <paramref name="stride"/> elements or maximum number of elements less than stride if <paramref name="allowPartial"/> is true.
        /// The <paramref name="stride"/> parameter could be negative.
        /// </summary>
        /// <param name="stride">The number of steps to move.</param>
        /// <param name="allowPartial">Allow to move by less then Abs(stride) steps.</param>
        /// <returns>Actual number of moves. Equals to <paramref name="stride"/> or zero if <paramref name="allowPartial"/> is false.</returns>
        long Move(long stride, bool allowPartial);

        // Alternative (previous) design was bool MoveNextBatch() + CurrentBatch, but it requires additional state in cursor and complicates implementation.
        // Also it is impossible to move normally after MNB without checking the state on every move and this penalizes performance of simple most important MN.
        // With TryMoveNextBatch we could calculate the batch view from current position to the end of a block and move the position there. After that moves
        // could be done normally. The returned view could be disposable with the version check inside Dispose. However, we should disable this for
        // mutable containers, for append-only existing ranges are immutable. TODO test when we consume by batches and add values in parallel in append-only mode.

        /// <summary>
        /// Moves the cursor to the end of the current contiguous block of underlying memory (if there is such a block).
        /// TODO Returns a view over that memory with direct read-only access to keys and values.
        /// TODO K/V are vectors, even if they are with stride > 1 navigating them is faster than moving this cursor.
        /// </summary>
        /// <param name="batch"></param>
        /// <returns></returns>
        bool TryMoveNextBatch(out Segment<TKey, TValue> batch);
    }

    /// <summary>
    /// ICursor is an advanced enumerator that supports moves to first, last, previous, next, exact
    /// positions and relative LT/LE/GT/GE moves.
    /// </summary>
    /// <remarks>
    /// Cursor is resilient to changes in an underlying sequence during movements, e.g. the
    /// sequence could grow during move next. (See documentation for out of order behavior.)
    ///
    /// Contracts:
    /// 1. At the beginning a cursor consumer could call any single move method.
    /// 2. Synchronous moves return true if data is instantly available, e.g. in a map data structure in memory or on fast disk DB.
    ///    ICursor implementations should not block threads, e.g. if a series is not completed synchronous MoveNext should not wait for
    ///    an update but return false if there is no data right now.
    /// 3. When synchronous MoveNext or MoveLast return false, the consumer should call MoveNextAsync. Inside the async
    ///    implementation of MoveNextAsync, a cursor must check if the source is could have new values and return Task.FromResult(false) immediately if it is not.
    /// 4. When any move returns false, a cursor stays at the position before that move. Current/CurrentKey/CurrentValue could be called
    ///    any number of times and they are ususually lazy and cached after the move (for containers) or the first call (for values that require evaluation).
    ///    Any change in underlying container data will not be reflected in the current cursor values if the cursor is not moving and
    ///    no out-of-order exception is thrown e.g. when `SortedMap.Set(k, v)` is called and the cursor is at `k` position.
    ///    Subsequent move of the cursor will throw OOO exception.
    /// </remarks>
    public interface ICursor<TKey, TValue> : IEnumerator<KeyValuePair<TKey, TValue>>// TODO, IAsyncBatchEnumerator<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// Cursor current state.
        /// </summary>
        CursorState State { get; }

        /// <summary>
        /// An optimized <see cref="IComparer{T}"/> implementation with additional members to further optimize performance in certain cases.
        /// </summary>
        KeyComparer<TKey> Comparer { get; }

        /// <summary>
        /// Move the cursor to the first element in series.
        /// </summary>
        /// <returns>Returns true if the <see cref="Source"/> is not empty.</returns>
        bool MoveFirst();

        /// <summary>
        /// Move the cursor to the last element in series.
        /// </summary>
        /// <returns>Returns true if the <see cref="Source"/> is not empty.</returns>
        bool MoveLast();

        /// <summary>
        /// Move the cursor to a previous item in the <see cref="Source"/> series.
        /// </summary>
        /// <returns>Returns true if the cursor moved. When false is returned the cursor stays at the same position where it was before calling this method.</returns>
        new bool MoveNext();

        // NB returning zero is the same as false, no need for TryXXX/Opt<>
        // if we moved by zero steps then we at the same position as before.
        // Zero means we are by stride or less close to the end. If allowPartial = true or stride = 1
        // then we are at the end of series if the return value is zero.

        /// <summary>
        /// Move next by <paramref name="stride"/> elements or maximum number of elements less than stride if <paramref name="allowPartial"/> is true.
        /// </summary>
        /// <param name="stride"></param>
        /// <param name="allowPartial"></param>
        /// <returns>Actual number of moves. Equals to <paramref name="stride"/> or zero if <paramref name="allowPartial"/> is false.</returns>
        [Obsolete("Use Move(2)")]
        long MoveNext(long stride, bool allowPartial);

        /// <summary>
        /// Move the cursor to a previous item in the <see cref="Source"/> series.
        /// </summary>
        /// <returns>Returns true if the cursor moved. When false is returned the cursor stays at the same position where it was before calling this method.</returns>
        bool MovePrevious();

        /// <summary>
        /// Opposite direction of <see cref="MoveNext(long,bool)"/>.
        /// </summary>
        [Obsolete("Use Move(2)")]
        long MovePrevious(long stride, bool allowPartial);

        /// <summary>
        /// Move the cursor to the position according to the Lookup direction. An observed value at key must exist. Use <see cref="ICursor{TKey,TValue}.TryGetValue"/> to get a calculated value for continuous series.
        /// </summary>
        /// <returns>Returns true if the cursor moved. When false is returned the cursor stays at the same position where it was before calling this method.</returns>
        bool MoveAt(TKey key, Lookup direction);

        /// <summary>
        /// Series key at the current cursor position.
        /// </summary>
        TKey CurrentKey { get; }

        /// <summary>
        /// Series value at the current cursor position.
        /// </summary>
        TValue CurrentValue { get; }

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
        bool TryGetValue(TKey key, out TValue value);

        // TODO (review)
        // The TryGetValue method should be optimized
        // for sort join case using enumerator, e.g. for repeat it should keep previous value and check if
        // the requested key is between the previous and the current keys, and then return the previous one.
        // NB This is not thread safe. ICursors must be used from a single thread.

        // TODO new Series<TKey, TValue, Segment<TKey, TValue>> CurrentBatch { get; }
    }

    /// <summary>
    /// An <see cref="T:Spreads.ICursor`2" /> with a known implementation type.
    /// </summary>
    public interface ISpecializedCursor<TKey, TValue, TCursor> : ICursor<TKey, TValue> // TODO rename to ICursor'3, no clashes
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        /// <summary>
        /// Returns an initialized (ready to move) instance of <typeparamref name="TCursor"/>.
        /// It could be the same instance for <see cref="Series{TKey,TValue,TCursor}"/>.
        /// It is the equivalent to calling the method <see cref="ISeries{TKey,TValue}.GetCursor"/> on <see cref="ICursor{TKey,TValue}.Source"/> for the non-specialized ICursor.
        /// </summary>
        /// <remarks>
        /// This method must work on disposed instances of <see cref="ISpecializedCursor{TKey, TValue, TCursor}"/>, i.e. it acts as a factory.
        /// </remarks>
        [Pure]
        TCursor Initialize();

        /// <summary>
        /// Copy this cursor and position the copy at the same place as this cursor.
        /// </summary>
        new TCursor Clone();

        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.IsIndexed"/>
        /// </summary>
        bool IsIndexed { get; }

        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.IsCompleted"/>
        /// </summary>
        bool IsCompleted { get; }

        IAsyncCompleter AsyncCompleter { get; }

        new Series<TKey, TValue, TCursor> Source { get; }
    }

    // Not needed, delete
    public interface IAsyncCursor<TKey, TValue> : ICursor<TKey, TValue>, IAsyncEnumerator<KeyValuePair<TKey, TValue>>
    {
    }

    public interface IAsyncCursor<TKey, TValue, TCursor> : ISpecializedCursor<TKey, TValue, TCursor>, IAsyncEnumerator<KeyValuePair<TKey, TValue>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
    }

    ///// <summary>
    ///// An untyped <see cref="ISeries{TKey, TValue}"/> interface with both keys and values as <see cref="Variant"/> types.
    ///// </summary>
    //public interface IVariantSeries : ISeries<Variant, Variant>
    //{
    //    /// <summary>
    //    /// <see cref="TypeEnum"/> for the keys type.
    //    /// </summary>
    //    TypeEnumEx KeyType { get; }

    //    /// <summary>
    //    /// <see cref="TypeEnum"/> for the values type.
    //    /// </summary>
    //    TypeEnumEx ValueType { get; }
    //}

    ///// <summary>
    ///// An untyped <see cref="ISeries{TKey, TValue}"/> interface with both keys and values as <see cref="Variant"/> types.
    ///// </summary>
    //public interface ISeries : ISeries, ISeries<Variant, Variant>
    //{
    //}

    /// <summary>
    /// Mutable series
    /// </summary>
    public interface IAppendSeries<TKey, TValue> : ISeries<TKey, TValue> //, IDictionary<TKey, TValue>
    {
        /// <summary>
        /// Adds key and value to the end of the series.
        /// </summary>
        /// <returns>True on successful addition. False if the key already exists or the new key breaks sorting order.</returns>
        Task<bool> TryAddLast(TKey key, TValue value);

        /// <summary>
        /// Make the map read-only and disable all subsequent Add/Remove/Set methods (they will throw InvalidOperationException)
        /// </summary>
        [Obsolete]
        Task Complete();
    }

    /// <summary>
    /// Mutable series
    /// </summary>
    public interface IMutableSeries<TKey, TValue> : IAppendSeries<TKey, TValue>
    {
        // NB even if Async methods add some overhead in sync case, it is small due to caching if Task<bool> return values
        // In persistence layer is used to be a PITA to deal with sync methods with async IO
        [Obsolete] // TODO make this Nullable and move up to Series.
        long Count { get; }

        /// <summary>
        /// Incremented after any change to data, including setting the same value to the same key.
        /// </summary>
        long Version { get; } // TODO move up to Series

        // TODO review Should we expose OrderVersion?

        [Obsolete]
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
        /// Removes all keys from the given key towards the given direction and returns the nearest removed key.
        /// </summary>
        ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction);

        /// <summary>
        /// Update value at key and remove other elements according to direction.
        /// Value of updatedAtKey could be invalid (e.g. null for reference types) and ignored,
        /// i.e. there is no guarantee that the given key is present after calling this method.
        /// This method is only used for atomic RemoveMany operation in SCM and could be not implemented/supported.
        /// </summary>
        Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction);

        /// <summary>
        /// Add values from appendMap to the end of this map.
        /// </summary>
        ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap, AppendOption option = AppendOption.RejectOnOverlap);

        
        // TODO Methods 
        // MarkComplete
        // MarkAppendOnly
        // All mutating methods must check mutability

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
