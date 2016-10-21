// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System

open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices



/// Mutable ordered map
[<Interface>]
[<AllowNullLiteral>]
type IOrderedMap<'K,'V> =
  inherit IReadOnlyOrderedMap<'K,'V>
  //inherit IDictionary<'K,'V>
  abstract Count: int64 with get
  /// Incremented after any change to data, including setting of the same value to the same key
  abstract Version: int64 with get,set
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
type IPersistentObject =
  inherit IDisposable
  abstract Flush : unit -> unit
  abstract Id : string with get

[<Interface>]
[<AllowNullLiteral>]
type IPersistentOrderedMap<'K,'V> =
  inherit IOrderedMap<'K,'V>
  inherit IPersistentObject

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



// TODO! use ArraySegment instead of byte[] everywhere where byte[] could be a reusable buffer
type ISerializer =
  abstract Serialize: 'T -> byte[]
  abstract Serialize: obj -> byte[]
  abstract Deserialize: byte[] -> 'T
  abstract Deserialize: byte[] * System.Type -> obj