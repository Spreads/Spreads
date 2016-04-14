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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads {

    // TODO ISeriesSegment that implements IReadOnlyCollection 


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

    /// <summary>
    /// Exposes the async enumerator, which supports a sync and async iteration over a collection of a specified type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAsyncEnumerable<out T> : IEnumerable<T> {
        /// <summary>
        /// Returns an async enumerator.
        /// </summary>
        IAsyncEnumerator<T> GetEnumerator();
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

    // ISeriesSubscription Request method overloads are duals to cursor move methods
    // MoveNext()       -> Request(1)
    // MovePrevious()   -> Request(1, Lookup.LE/LT)
    // MoveFirst()      -> Request(1, firstKey, Lookup.GE) // first/last keys could be known from cursor
    // MoveLast()       -> Request(1, lastKey, Lookup.LE)
    // MoveAt(key, direction) -> Request(1, key, direction)

    // NB: this could be done via Range, Reverse, etc. We need async only for incoming data (forward move), for all other cases we could use cursors
    //public interface ISeriesSubscription<TKey> : ISubscription {
    //    void Request(long n, TKey from);
    //    void Request(long n, TKey from, Lookup direction);
    //    void Request(long n, Lookup direction);
    //}
    //public interface ISeriesSubscriber<TKey, TValue> : ISubscriber<KeyValuePair<TKey, TValue>> {
    //    void OnSubscribe(ISeriesSubscription<TKey> s);
    //}

    public interface ISubscriber<in T> : IObserver<T> {
        void OnSubscribe(ISubscription s);
        //void OnCompleted();
        //void OnError(Exception error);
        //void OnNext(T value);
    }

    
    public interface IPublisher<out T> : IObservable<T> {
        // We do not need to expose ISubscription to publisher, only subscriber could request new data
        // However, publisher could cancel a subscription via Dispose()
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

    // TODO This is not final. I am not sure that ICursor should implement ISeriesSubscriber
    // or ISeriesSubscription, we probably need some composition not inheritance
    //public interface ICursor<TKey, TValue>
    //    : IAsyncEnumerator<KeyValuePair<TKey, TValue>>, ISeriesSubscriber<TKey, TValue>, ISeriesSubscription<TKey> {

    //    IComparer<TKey> Comparer { get; }
    //    IReadOnlyOrderedMap<TKey, TValue> CurrentBatch { get; }
    //    TKey CurrentKey { get; }
    //    TValue CurrentValue { get; }
    //    bool IsContinuous { get; }
    //    ISeries<TKey, TValue> Source { get; }

    //    ICursor<TKey, TValue> Clone();
    //    bool MoveAt(TKey key, Lookup direction);
    //    bool MoveFirst();
    //    bool MoveLast();
    //    Task<bool> MoveNextBatch(CancellationToken cancellationToken);
    //    bool MovePrevious();
    //    bool TryGetValue(TKey key, out TValue value);
    //}


    //public interface ISeries<TKey, TValue> : IPublisher<KeyValuePair<TKey, TValue>> {
    //    bool IsIndexed { get; }
    //    bool IsMutable { get; }
    //    object SyncRoot { get; }

    //    ICursor<TKey, TValue> GetCursor();

    //    // IDisposable Subscribe(IObserver<T> observer);
    //}

    //public interface IReadOnlyOrderedMap<K, V> : ISeries<K, V> {
    //    V this[K value] { get; }

    //    IComparer<K> Comparer { get; }
    //    KeyValuePair<K, V> First { get; }
    //    bool IsEmpty { get; }
    //    IEnumerable<K> Keys { get; }
    //    KeyValuePair<K, V> Last { get; }
    //    IEnumerable<V> Values { get; }

    //    V GetAt(int idx);
    //    bool TryFind(K key, Lookup direction, out KeyValuePair<K, V> value);
    //    //bool TryGetFirst(out KeyValuePair<K, V> value);
    //    //bool TryGetLast(out KeyValuePair<K, V> value);
    //    bool TryGetValue(K key, out V value);
    //}

    internal delegate void OnUpdateHandler(bool flag);
    internal interface IUpdateable {
        event OnUpdateHandler OnUpdate;
    }

    internal delegate void OnNextHandler<K, V>(KeyValuePair<K, V> kvp);
    internal delegate void OnCompletedHandler(bool isComplete);
    internal delegate void OnErrorHandler(Exception exception);

    internal interface IObservableEvents<K, V> {
        event OnNextHandler<K, V> OnNext;
        event OnCompletedHandler OnComplete;
        event OnErrorHandler OnError;
    }


}
