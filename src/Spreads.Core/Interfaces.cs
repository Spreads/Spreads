using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Experimental {

    // TODO a lightweight struct for a single value IEnumerable

    // TODO GetPublisher

    public interface IAsyncEnumerator<out T> : IEnumerator<T> {
        Task<bool> MoveNextAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

    public interface IAsyncEnumerable<out T> : IEnumerable<T> {
        new IAsyncEnumerator<T> GetEnumerator();
    }

    public interface IPublisher<out T> : IObservable<T> {
        new ISubscription Subscribe(IObserver<T> subscriber);
    }

    public interface ISubscriber<in T> : IObserver<IEnumerable<T>> {
        //void OnSubscribe(ISubscription s);
        //void OnCompleted();
        //void OnError(Exception error);
        //void OnNext(T value);
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

    public interface IDataStream<T> : ISeries<long, T> { }

    /// <summary>
    /// A Processor represents a processing stage—which is both a Subscriber
    /// and a Publisher
    /// and obeys the contracts of both.
    /// </summary>
    /// <typeparam name="TIn">the type of element signaled to the Subscriber</typeparam>
    /// <typeparam name="TOut">the type of element signaled by the Publisher</typeparam>
    public interface IProcessor<in TIn, out TOut> : ISubscriber<TIn>, IPublisher<TOut> {

    }


    public interface ICursor<TKey, TValue> : IAsyncEnumerator<KeyValuePair<TKey, TValue>>, ISubscriber<KeyValuePair<TKey, TValue>> {
        IComparer<TKey> Comparer { get; }
        IReadOnlyOrderedMap<TKey, TValue> CurrentBatch { get; }
        TKey CurrentKey { get; }
        TValue CurrentValue { get; }
        bool IsContinuous { get; }
        ISeries<TKey, TValue> Source { get; }

        ICursor<TKey, TValue> Clone();
        bool MoveAt(TKey key, Lookup direction);
        bool MoveFirst();
        bool MoveLast();
        Task<bool> MoveNextBatch(CancellationToken cancellationToken);
        bool MovePrevious();
        bool TryGetValue(TKey key, out TValue value);
    }


    public interface ISeries<TKey, TValue> : IAsyncEnumerable<KeyValuePair<TKey, TValue>> {
        bool IsIndexed { get; }
        bool IsMutable { get; }
        object SyncRoot { get; }

        ICursor<TKey, TValue> GetCursor();

        // we could observe series from any any starting point to any direction
        // Cursor should implement IPublisher, or IPublisher should be based on a cursor

        IPublisher<KeyValuePair<TKey, TValue>> Observe(TKey from, Lookup direction);
    }

    public interface IReadOnlyOrderedMap<K, V> : ISeries<K, V> {
        V this[K value] { get; }

        IComparer<K> Comparer { get; }
        KeyValuePair<K, V> First { get; }
        bool IsEmpty { get; }
        IEnumerable<K> Keys { get; }
        KeyValuePair<K, V> Last { get; }
        IEnumerable<V> Values { get; }

        V GetAt(int idx);
        bool TryFind(K key, Lookup direction, out KeyValuePair<K, V> value);
        bool TryGetFirst(out KeyValuePair<K, V> value);
        bool TryGetLast(out KeyValuePair<K, V> value);
        bool TryGetValue(K key, out V value);
    }




    
    // ToSeries() as ToDictionary with T => TKey, T => TValue

    //public abstract class BasePublisher<T> : IPublisher<T> {
    //    public abstract ISubscription Subscribe(IObserver<IEnumerable<T>> subscriber);
    //    IDisposable IObservable<IEnumerable<T>>.Subscribe(IObserver<IEnumerable<T>> observer) {
    //        return Subscribe(observer);
    //    }
    //}

    

    //public abstract class BaseSubscriber<T> : ISubscriber<T> {
    //    public abstract void OnCompleted();
    //    public abstract void OnError(Exception error);
    //    public abstract void OnNext(IEnumerable<T> value);
    //    public abstract void OnSubscribe(ISubscription s);
    //}

    

    //public abstract class BaseSubscription : ISubscription {
    //    public abstract void Request(long n);
    //    public abstract void Cancel();
    //    public virtual void Dispose() {
    //        Cancel();
    //    }
    //}

    


    //public abstract class BaseProcessor<TIn, TOut> : IProcessor<TIn, TOut> {
    //    public abstract void OnCompleted();
    //    public abstract void OnError(Exception error);
    //    public abstract void OnNext(IEnumerable<TIn> value);
    //    public abstract void OnSubscribe(ISubscription s);
    //    public abstract ISubscription Subscribe(IObserver<IEnumerable<TOut>> subscriber);
    //    IDisposable IObservable<IEnumerable<TOut>>.Subscribe(IObserver<IEnumerable<TOut>> observer) {
    //        return Subscribe(observer);
    //    }
    //}



}
