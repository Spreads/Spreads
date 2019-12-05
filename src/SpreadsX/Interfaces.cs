// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;

namespace Spreads
{
    // TODOs
    // Remove all IsIndexed
    // MutableSeries.Count -> RowCount
    // Ensure that we implement unspecialized interfaces explicitly and implement specialized generic methods with the same name.

    // TODO Split interfaces to files by groups
    // Interfaces in a single file because current order is logical from the most primitive to complex interfaces
    // DEFAULT_INTERFACE_IMPL block are mostly for documentation, we still target netstandard2.0

    #region Async interfaces

    // This interfaces match pattern-based compilation of IEnumerable and async streams (introduced in C# 8.0)
    // The compiler will bind to the pattern-based APIs if they exist, preferring those over using the interface
    // (the pattern may be satisfied with instance methods or extension methods). The requirements for the pattern for async streams are:

    // * The enumerable must expose a GetAsyncEnumerator method that may be called with no arguments and that returns an enumerator
    //   that meets the relevant pattern.
    // * The enumerator must expose a MoveNextAsync method that may be called with no arguments and that returns something which may
    //   be awaited and whose GetResult() returns a bool.
    // * The enumerator must also expose Current property whose getter returns a T representing the kind of data being enumerated.
    // * The enumerator may optionally expose a DisposeAsync method that may be invoked with no arguments and that returns something
    //   that can be awaited and whose GetResult() returns void.

    /// <summary>
    /// Combines <see cref="System.Collections.Generic"/>'s <see cref="IEnumerator{T}"/> and <see cref="System.Collections.Generic.IAsyncEnumerator{T}"/>.
    /// </summary>
    /// <remarks>
    /// Contract: When <see cref="IEnumerator.MoveNext"/> returns false it means that there are no more elements
    /// *right now*, and a consumer must call <see cref="System.Collections.Generic.IAsyncEnumerator{T}.MoveNextAsync()"/> and await a new element, or spin
    /// and repeatedly call <see cref="IEnumerator.MoveNext"/> when a new element is expected very soon.
    /// Repeated calls to MoveNext() could eventually return true. Changes to the underlying sequence that
    /// do not affect enumeration (e.g. append) do not invalidate the enumerator.
    ///
    /// <para />
    /// False moves from a valid state keep a cursor/enumerator at the previous valid state.
    /// </remarks>
    // ReSharper disable once PossibleInterfaceMemberAmbiguity : (VB) R# is wrong , new T Current is enough, it duplicates it as get_Current
    public interface IAsyncEnumerator<out T> : IEnumerator<T>, System.Collections.Generic.IAsyncEnumerator<T>
    {
        // Note that all interface members should be implemented explicitly and
        // implementers should have the same methods/props for pattern-based compilation.
        // We need to inherit from SCG.IAsyncEnumerator to be able to use Ix.NET and
        // potentially similar libraries that provide extensions for or accept as parameters
        // SCG's interface. In this case pattern-based implementation won't work.

        new T Current { get; }
    }

    /// <summary>
    /// Exposes the <see cref="IAsyncEnumerator{T}"/> async enumerator, which supports a sync and async
    /// iteration over a collection of a specified type.
    /// </summary>
    public interface IAsyncEnumerable<out T> : IEnumerable<T>, System.Collections.Generic.IAsyncEnumerable<T>
    {
        // Do not need new, delete
        ///// <summary>
        ///// Returns an async enumerator.
        ///// </summary>
        new IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// An interface to an object that could have an outstanding job (e.g. is awaiting async completion).
    /// </summary>
    public interface IAsyncCompletable
    {
        /// <summary>
        /// Try complete an outstanding operation via the thread pool. The default case is to
        /// notify continuation of <see cref="System.Collections.Generic.IAsyncEnumerator{T}.MoveNextAsync"/> when
        /// a data producer has a new value.
        /// </summary>
        /// <remarks>
        /// This method could be called multiple times. Implementations do check if new data is available
        /// in the source and call continuation only when data is available. This is a wake up call to
        /// awaiters.
        /// </remarks>
        /// <param name="cancel">Cancel completion. Causes <see cref="OperationCanceledException"/> in awaiters.</param>
        void TryComplete(bool cancel);
    }

    /// <summary>
    /// An interface to pass a notification to waiting consumers (if any) when new data is available at a producer.
    /// </summary>
    public interface IAsyncCompleter
    {
        IDisposable Subscribe(IAsyncCompletable subscriber);
    }

    #endregion Async interfaces

