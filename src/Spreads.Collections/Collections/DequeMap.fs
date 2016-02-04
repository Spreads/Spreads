(*  
    Copyright (c) 2014-2016 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
        
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

namespace Spreads.Collections.Experimental

open System
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks


open Spreads
open Spreads.Collections

[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedDequeMap<'K,'V>
  internal(dictionary:IDictionary<'K,'V> option, capacity:int option, comparerOpt:IComparer<'K> option) as this=
  inherit Series<'K,'V>()
  
  [<DefaultValueAttribute>]
  val mutable internal version : int
  [<DefaultValueAttribute>]
  val mutable internal sd : SortedDeque<KVP<'K,'V>>


  [<NonSerializedAttribute>]
  let mutable comparer : IComparer<'K> = 
    if comparerOpt.IsNone || Comparer<'K>.Default.Equals(comparerOpt.Value) then
      let kc = KeyComparer.GetDefault<'K>()
      if kc = Unchecked.defaultof<_> then Comparer<'K>.Default :> IComparer<'K> 
      else kc
    else comparerOpt.Value // do not try to replace with KeyComparer if a comparer was given

  
  [<DefaultValueAttribute>]
  [<NonSerializedAttribute>]
  val mutable internal isSynchronized : bool
  [<DefaultValueAttribute>]
  [<NonSerializedAttribute>]
  val mutable internal isReadOnly : bool
  [<NonSerializedAttribute>]
  let syncRoot = new Object()

  [<NonSerializedAttribute>]
  let isKeyReferenceType = not typeof<'K>.IsValueType


  do
    this.isSynchronized <- false
    this.isReadOnly <- false
    let capacity = if capacity.IsSome then capacity.Value else 2
    this.sd <- new SortedDeque<KVP<'K,'V>>(capacity, KVPKeyComparer(comparer))
    if dictionary.IsSome then
      for kvp in dictionary.Value do
        this.sd.Add(KVP(kvp.Key, kvp.Value))

  //#endregion


#if FX_NO_BINARY_SERIALIZATION
#else
  [<System.Runtime.Serialization.OnSerializingAttribute>]
  member internal __.OnSerializing(context: System.Runtime.Serialization.StreamingContext) =
    ignore(context)
    ()

  [<System.Runtime.Serialization.OnDeserializedAttribute>]
  member internal __.OnDeserialized(context: System.Runtime.Serialization.StreamingContext) =
    ignore(context)


#endif
    //#region Private & Internal members
  
  member internal this.GetByIndex(index) = this.sd.[index]

  member this.IsReadOnly with get() = this.isReadOnly
  member this.Complete() = this.isReadOnly <- true

  member internal this.IsSynchronized 
    with get() =  this.isSynchronized
    and set(synced:bool) = 
      let entered = enterLockIf syncRoot  this.isSynchronized
      this.isSynchronized <- synced
      exitLockIf syncRoot entered

  member this.SyncRoot with get() = syncRoot

  member this.Version with get() = this.version and set v = this.version <- v


  //#endregion


  //#region Public members

  member this.Capacity 
    with get() = this.sd.buffer.Length
    //and set(value) = failwith "TODO"
  member this.Comparer with get() = comparer

  member this.Clear() = 
    this.sd.Clear()
    this.version <- this.version + 1

  member this.Count with get() = this.sd.count

  member this.IsEmpty with get() = this.Count = 0


  member this.Keys 
    with get() : IList<'K> =
      {new IList<'K> with
        member x.Count with get() = this.Count
        member x.IsReadOnly with get() = true
        member x.Item 
          with get index : 'K = this.GetByIndex(index).Key
          and set index value = raise (NotSupportedException("Keys collection is read-only"))
        member x.Add(k) = raise (NotSupportedException("Keys collection is read-only"))
        member x.Clear() = raise (NotSupportedException("Keys collection is read-only"))
        member x.Contains(key) = this.ContainsKey(key)
        member x.CopyTo(array, arrayIndex) =
          let mutable arrayIndex = arrayIndex
          for kvp in this.sd do
            array.[arrayIndex] <- kvp.Key
            increment &arrayIndex
        member x.IndexOf(key:'K) = this.IndexOfKey(key)
        member x.Insert(index, value) = raise (NotSupportedException("Keys collection is read-only"))
        member x.Remove(key:'K) = raise (NotSupportedException("Keys collection is read-only"))
        member x.RemoveAt(index:int) = raise (NotSupportedException("Keys collection is read-only"))
        member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
        member x.GetEnumerator() : IEnumerator<'K> = 
          let index = ref 0
          let eVersion = ref this.version
          let currentKey : 'K ref = ref Unchecked.defaultof<'K>
          { new IEnumerator<'K> with
            member e.Current with get() = currentKey.Value
            member e.Current with get() = box e.Current
            member e.MoveNext() = 
              if eVersion.Value <> this.version then
                raise (InvalidOperationException("Collection changed during enumeration"))
              if index.Value < this.sd.count then
                currentKey := this.sd.[index.Value].Key
                incr index
                true
              else
                index := this.Count + 1
                currentKey := Unchecked.defaultof<'K>
                false
            member e.Reset() = 
              if eVersion.Value <> this.version then
                raise (InvalidOperationException("Collection changed during enumeration"))
              index := 0
              currentKey := Unchecked.defaultof<'K>
            member e.Dispose() = 
              index := 0
              currentKey := Unchecked.defaultof<'K>
          }
      }

  member this.Values 
    with get() : IList<'V> =
      { new IList<'V> with
        member x.Count with get() = this.Count
        member x.IsReadOnly with get() = true
        member x.Item 
          with get index : 'V = this.GetByIndex(index).Value
          and set index value = raise (NotSupportedException("Values colelction is read-only"))
        member x.Add(k) = raise (NotSupportedException("Values colelction is read-only"))
        member x.Clear() = raise (NotSupportedException("Values colelction is read-only"))
        member x.Contains(value) = this.ContainsValue(value)
        member x.CopyTo(array, arrayIndex) = 
          let mutable arrayIndex = arrayIndex
          for kvp in this.sd do
            array.[arrayIndex] <- kvp.Value
            increment &arrayIndex
        member x.IndexOf(value:'V) = this.IndexOfValue(value)
        member x.Insert(index, value) = raise (NotSupportedException("Values colelction is read-only"))
        member x.Remove(value:'V) = raise (NotSupportedException("Values colelction is read-only"))
        member x.RemoveAt(index:int) = raise (NotSupportedException("Values colelction is read-only"))
        member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
        member x.GetEnumerator() : IEnumerator<'V> = 
          let index = ref 0
          let eVersion = ref this.version
          let currentValue : 'V ref = ref Unchecked.defaultof<'V>
          { new IEnumerator<'V> with
            member e.Current with get() = currentValue.Value
            member e.Current with get() = box e.Current
            member e.MoveNext() = 
              if eVersion.Value <> this.version then
                raise (InvalidOperationException("Collection changed during enumeration"))
              if index.Value < this.Count then
                currentValue := this.sd.[index.Value].Value
                index := index.Value + 1
                true
              else
                index := this.Count + 1
                currentValue := Unchecked.defaultof<'V>
                false
            member e.Reset() = 
              if eVersion.Value <> this.version then
                raise (InvalidOperationException("Collection changed during enumeration"))
              index := 0
              currentValue := Unchecked.defaultof<'V>
            member e.Dispose() = 
              index := 0
              currentValue := Unchecked.defaultof<'V>
          }
        }

  member this.ContainsKey(key) = this.IndexOfKey(key) >= 0

  member this.ContainsValue(value) = this.IndexOfValue(value) >= 0

  member this.IndexOfValue(value:'V) : int =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let mutable res = 0
      let mutable found = false
      let valueComparer = Comparer<'V>.Default;
      while not found do
          if valueComparer.Compare(value,this.sd.[res].Value) = 0 then
              found <- true
          else res <- res + 1
      if found then res else -1
    finally
      exitLockIf syncRoot entered

  member this.IndexOfKey(key:'K) : int = 
    if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
      raise (ArgumentNullException("key"))
    this.sd.IndexOfElement(KVP(key, Unchecked.defaultof<_>))
    
  member this.First
    with get() = 
      if this.Count = 0 then raise (InvalidOperationException("Could not get the first element of an empty map"))
      this.sd.First

  member this.Last
    with get() =
      if this.Count = 0 then raise (InvalidOperationException("Could not get the last element of an empty map"))
      this.sd.Last

  member this.Item
    with get key =
      let entered = enterLockIf syncRoot  this.isSynchronized
      try
        // first/last optimization (only last here)
        if this.Count = 0 then
          raise (KeyNotFoundException())
        else
          let idx = this.sd.IndexOfElement(KVP(key, Unchecked.defaultof<_>))
          if idx >= 0 then
            this.sd.[idx].Value
          else
              raise (KeyNotFoundException())
      finally
        exitLockIf syncRoot entered
    and set k v =
      if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
      this.sd.Set(KVP(k,v))
      this.version <- this.version + 1

   // In-place (mutable) addition
  member this.Add(key, value) : unit =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
        raise (ArgumentNullException("key"))
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      this.sd.Add(KVP(key,value))
      this.version <- this.version + 1
    finally
      exitLockIf syncRoot entered

  member this.AddLast(key, value):unit =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let c = comparer.Compare(key, this.sd.Last.Key) 
      if c > 0 then 
        this.sd.Add(KVP(key,value))
        this.version <- this.version + 1
      else raise (ArgumentOutOfRangeException("New key is smaller or equal to the largest existing key"))
    finally
      exitLockIf syncRoot entered

  member this.AddFirst(key, value):unit =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let c = comparer.Compare(key, this.sd.Last.Key) 
      if c < 0 then
        this.sd.Add(KVP(key,value))
        this.version <- this.version + 1
      else raise (ArgumentOutOfRangeException("New key is smaller or equal to the largest existing key"))
    finally
      exitLockIf syncRoot entered
    
  member public this.RemoveAt(index):unit =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      this.sd.RemoveAt(index)
      this.version <- this.version + 1
    finally
      exitLockIf syncRoot entered

  member this.Remove(key):bool =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if this.Count > 0 then
        try
          this.sd.Remove(KVP(key, Unchecked.defaultof<_>)) |> ignore
          this.version <- this.version + 1
          true
        with | _ -> false
      else false
    finally
      exitLockIf syncRoot entered

  member this.RemoveFirst([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if this.Count > 0 then
        result <- this.sd.RemoveFirst()
        this.version <- this.version + 1
        true
      else false
    finally
      exitLockIf syncRoot entered

  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if this.Count > 0 then
        result <- this.sd.RemoveLast()
        this.version <- this.version + 1
        true
      else false
    finally
      exitLockIf syncRoot entered

  member this.RemoveMany(key:'K,direction:Lookup):bool =
    if this.isReadOnly then invalidOp "SortedDequeMap is read-only"
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let mutable result = false
      match direction with
      | Lookup.EQ -> result <- this.Remove(key)
      | Lookup.LT ->
        while this.Comparer.Compare(key, this.sd.First.Key) > 0 do
          this.sd.RemoveFirst() |> ignore
          result <- true
      | Lookup.LE ->
        while this.Comparer.Compare(key, this.sd.First.Key) >= 0 do
          this.sd.RemoveFirst() |> ignore
          result <- true
      | Lookup.GT -> 
        while this.Comparer.Compare(key, this.sd.Last.Key) < 0 do
          this.sd.RemoveLast() |> ignore
          result <- true
      | Lookup.GE ->
        while this.Comparer.Compare(key, this.sd.Last.Key) <= 0 do
          this.sd.RemoveLast() |> ignore
          result <- true
      | _ -> failwith "wrong direction"
      result
    finally
      exitLockIf syncRoot entered
    
  /// Returns the index of found KeyValuePair or a negative value:
  /// -1 if the non-found key is smaller than the first key
  /// -2 if the non-found key is larger than the last key
  /// -3 if the non-found key is within the key range (for EQ direction only)
  /// Example: (-1) [...current...(-3)...map ...] (-2)
  member internal this.TryFindWithIndex(key:'K,direction:Lookup, [<Out>]result: byref<KeyValuePair<'K, 'V>>) : int =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      match direction with
      | Lookup.EQ ->
        let index = this.IndexOfKey(key)
        if index >= 0 then
          result <-  this.GetByIndex(index)
          index
        else
          let index2 = ~~~index
          if index2 >= this.Count then // there are no elements larger than key, all this.keys are smaller
            -2 // the key could be in the next bucket
          elif index2 = 0 then //it is the index of the first element that is larger than value
            -1 // all this.keys in the map are larger than the desired key
          else
            -3
      | Lookup.LT ->
        let lastIdx = this.Count-1
        let lc = if this.Count > 0 then comparer.Compare(key, this.Last.Key) else -2
        if lc = 0 then // key = last key
          result <-  this.GetByIndex(lastIdx-1) // return item beforelast
          lastIdx - 1
        elif lc = 1 then // key greater than the last
          result <-  this.GetByIndex(lastIdx) // return the last item 
          lastIdx
        else
          let index = this.IndexOfKey(key)
          if index > 0 then
            result <- this.GetByIndex(index - 1)
            index - 1
          elif index = 0 then
             -1 // 
          else
            let index2 = ~~~index
            if index2 >= this.Count then // there are no elements larger than key
              result <-  this.GetByIndex(this.Count - 1) // last element is the one that LT key
              this.Count - 1
            elif index2 = 0 then
              -1
            else //  it is the index of the first element that is larger than value
              result <-  this.GetByIndex(index2 - 1)
              index2 - 1
      | Lookup.LE ->
        let lastIdx = this.Count-1
        let lc = if this.Count > 0 then comparer.Compare(key, this.Last.Key) else -2
        if lc >= 0 then // key = last key or greater than the last key
          result <-  this.GetByIndex(lastIdx)
          lastIdx
        else
          let index = this.IndexOfKey(key)
          if index >= 0 then
            result <-  this.GetByIndex(index) // equal
            index
          else
            let index2 = ~~~index
            if index2 >= this.Count then // there are no elements larger than key
              result <-  this.GetByIndex(this.Count - 1)
              this.Count - 1
            elif index2 = 0 then
              -1
            else //  it is the index of the first element that is larger than value
              result <-   this.GetByIndex(index2 - 1)
              index2 - 1
      | Lookup.GT ->
        let lc = if this.Count > 0 then comparer.Compare(key, this.First.Key) else 2
        if lc = 0 then // key = first key
          result <-  this.GetByIndex(1) // return item after first
          1
        elif lc = -1 then
          result <-  this.GetByIndex(0) // return first
          0
        else
          let index = this.IndexOfKey(key)
          if index >= 0 && index < this.Count - 1 then
            result <- this.GetByIndex(index + 1)
            index + 1
          elif index >= this.Count - 1 then
            -2
          else
            let index2 = ~~~index
            if index2 >= this.Count then // there are no elements larger than key
              -2
            else //  it is the index of the first element that is larger than value
              result <- this.GetByIndex(index2)
              index2
      | Lookup.GE ->
        let lc = if this.Count > 0 then comparer.Compare(key, this.First.Key) else 2
        if lc <= 0 then // key = first key or smaller than the first key
          result <-  this.GetByIndex(0)
          0
        else
          let index = this.IndexOfKey(key)
          if index >= 0 && index < this.Count then
            result <-  this.GetByIndex(index) // equal
            index
          else
            let index2 = ~~~index
            if index2 >= this.Count then // there are no elements larger than key
              -2
            else //  it is the index of the first element that is larger than value
              result <-   this.GetByIndex(index2)
              index2
      | _ -> raise (ApplicationException("Wrong lookup direction"))
    finally
      exitLockIf syncRoot entered


  member this.TryFind(k:'K, direction:Lookup, [<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
    let mutable kvp = Unchecked.defaultof<_>
    let idx = this.TryFindWithIndex(k, direction, &kvp)
    if idx >= 0 then
        res <- kvp
        true
    else
        false

  /// Return true if found exact key
  member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      // first/last optimization
      if this.Count = 0 then
        value <- Unchecked.defaultof<'V>
        false
      else
        let lc = comparer.Compare(key, this.Last.Key) 
        if lc = 0 then // key = last key
          value <- this.Last.Value
          true
        else   
          let index = this.IndexOfKey(key)
          if index >= 0 then
            value <- this.GetByIndex(index).Value
            true
          else
            value <- Unchecked.defaultof<'V>
            false
    finally
      exitLockIf syncRoot entered


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

  override this.GetCursor() = new SortedDequeMapCursor<_,_>(this) :> ICursor<_,_>

  // foreach optimization
  member this.GetEnumerator() = new SortedDequeMapCursor<'K,'V>(this)
  //#endregion


  //#region Interfaces

  interface IEnumerable with
    member this.GetEnumerator() = this.GetCursor() :> IEnumerator

  interface IEnumerable<KeyValuePair<'K,'V>> with
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
      this.GetCursor() :> IEnumerator<KeyValuePair<'K,'V>>

  interface ICollection  with
    member this.SyncRoot = syncRoot
    member this.CopyTo(array, arrayIndex) =
      if array = null then raise (ArgumentNullException("array"))
      if arrayIndex < 0 || arrayIndex > array.Length then raise (ArgumentOutOfRangeException("arrayIndex"))
      if array.Length - arrayIndex < this.Count then raise (ArgumentException("ArrayPlusOffTooSmall"))
      for index in 0..this.Count do
        let kvp = this.sd.[index]
        array.SetValue(kvp, arrayIndex + index)
    member this.Count = this.Count
    member this.IsSynchronized with get() =  this.isSynchronized

  interface IDictionary<'K,'V> with
    member this.Count = this.Count
    member this.IsReadOnly with get() = this.IsReadOnly
    member this.Item
      with get key = this.Item(key)
      and set key value = this.[key] <- value
    member this.Keys with get() = this.Keys :> ICollection<'K>
    member this.Values with get() = this.Values :> ICollection<'V>
    member this.Clear() = this.Clear()
    member this.ContainsKey(key) = this.ContainsKey(key)
    member this.Contains(kvp:KeyValuePair<'K,'V>) = this.ContainsKey(kvp.Key)
    member this.CopyTo(array, arrayIndex) =
      if array = null then raise (ArgumentNullException("array"))
      if arrayIndex < 0 || arrayIndex > array.Length then raise (ArgumentOutOfRangeException("arrayIndex"))
      if array.Length - arrayIndex < this.Count then raise (ArgumentException("ArrayPlusOffTooSmall"))
      for index in 0..this.Count do
        let kvp = KeyValuePair(this.Keys.[index], this.Values.[index])
        array.[arrayIndex + index] <- kvp
    member this.Add(key, value) = this.Add(key, value)
    member this.Add(kvp:KeyValuePair<'K,'V>) = this.Add(kvp.Key, kvp.Value)
    member this.Remove(key) = this.Remove(key)
    member this.Remove(kvp:KeyValuePair<'K,'V>) = this.Remove(kvp.Key)
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
      let index = this.IndexOfKey(key)
      if index >= 0 then
        value <- this.GetByIndex(index).Value
        true
      else
        value <- Unchecked.defaultof<'V>
        false

  interface IReadOnlyOrderedMap<'K,'V> with
    member this.Comparer with get() = comparer
    member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = this.GetCursor()
    member this.IsEmpty = this.Count = 0
    member this.IsIndexed with get() = false
    member this.IsReadOnly with get() = this.IsReadOnly
    //member this.Count with get() = int this.size
    member this.First with get() = this.First
    member this.Last with get() = this.Last
    member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.TryFindWithIndex(k, direction, &result) >=0
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
    member this.TryGetValue(k, [<Out>] value:byref<'V>) = this.TryGetValue(k, &value)
    member this.Item with get k = this.Item(k)
    member this.GetAt(idx:int) = this.GetByIndex(idx).Value
    member this.Keys with get() = this.Keys :> IEnumerable<'K>
    member this.Values with get() = this.Values :> IEnumerable<'V>
    member this.SyncRoot with get() = syncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Version with get() = int64(this.Version)
    member this.Complete() = this.Complete()
    member this.Count with get() = int64(this.Count)
    member this.Item with get k = this.Item(k) and set (k:'K) (v:'V) = this.[k] <- v
    member this.Add(k, v) = this.Add(k,v)
    member this.AddLast(k, v) = this.AddLast(k, v)
    member this.AddFirst(k, v) = this.AddFirst(k, v)
    member this.Remove(k) = this.Remove(k)
    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.RemoveFirst(&result)
    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.RemoveLast(&result)
    member this.RemoveMany(key:'K,direction:Lookup) = 
      this.RemoveMany(key, direction)
    member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>, option:AppendOption) =
//      for i in appendMap do
//        this.AddLast(i.Key, i.Value)
      raise (NotImplementedException("TODO append implementation"))
    
 
  //#endregion   

  //#region Constructors

  new() = SortedDequeMap(None, None, None)
  new(comparer:IComparer<'K>) = SortedDequeMap(None, None, Some(comparer))
  new(dictionary:IDictionary<'K,'V>) = SortedDequeMap(Some(dictionary), Some(dictionary.Count), None)
  new(capacity:int) = SortedDequeMap(None, Some(capacity), None)
  new(dictionary:IDictionary<'K,'V>,comparer:IComparer<'K>) = SortedDequeMap(Some(dictionary), Some(dictionary.Count), Some(comparer))
  new(capacity:int,comparer:IComparer<'K>) = SortedDequeMap(None, Some(capacity), Some(comparer))

  static member internal OfKeysAndValues(size:int, keys:'K[],values:'V[]) =
    let sdm = new SortedDequeMap<'K,'V>()
    for i in 0..(size - 1) do
      sdm.Add(keys.[i], values.[i])
    sdm
  //#endregion


and
  SortedDequeMapCursor<'K,'V> =
    struct
      val public source : SortedDequeMap<'K,'V>
      val mutable index : int
      val mutable current : KVP<'K,'V>
      val mutable cursorVersion : int
      val mutable isBatch : bool

      new(source:SortedDequeMap<'K,'V>) = 
        { source = source; 
          index = -1;
          current = Unchecked.defaultof<_>;
          cursorVersion = source.version;
          isBatch = false;
        }
    end

    member this.Comparer: IComparer<'K> = this.source.Comparer
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.source.TryGetValue(key, &value)
    member this.MoveNext() =
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.cursorVersion =  this.source.version then
          if this.index < (this.source.Count - 1) then
            this.index <- this.index + 1
            // TODO!! (perf) regular keys were supposed to speed up things, not to slow down by 50%! 
            this.current <- this.source.GetByIndex(this.index)
            true
          else
            false // NB! Do not reset cursor on false MoveNext
        else  // source change
          this.cursorVersion <- this.source.version // update state to new this.source.version
          let mutable kvp = Unchecked.defaultof<_>
          let position = this.source.TryFindWithIndex(this.current.Key, Lookup.GT, &kvp) // reposition cursor after source change //currentKey.Value
          if position > 0 then
            this.index <- position
            this.current <- kvp
            true
          else  // not found
            false // NB! Do not reset cursor on false MoveNext
      finally
        exitLockIf this.source.SyncRoot entered
    
    member this.MoveNext(ct: CancellationToken): Task<bool> = 
      if this.MoveNext() then trueTask else falseTask

    member this.Source: ISeries<'K,'V> = this.source :> ISeries<'K,'V>      
    member this.IsContinuous with get() = false
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = 
      let entered = enterLockIf this.source.SyncRoot  this.source.IsSynchronized
      try
        // TODO! how to do this correct for mutable case. Looks like impossible without copying
        if this.isBatch then
          Trace.Assert(this.index = this.source.Count - 1)
          Trace.Assert(this.source.IsReadOnly)
          this.source :> IReadOnlyOrderedMap<'K,'V>
        else raise (InvalidOperationException("SortedMap cursor is not at a batch position"))
      finally
        exitLockIf this.source.SyncRoot entered

    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> =
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if (this.source.IsReadOnly) && (this.index = -1) then
          this.index <- this.source.Count - 1 // at the last element of the batch
          this.current <- this.source.GetByIndex(this.index)
          this.isBatch <- true
          trueTask
        else falseTask
      finally
        exitLockIf this.source.SyncRoot entered

    member this.Clone() = 
      let mutable clone = new SortedDequeMapCursor<'K,'V>(this.source)
      clone.index <- this.index
      clone.current <- this.current
      clone.isBatch <- this.isBatch
      clone

    member this.MovePrevious() = 
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.index = -1 then this.MoveLast()  // first move when index = -1
        elif this.cursorVersion =  this.source.version then
          if this.index > 0 && this.index < this.source.Count then
            this.index <- this.index - 1
            this.current <- this.source.GetByIndex(this.index)
            true
          else
            //p.Reset()
            false
        else
          this.cursorVersion <-  this.source.version // update state to new this.source.version
          let mutable kvp = Unchecked.defaultof<_>
          let position = this.source.TryFindWithIndex(this.current.Key, Lookup.LT, &kvp)
          if position > 0 then
            this.index <- position
            this.current <- kvp
            true
          else  // not found
            //p.Reset()
            false
      finally
        exitLockIf this.source.SyncRoot entered

    member this.MoveAt(key:'K, lookup:Lookup) = 
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        let mutable kvp = Unchecked.defaultof<_>
        let position = this.source.TryFindWithIndex(key, lookup, &kvp)
        if position >= 0 then
          this.index <- position
          this.current <- kvp
          true
        else
          this.Reset()
          false
      finally
        exitLockIf this.source.SyncRoot entered

    member this.MoveFirst() = 
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.source.Count > 0 then
          this.index <- 0
          this.current <- this.source.GetByIndex(this.index)
          true
        else
          this.Reset()
          false
      finally
        exitLockIf this.source.SyncRoot entered

    member this.MoveLast() = 
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.source.Count > 0 then
          this.index <- this.source.Count - 1
          this.current <- this.source.GetByIndex(this.index)
          true
        else
          this.Reset()
          false
      finally
        exitLockIf this.source.SyncRoot entered

    member this.CurrentKey with get() = this.current.Key
    member this.CurrentValue with get() = this.current.Value
    member this.Current with get() : KVP<'K,'V> = this.current

    member this.Reset() = 
      this.cursorVersion <-  this.source.version
      this.index <- -1

    member this.Dispose() = this.Reset()

    interface IDisposable with
      member this.Dispose() = this.Dispose()

    interface IEnumerator<KVP<'K,'V>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext():bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V> = this.Current
      member this.Current with get(): obj = this.Current :> obj

    interface IAsyncEnumerator<KVP<'K,'V>> with
      member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 

    interface ICursor<'K,'V> with
      member this.Comparer with get() = this.source.Comparer
      member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = this.CurrentBatch
      member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatch(cancellationToken)
      member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
      member this.MoveFirst():bool = this.MoveFirst()
      member this.MoveLast():bool =  this.MoveLast()
      member this.MovePrevious():bool = this.MovePrevious()
      member this.CurrentKey with get():'K = this.CurrentKey
      member this.CurrentValue with get():'V = this.CurrentValue
      member this.Source with get() = this.source :> ISeries<'K,'V>
      member this.Clone() = this.Clone() :> ICursor<'K,'V>
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.source.TryGetValue(key, &value)
