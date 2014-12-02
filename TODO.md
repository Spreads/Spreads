TODO
* Make no assumption about data and implementation. Only TimePeriod has universal SortableHasher, 
DT/DTO require UnitPeriod and length, other types require explicit bucket size.
* Benchmark series and specifically overhead of wrapping maps inside series.
* Basic operations on series, e.g. map, join, etc.
* Bucket DU and load/save inner maps


NEXT RELEASE
* Series with runtime implementation choice, benchmark overhead of wrapping maps inside series;
* Basic operations on series, e.g. map, join, etc.

MEDIUM

* TimePeriod correctness tests
* Why SHM reads are so much slower SM? Inner bucket caching suggests that sequential reads should be faster. Investigate.
* SHM inner maps to synchronized and outer map lock only when outer is being modified

LOW

* Test SpinLocks instead of locks - could be relevant for AddLast() buckets. One insert is c.375 CPU cycles, while thread switching 
could tyake 2-10k cycles according to SO. http://stackoverflow.com/a/14613190/801189
* MoveNext/MoveAt should use hints, which act like external prevHash/Idx caches
* TimePeriod direct parsing from string