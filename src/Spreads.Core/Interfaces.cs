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

    // See also
    // https://github.com/dotnet/csharplang/blob/master/proposals/async-streams.md
    // https://github.com/dotnet/csharplang/issues/43
    // Pattern-based Compilation
    // The compiler will bind to the pattern-based APIs if they exist, preferring those over using the interface
    // (the pattern may be satisfied with instance methods or extension methods). The requirements for the pattern are:

    // * The enumerable must expose a GetAsyncEnumerator method that may be called with no arguments and that returns an enumerator
    //   that meets the relevant pattern.
    // * The enumerator must expose a MoveNextAsync method that may be called with no arguments and that returns something which may
    //   be awaited and whose GetResult() returns a bool.
    // * The enumerator must also expose Current property whose getter returns a T representing the kind of data being enumerated.
    // * The enumerator may optionally expose a DisposeAsync method that may be invoked with no arguments and that returns something
    //   that can be awaited and whose GetResult() returns void.

    public interface IAsyncDisposable
    {
        Task DisposeAsync();
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
    internal interface IAsyncBatchEnumerator<out T> // F# doesn't allow to implement this: IAsyncEnumerator<IEnumerable<T>>
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

    public interface IAsyncCompletable
    {
        /// <summary>
        /// Caller of this method completes an outstanding async operation.
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

    public interface IAsyncCompleter
    {
        IDisposable Subscribe(IAsyncCompletable subscriber);
    }

    /// <summary>
    /// Series are navigable ordered data streams of key-value pairs.
    /// </summary>
    public interface ISeries<TKey, TValue> : IAsyncEnumerable<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsCompleted in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// If true then elements are placed by some custom order (e.g. order of addition, index) and not sorted by keys.
        /// </summary>
        bool IsIndexed { get; }

        /// <summary>
        /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, exact
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
        /// Equivalent of <see cref="ICursor{TKey,TValue}.TryGetValue"/>.
        /// </summary>
        TValue this[TKey key] { get; }

        bool TryGetValue(TKey key, out TValue value);

        ///// <summary>
        ///// Value at index (offset). Implemented efficiently for indexed series and SortedMap, but default implementation
        ///// is LINQ's <code>[series].Skip(idx-1).Take(1).Value</code>.
        ///// </summary>
        bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp); // TODO support negative moves in all implementations, -1 is last

        /// <summary>
        /// The method finds value according to direction, returns false if it could not find such a value.
        /// For indexed series LE/GE directions are invalid (throws InvalidOperationException), while
        /// LT/GT search is done by index rather than by key and possible only when a key exists.
        /// TryFindAt works only with existing keys and is an equivalent of <see cref="ICursor{TKey,TValue}.MoveAt"/>.
        /// </summary>
        bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp);

        // NB: Key/Values are not async ones. Sometimes it's useful for optimization when we check underlying type.

        // TODO when default interfaces are implemented in C# 7.3/8 then Keys/Values should just redirect
        // to cursor.CurrentKey/CurrentValue (important not to Current.Key/Current.Value - slower)

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
        TCursor GetSpecializedCursor();

        // NB new name and not `new` keyword because this cursor does not implement MoveNextAsync,
        // but is used to build efficient computation tree without interface calls.
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
        // then we are at the end of series if the return value is zero.

        long MoveNext(long stride, bool allowPartial);

        bool MovePrevious();

        long MovePrevious(long stride, bool allowPartial);

        TKey CurrentKey { get; }

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
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        /// <summary>
        /// Returns an initialized (ready to move) instance of <typeparamref name="TCursor"/>.
        /// It could be the same instance for <see cref="Series{TKey,TValue,TCursor}"/>.
        /// It is the equivalent to calling the method <see cref="ISeries{TKey,TValue}.GetCursor"/> on <see cref="ICursor{TKey,TValue}.Source"/> for the non-specialized ICursor.
        /// </summary>
        /// <remarks>
        /// This method must work on disposed instances of <see cref="ISpecializedCursor{TKey, TValue, TCursor}"/>.
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

        /// <summary>
        /// Make the map read-only and disable all subsequent Add/Remove/Set methods (they will throw InvalidOperationException)
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
