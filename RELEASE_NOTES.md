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
* Add comparere to IROOM & ICursor interfaces;
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

