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
    // TODO move to a folder Interfaces without namespace

    /// <summary>
    /// Extends <see cref="IEnumerator{T}"/> to support asynchronous MoveNext with cancellation.
    /// </summary>
    /// <remarks>
    /// Contract: when MoveNext() returns false it means that there are no more elements
    /// right now, and a consumer should call MoveNextAsync() to await for a new element, or spin
    /// and repeatedly call MoveNext() when a new element is expected very soon. Repeated calls to MoveNext()
    /// could eventually return true. Changes to the underlying sequence, which do not affect enumeration,
    /// do not invalidate the enumerator.
    ///
    /// <c>Current</c> property follows the parent contracts as described here: https://msdn.microsoft.com/en-us/library/58e146b7(v=vs.110).aspx
    /// Some implementations guarantee that <c>Current</c> keeps its last value from successfull MoveNext(),
    /// but that must be explicitly stated in a data structure documentation (e.g. SortedMap).
    /// </remarks>
    public interface IAsyncEnumerator<out T> : IEnumerator<T>
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
        Task<bool> MoveNext(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Convenience extensions to <see cref="IAsyncEnumerator{T}"/>.
    /// </summary>
    public static class AsyncEnumeratorExtensions
    {
        /// <summary>
        /// An alias to <see cref="IAsyncEnumerator{T}.MoveNext(CancellationToken)"/> method with <see cref="CancellationToken.None"/>.
        /// </summary>
        public static Task<bool> MoveNextAsync<T>(this IAsyncEnumerator<T> enumerator)
        {
            return enumerator.MoveNext(CancellationToken.None);
        }

        /// <summary>
        /// An alias to <see cref="IAsyncEnumerator{T}.MoveNext(CancellationToken)"/> method.
        /// </summary>
        public static Task<bool> MoveNextAsync<T>(this IAsyncEnumerator<T> enumerator, CancellationToken cancellationToken)
        {
            return enumerator.MoveNext(cancellationToken);
        }
    }

    /// <summary>
    /// Exposes the <see cref="IAsyncEnumerator{T}"/> async enumerator, which supports a sync and async iteration over a collection of a specified type.
    /// </summary>
    public interface IAsyncEnumerable<out T> : IEnumerable<T>
    {
        /// <summary>
        /// Returns an async enumerator.
        /// </summary>
        new IAsyncEnumerator<T> GetEnumerator();
    }

    public interface ISubscription : IDisposable
    {
        /// <summary>
        /// No events will be sent by a Publisher until demand is signaled via this method.
        ///
        /// It can be called however often and whenever needed — but the outstanding cumulative demand must never exceed long.MaxValue.
        /// An outstanding cumulative demand of long.MaxValue may be treated by the Publisher as "effectively unbounded".
        ///
        /// Whatever has been requested can be sent by the Publisher so only signal demand for what can be safely handled.
        ///
        /// A Publisher can send less than is requested if the stream ends but then must emit either Subscriber.OnError(Throwable)}
        /// or Subscriber.OnCompleted().
        /// </summary>
        /// <param name="n">the strictly positive number of elements to requests to the upstream Publisher</param>
        void Request(long n);

        // NB Java doesn't have IDisposable and have to reinvent the pattern every time. Here we use Dispose() for original reactive streams Cancel().
        // <summary>
        // Request the Publisher to stop sending data and clean up resources.
        // Data may still be sent to meet previously signalled demand after calling cancel as this request is asynchronous.
        // </summary>
        //void Dispose();
    }

    public interface ISubscriber<in T> : IObserver<T>
    {
        void OnSubscribe(ISubscription s);

        //void OnCompleted();
        //void OnError(Exception error);
        //void OnNext(T value);
    }

    public interface IPublisher<out T> : IObservable<T>
    {
        //[Obsolete("Use typecheck in implementations")]
        //new ISubscription Subscribe(IObserver<T> subscriber);
    }

    // TODO see issue https://github.com/Spreads/Spreads/issues/99

    /// <summary>
    /// An <see cref="IAsyncEnumerable{KeyValuePair}"/> and <see cref="IPublisher{KeyValuePair}"/> with additional guarantee
    /// that items are ordered by <typeparamref name="TKey"/>.
    /// </summary>
    public interface IDataStream<TKey, TValue> : IAsyncEnumerable<KeyValuePair<TKey, TValue>>,
        IPublisher<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// Key comparer.
        /// </summary>
        KeyComparer<TKey> Comparer { get; }
    }

    /// <summary>
    /// A Processor represents a processing stage — which is both a <see cref="ISubscriber{T}"/>
    /// and a <see cref="IPublisher{T}"/>
    /// and obeys the contracts of both.
    /// </summary>
    /// <typeparam name="TIn">The type of element signaled to the <see cref="ISubscriber{T}"/></typeparam>
    /// <typeparam name="TOut">The type of element signaled by the <see cref="IPublisher{T}"/></typeparam>
    public interface IProcessor<in TIn, out TOut> : ISubscriber<TIn>, IPublisher<TOut>
    {
    }

    /// <summary>
    /// Main interface for data series.
    /// </summary>
    public interface ISeries<TKey, TValue> : IDataStream<TKey, TValue>
    {
        /// <summary>
        /// If true then elements are placed by some custom order (e.g. order of addition, index) and not sorted by keys.
        /// </summary>
        bool IsIndexed { get; }

        // TODO! Rename to IsComplete, see #102
        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete
        /// for adding (e.g. after OnCompleted in Rx) or IsReadOnly in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact
        /// positions and relative LT/LE/GT/GE moves.
        /// </summary>
        ICursor<TKey, TValue> GetCursor();

        /// <summary>
        /// A Task that is completed with True whenever underlying data is changed.
        /// Internally used for signaling to async cursors.
        /// After getting the Task one should check if any changes happened (version change or cursor move) before awating the task.
        /// If the task is completed with false then the series is read-only, immutable or complete.
        /// </summary>
        Task<bool> Updated { get; }
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
    /// IsBatch property indicates wether the cursor is positioned on a single value or a batch.
    ///
    /// Contracts:
    /// 1. At the beginning a cursor consumer could call any single move method or MoveNextBatch. MoveNextBatch could
    ///    be called only on the initial move or after a previous MoveNextBatch() call that returned true. It MUST NOT
    ///    be called in any other situation, ICursor implementations MUST return false on any such wrong call.
    /// 2. CurrentBatch contains a batch only after a call to MoveNextBatch() returns true. CurrentBatch is undefined
    ///    in all other cases.
    /// 3. After a call to MoveNextBatch() returns false, the consumer MUST use only single calls. ICursor implementations MUST
    ///    ensure that the relative moves MoveNext/Previous start from the last position of the previous batch.
    /// 4. Synchronous moves return true if data is instantly awailable, e.g. in a map data structure in memory or on fast disk DB.
    ///    ICursor implementations should not block threads, e.g. if a map is IUpdateable, synchronous MoveNext should not wait for
    ///    an update but return false if there is no data right now.
    /// 5. When synchronous MoveNext or MoveLast return false, the consumer should call async overload of MoveNext. Inside the async
    ///    implementation of MoveNext, a cursor must check if the source is IUpdateable and return Task.FromResult(false) immediately if it is not.
    /// 6. When any move returns false, cursor stays at the position before that move (TODO now this is ensured only for SM MN/MP and for Bind(ex.MA) )
    /// _. TODO If the source is updated during a lifetime of a cursor, cursor must recreate its state at its current position
    ///    Rewind logic only for async? Throw in all cases other than MoveNext, MoveAt? Or at least on MovePrevious.
    ///    Or special behaviour of MoveNext only on appends or changing the last value? On other changes must throw invalidOp (locks are there!)
    ///    So if update is before the current position of a cursor, then throw. If after - then this doesn't affect the cursor in any way.
    ///    TODO cursor could implement IUpdateable when source does, or pass through to CursorSeries
    ///
    /// </remarks>
    public interface ICursor<TKey, TValue>
        : IAsyncEnumerator<KeyValuePair<TKey, TValue>>
    {
        // TODO add CursorState. For non-CursorSeries the state is never None after creation - created ones are already initialized. What about disposed ones?

        KeyComparer<TKey> Comparer { get; }

        /// <summary>
        /// Puts the cursor to the position according to LookupDirection
        /// </summary>
        bool MoveAt(TKey key, Lookup direction);

        bool MoveFirst();

        bool MoveLast();

        bool MovePrevious();

        TKey CurrentKey { get; }
        TValue CurrentValue { get; }

        /// <summary>
        /// Optional (used for batch/SIMD optimization where gains are visible), MUST NOT throw NotImplementedException()
        /// Returns true when a batch is available immediately (async for IO, not for waiting for new values),
        /// returns false when there is no more immediate values and a consumer should switch to MoveNextAsync().
        /// </summary>
        Task<bool> MoveNextBatch(CancellationToken cancellationToken);

        /// <summary>
        /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
        /// The actual implementation of the batch could be mutable and could reference a part of the original series, therefore consumer
        /// should never try to mutate the batch directly even if type check reveals that this is possible, e.g. it is a SortedMap
        /// </summary>
        IReadOnlySeries<TKey, TValue> CurrentBatch { get; }

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
        bool TryGetValue(TKey key, out TValue value);
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
        KeyValuePair<TKey, TValue> First { get; }

        /// <summary>
        /// Last element, throws InvalidOperationException if empty
        /// </summary>
        KeyValuePair<TKey, TValue> Last { get; }

        /// <summary>
        /// Value at key, throws KeyNotFoundException if key is not present in the series (even for continuous series).
        /// Use TryGetValue to get a value between existing keys for continuous series.
        /// </summary>
        TValue this[TKey key] { get; }

        /// <summary>
        /// Value at index (offset). Implemented efficiently for indexed series and SortedMap, but default implementation
        /// is Linq's <code>[series].Skip(idx-1).Take(1).Value</code> .
        /// </summary>
        [Obsolete("Signature without TKey doesn't make sense. Only concrete series where optimization of this method works should have it, not the interface.")]
        TValue GetAt(int idx);

        // TODO should be KeyValuePair<TKey, TValue> GetAt(int idx) or completely removed.

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
        bool TryFind(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value);

        /// <summary>
        /// Try get first element.
        /// </summary>
        bool TryGetFirst(out KeyValuePair<TKey, TValue> value);

        /// <summary>
        /// Try get last element.
        /// </summary>
        bool TryGetLast(out KeyValuePair<TKey, TValue> value);
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
    /// An untyped <see cref="ISeries{DateTime, TValue}"/> interface with values as <see cref="Variant"/> types.
    /// </summary>
    public interface ITimeSeries : ISeries<DateTime, Variant>
    {
        /// <summary>
        /// <see cref="TypeEnum"/> for the values type.
        /// </summary>
        TypeEnum ValueType { get; }

        /// <summary>
        /// TimeSeries parameters.
        /// </summary>
        TimeSeriesInfo TimeSeriesInfo { get; }
    }

    /// <summary>
    /// An untyped <see cref="IReadOnlySeries{DateTime, TValue}"/> interface with values as <see cref="Variant"/> types.
    /// </summary>
    public interface IReadOnlyTimeSeries : ITimeSeries, IReadOnlySeries<DateTime, Variant>
    {
    }

    /// <summary>
    /// Mutable series
    /// </summary>
    public interface IMutableSeries<TKey, TValue> : IReadOnlySeries<TKey, TValue> //, IDictionary<TKey, TValue>
    {
        long Count { get; }

        /// <summary>
        /// Incremented after any change to data, including setting of the same value to the same key
        /// </summary>
        long Version { get; }

        new TValue this[TKey key] { get; set; }

        /// <summary>
        /// Adds new key and value to map, throws if the key already exists
        /// </summary>
        void Add(TKey key, TValue value);

        /// <summary>
        /// Checked addition, checks that new element's key is larger/later than the Last element's key
        /// and adds element to this map
        /// throws ArgumentOutOfRangeException if new key is smaller than the last
        /// </summary>
        void AddLast(TKey key, TValue value);

        /// <summary>
        /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
        /// and adds element to this map
        /// throws ArgumentOutOfRangeException if new key is larger than the first
        /// </summary>
        void AddFirst(TKey key, TValue value);

        bool Remove(TKey key);

        bool RemoveLast(out KeyValuePair<TKey, TValue> kvp);

        bool RemoveFirst(out KeyValuePair<TKey, TValue> kvp);

        bool RemoveMany(TKey key, Lookup direction);

        /// <summary>
        /// And values from appendMap to the end of this map
        /// </summary>
        int Append(IReadOnlySeries<TKey, TValue> appendMap, AppendOption option); // TODO int, bool option for ignoreEqualOverlap, or Enum with 1: thow, 2: ignoreEqual, 3: rewriteOld, 4: ignoreNew (nonsense option, should not do, the first 3 are good)

        /// <summary>
        /// Make the map read-only and disable all Add/Remove/Set methods (they will throw)
        /// </summary>
        void Complete();
    }

    /// <summary>
    /// `Flush` has a standard meaning, e.g. as in Stream, and saves all changes. `Dispose` calls `Flush`. `Id` is globally unique.
    /// </summary>
    public interface IPersistentObject : IDisposable
    {
        /// <summary>
        /// Persist any cached data.
        /// </summary>
        void Flush();

        /// <summary>
        /// String identificator of series.
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

    /// <summary>
    /// Time series
    /// </summary>
    public interface ITimeSeries<TKey, TValue> : IReadOnlySeries<TKey, TValue>
    {
        // NB we do not restrict TKey to DateTime, it could be long or whatever, depending on context
        // For storage we just require that TKey must have KeyComparer, i.e. strictly monotonic conversion to long
        UnitPeriod UnitPeriod { get; }

        int PeriodCount { get; }
        string TimeZone { get; }
    }

    public interface ICloneable<out T> where T : ICloneable<T>
    {
        T DeepCopy();
    }

    /// <summary>
    /// Signaling event handler.
    /// </summary>
    /// <param name="flag"></param>
    [Obsolete("This should no longer be used")]
    internal delegate void OnUpdateHandler(bool flag);

    [Obsolete("This should no longer be used")]
    internal interface IUpdateable
    {
        event OnUpdateHandler OnUpdate;
    }

    internal interface IMutableChunksSeries<TKey, TValue, TContainer> : IReadOnlySeries<TKey, TContainer>, IPersistentObject
        where TContainer : IMutableSeries<TKey, TValue>
    {
        /// <summary>
        /// Keep the key chunk if it is not empty, remove all other chunks to the direction side, update version from the key chunk
        /// </summary>
        bool RemoveMany(TKey key, TContainer keyChunk, Lookup direction);

        new TContainer this[TKey key] { get; set; }
        long Version { get; }
    }
}