    // Model untyped data closely to ML.NET IDataView, but do not binary depend on it
    // * We need serializable schema with TypeEnums, decoupled from .NET type system as much as possible (e.g. Tuple is logically a sequence of types, not .NET type)
    // * Do not depend on upstream future changes
    // * Make bridging the two very easy...
    // * ... but be flexible to adjust as needed here.

    /// <summary>
    /// O(1) methods to check if a container is empty or get the number of rows if it is available.
    /// </summary>
    public interface IRowCount
    {
        /// <summary>
        /// Returns non-null value if it is possible to get the count in O(1)
        /// fast call without blocking.
        /// </summary>
        ulong? RowCount { get; }

        /// <summary>
        ///
        /// </summary>
        bool IsEmpty { get; }
    }

    public interface IDataSource : IRowCount
    {
        ContainerLayout ContainerLayout { get; }

        /// <summary>
        /// <see cref="Mutability"/> of data source of this series.
        /// </summary>
        /// <remarks>
        /// Mutability composes well for combining multiple data sources:
        /// if any origin is mutable then resulting combination (projection/aggregation)
        /// is mutable.
        /// <para />
        /// Mutable data sources could make an aggregating projection (e.g. rolling/expanding
        /// window/average) invalid. Consuming (enumerating) mutable data sources
        /// could throw <see cref="OutOfOrderKeyException{TKey}"/>
        /// For data consumers that means that a aggregating projection could become invalid.
        /// <para />
        /// Immutable/Append allow structural sharing of existing data (requires ref counting of buffers)
        /// </remarks>
        Mutability Mutability { get; }

        /// <summary>
        /// <see cref="KeySorting"/> of row keys.
        /// </summary>
        KeySorting KeySorting { get; }

        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsCompleted in terms of ICollection/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsCompleted
        {
            get
#if DEFAULT_INTERFACE_IMPL
                => this.Mutability == Mutability.ReadOnly
#endif
            ;
        }

        ///// <summary>
        ///// Unique string identifier or a string expression describing the data (e.g. "a + b").
        ///// </summary>
        // string? Expression { get; } TODO this could be done much later and added as a layer, core functionality does not depend on this
    }

    /// <summary>
    /// <see cref="ICursor{TKey,TValue}"/> is an advanced enumerator that supports moves to first, last, previous, next, exact
    /// positions and relative LT/LE/GT/GE (<see cref="Lookup"/>) moves.
    /// </summary>
    /// <remarks>
    /// A cursor is resilient to changes in an underlying sequence during movements, e.g. the
    /// sequence could grow during move next. (See documentation for out of order behavior.)
    ///
    /// Contracts:
    /// 1. At the beginning a cursor consumer could call any single move method.
    /// 2. Synchronous moves return true if data is instantly available, e.g. in a series data structure in memory or on fast disk DB.
    ///    ICursor implementations should not block threads, e.g. if a series is not completed then synchronous MoveNext should not wait for
    ///    an update but return false if there is no data right now.
    /// 3. When synchronous MoveNext or MoveLast return false, the consumer should call MoveNextAsync. Inside the async
    ///    implementation of MoveNextAsync, a cursor must check if the source could have new values and return Task.FromResult(false) immediately if it is not.
    /// 4. When any move returns false, a cursor stays at the position before that move. Current/CurrentKey/CurrentValue could be called
    ///    any number of times and they are usually lazy and cached after the move (for containers) or the first call (for values that require evaluation).
    ///    Any change in underlying container data will not be reflected in the current cursor values if the cursor is not moving and
    ///    no out-of-order exception is thrown e.g. when `SortedMap.Set(k, v)` is called and the cursor is at `k` position.
    ///    Subsequent move of the cursor will throw OOO exception.
    /// </remarks>
    public interface ICursor<TKey, TValue> : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// Cursor current state.
        /// </summary>
        CursorState State { get; }

        /// <summary>
        /// An optimized <see cref="IComparer{T}"/> implementation with additional members to further optimize performance in certain cases.
        /// </summary>
        /// <seealso cref="KeyComparer{T}"/>
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
        /// Move by <paramref name="stride"/> elements or maximum number of elements less than stride if <paramref name="allowPartial"/> is true.
        /// The <paramref name="stride"/> parameter could be negative.
        /// </summary>
        /// <param name="stride">The number of steps to move.</param>
        /// <param name="allowPartial">Allow to move by less then Abs(stride) steps.</param>
        /// <returns>Actual number of moves. Equals to <paramref name="stride"/> or zero if <paramref name="allowPartial"/> is false.</returns>
        long Move(long stride, bool allowPartial);

