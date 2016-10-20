/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Spreads.DataTypes;

namespace Spreads {


    /// <summary>
    /// Extends <c>IEnumerator[out T]</c> to support asynchronous MoveNext with cancellation.
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
    public interface IAsyncEnumerator<out T> : IEnumerator<T> {
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


    // Convenience aliases, no need to pollute interfaces
    public static class AsyncEnumeratorExtensions {
        public static Task<bool> MoveNextAsync<T>(this IAsyncEnumerator<T> enumerator) {
            return enumerator.MoveNext(CancellationToken.None);
        }

        public static Task<bool> MoveNextAsync<T>(this IAsyncEnumerator<T> enumerator, CancellationToken cancellationToken) {
            return enumerator.MoveNext(cancellationToken);
        }
    }

    /// <summary>
    /// Exposes the async enumerator, which supports a sync and async iteration over a collection of a specified type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAsyncEnumerable<out T> : IEnumerable<T> {
        /// <summary>
        /// Returns an async enumerator.
        /// </summary>
        new IAsyncEnumerator<T> GetEnumerator();
    }



    public interface ISubscription : IDisposable {
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


    public interface ISubscriber<in T> : IObserver<T> {
        void OnSubscribe(ISubscription s);
        //void OnCompleted();
        //void OnError(Exception error);
        //void OnNext(T value);
    }


    public interface IPublisher<out T> : IObservable<T> {
        //[Obsolete("Use typecheck in implementations")]
        //new ISubscription Subscribe(IObserver<T> subscriber);
    }

    public interface IDataStream<T> : IAsyncEnumerable<T>, IPublisher<T> { }


    /// <summary>
    /// A Processor represents a processing stage—which is both a Subscriber
    /// and a Publisher
    /// and obeys the contracts of both.
    /// </summary>
    /// <typeparam name="TIn">the type of element signaled to the Subscriber</typeparam>
    /// <typeparam name="TOut">the type of element signaled by the Publisher</typeparam>
    public interface IProcessor<in TIn, out TOut> : ISubscriber<TIn>, IPublisher<TOut> {

    }


    /// <summary>
    /// Main interface for data series.
    /// </summary>
    public interface ISeries<TKey, TValue>
        : IPublisher<KeyValuePair<TKey, TValue>>, IAsyncEnumerable<KeyValuePair<TKey, TValue>> {

        /// <summary>
        /// If true then elements are placed by some custom order (e.g. order of addition, index) and not sorted by keys.
        /// </summary>
        bool IsIndexed { get; }

        /// <summary>
        /// False if the underlying collection could be changed, true if the underlying collection is immutable or is complete 
        /// for adding (e.g. after OnCompleted in Rx) or IsReadOnly in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Key comparer.
        /// </summary>
        IComparer<TKey> Comparer { get; }

        /// <summary>
        /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact 
        /// positions and relative LT/LE/GT/GE moves.
        /// </summary>
        ICursor<TKey, TValue> GetCursor();

        [Obsolete]
        object SyncRoot { get; }
    }


    public interface ISeries : ISeries<Variant, Variant> {

    }


    /// <summary>
    /// ICursor is an advanced enumerator that supports moves to first, last, previous, next, next batch, exact 
    /// positions and relative LT/LE/GT/GE moves.
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
    /// </summary>
    public interface ICursor<TKey, TValue>
        : IAsyncEnumerator<KeyValuePair<TKey, TValue>> {

        IComparer<TKey> Comparer { get; }

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
        IReadOnlyOrderedMap<TKey, TValue> CurrentBatch { get; }

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
        /// Create a copy of cursor that is positioned at the same place as this cursor.
        /// </summary>
        ICursor<TKey, TValue> Clone();

        /// <summary>
        /// Gets a calculated value for continuous series without moving the cursor position.
        /// E.g. a continuous cursor for Repeat() will check if current state allows to get previous value,
        /// and if not then .Source.GetCursor().MoveAt(key, LE). The TryGetValue method should be optimized
        /// for sort join case using enumerator, e.g. for repeat it should keep previous value and check if 
        /// the requested key is between the previous and the current keys, and then return the previous one.
        /// NB This is not thread safe. ICursors must be used from a single thread.
        /// </summary>
        bool TryGetValue(TKey key, out TValue value);
    }



    /// <summary>
    /// NB! 'Read-only' doesn't mean that the object is immutable or not changing. It only means
    /// that there is no methods to change the map *from* this interface, without any assumptions about 
    /// the implementation. Underlying sequence could be mutable and rapidly changing; to prevent any 
    /// changes use lock (Monitor.Enter) on the SyncRoot property. Doing so will block any changes for 
    /// mutable implementations and won't affect immutable implementations.
    /// </summary>
    public interface IReadOnlyOrderedMap<TKey, TValue> : ISeries<TKey, TValue> {

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
        TValue this[TKey value] { get; }

        /// <summary>
        /// Value at index (offset). Implemented efficiently for indexed series and SortedMap, but default implementation
        /// is Linq's [series].Skip(idx-1).Take(1).Value
        /// </summary>
        TValue GetAt(int idx);

        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }

        /// <summary>
        /// The method finds value according to direction, returns false if it could not find such a value
        /// For indexed series LE/GE directions are invalid (throws InvalidOperationException), while
        /// LT/GT search is done by index rather than by key and possible only when a key exists.
        /// </summary>
        bool TryFind(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value);
        bool TryGetFirst(out KeyValuePair<TKey, TValue> value);
        bool TryGetLast(out KeyValuePair<TKey, TValue> value);
        bool TryGetValue(TKey key, out TValue value);
    }


    /// <summary>
    /// Signaling event handler.
    /// </summary>
    /// <param name="flag"></param>
    internal delegate void OnUpdateHandler(bool flag);
    internal interface IUpdateable {
        event OnUpdateHandler OnUpdate;
    }
}
