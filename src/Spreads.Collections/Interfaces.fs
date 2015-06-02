namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices


[<AllowNullLiteral>]
type IAsyncEnumerator<'T> =
  inherit IDisposable
  /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
  /// <returns>
  /// Task containing the result of the operation: true if the enumerator was successfully advanced 
  /// to the next element; false if the enumerator has passed the end of the sequence.
  /// </returns>    
  abstract MoveNextAsync: cancellationToken:CancellationToken  -> Task<bool>
  abstract Current: 'T with get

[<AllowNullLiteral>]
type IAsyncEnumerable<'T> =
  /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
  /// <returns>
  /// Task containing the result of the operation: true if the enumerator was successfully advanced 
  /// to the next element; false if the enumerator has passed the end of the sequence.
  /// </returns>    
  abstract GetAsyncEnumerator: unit -> IAsyncEnumerator<'T>


// ISeries doesn't have any mutable properties or mutating methods, but implementation could 
// be either mutable or immutable
// Series have a single member that is enough to implement all other inherited interfaces

/// Main interface for data series. Modeled after IAsyncEnumerable from Ix.NET with advanced enumerator that could 
/// move not only to next values, but to next batches, previous, first, last and a custom excat or relative (LT/LE/GT/GE) position.
[<AllowNullLiteral>]
type ISeries<'K,'V when 'K : comparison> =
  inherit IAsyncEnumerable<KVP<'K,'V>>
  /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact 
  /// positions and relative LT/LE/GT/GE moves.
  abstract GetCursor : unit -> ICursor<'K,'V>
  // this is a part of interface because it depends on implementation. If using extensions, will have to check for actual implementation
  // anyway and will have to bother with internals visibility
  /// Evaluate lazy series. Similar to IEnumerable.ToArray()/ToList() extension methods.
  //abstract ToMapAsync : cancellationToken:CancellationToken -> Task<IReadOnlyOrderedMap<'K,'V>>

/// ICursor is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact 
/// positions and relative LT/LE/GT/GE moves.
/// Cursor is resilient to changes in an underlying sequence during movements, e.g. the
/// sequence could grow during move next. (See documentation for out of order behavior.)
/// 
/// Supports batches with MoveNextBatchAsync() and CurrentBatch members. Accessing current key
/// after MoveNextBatchAsync or CurrentBatch after any single key movement results in InvalidOperationException.
and
  [<AllowNullLiteral>]
  ICursor<'K,'V when 'K : comparison> =
    inherit IAsyncEnumerator<KVP<'K, 'V>>
    /// Puts the cursor to the position according to LookupDirection
    abstract MoveAtAsync: index:'K * direction:Lookup -> Task<bool>
    abstract MoveFirstAsync: unit -> Task<bool>
    abstract MoveLastAsync: unit -> Task<bool>
    abstract MovePreviousAsync: unit -> Task<bool>
    abstract CurrentKey:'K with get
    abstract CurrentValue:'V with get
    /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
    /// Returns true when a batch is available immediately (async for IO, not for waiting for new values),
    /// returns false when there is no more immediate values and a consumer should switch to MoveNextAsync().
    /// NB: Btach processing is synchronous via IEnumerable interface of a batch, real-time is pull-based asynchronous.
    abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
    /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
    abstract CurrentBatch: IReadOnlyOrderedMap<'K,'V> with get
    abstract Source : ISeries<'K,'V> with get

/// Important! 'Read-only' doesn't mean that the object is immutable or not changing. It only means
/// that there is no methods to change the map *from* this interface, without any assumptions about 
/// the implementation. Underlying sequence could be mutable and rapidly changing; to prevent any 
/// changes use lock (Monitor.Enter) on the SyncRoot property. Doing so will block any changes for 
/// mutable implementations and won't affect immutable implementations.
and
  [<AllowNullLiteral>]
  IReadOnlyOrderedMap<'K,'V when 'K : comparison> =
    inherit IEnumerable<KVP<'K, 'V>>
    inherit ISeries<'K,'V>
    /// True if this.size = 0
    abstract IsEmpty: bool with get
    /// If true then elements are sorted by some custom order (e.g. order of addition (index) and not by keys
    abstract IsIndexed : bool with get
    /// First element, throws InvalidOperationException if empty
    abstract First : KVP<'K, 'V> with get
    /// Last element, throws InvalidOperationException if empty
    abstract Last : unit -> KVP<'K, 'V> with get
    /// Values at key, throws KeyNotFoundException if key is not found
    abstract Item : 'K -> 'V with get
    abstract Keys : IEnumerable<'K> with get
    abstract Values : IEnumerable<'V> with get
    /// The method finds value according to direction, returns false if it could not find such a value
    /// For indexed series LE/GE directions are invalid (throws InvalidOperationException), while
    /// LT/GT search is done by index rather than by key and possible only when a key exists.
    abstract TryFind: key:'K * direction:Lookup * [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetFirst: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetLast: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool
    /// Locks any mutations for mutable implementations
    abstract SyncRoot : obj with get



