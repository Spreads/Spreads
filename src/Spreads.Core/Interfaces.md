# Interface changes in 1.0

We changed interfaces to make series and cursors dual from navigation point of view.

### Cursor and Series are dual

```
Cursor											| Series

bool MoveAt(TKey key, Lookup direction);		| Opt<KeyValuePair<TKey, TValue>> TryFindAt(TKey key, Lookup direction);

bool MoveFirst && MoveNext(idx - 1)			| Opt<KeyValuePair<TKey, TValue>> TryGetAt(long idx);
or MoveNext(idx) when not initialized		|

bool MoveFirst()/MoveLast();					| Opt<KeyValuePair<TKey, TValue>> First/Last { get; }

Opt{TValue} TryGetValue(TKey key);			| Opt{TValue} this[TKey key] { get; }

```

## All throwing methods were changed to return Opt<> type.
* With TryXXX pattern stack usage is the same.
* We often need to check existence - then exceptions are expensive.
* Continuous series have valid value at infinite number of keys.

We also changed the contract of series indexer: for continuous series it now returns the same result
as cursors' TryGetValue function Opt{TValue}. Users should be able to just use an indexer for continuous series.

Cursors could have an indexer instead of `TryGetValue`, but that could be confusing because
a cursor is a point on a line, TGV is a virtual move + return (for continuous series it "moves 
infinitly large number of infinitly small steps"). An indexer is intuitive for collections. 
Just reading the code cursor[k] vs series[k] is confusing.

## Added `long MoveNext/MovePrevious(long stride, bool allowPartial)`
* Need to keep `MoveNext()` to implement `IEnumerator` interface and hit patterns in Roslyn
* MovePrevious() was more often called multiple times than a single time.

## Renamed MoveNext(CancellationToken ct) to MoveNextAsync()/MoveNextAsync(CancellationToken ct)
* Need the one without args for pattern matching when [async streams](https://github.com/dotnet/csharplang/blob/master/proposals/async-streams.md) are implemented
* Avoid default parameters in ctor (also unsure if the async stream pattern will match)
* They say that extension methods are check in the pattern, but the type is generic, need to see how it will work.


## All mutating methods are made async and returning `Task<bool>`
* `Task<bool>` could be cached. See the link above why async stream interface didn't opted for ValueTask.
* We had a lot of troubles implementing sync mutating API over async IO in persistence layer.

## CurrentBatch returns KeyValueReadOnlyMemory
* Batch is for fast processing in special cases. Actually not implemented after last series rework in 2017.
* This is safe only for order-preserving containers (read-only, complete or append-only).
* Async because we persist series in chunks and read from disk/network using async IO.
