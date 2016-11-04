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