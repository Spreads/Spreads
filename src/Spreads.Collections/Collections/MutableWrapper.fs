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

namespace Spreads.Collections

open System
open System.Linq
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections


[<Serializable>]
/// Make immutable map behave like a mutable one
type internal MutableWrapper<'K,'V  when 'K : comparison>
  (immutableMap:IImmutableOrderedMap<'K,'V>)=
    
  let mutable map = immutableMap

  let syncRoot = new Object()

  member internal this.SyncRoot with get() = syncRoot

  interface IEnumerable with
    member this.GetEnumerator() = (map :> IEnumerable<_>).GetEnumerator() :> IEnumerator

  interface IEnumerable<KeyValuePair<'K,'V>> with
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
      (map:> IEnumerable<_>).GetEnumerator() :> IEnumerator<KeyValuePair<'K,'V>>
   
  interface IReadOnlyOrderedMap<'K,'V> with
    member this.Comparer with get() = map.Comparer
    member this.GetEnumerator() = map.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = map.GetCursor()
    member this.IsEmpty = map.IsEmpty
    member this.IsIndexed with get() = false
    member this.IsMutable with get() = true
    //member this.Count with get() = int map.Size
    member this.First with get() = map.First
    member this.Last with get() = map.Last
    member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      map.TryFind(k, direction, &result)
    member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      try
        res <- map.First
        true
      with
      | _ -> 
        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        false
    member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      try
        res <- map.Last
        true
      with
      | _ -> 
        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        false
    member this.TryGetValue(k, [<Out>] value:byref<'V>) = 
      map.TryGetValue(k, &value)
    member this.Item with get k = map.Item(k)
    member this.GetAt(idx:int) = this.Skip(Math.Max(0, idx-1)).First().Value
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)

    member this.SyncRoot with get() = map.SyncRoot
    


  interface IOrderedMap<'K,'V> with
    member this.Version with get() = raise (NotImplementedException("TODO (low) if this type is ever used"))
    member this.Count with get() = map.Size
    member this.Item
      with get (k:'K) : 'V = map.Item(k)
      and set (k:'K) (v:'V) = 
        lock this.SyncRoot ( fun () ->
          map <- map.Add(k, v)
        )
   

    member this.Add(k, v) = 
      lock this.SyncRoot ( fun () ->
        if fst (map.TryFind(k, Lookup.EQ)) then raise (ArgumentException("key already exists"))
        map <- map.Add(k, v)
      )
    member this.AddLast(k, v) = 
      lock this.SyncRoot ( fun () ->
        map <- map.AddLast(k, v)
      )

    member this.AddFirst(k, v) = 
      lock this.SyncRoot ( fun () ->
        map <- map.AddFirst(k, v)
      )

    member this.Remove(k) = 
      lock this.SyncRoot ( fun () ->
        map <- map.Remove(k)
        true
      )

    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      try
        use lock = makeLock this.SyncRoot
        let m,r = map.RemoveFirst()
        result <- r
        map <- m
        true
      with | _ -> false

    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      try
        use lock = makeLock this.SyncRoot
        let m,r = map.RemoveLast()
        result <- r
        map <- m
        true
      with | _ -> false

    member this.RemoveMany(key:'K,direction:Lookup) = 
      try
        lock this.SyncRoot ( fun () ->
            map <- map.RemoveMany(key, direction)
            true
        )
      with | _ -> false

    member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>, option:AppendOption) =
      // do not need transaction because if the first addition succeeds then all others will be added as well
//      for i in appendMap do
//        (this :> IOrderedMap<'K,'V>).AddLast(i.Key, i.Value)
      raise (NotImplementedException("TODO append impl"))

[<Sealed>]
[<Serializable>]
type internal MutableIntMap64<'T>(map: ImmutableIntMap64<'T>)=
  inherit MutableWrapper<int64,'T>(map)
  new() = MutableIntMap64(ImmutableIntMap64<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableIntMap64U<'T>(map: ImmutableIntMap64U<'T>)=
  inherit MutableWrapper<uint64,'T>(map)
  new() = MutableIntMap64U(ImmutableIntMap64U<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableIntMap32<'T>(map: ImmutableIntMap32<'T>)=
  inherit MutableWrapper<int32,'T>(map)
  new() = MutableIntMap32(ImmutableIntMap32<'T>.Empty)

[<Sealed>]
[<Serializable>]
type internal MutableIntMap32U<'T>(map: ImmutableIntMap32U<'T>)=
  inherit MutableWrapper<uint32,'T>(map)
  new() = MutableIntMap32U(ImmutableIntMap32U<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableDateTimeMap<'T>(map: ImmutableDateTimeMap<'T>)=
  inherit MutableWrapper<DateTime,'T>(map)
  new() = MutableDateTimeMap(ImmutableDateTimeMap<'T>.Empty)
    

[<Sealed>]
[<Serializable>]
type internal MutableDateTimeOffsetMap<'T>(map: ImmutableDateTimeOffsetMap<'T>)=
  inherit MutableWrapper<DateTimeOffset,'T>(map)
  new() = MutableDateTimeOffsetMap(ImmutableDateTimeOffsetMap<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableSortedMap<'K,'V  when 'K : comparison>(map: ImmutableSortedMap<'K,'V>)=
  inherit MutableWrapper<'K,'V>(map)
  new() = MutableSortedMap(ImmutableSortedMap<'K,'V>.Empty)