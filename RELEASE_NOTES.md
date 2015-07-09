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

