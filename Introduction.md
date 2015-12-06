#Spreads

**Spreads** stands for **S**eries and **P**anels for **R**eactive and **E**xploratory **A**nalysis of 
**D**ata **S**treams.

+ **Data streams** are endless sequences of any items, either recorded or 
arriving in real-time;
+ **Series** are navigable ordered data streams of key-value pairs or key-value mappings;
+ **Panels** are series of series or data frames;
+ **Exploratory** data manupulation in C#/F# REPLs;
+ **Reactive** fast incremental calculations in real-time.

Also (pun intended): Spreads library spreads data accross network,
and one of the simplest use case of the library is to calculate spreads between
time series, e.g. stock quotes.

## Design principles


+ Lazy (unless it hurts performance, but usually it helps)
+ Declarative functional transformations (with access to low-level details when needed)
+ Async or reactive pull model for series, no blocking code
+ Missing values are really missing, not present as "missing" or N/A.
+ Benchmark every change

(TODO explain inspiration from Disruptor pattern and why it works well with lazyness)


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



## Background
Data series could be modeled as IEnumerable or IObservable sequences. Existing libraries 
such as LINQ and Rx provide rich functionality and we used them initially. However, 
most data series, e.g. time series, are navigable sequences of key-value pairs. 
This semantic could not be fully leveraged by those libraries.

In 2013, [Deedle](https://github.com/BlueMountainCapital/Deedle) library was open 
sourced and we used it for rapid application development. However, it had some drawbacks
which made it impossible to continue our development with Deedle (see comparison section 
for technical details). The most critical things were internal complexity, design, 
performance and memory consumption. When we moved from a prototype on small data to 
a high-volume time series, Windows/Chunks caused OutOfMemory exception because of 
eager intermediate evaluation of each window/chunk. Windowing was one of the most
used functionality and we quickly realized that to avoid allocations we need lazy windows
and other lazy LINQ-like calculations that leverage data series nature.

By that time the core functionality of Spreads was ready. We ran some 
benchmarks and found that Spreads implementation was significantly faster with less
memory consumption and allocations. Additionaly, Spreads library was designed to support
real-time streaming data, but [Deedle does not and will not support streaming data](https://github.com/BlueMountainCapital/Deedle/issues/51) 
due to fundametal design decisions (immutability, Panda/R-like data structures).
With performance boost and real-time support, we started to migrate from Deedle to
Spreads and added all functionality from Deedle that was missing in Spreads and that 
we used. Due to the fact that Spreads' Series are IEnumerable, we could use LINQ when
we did not depend on ordered key-value semantic.

Later we implemented live streaming of data and built our entire data processing 
using Spreads library. As of version 0.1, existing functionality became fast and 
stable enough for use in our production.