        /// <summary>
        /// Move the cursor to a previous item in the <see cref="Source"/> series.
        /// </summary>
        /// <returns>Returns true if the cursor moved. When false is returned the cursor stays at the same position where it was before calling this method.</returns>
        bool MovePrevious();

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
        /// Original series. Note that <see cref="Source"/>'s <see cref="ISeries{TKey,TValue}.GetCursor"/> is equivalent to <see cref="ICursor{TKey,TValue,TCursor}.Initialize"/> or <see cref="Clone"/> called on not started cursor.
        /// </summary>
        ISeries<TKey, TValue> Source { get; }

        /// <summary>
        /// Copy this cursor and position the copy at the same place as this cursor.
        /// </summary>
        ICursor<TKey, TValue> Clone();

        /// <summary>
        /// If true then <see cref="TryGetValue"/> could return values for any keys, not only for existing keys.
        /// E.g. previous value, interpolated value, etc.
        /// </summary>
        bool IsContinuous { get; }

        /// <summary>
        /// Gets a calculated value for continuous series without moving the cursor position.
        /// E.g. a continuous cursor for Repeat() will check if current state allows to get previous value,
        /// and if not then .Source.GetCursor().MoveAt(key, LE).
        /// </summary>
        bool TryGetValue(TKey key, out TValue value);

        // TODO Problem with KeyValueReadOnlySpan is that it cannot support untyped data row case
        // Key/Values getters there are span factories, so we should do column span factories
        // The return value should be untyped with GetKeys<TKey>():Span<TKey>, GetValues, GetColumn...
        // Also we may want to return Keys as IEnumerable, e.g. virtual/calculated keys
        ///// <summary>
        ///// Moves the cursor to the end of the current contiguous block of underlying memory (if there is such a block).
        ///// </summary>
        ///// <param name="batch"></param>
        ///// <returns></returns>
        // bool TryMoveNextBatch(out Span<TKey> batch);
    }

    /// <summary>
    /// A specialized <see cref="ICursor{TKey,TValue}" /> with a known implementation type.
    /// </summary>
    public interface ICursor<TKey, TValue, TCursor> : ICursor<TKey, TValue>
        where TCursor : ICursor<TKey, TValue, TCursor>
    {
        /// <summary>
        /// Returns an initialized (ready to move) instance of <typeparamref name="TCursor"/>.
        /// It could be the same instance for <see cref="Series{TKey,TValue,TCursor}"/>.
        /// It is the equivalent to calling the method <see cref="ISeries{TKey,TValue}.GetCursor"/> on <see cref="ICursor{TKey,TValue}.Source"/> for the non-specialized ICursor.
        /// </summary>
        /// <remarks>
        /// This method must work on disposed instances of <see cref="ICursor{TKey,TValue,TCursor}"/>, i.e. it acts as a factory.
        /// </remarks>
        [Pure]
        TCursor Initialize();

        /// <summary>
        /// Copy this cursor and move the copy to the same <see cref="ICursor{TKey,Tvalue}.CurrentKey"/> as this cursor.
        /// </summary>
        new TCursor Clone();

        /// <summary>
        /// Same as <see cref="ISeries{TKey,TValue}.IsCompleted"/>
        /// </summary>
        bool IsCompleted { get; }

        IAsyncCompleter AsyncCompleter { get; }

        new Series<TKey, TValue, TCursor> Source { get; }
    }

    /// <summary>
    /// Series are navigable ordered data streams of key-value pairs.
    /// </summary>
    public interface ISeries<TKey, TValue> : IDataSource, IAsyncEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
    {
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
        /// Optimized access to last value. For value types <see cref="IDataSource.IsEmpty"/> could be used to distinguish between a present default value and a missing value.
        /// </summary>
        /// <remarks>
        /// <see cref="Last"/> property is convenient but slowish in hot loops.
        /// </remarks>
        [Obsolete("TODO implement on Series, not on the interface. Check type of interface, use Opt Last if not series.")]
        TValue LastValueOrDefault { get; }

        /// <summary>
        /// Keys enumerable.
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        /// <summary>
        /// Values enumerable.
        /// </summary>
        IEnumerable<TValue> Values { get; }

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
        /// is LINQ <code>[series].Skip(idx-1).Take(1).Value</code>.
        /// </summary>
        bool TryGetAt(long index, out KeyValuePair<TKey, TValue> kvp);

        /// <summary>
        /// The method finds value according to direction, returns false if it could not find such a value.
        /// For indexed series LE/GE directions are invalid and throw InvalidOperationException, while
        /// LT/GT search is done by index rather than by key and is possible only when a key exists.
        /// TryFindAt works only with observed keys and is a series counterpart of <see cref="ICursor{TKey,TValue}.MoveAt"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The <paramref name="direction">direction</paramref> is <see cref="Lookup.LE"/> or <see cref="Lookup.GE"/> for not sorted series (<see cref="KeySorting.NotSorted"/>). </exception>
        bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp);

