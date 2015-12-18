#### Note: Observable/Observer is not implemented yet.

#Spreads

**Spreads** stands for **S**eries and **P**anels for **R**ealtime and **E**xploratory **A**nalysis of 
**D**ata **S**treams.

+ **Data streams** are endless sequences of any items, either recorded or 
arriving in real-time;
+ **Series** are navigable ordered data streams of key-value pairs or key-value mappings;
+ **Panels** are series of series or data frames;
+ **Exploratory** data manupulation in C#/F# REPLs;
+ **Reactive** fast incremental calculations in real-time.



## Design principles

+ #MechanicalSympathy
+ Lazy (unless it hurts performance, but usually it helps)
+ Declarative functional transformations (with access to low-level details when needed)
+ Async or reactive pull model for series, no blocking code
+ Missing values are really missing, not present as "missing" or N/A.
+ Benchmark every change


## Design overview

### Data streams
Data streams are both enumerable and observable. [Erik Meijer](https://twitter.com/headinthebox) 
[explains](http://csl.stanford.edu/~christos/pldi2010.fit/meijer.duality.pdf) [that](https://channel9.msdn.com/Events/Lang-NEXT/Lang-NEXT-2014/Keynote-Duality): 
"Not only are pull-based enumerable streams and push-based observable streams 
each other's dual, they are in fact isomorphic." However, native push-based sequences
do not support backpressure. We took the idea of reactive pull implementation from
[Reactive Streams](https://github.com/reactive-streams/reactive-streams-jvm/blob/master/api/src/main/java/org/reactivestreams/Publisher.java)
projects and adapted it to existing .NET interfaces:

    public interface IPublisher<out T> : IObservable<T> {
        new ISubscription Subscribe(IObserver<T> subscriber);
    }

    public interface ISubscription : IDisposable {
        void Request(long n);

        // Cancels subscriptions, inherited from IDisposable
        // void Dispose();
    }

To support pull-based real-time series, we need an enumerator that 
could wait for new data without blocking a thread:

    public interface IAsyncEnumerator<out T> : IEnumerator<T> {
            Task<bool> MoveNext(CancellationToken cancellationToken);
    }

It extends the syncronous enumerator interface. In Spreads we define the behavior
of the async enumerator in the following way:

 > Contract: when MoveNext() returns false it means that there are no more elements 
right now, and a consumer should call MoveNextAsync() to await for a new element,
or spin and repeatedly call MoveNext() when a new element is expected very soon.
 Repeated calls to MoveNext() could eventually return true. Changes to the underlying 
sequence, which do not affect enumeration, do not invalidate the enumerator.

With this async enumerator we define an async enumerable interface:

    public interface IAsyncEnumerable<out T> : IEnumerable<T> {
        new IAsyncEnumerator<T> GetEnumerator();
    }

Now we could define a data stream as:

    public interface IDataStream<T> : IAsyncEnumerable<T>, IPublisher<T> { }

It support both pull/push model, but due to duality applied by Erik Meijer to these 
concepts we do not have an opinion which one is better. It is just a different view
on the same data, and data stream concumers should decide themselves which model to use.


### Series
Series are navigable ordered data streams of key-value pairs or key-value mappings.
To support navigation and key-value mappings, we need to extend the enumerator interface
and work with a cursor that could move freely on a series by adding directional 
`Move` methods. These method pull data from series.

To support a dual push behavior, 

Subscriber direction is set in `Subscribe()` method on series publisher. 




## Implementation overview


