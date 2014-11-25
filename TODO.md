TODO

* Store non-immediate TODOs in a separate file
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

* TimePeriod direct parsing from string