// Main differences between immutable and mutable ordered maps:
// * An immutable map returns a new version of itself on each mutation,
//   while mutable map changes data in-place;
// * An immutable map doesn't have an Item setter (impossible to use a return 
//   type of the map) - instead use Add method that will return a new map if a 
//   key exists and new/old values is different or the same map if new value is the same;
// * Mutable map will throw on addition of a key that already exists, use Item setter 
//   to set value for existing key.


/// Mutable ordered map
[<AllowNullLiteral>]
type IOrderedMap<'K,'V when 'K : comparison> =
  inherit IReadOnlyOrderedMap<'K,'V>
  abstract Size: int64 with get
  abstract Item : 'K -> 'V with get,set
  /// Adds new key and value to map, throws if the key already exists
  abstract Add : k:'K*v:'V -> unit
  /// Checked addition, checks that new element's key is larger/later than the Last element's key
  /// and adds element to this map
  /// throws ArgumentOutOfRangeException if new key is smaller than the last
  abstract AddLast : k:'K*v:'V -> unit
  /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
  /// and adds element to this map
  /// throws ArgumentOutOfRangeException if new key is larger than the first
  abstract AddFirst : k:'K*v:'V -> unit
  /// Remove a key and its value in-place.
  abstract Remove : k:'K -> bool
  abstract RemoveLast: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> unit
  abstract RemoveFirst: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> unit
  abstract RemoveMany: k:'K * direction:Lookup -> unit
  //abstract TryFindWithIndex: key:'K*direction:Lookup * [<Out>]result: byref<KeyValuePair<'K, 'V>> -> int


[<AllowNullLiteral>]
type IImmutableOrderedMap<'K,'V when 'K : comparison> =
  inherit IReadOnlyOrderedMap<'K,'V>
  abstract Size: int64 with get
  /// Unchecked addition, returns a new version of map with new element added
  abstract Add : k:'K*v:'V -> IImmutableOrderedMap<'K,'V>
  /// Checked addition, checks that new element's key is larger/later than the Last element's key
  /// and returns a new version of map with new element added
  abstract AddLast : k:'K*v:'V -> IImmutableOrderedMap<'K,'V>
  /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
  /// and returns a new version of map with new element added
  abstract AddFirst : k:'K*v:'V -> IImmutableOrderedMap<'K,'V>
  /// Remove a key and its value from the map. When the key is not a member of the map, the original map is returned.
  abstract Remove : k:'K -> IImmutableOrderedMap<'K,'V>
  abstract RemoveLast: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> IImmutableOrderedMap<'K,'V>
  abstract RemoveFirst: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> IImmutableOrderedMap<'K,'V>
  abstract RemoveMany: k:'K * direction:Lookup -> IImmutableOrderedMap<'K,'V>






// Types are good, do not drop them. Use marker interfaces only if they really help to achieve something
//[<AllowNullLiteral>]
//type IPanel = interface end
///// Marker interface for a panel with
//[<AllowNullLiteral>]
//type IPanel<'KC when 'KC : comparison> = interface end
//
//[<AllowNullLiteral; Obsolete("Rethink it")>]
//type ISeries<'K when 'K : comparison> = 
//    interface
//        inherit IPanel<'K>
//    end





//and 
//  [<AllowNullLiteral>]
//  IIdentitySeries<'K,'V,'TId when 'K : comparison and 'TId : comparison> =   
//    inherit ISeries<'K,'V>
//    abstract Identity : 'TId with get


type internal UpdateHandler<'K,'V when 'K : comparison> = delegate of obj * KVP<'K,'V> -> unit
[<AllowNullLiteral>]
type internal IUpdateable<'K,'V when 'K : comparison> =   
  [<CLIEvent>]
  abstract member OnData : IEvent<UpdateHandler<'K,'V>,KVP<'K,'V>>

