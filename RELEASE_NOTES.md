#### 0.2.1 - December 21, 2015
* Fix ZipN #1 and comparer #3 bugs
* Fix bug in LagCursor

#### 0.2.0 - December 20, 2015
* The first public version. See Readme for details.

#### 0.1.9 - December 16, 2015
* Add additional cursor, implement Window, SMA, StDev via lambdas to ScanLagAllowIncompleteCursor.
* Make ToSortedMap() live, but unbounded. Could cause out of memory exceptions if used heavily on large series.
* Clean up tests and start Contracts Suite. Failing tests are now meaningful.
* Fix some bugs with cursors

#### 0.1.8 - December 11, 2015
* Optimize cursors further. Virtual methods are 5x faster than Func<>s.
* Fix SCM clone bug

#### 0.1.7 - December 7, 2015
* Optimize Map() and arithmetics on series by 4x
* Fix bug with explicit interface implementation in ProjectValuesWrapper

#### 0.1.5 - November 23, 2015
* Make SCM updateable

#### 0.1.4 - November 20, 2015
* Remove concrete implementations

#### 0.1.3 - November 11, 2015
* Add bi-directional projection

#### 0.1.0 - November 6, 2015
* Cleanup unused legacy code
* Add empty Spreads project and NuGet to install all dependencies
* First minor version. This is used in production already. Going forward follow semantic versioning rules.

#### 0.0.79 - October 30, 2015
* Fix bug in SortedDeque AddWithIndex (introduced because "it was so trivial that no unit tests were needed"...)

#### 0.0.78 - October 29, 2015
* Change interfaces IAsyncEnumerable/IAsyncEnumerator now inherit IEnumerable/IEnumerator.
* Adapt Ix.NET for the Spreads interfaces, add repo as a submodule

#### 0.0.77 - October 16, 2015
* Fix series repo, add Zip overload without key

#### 0.0.75 - October 15, 2015
* Add AddWithIndex method to SortedDeque

#### 0.0.73 - October 15, 2015
* Fix bug when `:?>` unsafe cast didn't account for signature change

#### 0.0.72 - October 9, 2015
* Some clean-up

#### 0.0.71 - October 1, 2015
* Async series repo

#### 0.0.69 - October 1, 2015
* Fix regular keys bug with int overflow

#### 0.0.60 - September 17, 2015
* SortedDeque constructor with comparer was internal, now public

#### 0.0.59 - September 14, 2015
* AsPanel extension

#### 0.0.58 - September 11, 2015
* ZipN continuous works on existing data. With real-time, results depend on data arrival

#### 0.0.57 - September 11, 2015
* Fix doMoveNextContinuous missing values

#### 0.0.56 - September 10, 2015
* Fix IndexOfKeyUnchecked bug

#### 0.0.54 - September 9, 2015
* Real-time SMA

