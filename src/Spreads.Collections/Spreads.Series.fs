namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections

// TODO always chunked if SHM will work without IKeyComparer

// TODO extension methods
// .Push(name) - write ISeries to named persistent series
// .ObserveFrom(start = null) - turn series into IObservable; this is an extension method available only with Spreads.DB

/// In-memory materialized series
[<AllowNullLiteral>]
[<Serializable>]
type Series<'K,'V when 'K : comparison> 
  internal(map:IOrderedMap<'K,'V>) =

  let map: IOrderedMap<'K,'V> = map

  member this.Map with get() = map    

  new() =
    let comparer = KeyComparer.GetDefault<'K>()
    Series(comparer)

  internal new (comparer:IKeyComparer<'K>) =
    let map : IOrderedMap<'K,'V> = 
      if comparer = Unchecked.defaultof<IKeyComparer<'K>> then
        SortedMap<'K,'V>() :> IOrderedMap<'K,'V>
      else SortedHashMap<'K,'V>(comparer) :> IOrderedMap<'K,'V>
    Series(map)

  /// Indexed series preserve order or elements and are not sorted by keys
  new(indexed:bool) =
    if not indexed then 
      Series()
    else
      let map = IndexedMap<'K,'V>() :> IOrderedMap<'K,'V>
      Series(map)


  new(input:IEnumerable<KVP<'K,'V>>) =
    let ser = Series() // will know which kind of map to use from new() ctor
    let map = ref ser.Map
    input |> Seq.map (fun kvp -> map.Value.Add(kvp.Key, kvp.Value)) |> ignore
    Series(map.Value)

  new(input:IEnumerable<KVP<'K,'V>>, indexed:bool) =
    let ser = Series(indexed) // will know which kind of map to use from new() ctor
    let map = ref ser.Map
    input |> Seq.map (fun kvp -> map.Value.Add(kvp.Key, kvp.Value)) |> ignore
    Series(map.Value)
  // TODO more standard convenience constructors??

  member this.IsEmpty = map.IsEmpty

  member this.IsIndexed with get() = map.IsIndexed

  member this.First with get() = map.First

  member this.Last with get() = map.Last

  member this.TryFind(k, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
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

  member this.GetCursor() = map.GetCursor()

  member this.Item
    with get (k) : 'V = map.Item(k)
    and set k v = map.[k] <- v

  member this.Keys with get() = map.Keys
  member this.Values with get() = map.Values

  member this.Size with get() = map.Size

  member this.SyncRoot with get() = map.SyncRoot

  member this.Add(key, value) = map.Add(key, value)

  member this.AddFirst(key, value) = map.AddFirst(key, value)

  member this.AddLast(key, value) = map.AddLast(key, value)

  member this.Remove(key) = map.Remove(key)

  member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let rf = map.RemoveFirst()
    result <- rf
    ()

  member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let rl = map.RemoveLast()
    result <- rl
    ()

  member this.RemoveMany(key:'K,direction:Lookup) = 
    map.RemoveMany(key, direction) |> ignore
    ()

  interface IEnumerable<KeyValuePair<'K, 'V>> with
    member this.GetEnumerator() = map.GetEnumerator()

  interface System.Collections.IEnumerable with
    member this.GetEnumerator() = (map.GetEnumerator() :> System.Collections.IEnumerator)
   
  interface IReadOnlyOrderedMap<'K,'V> with
    member this.IsEmpty = map.IsEmpty
    //member this.Count with get() = map.Count
    member this.IsIndexed with get() = false
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
    member this.Keys with get() = map.Keys
    member this.Values with get() = map.Values
    member this.SyncRoot with get() = this.SyncRoot
    

  interface IOrderedMap<'K, 'V> with
    member this.Size with get() = map.Size
    member this.Item
      with get (k) : 'V = map.Item(k)
      and set k v = map.[k] <- v
    member this.Add(key, value) = map.Add(key, value)
    member this.AddFirst(key, value) = map.AddFirst(key, value)
    member this.AddLast(key, value) = map.AddLast(key, value)
    member this.Remove(key) = map.Remove(key)
    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      map.RemoveFirst(&result)
    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      map.RemoveLast(&result)
    member this.RemoveMany(key:'K,direction:Lookup) = 
      map.RemoveMany(key, direction) |> ignore
      ()
    
  interface ISeries<'K, 'V> with
    member this.GetCursor() = map.GetCursor()
    member this.GetAsyncEnumerator() = map.GetAsyncEnumerator()