//type internal MyType<'K,'V when 'K : comparison> () =
//    let myEvent = new Event<UpdateHandler<'K,'V>,KVP<'K,'V>> ()
//
//    interface IUpdateable<'K,'V> with
//        [<CLIEvent>]
//        member this.OnData = myEvent.Publish

// TODO?? maybe this is enough? All other methods could be done as extensions??
type Panel<'TRowKey,'TColumnKey, 'TValue when 'TRowKey: comparison and 'TColumnKey : comparison> = 
  //ISeries<'TColumnKey,ISeries<'TRowKey,'TValue>> // this is how it could be implemented
  ISeries<'TRowKey,IReadOnlyOrderedMap<'TColumnKey,'TValue>> // this is how it is used most of the time
  // panel stores references to series that could be lazy/persistent/(not in memory), while panel's consumer need rows on demand


  // TODO data/event stream interface requires a special interface - or it is just an IAsynEnumerable<'T> which then
  // could be unpacked into series.
  // DS could be unirdered, with a method like ds.SaveSeries(repo, ds => key, ds => value), same with Rx
  // out of order KVPs will result only in that active cursors will be repositioned on MoveNext and replay values in the correct order.


//and    //TODO?? Panel could be replaced by extension methods on ISeries<'TColumnKey,ISeries<'TRowKey,'TValue>>
//  [<AllowNullLiteral>]
//  IPanel<'TRowKey,'TColumnKey, 'TValue when 'TRowKey: comparison and 'TColumnKey : comparison> =
//    //inherit ISeries<'TColumnKey,ISeries<'TRowKey,'TValue>>
//    /// 
//    abstract Columns: ISeries<'TColumnKey, ISeries<'TRowKey,'TValue>> with get
//    /// Series of balanced row, i.e. when each column has a value for the key.
//    /// This is the same as inner join in SQL.
//    abstract Rows: ISeries<'TRowKey, ISeries<'TColumnKey,'TValue>> with get
//
//    abstract UnbalancedColumns: ISeries<'TColumnKey, ISeries<'TRowKey,'TValue>> with get
//    /// Series of unbalanced balanced row, i.e. when at least one column has a value for the key.
//    /// This is the same as full outer join in SQL.
//    abstract UnbalancedRows: ISeries<'TRowKey, ISeries<'TColumnKey,'TValue opt>> with get
//
//    /// Apply a function for each column and returns a panel backed by ISeries after function application
//    /// e.g.  in C#: var newPanel = oldPanel.Columnwise(c => c.Repeat())
//    abstract ColumnWise : (Func<ISeries<'TRowKey,'TValue>,ISeries<'TRowKey,'TValue>>) -> IPanel<'TRowKey,'TColumnKey, 'TValue>
//    abstract RowWise : (Func<ISeries<'TColumnKey,'TValue>,ISeries<'TColumnKey,'TValue>>) -> IPanel<'TRowKey,'TColumnKey, 'TValue>
//    /// Apply a function to each value, alias to map
//    abstract PointWise : (Func<ISeries<'TColumnKey,'TValue>,ISeries<'TColumnKey,'TValue>>) -> IPanel<'TRowKey,'TColumnKey, 'TValue>
//
//
    // when adding a column, all rows must be invalidated





/// <summary>
/// IComparer<'K> with additional methods for regular keys
/// </summary>
[<AllowNullLiteral>]
type IKeyComparer<'K>= // when 'K : comparison
  inherit IComparer<'K>
  /// Returns int32 distance between two values when they are stored in 
  /// a regular sorted map. Regular means continuous integers or days or seonds, etc.
  /// This method could be used for IComparer<'K>.Compare implementation.
  abstract Diff : a:'K * b:'K -> int
  /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
  abstract Add : 'K * diff:int -> 'K
  /// Generates an order-preserving hash.
  /// The hashes are used as bucket keys and should be a 
  /// http://en.wikipedia.org/wiki/Monotonic_function
  abstract Hash: k:'K -> 'K
  /// <summary>
  /// Get UInt64 representation of a key.
  /// In general, (a.AsUInt64 - b.AsUInt64) is not equal to ISpreadsComparer.Diff(a,b), e.g. for non-tick TimePeriod.
  /// </summary>
  abstract AsUInt64: k:'K -> uint64
  /// <summary>
  /// Get a key from its UInt64 representation.
  /// In general, (a.AsUInt64 - b.AsUInt64) is not equal to ISpreadsComparer.Diff(a,b), e.g. for non-tick TimePeriod.
  /// </summary>
  abstract FromUInt64: uint64 -> 'K