#### 0.0.53 - September 9, 2015
* SM returns batch only when marked as not mutable (TODO? copy if mutable, or make BatchMapValuesCursor collect values into batches)
* Simpler and faster BatchMapValuesCursor cursor
* ZipN MoveNextAsync works and is fast (awaits TPL task via GetAwaiter and completion action vs. F# slow async-to-TPL border)

#### 0.0.52 - September 2, 2015
* Update SortedMap and SortedDequeue iterator to avoid virtual calls, perf gain is visible in tests. There is
almost no room for further improvement, we are close to `SCG.SortedList<>` and `SCG.List<>`

#### 0.0.51 - August 27, 2015
* Revert SortedChunkedMap implementation and add SortedChunkedMap2 with inner factory

#### 0.0.50 - August 26, 2015
* IndexedMap as an unsorted equivalent of SortedMap, when values are ordered by sequence of addition.
* Add IsMutable property to ISeries. If this value if false, it is safe to reuse keys of the source, which is 
useful for SortedMap/OrderedMap as rows in panels. We could compare keys by reference and apply batch operations,
such as add, to values in case keys are equal.
* In Serializer, store version as negative value for immutable SortedMap. TODO support IndexedMap as a special case in serializer.
* Add `GetAt : idx:int -> 'V` method to IReadOnlyOrderedMap, which gets a value by index. It is implemented efficiently 
for indexed series and SortedMap, but default implementation is Linq's [series].Skip(idx-1).Take(1).Value
* Add Panel base class amd two implementations for column-based and row-based materialized panels. TODO lazy panel with rows as projected series.
* Add proof-of-concept circular calculation test
* Make previous SortedChankedMap implementation obsolete and replace it with SCM2, which accepts inner map factory with
default to SortedMap constructor. TODO? could SCM be indexed, or it does little sense?


#### 0.0.49 - August 17, 2015
* ZipNCursor preliminary tested and profiled for all non-continuous and all continuous series.

#### 0.0.47 - August 13, 2015
* Implement SortedDeque, which is much faster than FixedMinHeap or SortedList1 (TODO test off-by-one bugs for all cases in insert)
* Implement FixedMinHeap and SortedList1

#### 0.0.46 - August 11, 2015
* ZipNCursor MoveNext single-threaded implementation

#### 0.0.45 - August 10, 2015
* Add symbols to NuGet

#### 0.0.44 - August 7, 2015
* Add simple SortedMap generation

#### 0.0.43 - August 5, 2015
* Fix OfSortedKeysAndValues bug with regular keys

#### 0.0.42 - August 4, 2015
* Fix Repeat cursor logic

#### 0.0.40 - August 4, 2015
* Fix remaining cursor clone bug in StDev
* Fix `MoveNext(ct)` and FastEvents, add simple tests

#### 0.0.39 - August 3, 2015
* Fix SM remove off by one bug. TODO try..finally probably hide more interesting errors, need a switch to conditionally remove it.
* Fix another `Cursor.Clone()` stupid bug that was introduced (overlooked) in the previous fix

#### 0.0.38 - July 31, 2015
* Fix empty byte array serialization

#### 0.0.37 - July 29, 2015
* Overlapped windows allow incomplete windows

#### 0.0.35 - July 28, 2015
* Each derived cursor with a constructor that takes anything other than cursorFactory must have its own overriden clone
method, there is no way to clone it (even is there is a way, tons of reflection is worse than a single line of overriden method)
* Implemented and tested SMA and StDev (thanks to B.S.)
* Minor bug fix and refactoring of CursorBind (TODO clone repositioning could be done in CursorBind, abstract method could only 
return a new cursor in reset state)

#### 0.0.33 - July 24, 2015
* Add back current key/value fields to SM cursor (this is actually faster, since we must check index all the time otherwise)
* Fix operators (cursor factory must always return new cursor)

#### 0.0.32 - July 24, 2015
* Fix mscorlib Debug [bug](http://stackoverflow.com/questions/31616816/f-system-typeloadexception-the-generic-type-system-tuple3-was-used-with-an)
* Remove current key/value fields from SM cursor and use only index

#### 0.0.31 - July 24, 2015
* Change F# functions to Func
* Stabilize serializer

#### 0.0.29 - July 23, 2015
* Fix bug when constructing empty SM using OfKeysAndValues

#### 0.0.28 - July 21, 2015
* Fix serializer edge cases when complex object has null or empty map/collection fields (TODO cover edge cases with tests)

#### 0.0.27 - July 18, 2015
* Fix Boostrapper bug in non-interactive mode and change default folder
* Fix (mute) serializer bug (TODO investigate why dynamic overload resolution wasn't working)

#### 0.0.25 - July 18, 2015
* Minor bug fix in SM cursor

#### 0.0.24 - July 18, 2015
* Fix SCM cursor MoveAt GE/GT bug, that was caused by SM cursor index set to size rather than -1 on false move.
* Add IPersistentOrderedMap with Flush method to interfaces

#### 0.0.23 - July 17, 2015
* Extension methods for ISeries interface and not for Series class.

#### 0.0.22 - July 17, 2015
* Implement Fold, Scan, Range, Window (overlapping moving window). Basic tests on forward-only enumeration.

#### 0.0.20 - July 16, 2015
* Change CursorBind so that TryGetValue/TryUpdateNext/Prev do not move InputCursor and return a single value for provided key, not a KVP
* Reimplement cursor series and optimize+fix Repeat() series
* Remove MapKeys CursorBind - it is impossible to do with one-way map, unless we evaluate entire new series into a buffer,

#### 0.0.19 - July 13, 2015
* Fix SCM AddLast/AddFirst (they were effectively flushing on each addition), now we just check with this.Last/.First and use this.Add
* Add MapKeysCursor
* Add NodaTime to Spreads.Extensions and extension methods to/from UTC
* Minimize F# types in public API (TODO the goal is to eliminate them completely)

#### 0.0.16 - July 13, 2015
* Fix Collections: SCM count and SM regular index of key when step <> diff

#### 0.0.15 - July 13, 2015
* Fix some edge-case bugs in collections (TODO good test coverage)
* Test SCM with MySQL for outer map: could reach 500k writes and 1.2m sequential
reads per second on MacBook Air 2012 with MySQL 5.6 Community (default dev settings)

#### 0.0.14 - July 10, 2015
* Change IOrderedMap interface for RemoveXXX (returns bool) and Append (accepts 
AppendOption parameter)
* Add generic array pool (adapted from MSFT's BufferPoolManager)
* Add comparer to IROOM & ICursor interfaces;
* Do not trim excess on bucket switch in SCM (TODO this should be done by serializer,
 or check %% of unused capacity)
* Implement Append for SCM (TODO test it)
* Optimize regular SortedMap (modulo was slow)
* Use IArrayPool in SortedMap capacity setter (tested, gain is visible. Together 
with the previous regular optimization, increased Add benchmark from 10.2 mops to 
14.4 mops on Air, but pooling was less important than modulo operation!)
* Fix int overflow bug in BaseKeyComparer.Compare
* Fix regular SM serialization in the Serializer
* Add Collections.Tests project with basic tests

#### 0.0.13 - July 10, 2015
* Add Flush method to SortedChunkedMap to save current state, make constructors with outer factory 

#### 0.0.12 - July 10, 2015
* Extracted key slicer from IKeyComparer and made it optional. By default, keys are sliced by a fixed upper limit (1000 in this version)
* Added default implementations of IKeyComparer for (u)int(32|64) and DateTime. SortedMap tries to get them if comparer was not supplied.

#### 0.0.11 - July 9, 2015
* Serializer/compressor works for generics, value-type arrays and Spreads-specific types

#### 0.0.10 - July 9, 2015
* Publish Spreads.Extensions with integrated Ix.NET, Blosc (v1.6) compressor and Yeppp vectorized calculations.
* Change IKeyComparer interface from int to int64. Diff and Add with default(K) are now equivalent to deprecated AsInt64/FromInt64.

#### 0.0.9 - July 8, 2015
* Implement CursorBind and CursorZip and arithmetic operators on series. Implement map, filter and repeat (first draft)
 series transformations with CursorBind.

#### 0.0.8 - July 1, 2015
* Synch interfaces to Ix-Asyn (but do not add binary dependency, will use Paket to import files from github)

#### 0.0.7 - June 25, 2015
* Make SortedMap IUpdatable (TODO SCM as well)
* Change Colelctions target from 4.5 to 4.0

#### 0.0.6 - June 22, 2015
* Rework regular keys optimization for sorted map - now it supports a custom step
* Rework cursor logic. Now Series/ISeries are pull-based IAsyncEnumerable-like sequences
with batching support. A cursor takes the role of IEnumerator and completely defines/generates ISeries.

#### 0.0.5 - May 29, 2015
* Delete Spreads.Common project and package and move all files to Spreads.Collection because it is always used
* Clean and update interfaces

#### 0.0.4 - May 8, 2015
* Core packages convenient distribution

#### 0.0.3-alpha - November 25, 2014
* Added TimePeriod structure in a separate PCL library;
* Updated collections, optimized SortedMap and SortedHashMap and added benchmarks to test their performance;
* NuGet packages could be already used for collections specifically designed for streaming time series. Benchmarks in the commit comment show differences in speed and memory.


#### 0.0.2-alpha - November 19, 2014
* Initial commit with updated project structure;
* Moved legacy broken code to the `legacy` branch.