        /// <summary>
        /// Get <see cref="ICursor{TKey,TValue}"/>, which is an advanced enumerator supporting moves to first, last, previous, next, exact
        /// positions and relative LT/LE/GT/GE (<see cref="Lookup"/>) moves.
        /// </summary>
        ICursor<TKey, TValue> GetCursor();
    }

    /// <summary>
    /// A series with a known strongly typed cursor type.
    /// </summary>
    public interface ISeries<TKey, TValue, out TCursor> : ISeries<TKey, TValue>
        where TCursor : ICursor<TKey, TValue, TCursor>
    {
        /// <summary>
        /// Get a strongly typed cursor that implements the <see cref="ICursor{TKey,TValue,TCursor}"/> interface.
        /// </summary>
        /// <returns></returns>
        new TCursor GetCursor();
    }

    /// <summary>
    /// Append series.
    /// </summary>
    public interface IAppendSeries<TKey, TValue> : ISeries<TKey, TValue>
    {
        /// <summary>
        /// Attempts to add the specified key and value to the end of the series.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">
        /// The value of the element to add. The value can be null for reference types.
        /// </param>
        /// <returns>
        /// True on successful addition. False if the <paramref name="key"/>
        /// is null or breaks sorting order or series <see cref="IDataSource.Mutability"/>
        /// is <see cref="Mutability.ReadOnly"/>.
        /// </returns>
        bool TryAppend(TKey key, TValue value);

        /// <summary>
        /// Adds key and value to the end of the series.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">
        /// The value of the element to add. The value can be null for reference types.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="key"/> is null or breaks sorting order.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Series <see cref="IDataSource.Mutability"/> is <see cref="Mutability.ReadOnly"/>.
        /// </exception>
        void Append(TKey key, TValue value);

        bool TryAppend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>;

        void Append<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>;

        /// <summary>
        /// Change <see cref="Mutability"/> of this series to <see cref="Mutability.ReadOnly"/>.
        /// If the current mutability is already <see cref="Mutability.ReadOnly"/> then this is noop.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Mutability"/> of this series is already <see cref="Mutability.ReadOnly"/></exception>
        void MarkReadOnly();
    }

    /// <summary>
    /// Mutable series.
    /// </summary>
    public interface IMutableSeries<TKey, TValue> : IAppendSeries<TKey, TValue>
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <seealso cref="ISeries{TKey, TValue}.TryGetValue"/>
        /// <seealso cref="Set"/>
        new TValue this[TKey key] { get; set; }

        /// <summary>
        /// Sets the specified value at the key. Returns true if key did not exist before.
        /// </summary>
        bool Set(TKey key, TValue value);

        void Set<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>;

        bool TryAdd(TKey key, TValue value);

        void Add(TKey key, TValue value);

        bool TryAdd<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>;

        void Add<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>;

        bool TryPrepend(TKey key, TValue value);

        void Prepend(TKey key, TValue value);

        bool TryPrepend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>;

        void Prepend<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>;

        /// <summary>
        ///
        /// </summary>
        /// <returns>Returns true and the value removed at the specified key on success
        /// or false if the key does not exists.</returns>
        bool TryRemove(TKey key, out TValue value);

        /// <summary>
        /// Attempts to remove the first key/value pair from a series.
        /// </summary>
        /// <returns>Returns true and the removed first element if the series was not
        /// empty before calling this method. Returns false otherwise.</returns>
        bool TryRemoveFirst(out KeyValuePair<TKey, TValue> pair);

        /// <summary>
        /// Attempts to remove the last key/value pair from a series.
        /// </summary>
        /// <returns>Returns true and the removed last element if the series was not
        /// empty before calling this method. Returns false otherwise.</returns>
        bool TryRemoveLast(out KeyValuePair<TKey, TValue> pair);

        /// <summary>
        /// Removes all keys from the specified key towards the specified direction and returns the nearest removed key/value pair.
        /// </summary>
        bool TryRemoveMany(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> pair);

        /// <summary>
        /// Change <see cref="Mutability"/> of this series to <see cref="Mutability.AppendOnly"/>.
        /// If the current mutability is already <see cref="Mutability.AppendOnly"/> then this is noop.
        /// If the current mutability is <see cref="Mutability.ReadOnly"/> then this methods throws <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Mutability"/> of this series is already <see cref="Mutability.ReadOnly"/></exception>
        void MarkAppendOnly();
    }
}
