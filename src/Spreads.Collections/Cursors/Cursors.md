
Cursors implementation
=====================

In this folder, we have implementations of all cursors, except Map and Zip that are used for operators on 
Series and are implemented as recursive types with Series.

In the file SeriesExtensions we provide extension methods for each cursor via creating a CursorSeries. Order of extensions follows order of cursor.


Implemented cursors & extensions
-------------------
**Basic**
* ReadOnly - exposes any series as read-only series by passing through original cursors to CursorSeries, with no transformations.
- ToSeries
    - ToMaterializedSeries() - SortedMap/OrderedMap in-memory
    - ToBufferSeries(n) - keeps at least n latest elements as a sorted/ordered map

**Missing values replacement during join and lookup**
+ `ISeries<>.Repeat()` - at each point, return value for less or equal key. Same as lookup with `Lookup.LE`, optimized for `Zip`.
- Fill


**Higher-order functions**

- Map
    - MapValues
    - MapValuesWithKeys
    - MapKeysAndValues
- Filter
    - FilterKeys
    - FilterValues
- 

