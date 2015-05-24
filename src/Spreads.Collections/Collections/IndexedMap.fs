namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Collections.ObjectModel
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads

[<AllowNullLiteral>]
[<SerializableAttribute>]
type private KC<'K,'V>()=
  inherit KeyedCollection<'K, int*KeyValuePair<'K,'V>>()
  override this.GetKeyForItem(i) = (snd i).Key

[<AllowNullLiteral>]
[<SerializableAttribute>]
type internal IndexedMap<'K,'V when 'K : comparison>
  private(kc:KC<'K,'V>)=

  [<NonSerializedAttribute>]
  let mutable kc = kc
    
  // WARNING: The compiled name of this field may never be changed because it is part of the logical 
  // WARNING: permanent serialization format for this type.
  let mutable serializedData : KeyValuePair<'K,'V> array = null // 
  [<NonSerializedAttribute>]
  let syncRoot = obj()

  member private this.Clone() =
    let newKc = KC<'K,'V>()
    for existing in kc do
      newKc.Add(existing)
    IndexedMap(newKc)

  member internal this.SyncRoot with get() = syncRoot // unused valriable during runtime

  member this.IsEmpty with get() = kc.Count = 0
    
  /// Retrieving the value of this property is an O(1) operation.
  member this.Size with get() = kc.Count

  member this.First 
    with get() = 
      if kc.Count > 0 then snd kc.[0]
      else raise (InvalidOperationException("Could not get the first element of an empty map"))

  member this.Last 
    with get() = 
      if kc.Count > 0 then snd kc.[kc.Count-1]
      else raise (InvalidOperationException("Could not get the last element of an empty map"))

  member this.AddLast(k:'K, v:'V) : unit =
    kc.Add(kc.Count, KeyValuePair(k, v))

  member this.AddFirst(k:'K, v:'V)  =
    kc.Insert(0, (0, KeyValuePair(k, v)))

  member this.Add(k:'K, v:'V) =
    kc.Add(kc.Count, KeyValuePair(k, v))

  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    result <- this.Last
    kc.Remove(result.Key)

  member this.RemoveFirst([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    result <- this.First
    kc.Remove(result.Key)

  member this.Remove(k:'K) =
    kc.Remove(k)

  member this.RemoveMany(key:'K,direction:Lookup):bool =
    match direction with
    | Lookup.EQ ->
      this.Remove(key)
    | _ ->
      raise (InvalidOperationException("Indexed series do not support directional operations"))
    

  member this.TryFind(k:'K,lu:Lookup, [<Out>]res: byref<KeyValuePair<'K, 'V>>):bool =
    res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
    match lu with
    | Lookup.EQ -> 
        if kc.Contains(k) then
            res <- snd kc.[k]
            true
        else
            false
    | Lookup.LT -> // previous by index
      if kc.Contains(k) then
        let cur = kc.[k]
        if fst cur > 0 then 
          res <- snd kc.[(fst cur) - 1]
          true
        else
          false
      else
        false
    | Lookup.GT -> // next by index
      if kc.Contains(k) then
        let cur = kc.[k]
        if fst cur < kc.Count - 1 then 
          res <- snd kc.[(fst cur) + 1]
          true
        else
          false
      else
        false
    | Lookup.GE | Lookup.LE -> 
      raise (InvalidOperationException("Indexed series do not support directional operations"))
    | _ -> raise (ApplicationException("Wrong lookup direction"))


  member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
    try
      res <- this.First
      true
    with
    | _ -> 
      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
      false
            
  member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
    try
      res <- this.Last
      true
    with
    | _ -> 
      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
      false
        
  member this.TryGetValue(k, [<Out>] value:byref<'V>) = 
    let success, pair = this.TryFind(k, Lookup.EQ)
    if success then 
      value <- pair.Value
      true
    else false

  member this.Item 
    with get(k : 'K) = 
      if kc.Contains(k) then
        (snd kc.[k]).Value
      else
        raise (KeyNotFoundException())
    and set k v =
      ()
       
       
  member this.GetPointer() = new BasePointer<'K,'V>(this) :> ICursor<'K,'V>     

  new() =  IndexedMap(KC<'K,'V>())

  static member Empty() = IndexedMap()
  static member Create(input:seq<KeyValuePair<'K,'V>>) =
    let newKc = KC()
    for i in input do
      newKc.Add(newKc.Count, KeyValuePair(i.Key, i.Value))
    IndexedMap(newKc)

//    static member Empty : ImmutableSortedMap<'Key,'Value> = empty
//
//    static member Create(elements : IEnumerable<KeyValuePair<'Key, 'Value>>) : ImmutableSortedMap<'Key,'Value> = 
//        let comparer = LanguagePrimitives.FastGenericComparer<'Key> 
//        new ImmutableSortedMap<_,_>(comparer,MapTree.ofSeq comparer (elements |> Seq.map (fun x -> x.Key,x.Value) ))
//    
//    static member Create() : ImmutableSortedMap<'Key,'Value> = empty
//
//    new(elements : IEnumerable<KeyValuePair<'Key, 'Value>>) = 
//        let comparer = LanguagePrimitives.FastGenericComparer<'Key> 
//        new ImmutableSortedMap<_,_>(comparer,MapTree.ofSeq comparer (elements |> Seq.map (fun x -> x.Key,x.Value) ))
    
    
  interface IEnumerable with
     member this.GetEnumerator() = this.GetPointer() :> IEnumerator

  interface IEnumerable<KeyValuePair<'K,'V>> with
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
      this.GetPointer() :> IEnumerator<KeyValuePair<'K,'V>>


  interface IReadOnlySortedMap<'K,'V> with
    member this.IsEmpty = this.IsEmpty
    member this.IsIndexed with get() = false
    //member this.Count with get() = int this.Size
    member this.First with get() = this.First
    member this.Last with get() = this.Last
    member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.TryFind(k, direction, &result)
    member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      try
        res <- this.First
        true
      with
      | _ -> 
        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        false
    member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      try
        res <- this.Last
        true
      with
      | _ -> 
        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        false
    member this.TryGetValue(k, [<Out>] value:byref<'V>) = 
      this.TryGetValue(k, &value)
    member this.GetCursor() = this.GetPointer()
    member this.Item with get k = this.Item(k)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)

    member this.SyncRoot with get() = this.SyncRoot
    member this.Size with get() = int64(this.Size)

  interface ISortedMap<'K,'V> with
    member this.Item with get k = this.Item(k) and set (k:'K) (v:'V) = this.[k] <- v
    member this.Add(k, v) = this.Add(k,v)
    member this.AddLast(k, v) = this.AddLast(k, v)
    member this.AddFirst(k, v) = this.AddFirst(k, v)
    member this.Remove(k) = this.Remove(k)
    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.RemoveFirst(&result) |> ignore
    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.RemoveLast(&result) |> ignore
    member this.RemoveMany(key:'K,direction:Lookup) = 
      this.RemoveMany(key, direction) |> ignore
      ()

    

  interface IImmutableSortedMap<'K,'V> with
    
    /// Immutable addition, returns a new map with added value
    member this.Add(key, value):IImmutableSortedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.Add(key, value)
      newMap :> IImmutableSortedMap<'K,'V>

    member this.AddFirst(key, value):IImmutableSortedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.AddFirst(key, value)
      newMap :> IImmutableSortedMap<'K,'V>

    member this.AddLast(key, value):IImmutableSortedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.AddLast(key, value)
      newMap :> IImmutableSortedMap<'K,'V>

    member this.Remove(key):IImmutableSortedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.Remove(key) |> ignore
      newMap :> IImmutableSortedMap<'K,'V>

    member this.RemoveLast([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableSortedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.RemoveLast(&value) |> ignore
      newMap :> IImmutableSortedMap<'K,'V>

    member this.RemoveFirst([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableSortedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.RemoveFirst(&value) |> ignore
      newMap :> IImmutableSortedMap<'K,'V>

    member this.RemoveMany(key:'K,direction:Lookup):IImmutableSortedMap<'K,'V>=
      let newMap = this.Clone()
      newMap.RemoveMany(key, direction) |> ignore
      newMap :> IImmutableSortedMap<'K,'V>

