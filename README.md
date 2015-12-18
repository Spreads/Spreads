#Spreads

**Spreads** stands for **S**eries and **P**anels for **R**eal-time and **E**xploratory **A**nalysis of
**D**ata **S**treams.


+ **Data streams** are endless sequences of any items, either recorded or
arriving in real-time;
+ **Series** are navigable ordered data streams of key-value pairs or key-value mappings;
+ **Panels** are series of series or data frames;
+ **Exploratory** data manupulation in C#/F# REPLs;
+ **Real-time** fast incremental calculations.

Spreads is an ultra-fast library for complex event processing and time series manipulation.
It could process tens of millions items of historical and realtime data in the same fashion, which allows to
build and test analytical systems on historical data and use the same code for processing
real-time data.

Spreads is a [library, not a framework](http://tomasp.net/blog/2015/library-frameworks/). Even though the primary domain is stock market
and financial data, Spreads was designed as a generic complex event processing library,
with a performance requirement that it must be suitable for ticks anf full order log processing.



## Performance

Spreads library was optimized for performance and memory usage on hot paths. For simple
arithmetic operations (see [the test](http://to.do)) it is **150** (one hundread fifty) **times faster** than [Deedle](https://github.com/BlueMountainCapital/Deedle)
and **14%** faster than [Streams](https://github.com/nessos/Streams),
with additional features such as real-time updates and navigation.

    let input : Series<DateTime,double> = ...
    let result = ((input + 123456.0)/789.0)*10.0 // apply arithmetic operations to each item


+ **Spreads**:  32.5 millions items per second (MOps), zero memory allocation
+ **Streams**:  28.5 MOps, zero
+ **Deedle**: 0.2 MOps, 8 bytes per item

(Release mode, i5-4270 @ 3.2 GHz, latest libraries from NuGet)


For a more complex scenario from the example below, the numbers are:



Spreads library is written in F# (core parts) and C#. .NET gives native performance when
optimized more memory access patterns, which means no functional data structures.


### Series manipulation and join

One of the core feature of Spreads library is declarative lazy series manipulation.
A calculation on series is not 



####Example

The simplest trading strategy could be implemented like this:


## Extensions

The project Spreads.Extensions:

+ Very efficient serializer (based on [Blosc](https://github.com/Blosc/c-blosc))
+ Interactive Extensions adapted for Spreads
+ Yeppp math library for SIMD calculations
+ Array pool (gives very visible performance gain)
+ Concrete implementations of series transformations, such as SMA and StDev, and useful
utils for time zones (using NodaTime).

##Install

`Spreads.Core` package contains the core calculations functionality. `Spreads` package
adds `Spreads.Extensions` as dependency.

    PM> Install-Package Spreads.Core
    PM> Install-Package Spreads

## Contributing

Contributors are very welcome! Currently Spreads library implements only the 
core functionality via higher-orderseries projections like `Map`, `Zip`, etc. 
One could do a lot of things just by using
them with sophisticated lambdas. In addition, due to the fact that Series implement
`IAsyncEnumerable` interface, one could use LINQ and [Interactive Extensions](https://github.com/Spreads/Spreads.Ix) for 
additional functionality. However, there are a lot of work to do with regards to 
usability and performance. There are many features still missing compared to Deedle or Streams,
e.g. aggregation, grouping, resampling. They could be easily implemented via `BindCursor`.

In addition, correctness [is paramount](http://qz.com/119578/damn-you-excel-spreadsheets-jp-morgan-edition/) 
for this kind of code and its intended usage - some subtle bugs could cause a big loss.
Therefore, one of the main goals is exhausting random testing of calculations. ZipN tests contain
an example where reference series are generated manually and then compared to ZipN output.
The project Spreads.Collection.Tests has an unfinished test suite to extend this idea and
ensure that all series adhere to contract behavior and produce correct values.

Spreads library is free software; you can redistribute it and/or modify it
underthe terms of the GNU Lesser General Public License as published
by the Free Software Foundation; either version 3 of the License,
or (at your option) any later version.


## History

While I worked at Goldman as research analyst, I get used to internal declarative
time series processing langauge. Probably that was [Slang](http://news.efinancialcareers.com/uk-en/147434/inside-goldman-sachs-secret-sauce/)
that saved the firm from the Great Recession. After I left the firm, I cannot stand for
other existing tools and wanted the same functionality as an end user. I was far away from
the IT department, had no idea about implementation, never uploaded any code to a German or
any other file hostings and wrote my own implementation. (This paragraph is just a shameless plug and
name dropping for search engines, there is no affiliation or any other relation other than inspiration).

Data series could be modeled as IEnumerable or IObservable sequences. Existing libraries
such as LINQ and Rx provided rich functionality and I used them initially from code. However,
most data series, e.g. time series, are navigable sequences of key-value pairs.
This semantic could not be fully leveraged by those libraries.

In 2013, [Deedle](https://github.com/BlueMountainCapital/Deedle) library was open
sourced. At my current work, we used it for rapid development. However, due to internal
complexity, immutable design, performance and memory consumption we coundn't continue
our development with it. When we moved from a prototype on small data to
a high-volume time series, Windows/Chunks caused OutOfMemory exception because of
eager intermediate evaluation of each window/chunk. Windowing was one of the most
used functionality and we quickly realized that to avoid allocations we need lazy windows
and other lazy LINQ-like calculations that leverage data series nature.

After the core functionality of Spreads was ready, we did some performance
tests and found that Spreads implementation was significantly faster with less
memory consumption and allocations. Additionaly, Spreads library was initially designed to support
real-time streaming data, but Deedle [does not and will not support streaming data](https://github.com/BlueMountainCapital/Deedle/issues/51)
due to fundametal design decisions (immutability, Panda/R-like data structures).
We migrated to Spreads for backtesting and later implemented live streaming of data
and built our entire data processing pipeline on Spreads - from strategy backtesting to trading.
As of version 0.1+, existing functionality became fast and stable enough for use in
our production (where we alos test, debug and fix it - current status is alpha).
