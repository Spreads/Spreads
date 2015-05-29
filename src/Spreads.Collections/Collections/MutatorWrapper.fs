namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections


[<Serializable>]
/// Make immutable map behave like a mutable one
type internal MutatorWrapper<'K,'V  when 'K : comparison>
  (immutableMap:IImmutableOrderedMap<'K,'V>)=
    
  let mutable map = immutableMap

  let syncRoot = new Object()

  member internal this.SyncRoot with get() = syncRoot

  interface IEnumerable with
    member this.GetEnumerator() = map.GetEnumerator() :> IEnumerator

  interface IEnumerable<KeyValuePair<'K,'V>> with
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
      map.GetEnumerator() :> IEnumerator<KeyValuePair<'K,'V>>


    
  interface IReadOnlyOrderedMap<'K,'V> with
    member this.GetAsyncEnumerator() = map.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = map.GetCursor()
    member this.IsEmpty = map.IsEmpty
    member this.IsIndexed with get() = false
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
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)

    member this.SyncRoot with get() = map.SyncRoot
    


  interface IOrderedMap<'K,'V> with
    member this.Size with get() = map.Size
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
      use lock = makeLock this.SyncRoot
      let m,r = map.RemoveFirst()
      result <- r
      map <- m

    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      use lock = makeLock this.SyncRoot
      let m,r = map.RemoveLast()
      result <- r
      map <- m

    member this.RemoveMany(key:'K,direction:Lookup) = 
      lock this.SyncRoot ( fun () ->
          map <- map.RemoveMany(key, direction)
      )


[<Sealed>]
[<Serializable>]
type internal MutableIntMap64<'T>(map: ImmutableIntMap64<'T>)=
  inherit MutatorWrapper<int64,'T>(map)
  new() = MutableIntMap64(ImmutableIntMap64<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableIntMap64U<'T>(map: ImmutableIntMap64U<'T>)=
  inherit MutatorWrapper<uint64,'T>(map)
  new() = MutableIntMap64U(ImmutableIntMap64U<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableIntMap32<'T>(map: ImmutableIntMap32<'T>)=
  inherit MutatorWrapper<int32,'T>(map)
  new() = MutableIntMap32(ImmutableIntMap32<'T>.Empty)

[<Sealed>]
[<Serializable>]
type internal MutableIntMap32U<'T>(map: ImmutableIntMap32U<'T>)=
  inherit MutatorWrapper<uint32,'T>(map)
  new() = MutableIntMap32U(ImmutableIntMap32U<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableDateTimeMap<'T>(map: ImmutableDateTimeMap<'T>)=
  inherit MutatorWrapper<DateTime,'T>(map)
  new() = MutableDateTimeMap(ImmutableDateTimeMap<'T>.Empty)
    

[<Sealed>]
[<Serializable>]
type internal MutableDateTimeOffsetMap<'T>(map: ImmutableDateTimeOffsetMap<'T>)=
  inherit MutatorWrapper<DateTimeOffset,'T>(map)
  new() = MutableDateTimeOffsetMap(ImmutableDateTimeOffsetMap<'T>.Empty)


[<Sealed>]
[<Serializable>]
type internal MutableSortedMap<'K,'V  when 'K : comparison>(map: ImmutableSortedMap<'K,'V>)=
  inherit MutatorWrapper<'K,'V>(map)
  new() = MutableSortedMap(ImmutableSortedMap<'K,'V>.Empty)