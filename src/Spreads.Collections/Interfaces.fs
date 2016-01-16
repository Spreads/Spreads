(*  
    Copyright (c) 2014-2015 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.
        
    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

namespace Spreads

open System

open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices


///// Asynchronous version of the IEnumerator<T> interface, allowing elements to be retrieved asynchronously.
//[<Interface>]
//[<AllowNullLiteral>]
//type IAsyncEnumerator<'T> =
//  inherit System.IDisposable
//  inherit IEnumerator<'T>
//  /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
//  /// cancellationToken: Cancellation token that can be used to cancel the operation.
//  abstract member MoveNext : cancellationToken:CancellationToken -> Task<bool>
//
///// Asynchronous version of the IEnumerable<T> interface, allowing elements of the enumerable sequence to be retrieved asynchronously.
//[<Interface>]
//[<AllowNullLiteral>]
//type IAsyncEnumerable<'T> =
//  inherit IEnumerable<'T>
//  /// Gets an asynchronous enumerator over the sequence.
//  abstract member GetEnumerator : unit -> IAsyncEnumerator<'T>



/// Main interface for data series.
[<Interface>]
[<AllowNullLiteral>]
type ISeries<'K,'V> =
  inherit IAsyncEnumerable<KVP<'K,'V>>
  /// Get cursor, which is an advanced enumerator supporting moves to first, last, previous, next, next batch, exact 
  /// positions and relative LT/LE/GT/GE moves.
  abstract GetCursor : unit -> ICursor<'K,'V>
  abstract Comparer: IComparer<'K> with get
  /// If true then elements are placed by some custom order (e.g. order of addition, index) and not sorted by keys
  abstract IsIndexed : bool with get
  /// True if the underlying collection could be changed, false if the underlying collection is immutable or is complete 
  /// for adding (e.g. after OnCompleted in Rx) or IsReadOnly in terms of ICollectio/IDictionary or has fixed keys/values (all 4 definitions are the same).
  abstract IsMutable: bool with get
  /// Locks any mutations for mutable implementations
  abstract SyncRoot : obj with get




/// ICursor is an advanced enumerator that supports moves to first, last, previous, next, next batch, exact 
/// positions and relative LT/LE/GT/GE moves.
/// Cursor is resilient to changes in an underlying sequence during movements, e.g. the
/// sequence could grow during move next. (See documentation for out of order behavior.)
/// 
/// Supports batches with MoveNextBatchAsync() and CurrentBatch members. Accessing current key
/// after MoveNextBatchAsync or CurrentBatch after any single key movement results in InvalidOperationException.
/// IsBatch property indicates wether the cursor is positioned on a single value or a batch.
/// 
/// Contracts:
/// 1. At the beginning a cursor consumer could call any single move method or MoveNextBatch. MoveNextBatch could 
///    be called only on the initial move or after a previous MoveNextBatch() call that returned true. It MUST NOT
///    be called in any other situation, ICursor implementations MUST return false on any such wrong call.
/// 2. CurrentBatch contains a batch only after a call to MoveNextBatch() returns true. CurrentBatch is undefined 
///    in all other cases.
/// 3. After a call to MoveNextBatch() returns false, the consumer MUST use only single calls. ICursor implementations MUST
///    ensure that the relative moves MoveNext/Previous start from the last position of the previous batch.
/// 4. Synchronous moves return true if data is instantly awailable, e.g. in a map data structure in memory or on fast disk DB.
///    ICursor implementations should not block threads, e.g. if a map is IUpdateable, synchronous MoveNext should not wait for 
///    an update but return false if there is no data right now.
/// 5. When synchronous MoveNext or MoveLast return false, the consumer should call async overload of MoveNext. Inside the async
///    implementation of MoveNext, a cursor must check if the source is IUpdateable and return Task.FromResult(false) immediately if it is not.
/// 6. When any move returns false, cursor stays at the position before that move (TODO now this is ensured only for SM MN/MP and for Bind(ex.MA) )
/// _. TODO If the source is updated during a lifetime of a cursor, cursor must recreate its state at its current position
///    Rewind logic only for async? Throw in all cases other than MoveNext, MoveAt? Or at least on MovePrevious.
///    Or special behaviour of MoveNext only on appends or changing the last value? On other changes must throw invalidOp (locks are there!)
///    So if update is before the current position of a cursor, then throw. If after - then this doesn't affect the cursor in any way.
///    TODO cursor could implement IUpdateable when source does, or pass through to CursorSeries
/// 

and
  [<Interface>]
  [<AllowNullLiteral>]
  ICursor<'K,'V> =
    inherit IAsyncEnumerator<KVP<'K, 'V>>
    abstract Comparer: IComparer<'K> with get
    /// Puts the cursor to the position according to LookupDirection
    abstract MoveAt: key:'K * direction:Lookup -> bool
    abstract MoveFirst: unit -> bool
    abstract MoveLast: unit -> bool
    abstract MovePrevious: unit -> bool
    abstract CurrentKey:'K with get
    abstract CurrentValue:'V with get

    /// Optional (used for batch/SIMD optimization where gains are visible), MUST NOT throw NotImplementedException()
    /// Returns true when a batch is available immediately (async for IO, not for waiting for new values),
    /// returns false when there is no more immediate values and a consumer should switch to MoveNextAsync().
    /// NB: Batch processing is synchronous via IEnumerable interface of a batch, real-time is pull-based asynchronous.
    abstract MoveNextBatch: cancellationToken:CancellationToken  -> Task<bool>
    // TODO Size limit as a parameter. When size limit is not zero, we should create a buffer even if the origin doesn't have batches

    /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
    /// The actual implementation of the batch could be mutable and could reference a part of the original series, therefore consumer
    /// should never try to mutate the batch directly even if type check reveals that this is possible, e.g. it is a SortedMap
    abstract CurrentBatch: IReadOnlyOrderedMap<'K,'V> with get

    /// True if last successful move was MoveNextBatchAsync and CurrentBatch contains a valid value.
//    [<ObsoleteAttribute>]
//    abstract IsBatch: bool with get

    /// Original series. Note that .Source.GetCursor() is equivalent to .Clone() called on not started cursor
    abstract Source : ISeries<'K,'V> with get

    /// If true then TryGetValue could return values for any keys, not only for existing keys.
    /// E.g. previous value, interpolated value, etc.
    abstract IsContinuous: bool with get

    /// Create a copy of cursor that is positioned at the same place as this cursor.
    abstract Clone: unit -> ICursor<'K,'V>

    /// Gets a calculated value for continuous series without moving the cursor position.
    /// E.g. a continuous cursor for Repeat() will check if current state allows to get previous value,
    /// and if not then .Source.GetCursor().MoveAt(key, LE). The TryGetValue method should be optimized
    /// for sort join case using enumerator, e.g. for repeat it should keep previous value and check if 
    /// the requested key is between the previous and the current keys, and then return the previous one.
    /// NB This is not thread safe. ICursors must be used from a single thread.
    abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool


// TODO for chains of batch operation it is a quetion how to minimize allocations:
// one simple strategy is to check if the batch is SortedMap and check if it is read-only or not
// that will require changes to SortedMap - a constructor that uses references to keys/values 
// and not copies them or uses another SM as a source and blocks all write methods.

/// NB! 'Read-only' doesn't mean that the object is immutable or not changing. It only means
/// that there is no methods to change the map *from* this interface, without any assumptions about 
/// the implementation. Underlying sequence could be mutable and rapidly changing; to prevent any 
/// changes use lock (Monitor.Enter) on the SyncRoot property. Doing so will block any changes for 
/// mutable implementations and won't affect immutable implementations.
and
  [<Interface>]
  [<AllowNullLiteral>]
  IReadOnlyOrderedMap<'K,'V> =
    inherit ISeries<'K,'V>
    // NB cannot inherit IReadOnlyDictionary<'K,'V> because IReadOnlyOrderedMap does not support Count property (for non-metarialized series we will need to iterate the entire series).
    /// True if this.size = 0
    abstract IsEmpty: bool with get
    /// First element, throws InvalidOperationException if empty
    abstract First : KVP<'K, 'V> with get
    /// Last element, throws InvalidOperationException if empty
    abstract Last : unit -> KVP<'K, 'V> with get
    /// Value at key, throws KeyNotFoundException if key is not present in the series (even for continuous series).
    /// Use TryGetValue to get a value between existing keys for continuous series.
    abstract Item : 'K -> 'V with get
    /// Value at index (offset). Implemented efficiently for indexed series and SortedMap, but default implementation
    /// is Linq's [series].Skip(idx-1).Take(1).Value
    abstract GetAt : idx:int -> 'V
    abstract Keys : IEnumerable<'K> with get
    abstract Values : IEnumerable<'V> with get
    /// The method finds value according to direction, returns false if it could not find such a value
    /// For indexed series LE/GE directions are invalid (throws InvalidOperationException), while
    /// LT/GT search is done by index rather than by key and possible only when a key exists.
    abstract TryFind: key:'K * direction:Lookup * [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetFirst: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetLast: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool



// Main differences between immutable and mutable ordered maps:
// * An immutable map returns a new version of itself on each mutation,
//   while mutable map changes data in-place;
// * An immutable map doesn't have an Item setter (impossible to use a return 
//   type of the map) - instead use Add method that will return a new map if a 
//   key exists and new/old values is different or the same map if new value is the same;
// * Mutable map will throw on addition of a key that already exists, use Item setter 
//   to set value for existing key.


/// Mutable ordered map
[<Interface>]
[<AllowNullLiteral>]
type IOrderedMap<'K,'V> =
  inherit IReadOnlyOrderedMap<'K,'V>
  //inherit IDictionary<'K,'V>
  abstract Count: int64 with get
  /// Incremented after any change to data, including setting of the same value to the same key
  abstract Version: int64 with get
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
  /// Add a new map to the end of the current map. Async for IO bound implementations.
  //abstract AppendAsync: map:IReadOnlyOrderedMap<'K,'V> -> Task<bool>
  /// Remove a key and its value in-place.
  abstract Remove : k:'K -> bool
  abstract RemoveLast: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> bool // TODO bool
  abstract RemoveFirst: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> bool // TODO bool
  abstract RemoveMany: k:'K * direction:Lookup -> bool // TODO int
  /// And values from appendMap to the end of this map
  abstract Append: appendMap:IReadOnlyOrderedMap<'K,'V> * option:AppendOption -> int // TODO int, bool option for ignoreEqualOverlap, or Enum with 1: thow, 2: ignoreEqual, 3: rewriteOld, 4: ignoreNew (nonsense option, should not do, the first 3 are good)
  /// Make the map immutable and disable all Add/Remove/Set methods (they will throw)
  abstract Complete: unit -> unit

/// `Flush` has a standard meaning, e.g. as in Stream, and saves all changes. `Dispose` calls `Flush`. `Id` is globally unique.
[<Interface>]
[<AllowNullLiteral>]
type IPersistentOrderedMap<'K,'V> =
  inherit IOrderedMap<'K,'V>
  inherit IDisposable
  abstract Flush : unit -> unit
  abstract Id : string with get

[<Interface>]
[<AllowNullLiteral>]
type IImmutableOrderedMap<'K,'V> =
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
  // not much sense to have a separate append method for immutable maps


// Types are good, do not drop them. Use marker interfaces only if they really help to achieve something
//[<AllowNullLiteral>]
//type IPanel = interface end
///// Marker interface for a panel with
//[<AllowNullLiteral>]
//type IPanel<'KC> = interface end
//
//[<AllowNullLiteral; Obsolete("Rethink it")>]
//type ISeries<'K> = 
//    interface
//        inherit IPanel<'K>
//    end

//and i
//  [<AllowNullLiteral>]
//  IIdentitySeries<'K,'V,'TId> =
//    inherit ISeries<'K,'V>
//    abstract Identity : 'TId with get


//type internal OnNextHandler<'K,'V> = EventHandler<KVP<'K,'V>>
//[<AllowNullLiteral>]
//type internal IUpdateable<'K,'V> =
//  [<CLIEvent>]
//  abstract member OnNext : IDelegateEvent<OnNextHandler<'K,'V>>
////  [<CLIEvent>]
////  abstract member OnComplete : IDelegateEvent<EventHandler>


// TODO?? maybe this is enough? All other methods could be done as extensions??
type IPanel<'TRowKey,'TColumnKey, 'TValue> =
  //ISeries<'TColumnKey,ISeries<'TRowKey,'TValue>> // this is how it could be implemented
  inherit ISeries<'TRowKey,IReadOnlyOrderedMap<'TColumnKey,'TValue>> // this is how it is used most of the time


//and    //TODO?? Panel could be replaced by extension methods on ISeries<'TColumnKey,ISeries<'TRowKey,'TValue>>
//  [<AllowNullLiteral>]
//  IPanel<'TRowKey,'TColumnKey, 'TValue> =
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



/// Generic array pool with reference counting.
type IArrayPool =
  /// Take a new buffer from the pool
  abstract TakeBuffer<'T> : bufferCount:int -> 'T[]
  /// Increment reference count on the buffer if it was taken from the buffer
  abstract BorrowBuffer<'T> : 'T[] -> int
  /// Decrement reference count on a buffer and return the buffer to the pool if refcount is zero
  abstract ReturnBuffer<'T> : 'T[] -> int
  /// Get current reference count 
  abstract ReferenceCount<'T> : 'T[] -> int
  /// Clear the entire pool
  abstract Clear: unit -> unit


// TODO! use ArraySegment instead of byte[] everywhere where byte[] could be a reusable buffer
type ISerializer =
  abstract Serialize: 'T -> byte[]
  abstract Serialize: obj -> byte[]
  abstract Deserialize: byte[] -> 'T
  abstract Deserialize: byte[] * System.Type -> obj