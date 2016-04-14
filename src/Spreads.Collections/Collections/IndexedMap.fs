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

namespace Spreads.Collections

open System
open System.Linq
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks


open Spreads
open Spreads.Collections

// This could be done as a base class OrderedMap and derived classes IndexedMap and IndexedMap
// with the only difference in an abstract method GetIndexOfElement, but IndexedMap is too
// complicated with regular keys stuff and it will require most methods to be abstract

/// Mutable indexed IOrderedMap<'K,'V> implementation based on IndexedMap<'K,'V>
[<AllowNullLiteral>]
[<SerializableAttribute>]
type IndexedMap<'K,'V> // when 'K:equality
  internal(dictionary:IDictionary<'K,'V> option, capacity:int option, comparerOpt:IComparer<'K> option) as this=
  inherit Series<'K,'V>()

  //#region Main internal constructor
    
  // data fields
  let mutable version : int = 0 // enumeration doesn't lock but checks version
  [<DefaultValueAttribute>] // if size > 2 and keys.Length = 2 then the keys are regular
  val mutable internal size : int
  [<DefaultValueAttribute>]
  val mutable internal keys : 'K array
  [<DefaultValueAttribute>]
  val mutable internal values : 'V array


  [<NonSerializedAttribute>]
  // This type is logically immutable. This field is only mutated during deserialization. 
  let mutable comparer : IComparer<'K> = 
    if comparerOpt.IsNone || Comparer<'K>.Default.Equals(comparerOpt.Value) then
      let kc = KeyComparer.GetDefault<'K>()
      if kc = Unchecked.defaultof<_> then Comparer<'K>.Default :> IComparer<'K> 
      else kc
    else comparerOpt.Value
  [<NonSerializedAttribute>]
  let isKeyReferenceType : bool = not typeof<'K>.IsValueType
  [<NonSerializedAttribute>]
  let mutable cursorCounter : int = 1

  [<NonSerializedAttribute>]
  let mutable isSynchronized : bool = false
  [<NonSerializedAttribute>]
  let mutable isMutable : bool = true
  [<NonSerializedAttribute>]
  let syncRoot = new Object()
  [<NonSerializedAttribute>]
  let mutable mapKey = ""

  do
    let tempCap = if capacity.IsSome then capacity.Value else 1
    if dictionary.IsNone then // otherwise we will set them in dict processing part
      this.keys <- OptimizationSettings.ArrayPool.Take tempCap
    this.values <- OptimizationSettings.ArrayPool.Take tempCap

    if dictionary.IsSome && dictionary.Value.Count > 0 then
      match dictionary.Value with
      | :? IndexedMap<'K,'V> as map ->
        let entered = enterLockIf map.SyncRoot map.IsSynchronized
        try
          this.Capacity <- map.Capacity
          this.size <- map.size
          this.IsSynchronized <- map.IsSynchronized
          map.keys.CopyTo(this.keys, 0)
          map.values.CopyTo(this.values, 0)
        finally
          exitLockIf map.SyncRoot entered 
      | _ ->
        if capacity.IsSome && capacity.Value < dictionary.Value.Count then 
          raise (ArgumentException("capacity is less then dictionary this.size"))
        else
          this.Capacity <- dictionary.Value.Count
        dictionary.Value.Keys.CopyTo(this.keys, 0)
        dictionary.Value.Values.CopyTo(this.values, 0)
        this.size <- dictionary.Value.Count
        
        
  //#endregion

#if FX_NO_BINARY_SERIALIZATION
#else
  [<System.Runtime.Serialization.OnSerializingAttribute>]
  member __.OnSerializing(context: System.Runtime.Serialization.StreamingContext) =
    ignore(context)

  [<System.Runtime.Serialization.OnDeserializedAttribute>]
  member __.OnDeserialized(context: System.Runtime.Serialization.StreamingContext) =
    ignore(context)

#endif


  //#region Private & Internal members

  member private this.Clone() = new IndexedMap<'K,'V>(Some(this :> IDictionary<'K,'V>), None, Some(comparer))

  member internal this.GetKeyByIndex(index) =
    this.keys.[index]
    
  member private this.GetPairByIndexUnchecked(index) = //inline
    KeyValuePair(this.keys.[index], this.values.[index]) 
  
  member private this.EnsureCapacity(min) = 
    let mutable num = this.values.Length * 2 
    if num > 2146435071 then num <- 2146435071
    if num < min then num <- min // either double or min if min > 2xprevious
    this.Capacity <- num


  member private this.Insert(index:int, k, v) = 
    if this.size = this.values.Length then this.EnsureCapacity(this.size + 1)
    if index > this.size then
      Console.WriteLine("debug me") |> ignore
    Trace.Assert(index <= this.size, "index must be <= this.size")
    if index < this.size then
      Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
      Array.Copy(this.values, index, this.values, index + 1, this.size - index);
    this.keys.[index] <- k
    this.values.[index] <- v     
    version <- version + 1
    this.size <- this.size + 1
    if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(k,v))
    
  member this.Complete() = 
    if isMutable then 
        isMutable <- false
        if cursorCounter > 0 then 
          this.onCompletedEvent.Trigger(true)
  member internal this.IsMutable with get() = isMutable
  override this.IsReadOnly with get() = not isMutable
  override this.IsIndexed with get() = false

  member this.IsSynchronized 
    with get() =  isSynchronized
    and set(synced:bool) = 
      let entered = enterLockIf syncRoot isSynchronized
      isSynchronized <- synced
      exitLockIf syncRoot entered

  member internal this.MapKey with get() = mapKey and set(key:string) = mapKey <- key

  member this.SyncRoot with get() = syncRoot
  member this.Version with get() = version and set v = version <- v

  //#endregion


  //#region Public members

  member this.Capacity
    with get() = this.values.Length
    and set(value) =
      let entered = enterLockIf syncRoot  isSynchronized
      try
        match value with
        | c when c = this.values.Length -> ()
        | c when c < this.size -> raise (ArgumentOutOfRangeException("Small capacity"))
        | c when c > 0 -> 
          let kArr : 'K array = OptimizationSettings.ArrayPool.Take(c)
          Array.Copy(this.keys, 0, kArr, 0, this.size)
          let toReturn = this.keys
          this.keys <- kArr
          OptimizationSettings.ArrayPool.Return(toReturn) |> ignore

          let vArr : 'V array = OptimizationSettings.ArrayPool.Take(c)
          Array.Copy(this.values, 0, vArr, 0, this.size)
          let toReturn = this.values
          this.values <- vArr
          OptimizationSettings.ArrayPool.Return(toReturn) |> ignore
        | _ -> ()
      finally
        exitLockIf syncRoot entered

  member this.Comparer with get() = Unchecked.defaultof<_>

  member this.Clear() =
    version <- version + 1
    Array.Clear(this.keys, 0, this.size)
    Array.Clear(this.values, 0, this.size)
    this.size <- 0
    ()

  member this.Count with get() = this.size

  member this.IsEmpty with get() = this.size = 0

  member this.Keys 
    with get() : IList<'K> =
      {new IList<'K> with
        member x.Count with get() = this.size
        member x.IsReadOnly with get() = true
        member x.Item 
          with get index : 'K = this.GetKeyByIndex(index)
          and set index value = raise (NotSupportedException("Keys collection is read-only"))
        member x.Add(k) = raise (NotSupportedException("Keys collection is read-only"))
        member x.Clear() = raise (NotSupportedException("Keys collection is read-only"))
        member x.Contains(key) = this.ContainsKey(key)
        member x.CopyTo(array, arrayIndex) = 
          Array.Copy(this.keys, 0, array, arrayIndex, this.size)
        member x.IndexOf(key:'K) = this.IndexOfKey(key)
        member x.Insert(index, value) = raise (NotSupportedException("Keys collection is read-only"))
        member x.Remove(key:'K) = raise (NotSupportedException("Keys collection is read-only"))
        member x.RemoveAt(index:int) = raise (NotSupportedException("Keys collection is read-only"))
        member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
        member x.GetEnumerator() : IEnumerator<'K> = 
          let index = ref 0
          let eVersion = ref version
          let currentKey : 'K ref = ref Unchecked.defaultof<'K>
          { new IEnumerator<'K> with
            member e.Current with get() = currentKey.Value
            member e.Current with get() = box e.Current
            member e.MoveNext() = 
              if eVersion.Value <> version then
                raise (InvalidOperationException("Collection changed during enumeration"))
              if index.Value < this.size then
                currentKey := this.keys.[!index]
                index := index.Value + 1
                true
              else
                index := this.size + 1
                currentKey := Unchecked.defaultof<'K>
                false
            member e.Reset() = 
              if eVersion.Value <> version then
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
        member x.Count with get() = this.size
        member x.IsReadOnly with get() = true
        member x.Item 
          with get index : 'V = this.values.[index]
          and set index value = raise (NotSupportedException("Values colelction is read-only"))
        member x.Add(k) = raise (NotSupportedException("Values colelction is read-only"))
        member x.Clear() = raise (NotSupportedException("Values colelction is read-only"))
        member x.Contains(value) = this.ContainsValue(value)
        member x.CopyTo(array, arrayIndex) = 
          Array.Copy(this.values, 0, array, arrayIndex, this.size)
        member x.IndexOf(value:'V) = this.IndexOfValue(value)
        member x.Insert(index, value) = raise (NotSupportedException("Values colelction is read-only"))
        member x.Remove(value:'V) = raise (NotSupportedException("Values colelction is read-only"))
        member x.RemoveAt(index:int) = raise (NotSupportedException("Values colelction is read-only"))
        member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
        member x.GetEnumerator() : IEnumerator<'V> = 
          let index = ref 0
          let eVersion = ref version
          let currentValue : 'V ref = ref Unchecked.defaultof<'V>
          { new IEnumerator<'V> with
            member e.Current with get() = currentValue.Value
            member e.Current with get() = box e.Current
            member e.MoveNext() = 
              if eVersion.Value <> version then
                raise (InvalidOperationException("Collection changed during enumeration"))
              if index.Value < this.size then
                currentValue := this.values.[index.Value]
                index := index.Value + 1
                true
              else
                index := this.size + 1
                currentValue := Unchecked.defaultof<'V>
                false
            member e.Reset() = 
              if eVersion.Value <> version then
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

  member internal this.IndexOfKeyUnchecked(key:'K) : int =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      let mutable res = 0
      let mutable found = false
      while not found && res < this.keys.Length  do
          if comparer.Compare(key, this.keys.[res]) = 0 then
              found <- true
          else res <- res + 1
      if found then res else ~~~this.size// add to the end
    finally
      exitLockIf syncRoot entered

  member this.IndexOfKey(key:'K) : int =
    if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
      raise (ArgumentNullException("key"))
    this.IndexOfKeyUnchecked(key)

  member this.IndexOfValue(value:'V) : int =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      let mutable res = 0
      let mutable found = false
      let valueComparer = Comparer<'V>.Default;
      while not found do
          if valueComparer.Compare(value,this.values.[res]) = 0 then
              found <- true
          else res <- res + 1
      if found then res else -1
    finally
      exitLockIf syncRoot entered


  member this.First
    with get() = 
      if this.size = 0 then raise (InvalidOperationException("Could not get the first element of an empty map"))
      KeyValuePair(this.keys.[0], this.values.[0])

  member this.Last
    with get() =
      if this.size = 0 then raise (InvalidOperationException("Could not get the last element of an empty map"))
      KeyValuePair(this.keys.[this.size - 1], this.values.[this.size - 1])

  member this.Item
      with get key =
        if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
          raise (ArgumentNullException("key"))
        let entered = enterLockIf syncRoot  isSynchronized
        try
          // first/last optimization (only last here)
          if this.size = 0 then
            raise (KeyNotFoundException())
          else
            let index = this.IndexOfKeyUnchecked(key)
            if index >= 0 then
              this.values.[index]
            else
              raise (KeyNotFoundException())
        finally
          exitLockIf syncRoot entered
      and set k v =
        if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(k, Unchecked.defaultof<'K>) then 
          raise (ArgumentNullException("key"))
        this.SetWithIndex(k, v) |> ignore

  /// Sets the value to the key position and returns the index of the key
  member internal this.SetWithIndex(k, v) =
    let entered = enterLockIf syncRoot isSynchronized
    try
      // first/last optimization (only last here)
      if this.size = 0 then
        this.Insert(0, k, v)
        0
      else
        let lastIdx = this.size-1
        if comparer.Compare(k, this.keys.[lastIdx]) = 0 then // key = last key
          this.values.[lastIdx] <- v
          version <- version + 1
          if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(k,v))
          lastIdx
        else   
          let index = this.IndexOfKeyUnchecked(k)
          if index >= 0 then // contains key 
            this.values.[index] <- v
            version <- version + 1 
            if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(k,v))
            index     
          else
            this.Insert(~~~index, k, v)
            ~~~index
    finally
      exitLockIf syncRoot entered

  member this.Add(key, value) : unit =
    if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
        raise (ArgumentNullException("key"))
    let entered = enterLockIf syncRoot  isSynchronized
    //try
    if this.size = 0 then
      this.Insert(0, key, value)
    else
      let index = this.IndexOfKeyUnchecked(key)
      if index >= 0 then // contains key 
          raise (ArgumentException("IndexedMap.Add: key already exists: " + key.ToString()))
      else
          this.Insert(~~~index, key, value)
    //finally
    exitLockIf syncRoot entered

  member this.AddLast(key, value) : unit = this.Add(key, value)

  member this.AddFirst(key, value):unit =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if this.size = 0 then
        this.Insert(0, key, value)
      else
        let index = this.IndexOfKeyUnchecked(key)
        if index >= 0 then // contains key 
            raise (ArgumentException("IndexedMap.Add: key already exists: " + key.ToString()))
        else
            this.Insert(0, key, value) 
    finally
      exitLockIf syncRoot entered
    
  member internal this.RemoveAt(index):unit =
    let entered = enterLockIf syncRoot isSynchronized
    try
      if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
      let newSize = this.size - 1

      if index < this.size then
        Array.Copy(this.keys, index + 1, this.keys, index, newSize - index) // this.size
        Array.Copy(this.values, index + 1, this.values, index, newSize - index) //this.size

      this.keys.[newSize] <- Unchecked.defaultof<'K>
      this.values.[newSize] <- Unchecked.defaultof<'V>

      this.size <- newSize
      version <- version + 1

      if cursorCounter > 0 then
        this.onErrorEvent.Trigger(NotImplementedException("TODO remove should trigger a special exception"))
//        // on removal, the next valid value is the previous one and all downstreams must reposition and replay from it
//        if index > 0 then this.onNextEvent.Trigger(this.GetPairByIndexUnchecked(index - 1)) // after removal (index - 1) is unchanged
//        else this.onNextEvent.Trigger(Unchecked.defaultof<_>)
    finally
      exitLockIf syncRoot entered

  member this.Remove(key):bool =
    let entered = enterLockIf syncRoot isSynchronized
    try
      let index = this.IndexOfKey(key)
      if index >= 0 then this.RemoveAt(index)
      index >= 0
    finally
      exitLockIf syncRoot entered

  member this.RemoveFirst([<Out>]result: byref<KVP<'K,'V>>):bool =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      try
        result <- this.First // could throw
        let ret = this.Remove(result.Key)
        ret
      with | _ -> false
    finally
      exitLockIf syncRoot entered

  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      try
        result <-this.Last // could throw
        this.Remove(result.Key)
      with | _ -> false
    finally
      exitLockIf syncRoot entered

  /// Removes all elements that are to `direction` from `key`
  member this.RemoveMany(key:'K,direction:Lookup):bool =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if this.size = 0 then false
      else
        let pivotIndex,_ = this.TryFindWithIndex(key, direction)
        // pivot should be removed, after calling TFWI pivot is always inclusive
        match direction with
        | Lookup.EQ -> this.Remove(key)
        | Lookup.LT | Lookup.LE ->
          if pivotIndex = -1 then // pivot is not here but to the left, keep all elements
            false
          elif pivotIndex >=0 then // remove elements below pivot and pivot
            this.size <- this.size - (pivotIndex + 1)
            version <- version + 1
            Array.Copy(this.keys, pivotIndex + 1, this.keys, 0, this.size) // move this.values to 
            Array.fill this.keys this.size (this.values.Length - this.size) Unchecked.defaultof<'K>

            Array.Copy(this.values, pivotIndex + 1, this.values, 0, this.size)
            Array.fill this.values this.size (this.values.Length - this.size) Unchecked.defaultof<'V>
            true
          else
            raise (ApplicationException("wrong result of TryFindWithIndex with LT/LE direction"))
        | Lookup.GT | Lookup.GE ->
          if pivotIndex = -2 then // pivot is not here but to the right, keep all elements
            false
          elif pivotIndex >=0 then // remove elements above and including pivot
            this.size <- pivotIndex
            Array.fill this.keys pivotIndex (this.values.Length - pivotIndex) Unchecked.defaultof<'K>
            Array.fill this.values pivotIndex (this.values.Length - pivotIndex) Unchecked.defaultof<'V>
            version <- version + 1
            this.Capacity <- this.size
            true
          else
            raise (ApplicationException("wrong result of TryFindWithIndex with GT/GE direction"))
        | _ -> failwith "wrong direction"
    finally
      exitLockIf syncRoot entered
    
  /// Returns the index of found KeyValuePair or a negative value:
  /// -1 if the non-found key is smaller than the first key
  /// -2 if the non-found key is larger than the last key
  /// -3 if the non-found key is within the key range (for EQ direction only)
  /// -4 empty
  /// Example: (-1) [...current...(-3)...map ...] (-2)
  member internal this.TryFindWithIndex(key:'K,direction:Lookup, [<Out>]result: byref<KeyValuePair<'K, 'V>>) : int = // rkok
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if this.size = 0 then -4
      else
        // TODO first/last optimization
        match direction with
        | Lookup.EQ ->
          let lastIdx = this.size-1
          if this.size > 0 && comparer.Compare(key, this.keys.[lastIdx]) = 0 then // key = last key
            result <-  this.GetPairByIndexUnchecked(lastIdx)
            lastIdx
          else
            let index = this.IndexOfKey(key)
            if index >= 0 then
              result <-  this.GetPairByIndexUnchecked(index)
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
          let lastIdx = this.size-1
          let lc = if this.size > 0 then comparer.Compare(key, this.keys.[lastIdx]) else -2
          if lc = 0 then // key = last key
            if this.size > 1 then
              result <-  this.GetPairByIndexUnchecked(lastIdx-1) // return item beforelast
              lastIdx - 1
            else -1
          else
            let index = this.IndexOfKey(key)
            if index > 0 then
              result <- this.GetPairByIndexUnchecked(index - 1)
              index - 1
            elif index = 0 then
               -1 // 
            else
              let index2 = ~~~index
              if index2 >= this.Count then // there are no elements larger than key
                result <-  this.GetPairByIndexUnchecked(this.Count - 1) // last element is the one that LT key
                this.Count - 1
              elif index2 = 0 then
                -1
              else //  it is the index of the first element that is larger than value
                result <-  this.GetPairByIndexUnchecked(index2 - 1)
                index2 - 1
        | Lookup.LE ->
          let lastIdx = this.size-1
          let lc = if this.size > 0 then comparer.Compare(key, this.keys.[lastIdx]) else -2
          if lc = 0 then // key = last key or greater than the last key
            result <-  this.GetPairByIndexUnchecked(lastIdx)
            lastIdx
          else
            let index = this.IndexOfKey(key)
            if index >= 0 then
              result <-  this.GetPairByIndexUnchecked(index) // equal
              index
            else
              let index2 = ~~~index
              if index2 >= this.size then // there are no elements larger than key
                result <-  this.GetPairByIndexUnchecked(this.size - 1)
                this.size - 1
              elif index2 = 0 then
                -1
              else //  it is the index of the first element that is larger than value
                result <-   this.GetPairByIndexUnchecked(index2 - 1)
                index2 - 1
        | Lookup.GT ->
          let lc = if this.size > 0 then comparer.Compare(key, this.keys.[0]) else 2
          if lc = 0 then // key = first key
            if this.size > 1 then
              result <-  this.GetPairByIndexUnchecked(1) // return item after first
              1
            else -2 // cannot get greater than a single value when k equals to it
          elif lc < 0 then
            result <-  this.GetPairByIndexUnchecked(0) // return first
            0
          else
            let index = this.IndexOfKey(key)
            if index >= 0 && index < this.Count - 1 then
              result <- this.GetPairByIndexUnchecked(index + 1)
              index + 1
            elif index >= this.Count - 1 then
              -2
            else
              let index2 = ~~~index
              if index2 >= this.Count then // there are no elements larger than key
                -2
              else //  it is the index of the first element that is larger than value
                result <- this.GetPairByIndexUnchecked(index2)
                index2
        | Lookup.GE ->
          let lc = if this.size > 0 then comparer.Compare(key, this.keys.[0]) else 2
          if lc <= 0 then // key = first key or smaller than the first key
            result <-  this.GetPairByIndexUnchecked(0)
            0
          else
            let index = this.IndexOfKey(key)
            if index >= 0 && index < this.Count then
              result <-  this.GetPairByIndexUnchecked(index) // equal
              index
            else
              let index2 = ~~~index
              if index2 >= this.Count then // there are no elements larger than key
                -2
              else //  it is the index of the first element that is larger than value
                result <-   this.GetPairByIndexUnchecked(index2)
                index2
        | _ -> raise (ApplicationException("Wrong lookup direction"))
    finally
      exitLockIf syncRoot entered


  member this.TryFind(k:'K, direction:Lookup, [<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
    res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
    let idx, v = this.TryFindWithIndex(k, direction)
    if idx >= 0 then
        res <- v
        true
    else false

  /// Return true if found exact key
  member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      // first/last optimization
      if this.size = 0 then
        value <- Unchecked.defaultof<'V>
        false
      else
        let lc = comparer.Compare(key, this.keys.[this.size - 1])
        if lc = 0 then // key = last key
          value <- this.values.[this.size-1]
          true
        else   
          let index = this.IndexOfKey(key)
          if index >= 0 then
            value <- this.values.[index]
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

  override this.GetCursor() = this.GetCursor(-1, version, Unchecked.defaultof<'K>, Unchecked.defaultof<'V>)
    
  // TODO(?) replace with a mutable struct, like in SCG.SortedList<T>, there are too many virtual calls and reference cells in the most critical paths like MoveNext
  // NB Object expression with ref cells are surprisingly fast insteads of custom class
  member internal this.GetCursor(index:int,cursorVersion:int,currentKey:'K, currentValue:'V) =
    //IndexedMapCursor(this, index, cursorVersion, currentKey, currentValue) :> ICursor<'K,'V>
    let index = ref index
    let cursorVersion = ref cursorVersion
    let currentKey : 'K ref = ref currentKey
    let currentValue : 'V ref = ref currentValue
    let isBatch = ref false
    { 
    new BaseCursor<'K,'V>(this) with
      override this.IsContinuous with get() = false
      override p.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = 
        if !index = -1 then this :> IReadOnlyOrderedMap<'K,'V>
        else raise (InvalidOperationException(""))
      override p.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> =
        if !index = -1 then 
          index := this.size
          isBatch := true
          trueTask
        else falseTask
      override p.IsBatch with get() = !isBatch
      override p.Clone() = this.GetCursor(!index,!cursorVersion, p.CurrentKey, p.CurrentValue) //!currentKey,!currentValue)
      override p.Current with get() = KeyValuePair(p.CurrentKey, p.CurrentValue) // currentKey.Value, currentValue.Value)
      override p.MoveNext() = 
        let entered = enterLockIf syncRoot isSynchronized
        try
          if index.Value = -1 then p.MoveFirst() // first move when index = -1
          elif cursorVersion.Value = version then
            if index.Value < (this.size - 1) then
              index := !index + 1
              currentKey := this.GetKeyByIndex(index.Value)
              currentValue := this.values.[!index]
              true
            else
              //p.Reset() // NB! Do not reset cursor on false MoveNext
              false
          else  // source change
            cursorVersion := version // update state to new version
            let position, kvp = this.TryFindWithIndex(p.CurrentKey, Lookup.GT) // reposition cursor after source change //currentKey.Value
            if position > 0 then
              index := position
              currentKey := kvp.Key
              currentValue := kvp.Value
              true
            else  // not found
              //p.Reset() // NB! Do not reset cursor on false MoveNext
              false
        finally
          exitLockIf syncRoot entered

      override p.MovePrevious() = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          if index.Value = -1 then p.MoveLast()  // first move when index = -1
          elif cursorVersion.Value = version then
            if index.Value > 0 && index.Value < this.size then
              index := index.Value - 1
              currentKey := this.GetKeyByIndex(index.Value)
              currentValue := this.values.[index.Value]
              true
            else
              p.Reset()
              false
          else
            cursorVersion := version // update state to new version
            let position, kvp = this.TryFindWithIndex(p.CurrentKey, Lookup.LT) //currentKey.Value
            if position > 0 then
              index := position
              currentKey := kvp.Key
              currentValue := kvp.Value
              true
            else  // not found
              p.Reset()
              false
        finally
          exitLockIf syncRoot entered

      override p.MoveAt(key:'K, lookup:Lookup) = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          let position, kvp = this.TryFindWithIndex(key, lookup)
          if position >= 0 then
            index := position
            currentKey := kvp.Key
            currentValue := kvp.Value
            true
          else
            p.Reset()
            false
        finally
          exitLockIf syncRoot entered

      override p.MoveFirst() = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          if this.size > 0 then
            index := 0
            currentKey := this.GetKeyByIndex(index.Value)
            currentValue := this.values.[index.Value]
            true
          else
            p.Reset()
            false
        finally
          exitLockIf syncRoot entered

      override p.MoveLast() = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          if this.size > 0 then
            index := this.size - 1
            currentKey := this.GetKeyByIndex(index.Value)
            currentValue := this.values.[index.Value]
            true
          else
            p.Reset()
            false
        finally
          exitLockIf syncRoot entered

      override p.CurrentKey with get() = currentKey.Value //if index.Value >= 0 then this.GetKeyByIndex(index.Value) else Unchecked.defaultof<'K> //currentKey.Value

      override p.CurrentValue with get() = currentValue.Value //if index.Value >= 0 then this.values.[index.Value] else Unchecked.defaultof<'V> //currentValue.Value

      override p.Reset() = 
        cursorVersion := version // update state to new version
        index := -1
        currentKey := Unchecked.defaultof<'K>
        currentValue := Unchecked.defaultof<'V>

      override p.Dispose() = p.Reset()
    } :> ICursor<'K,'V>

  /// Make the capacity equal to the size
  member this.TrimExcess() = this.Capacity <- this.size

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
      for index in 0..this.size do
        let kvp = KeyValuePair(this.GetKeyByIndex(index), this.values.[index])
        array.SetValue(kvp, arrayIndex + index)
    member this.Count = this.Count
    member this.IsSynchronized with get() =  isSynchronized

  interface IDictionary<'K,'V> with
    member this.Count = this.Count
    member this.IsReadOnly with get() = not this.IsMutable
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
        value <- this.values.[index]
        true
      else
        value <- Unchecked.defaultof<'V>
        false

  interface IReadOnlyOrderedMap<'K,'V> with
    member this.Comparer with get() = this.Comparer
    member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = this.GetCursor()
    member this.IsEmpty = this.size = 0
    member this.IsIndexed with get() = false
    member this.IsReadOnly with get() = not this.IsMutable
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
    member this.GetAt(idx:int) = this.values.[idx]
    member this.Keys with get() = this.Keys :> IEnumerable<'K>
    member this.Values with get() = this.values :> IEnumerable<'V>
    member this.SyncRoot with get() = syncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Complete() = this.Complete()
    member this.Version with get() = int64(this.Version) and set v = version <- int v
    member this.Count with get() = int64(this.size)
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

    // TODO move to type memeber, cheack if IROOM is SM and copy arrays in one go
    member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>, option:AppendOption) =
      let hasEqOverlap (old:IReadOnlyOrderedMap<'K,'V>) (append:IReadOnlyOrderedMap<'K,'V>) : bool =
        if comparer.Compare(append.First.Key, old.Last.Key) > 0 then false
        else
          let oldC = old.GetCursor()
          let appC = append.GetCursor();
          let mutable cont = true
          let mutable overlapOk = 
            oldC.MoveAt(append.First.Key, Lookup.EQ) 
              && appC.MoveFirst() 
              && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
              && Unchecked.equals oldC.CurrentValue appC.CurrentValue
          while overlapOk && cont do
            if oldC.MoveNext() then
              overlapOk <-
                appC.MoveNext() 
                && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
                && Unchecked.equals oldC.CurrentValue appC.CurrentValue
            else cont <- false
          overlapOk
      if appendMap.IsEmpty then
        0
      else
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          match option with
          | AppendOption.ThrowOnOverlap _ ->
            if this.IsEmpty || comparer.Compare(appendMap.First.Key, this.Last.Key) > 0 then
              let mutable c = 0
              for i in appendMap do
                c <- c + 1
                this.AddLast(i.Key, i.Value) // TODO Add last when fixed flushing
              c
            else invalidOp "values overlap with existing"
          | AppendOption.DropOldOverlap ->
            if this.IsEmpty || comparer.Compare(appendMap.First.Key, this.Last.Key) > 0 then
              let mutable c = 0
              for i in appendMap do
                c <- c + 1
                this.AddLast(i.Key, i.Value) // TODO Add last when fixed flushing
              c
            else
              let removed = this.RemoveMany(appendMap.First.Key, Lookup.GE)
              Trace.Assert(removed)
              let mutable c = 0
              for i in appendMap do
                c <- c + 1
                this.AddLast(i.Key, i.Value) // TODO Add last when fixed flushing
              c
          | AppendOption.IgnoreEqualOverlap ->
            if this.IsEmpty || comparer.Compare(appendMap.First.Key, this.Last.Key) > 0 then
              let mutable c = 0
              for i in appendMap do
                c <- c + 1
                this.AddLast(i.Key, i.Value) // TODO Add last when fixed flushing
              c
            else
              let isEqOverlap = hasEqOverlap this appendMap
              if isEqOverlap then
                let appC = appendMap.GetCursor();
                if appC.MoveAt(this.Last.Key, Lookup.GT) then
                  this.AddLast(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                  let mutable c = 1
                  while appC.MoveNext() do
                    this.AddLast(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                    c <- c + 1
                  c
                else 0
              else invalidOp "overlapping values are not equal" // TODO unit test
          | AppendOption.RequireEqualOverlap ->
            if this.IsEmpty then
              let mutable c = 0
              for i in appendMap do
                c <- c + 1
                this.AddLast(i.Key, i.Value) // TODO Add last when fixed flushing
              c
            elif comparer.Compare(appendMap.First.Key, this.Last.Key) > 0 then
              invalidOp "values do not overlap with existing"
            else
              let isEqOverlap = hasEqOverlap this appendMap
              if isEqOverlap then
                let appC = appendMap.GetCursor();
                if appC.MoveAt(this.Last.Key, Lookup.GT) then
                  this.AddLast(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                  let mutable c = 1
                  while appC.MoveNext() do
                    this.AddLast(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                    c <- c + 1
                  c
                else 0
              else invalidOp "overlapping values are not equal" // TODO unit test
          | _ -> failwith "Unknown AppendOption"
        finally
          exitLockIf this.SyncRoot entered
      // do not need transaction because if the first addition succeeds then all others will be added as well
//      for i in appendMap do
//        this.AddLast(i.Key, i.Value)
      //raise (NotImplementedException("TODO append impl"))
    

  //#endregion

  //#region Constructors

  // TODO try resolve KeyComparer for know types
  new() = IndexedMap(None, None, None)
  new(dictionary:IDictionary<'K,'V>) = IndexedMap(Some(dictionary), Some(dictionary.Count), Some(Comparer<'K>.Default :> IComparer<'K>))
  new(capacity:int) = IndexedMap(None, Some(capacity), Some(Comparer<'K>.Default :> IComparer<'K>))

  // do not expose ctors with comparer to public
  internal new(comparer:IComparer<'K>) = IndexedMap(None, None, Some(comparer))
  internal new(dictionary:IDictionary<'K,'V>,comparer:IComparer<'K>) = IndexedMap(Some(dictionary), Some(dictionary.Count), Some(comparer))
  internal new(capacity:int,comparer:IComparer<'K>) = IndexedMap(None, Some(capacity), Some(comparer))

  internal new(comparer:IEqualityComparer<'K>) = 
    let comparer' = 
      {new IComparer<'K> with
          member this.Compare(x,y) = 
            if comparer.Equals(x,y) then 0 else -1
      }
    IndexedMap(None, None, Some(comparer'))

  internal new(dictionary:IDictionary<'K,'V>,comparer:IEqualityComparer<'K>) = 
    let comparer' = 
      {new IComparer<'K> with
          member this.Compare(x,y) = 
            if comparer.Equals(x,y) then 0 else -1
      }
    IndexedMap(Some(dictionary), Some(dictionary.Count), Some(comparer'))

  internal new(capacity:int,comparer:IEqualityComparer<'K>) =
    let comparer' = 
      {new IComparer<'K> with
          member this.Compare(x,y) = 
            if comparer.Equals(x,y) then 0 else -1
      }
    IndexedMap(None, Some(capacity), Some(comparer'))


  static member internal OfSortedKeysAndValues(keys:'K[], values:'V[], size:int) =
    if keys.Length < size then raise (new ArgumentException("Keys array is smaller than provided size"))
    if values.Length < size then raise (new ArgumentException("Values array is smaller than provided size"))
    let im = new IndexedMap<'K,'V>()
    im.keys <- keys
    im.size <- size
    im.values <- values
    im

  static member OfSortedKeysAndValues(keys:'K[], values:'V[]) =
    if keys.Length <> values.Length then raise (new ArgumentException("Keys and values arrays are of different sizes"))
    IndexedMap.OfSortedKeysAndValues(keys, values, values.Length)
  //#endregion


