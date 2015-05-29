namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices


[<AllowNullLiteral>]
type IAsyncEnumerator<'T> =
  inherit IEnumerator<'T>
  /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
  /// <returns>
  /// Task containing the result of the operation: true if the enumerator was successfully advanced 
  /// to the next element; false if the enumerator has passed the end of the sequence.
  /// </returns>    
  abstract MoveNextAsync: cancellationToken:CancellationToken  -> Task<bool>


[<AllowNullLiteral>]
type IAsyncEnumerable<'T> =
  inherit IEnumerable<'T>
  /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
  /// <returns>
  /// Task containing the result of the operation: true if the enumerator was successfully advanced 
  /// to the next element; false if the enumerator has passed the end of the sequence.
  /// </returns>    
  abstract GetAsyncEnumerator: unit -> IAsyncEnumerator<'T>


/// Important! 'Read-only' doesn't mean that the object is immutable or not changing. It only means
/// that there is no methods to change the map *from* this interface, without any assumptions about 
/// the implementation. Underlying sequence could be mutable and rapidly changing; to prevent any 
/// changes use lock (Monitor.Enter) on the SyncRoot property. Doing so will block any changes for 
/// mutable implementations and won't affect immutable implementations.
[<AllowNullLiteral>]
type IReadOnlyOrderedMap<'K,'V when 'K : comparison> =
  //inherit IEnumerable<KVP<'K,'V>>
  inherit IAsyncEnumerable<KVP<'K,'V>> 
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
  /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact 
  /// positions and relative LT/LE/GT/GE moves
  abstract GetCursor : unit -> ICursor<'K,'V>

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
    abstract MoveAt: index:'K * direction:Lookup -> bool
    abstract MoveFirst: unit -> bool
    abstract MoveLast: unit -> bool
    abstract MovePrevious: unit -> bool
    abstract CurrentKey:'K with get
    abstract CurrentValue:'V with get
    /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
    abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
    /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
    abstract CurrentBatch: IReadOnlyOrderedMap<'K,'V> with get
    abstract Source : IReadOnlyOrderedMap<'K,'V> with get

// Main differences between immutable and mutable sorted maps:
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


// ISeries doesn't have any mutable properties or mutating methods, but implementation could 
// be either mutable or immutable
// Series have a single member that is enough to implement all other inherited interfaces

[<AllowNullLiteral>]
type ISeries<'K,'V when 'K : comparison> =   
  //inherit IAsyncEnumerable<KVP<'K,'V>> // TODO move into ROOM?
  inherit IReadOnlyOrderedMap<'K,'V>

//  abstract WithId: 'TId -> IIdentitySeries<'K,'V,'TId> when 'TId : comparison

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
type Panel<'TRowKey,'TColumnKey, 'TValue when 'TRowKey: comparison and 'TColumnKey : comparison> = ISeries<'TColumnKey,ISeries<'TRowKey,'TValue>>

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