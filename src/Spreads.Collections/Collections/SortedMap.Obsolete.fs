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

namespace Spreads.Collections.Obsolete

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices

open Spreads
//
//// this is a simple version of SM without regular keys mess, but regular keys are ok
//// TODO OrderedMap that could be sorted or indexed with a bool switch
//
///// Thread-unsafe (unsynced) array-based sorted map
///// Implemented as SCG's SortedList with additional methods
//[<AllowNullLiteral>]
//[<SerializableAttribute>]
//type SortedMap<'K,'V when 'K : comparison>
//  internal(?dictionary:IDictionary<'K,'V>, ?capacity:int, ?comparer:IComparer<'K>) as this=
//
//  //#region Main private constructor
//
//  let mutable comparer : IComparer<'K> = 
//    if comparer.IsNone then Comparer<'K>.Default :> IComparer<'K> //LanguagePrimitives.FastGenericComparer
//    else comparer.Value
//    
//  [<DefaultValueAttribute>]
//  val mutable internal keys : 'K array // = [||]
//  [<DefaultValueAttribute>]
//  val mutable internal values : 'V array //= [||]
//  [<DefaultValueAttribute>]
//  val mutable internal size : int //= 0
//  [<VolatileFieldAttribute>] // enumeration doesn't locks but checks versions
//  let mutable version = 0
//  [<DefaultValueAttribute>]
//  val mutable internal isSynchronized : bool // = false // safer to set true by default
//  let mutable mapKey = ""
//
//  [<NonSerializedAttribute>]
//  let syncRoot = new Object()
//
//
//  [<NonSerializedAttribute>]
//  let isKeyReferenceType = not typeof<'K>.IsValueType
//
//
//  do
//    this.isSynchronized <- false
//    this.keys <- [||]
//    this.values <- [||]
//    this.Capacity <- if capacity.IsSome then capacity.Value else 4
//
//    if dictionary.IsSome then
//      if capacity.IsSome && capacity.Value < dictionary.Value.Count then raise (ArgumentException("capacity is less then dictionary this.size"))
//      dictionary.Value.Keys.CopyTo(this.keys, 0)
//      dictionary.Value.Values.CopyTo(this.values, 0)
//      Array.Sort(this.keys, this.values, comparer)
//      this.size <- dictionary.Value.Count
//
//  //#endregion
//
//
//#if FX_NO_BINARY_SERIALIZATION
//#else
//  [<System.Runtime.Serialization.OnSerializingAttribute>]
//  member internal __.OnSerializing(context: System.Runtime.Serialization.StreamingContext) =
//    ignore(context)
//    ()
//
//  [<System.Runtime.Serialization.OnDeserializedAttribute>]
//  member internal __.OnDeserialized(context: System.Runtime.Serialization.StreamingContext) =
//    ignore(context)
//
//
//#endif
//    //#region Private & Internal members
//
//  member private this.Clone() =
//    let newMap = new SortedMap<'K,'V>(this.keys.Length, comparer)
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      this.keys.CopyTo(newMap.keys, 0)
//      this.values.CopyTo(newMap.values, 0)
//    finally
//      exitLockIf syncRoot entered
//
//    newMap.size <- this.size
//    newMap.IsSynchronized <-  this.isSynchronized
//    newMap
//    
//
//  member inline private this.GetByIndex(index) = 
//    KeyValuePair(this.keys.[index], this.values.[index])
//       
//  member private this.Insert(index:int, k, v) =
//    if this.size = this.keys.Length then 
//        this.EnsureCapacity(this.size + 1)
//        
//    if index < this.size then
//        Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
//        Array.Copy(this.values, index, this.values, index + 1, this.size - index);
//      
//    this.keys.[index] <- k
//    this.values.[index] <- v
//    version <- version + 1
//    this.size <- this.size + 1
//
//  member private this.EnsureCapacity(min) =
//    let mutable num = this.keys.Length * 2 
//    if num > 2146435071 then num <- 2146435071
//    if num < min then num <- min
//    this.Capacity <- num
//    
//  member private this.GetKeyByIndex(index) = 
//    if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
//    this.keys.[index]
//    
//  member private this.GetValueByIndex(index) = 
//    if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
//    this.values.[index]
//
//  member internal this.IsReadOnly with get() = false
//
//  member internal this.IsSynchronized 
//    with get() =  this.isSynchronized
//    and set(synced:bool) = 
//      let entered = enterLockIf syncRoot  this.isSynchronized
//      this.isSynchronized <- synced
//      exitLockIf syncRoot entered
//
//  member internal this.MapKey 
//    with get() = mapKey
//    and set(key:string) = mapKey <- key
//
//  member this.SyncRoot with get() = syncRoot
//
//  member this.Version with get() = version and set v = version <- v
//
//
//  //#endregion
//
//
//  //#region Public members
//
//  member this.Capacity 
//    with get() = this.keys.Length
//    and set(value) =
//      let entered = enterLockIf syncRoot  this.isSynchronized
//      try
//        match value with
//        | c when c = this.keys.Length -> ()
//        | c when c < this.size -> raise (ArgumentOutOfRangeException("Small capacity"))
//        | c when c > 0 -> 
//          let kArr : 'K array = Array.zeroCreate c
//          let vArr : 'V array = Array.zeroCreate c
//          Array.Copy(this.keys, 0, kArr, 0, this.size)
//          Array.Copy(this.values, 0, vArr, 0, this.size)
//          this.keys <- kArr
//          this.values <- vArr
//        | _ -> ()
//      finally
//        exitLockIf syncRoot entered
//  member this.Comparer with get() = comparer
//
//  member this.Clear() =
//    version <- version + 1
//    Array.Clear(this.keys, 0, this.size)
//    Array.Clear(this.values, 0, this.size)
//    this.size <- 0
//
//  member this.Count with get() = this.size
//
//  member this.IsEmpty with get() = this.size = 0
//
//
//  member this.Keys 
//    with get() : IList<'K> =
//      {new IList<'K> with
//        member x.Count with get() = this.size
//        member x.IsReadOnly with get() = true
//        member x.Item 
//          with get index : 'K = this.GetKeyByIndex(index)
//          and set index value = raise (NotSupportedException("Keys collection is read-only"))
//        member x.Add(k) = raise (NotSupportedException("Keys collection is read-only"))
//        member x.Clear() = raise (NotSupportedException("Keys collection is read-only"))
//        member x.Contains(key) = this.ContainsKey(key)
//        member x.CopyTo(array, arrayIndex) = 
//          Array.Copy(this.keys, 0, array, arrayIndex, this.size)
//        member x.IndexOf(key:'K) = this.IndexOfKey(key)
//        member x.Insert(index, value) = raise (NotSupportedException("Keys collection is read-only"))
//        member x.Remove(key:'K) = raise (NotSupportedException("Keys collection is read-only"))
//        member x.RemoveAt(index:int) = raise (NotSupportedException("Keys collection is read-only"))
//        member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
//        member x.GetEnumerator() : IEnumerator<'K> = 
//          let index = ref 0
//          let eVersion = ref version
//          let currentKey : 'K ref = ref Unchecked.defaultof<'K>
//          { new IEnumerator<'K> with
//            member e.Current with get() = currentKey.Value
//            member e.Current with get() = box e.Current
//            member e.MoveNext() = 
//              if eVersion.Value <> version then
//                raise (InvalidOperationException("Collection changed during enumeration"))
//              if index.Value < this.size then
//                currentKey := this.keys.[index.Value]
//                index := index.Value + 1
//                true
//              else
//                index := this.size + 1
//                currentKey := Unchecked.defaultof<'K>
//                false
//            member e.Reset() = 
//              if eVersion.Value <> version then
//                raise (InvalidOperationException("Collection changed during enumeration"))
//              index := 0
//              currentKey := Unchecked.defaultof<'K>
//            member e.Dispose() = 
//              index := 0
//              currentKey := Unchecked.defaultof<'K>
//          }
//      }
//
//  member this.Values 
//    with get() : IList<'V> =
//      { new IList<'V> with
//        member x.Count with get() = this.size
//        member x.IsReadOnly with get() = true
//        member x.Item 
//          with get index : 'V = this.GetValueByIndex(index)
//          and set index value = raise (NotSupportedException("Values colelction is read-only"))
//        member x.Add(k) = raise (NotSupportedException("Values colelction is read-only"))
//        member x.Clear() = raise (NotSupportedException("Values colelction is read-only"))
//        member x.Contains(value) = this.ContainsValue(value)
//        member x.CopyTo(array, arrayIndex) = 
//          Array.Copy(this.values, 0, array, arrayIndex, this.size)
//        member x.IndexOf(value:'V) = this.IndexOfValue(value)
//        member x.Insert(index, value) = raise (NotSupportedException("Values colelction is read-only"))
//        member x.Remove(value:'V) = raise (NotSupportedException("Values colelction is read-only"))
//        member x.RemoveAt(index:int) = raise (NotSupportedException("Values colelction is read-only"))
//        member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
//        member x.GetEnumerator() : IEnumerator<'V> = 
//          let index = ref 0
//          let eVersion = ref version
//          let currentValue : 'V ref = ref Unchecked.defaultof<'V>
//          { new IEnumerator<'V> with
//            member e.Current with get() = currentValue.Value
//            member e.Current with get() = box e.Current
//            member e.MoveNext() = 
//              if eVersion.Value <> version then
//                raise (InvalidOperationException("Collection changed during enumeration"))
//              if index.Value < this.size then
//                currentValue := this.values.[index.Value]
//                index := index.Value + 1
//                true
//              else
//                index := this.size + 1
//                currentValue := Unchecked.defaultof<'V>
//                false
//            member e.Reset() = 
//              if eVersion.Value <> version then
//                raise (InvalidOperationException("Collection changed during enumeration"))
//              index := 0
//              currentValue := Unchecked.defaultof<'V>
//            member e.Dispose() = 
//              index := 0
//              currentValue := Unchecked.defaultof<'V>
//          }
//        }
//
//  member this.ContainsKey(key) = this.IndexOfKey(key) >= 0
//
//  member this.ContainsValue(value) = this.IndexOfValue(value) >= 0
//
//  member this.IndexOfValue(value:'V) : int =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      let mutable res = 0
//      let mutable found = false
//      let valueComparer = Comparer<'V>.Default;
//      while not found do
//          if valueComparer.Compare(value,this.values.[res]) = 0 then
//              found <- true
//          else res <- res + 1
//      if found then res else -1
//    finally
//      exitLockIf syncRoot entered
//
//  member this.IndexOfKey(key:'K) : int =
//    if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
//      raise (ArgumentNullException("key"))
//    Array.BinarySearch(this.keys, 0, this.size, key, comparer)
//    
//  member this.First
//    with get() = 
//      if this.size = 0 then raise (InvalidOperationException("Could not get the first element of an empty map"))
//      KeyValuePair(this.keys.[0], this.values.[0])
//
//  member this.Last
//    with get() =
//      if this.size = 0 then raise (InvalidOperationException("Could not get the last element of an empty map"))
//      KeyValuePair(this.keys.[this.size - 1], this.values.[this.size - 1])
//
//  member this.Item
//      with get key =
//        let entered = enterLockIf syncRoot  this.isSynchronized
//        try
//          // first/last optimization (only last here)
//          if this.size = 0 then
//              raise (KeyNotFoundException())
//          else
//              let lc = comparer.Compare(key, this.keys.[this.size-1]) 
//              if lc = 0 then // key = last key
//                this.values.[this.size-1]
//              elif lc > 0 then raise (KeyNotFoundException())
//              else   
//                let index = this.IndexOfKey(key)
//                if index >= 0 then
//                    this.GetValueByIndex(index)
//                else
//                    raise (KeyNotFoundException())
//        finally
//          exitLockIf syncRoot entered
//      and set k v =
//          this.SetWithIndex(k, v) |> ignore
//
//  /// Sets the value to the key position and returns the index of the key
//  member internal this.SetWithIndex(k, v) = 
//    if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(k, Unchecked.defaultof<'K>) then 
//      raise (ArgumentNullException("key"))
//    let entered = enterLockIf syncRoot this.isSynchronized
//    try
//      // first/last optimization (only last here)
//      if this.size = 0 then
//        this.Insert(0, k, v)
//        0
//      else
//        let lastIdx = this.size-1
//        let lc = comparer.Compare(k, this.keys.[lastIdx]) 
//        if lc = 0 then // key = last key
//          this.values.[lastIdx] <- v
//          version <- version + 1
//          lastIdx
//        elif lc = 1 then // adding last value, Insert won't copy arrays if enough capacity
//          this.Insert(this.size, k, v)
//          this.size
//        else   
//          let index = Array.BinarySearch(this.keys, 0, this.size, k, comparer)
//          if index >= 0 then // contains key 
//            this.values.[index] <- v
//            version <- version + 1 
//            index          
//          else
//            this.Insert(~~~index, k, v)
//            ~~~index
//    finally
//      exitLockIf syncRoot entered
//
//  // In-place (mutable) addition
//  member this.Add(key, value) : unit =
//    if isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
//        raise (ArgumentNullException("key"))
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      if this.size = 0 then
//        this.Insert(0, key, value)
//      else
//        // last optimization gives near 2x performance boost
//        let lastIdx = this.size-1
//        let lc = comparer.Compare(key, this.keys.[lastIdx]) 
//        if lc = 0 then // key = last key
//            raise (ArgumentException("key already exists"))
//        elif lc = 1 then // adding last value, Insert won't copy arrays if enough capacity
//            this.Insert(this.size, key, value)
//        else
//            let index = Array.BinarySearch(this.keys, 0, this.size, key, comparer)
//            if index >= 0 then // contains key 
//                raise (ArgumentException("key already exists"))
//            else
//                this.Insert(~~~index, key, value)
//    finally
//      exitLockIf syncRoot entered
//
//  member this.AddLast(key, value):unit =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      if this.size = 0 then
//        this.Insert(0, key, value)
//      else
//        let c = comparer.Compare(key, this.keys.[this.size-1]) 
//        if c = 1 then 
//          this.Insert(this.size, key, value)
//        else raise (ArgumentOutOfRangeException("New key is smaller or equal to the largest existing key"))
//    finally
//      exitLockIf syncRoot entered
//
//  member this.AddFirst(key, value):unit =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      if this.size = 0 then
//        this.Insert(0, key, value)
//      else
//        let c = comparer.Compare(key, this.keys.[0]) 
//        if c = -1 then
//            this.Insert(0, key, value)
//        else raise (ArgumentOutOfRangeException("New key is larger or equal to the smallest existing key"))
//    finally
//      exitLockIf syncRoot entered
//    
//  member public this.RemoveAt(index):unit =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      if index < 0 || index >= this.size then
//          raise (ArgumentOutOfRangeException("index"))
//      this.size <- this.size - 1
//      if index < this.size then
//          Array.Copy(this.keys, index + 1, this.keys, index, this.size - index)
//          Array.Copy(this.values, index + 1, this.values, index, this.size - index)
//      this.keys.[this.size] <- Unchecked.defaultof<'K>
//      this.values.[this.size] <- Unchecked.defaultof<'V>
//      version <- version + 1
//    finally
//      exitLockIf syncRoot entered
//
//  member this.Remove(key):bool =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      let index = this.IndexOfKey(key)
//      if index >= 0 then this.RemoveAt(index)
//      index >= 0
//    finally
//      exitLockIf syncRoot entered
//
//  // TODO first/last optimization
//  member this.RemoveFirst([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      result <- this.First
//      this.Remove(result.Key)
//    finally
//      exitLockIf syncRoot entered
//
//  // TODO first/last optimization
//  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      result <-this.Last
//      this.Remove(result.Key)
//    finally
//      exitLockIf syncRoot entered
//
//  /// Removes all elements that are to `direction` from `key`
//  member this.RemoveMany(key:'K,direction:Lookup):bool =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      let pivot = this.TryFindWithIndex(key, direction)
//      let index = fst pivot
//      // pivot should be removed, after calling TFWI pivot is always inclusive
//      match direction with
//      | Lookup.EQ -> this.Remove(key)
//      | Lookup.LT | Lookup.LE ->
//        if index = -1 then // pivot is not here but to the left, keep all elements
//          false
//        elif index >=0 then // remove elements below pivot
//          this.size <- this.size - (index + 1)
//          version <- version + 1
//          Array.Copy(this.keys, index + 1, this.keys, 0, this.size) // move this.values to 
//          Array.Copy(this.values, index + 1, this.values, 0, this.size)
//          Array.fill this.keys this.size (this.keys.Length - this.size) Unchecked.defaultof<'K>
//          Array.fill this.values this.size (this.values.Length - this.size) Unchecked.defaultof<'V>
//          true
//        else
//          raise (ApplicationException("wrong result of TryFindWithIndex with LT/LE direction"))
//      | Lookup.GT | Lookup.GE ->
//        if index = -2 then // pivot is not here but to the right, keep all elements
//          false
//        elif index >=0 then // remove elements above and including pivot
//          this.size <- index
//          Array.fill this.keys index (this.keys.Length - index) Unchecked.defaultof<'K>
//          Array.fill this.values index (this.values.Length - index) Unchecked.defaultof<'V>
//          version <- version + 1
//          true
//        else
//          raise (ApplicationException("wrong result of TryFindWithIndex with GT/GE direction"))
//      | _ -> failwith "wrong direction"
//    finally
//      exitLockIf syncRoot entered
//    
//  /// Returns the index of found KeyValuePair or a negative value:
//  /// -1 if the non-found key is smaller than the first key
//  /// -2 if the non-found key is larger than the last key
//  /// -3 if the non-found key is within the key range (for EQ direction only)
//  /// Example: (-1) [...current...(-3)...map ...] (-2)
//  member internal this.TryFindWithIndex(key:'K,direction:Lookup, [<Out>]result: byref<KeyValuePair<'K, 'V>>) : int =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      // TODO first/last optimization
//      match direction with
//      | Lookup.EQ ->
//        let lastIdx = this.size-1
//        if this.size > 0 && comparer.Compare(key, this.keys.[this.size-1]) = 0 then // key = last key
//          result <-  this.GetByIndex(lastIdx)
//          lastIdx
//        else
//          let index = this.IndexOfKey(key)
//          if index >= 0 then
//            result <-  this.GetByIndex(index)
//            index
//          else
//            let index2 = ~~~index
//            if index2 >= this.Count then // there are no elements larger than key, all this.keys are smaller
//              -2 // the key could be in the next bucket
//            elif index2 = 0 then //it is the index of the first element that is larger than value
//              -1 // all this.keys in the map are larger than the desired key
//            else
//              -3
//      | Lookup.LT ->
//        let lastIdx = this.size-1
//        let lc = if this.size > 0 then comparer.Compare(key, this.keys.[lastIdx]) else -2
//        if lc = 0 then // key = last key
//          result <-  this.GetByIndex(lastIdx-1) // return item beforelast
//          lastIdx - 1
//        elif lc = 1 then // key greater than the last
//          result <-  this.GetByIndex(lastIdx) // return the last item 
//          lastIdx
//        else
//          let index = this.IndexOfKey(key)
//          if index > 0 then
//            result <- this.GetByIndex(index - 1)
//            index - 1
//          elif index = 0 then
//             -1 // 
//          else
//            let index2 = ~~~index
//            if index2 >= this.Count then // there are no elements larger than key
//              result <-  this.GetByIndex(this.Count - 1) // last element is the one that LT key
//              this.Count - 1
//            elif index2 = 0 then
//              -1
//            else //  it is the index of the first element that is larger than value
//              result <-  this.GetByIndex(index2 - 1)
//              index2 - 1
//      | Lookup.LE ->
//        let lastIdx = this.size-1
//        let lc = if this.size > 0 then comparer.Compare(key, this.keys.[lastIdx]) else -2
//        if lc >= 0 then // key = last key or greater than the last key
//          result <-  this.GetByIndex(lastIdx)
//          lastIdx
//        else
//          let index = this.IndexOfKey(key)
//          if index >= 0 then
//            result <-  this.GetByIndex(index) // equal
//            index
//          else
//            let index2 = ~~~index
//            if index2 >= this.Count then // there are no elements larger than key
//              result <-  this.GetByIndex(this.Count - 1)
//              this.Count - 1
//            elif index2 = 0 then
//              -1
//            else //  it is the index of the first element that is larger than value
//              result <-   this.GetByIndex(index2 - 1)
//              index2 - 1
//      | Lookup.GT ->
//        let lc = if this.size > 0 then comparer.Compare(key, this.keys.[0]) else 2
//        if lc = 0 then // key = first key
//          result <-  this.GetByIndex(1) // return item after first
//          1
//        elif lc = -1 then
//          result <-  this.GetByIndex(0) // return first
//          0
//        else
//          let index = this.IndexOfKey(key)
//          if index >= 0 && index < this.Count - 1 then
//            result <- this.GetByIndex(index + 1)
//            index + 1
//          elif index >= this.Count - 1 then
//            -2
//          else
//            let index2 = ~~~index
//            if index2 >= this.Count then // there are no elements larger than key
//              -2
//            else //  it is the index of the first element that is larger than value
//              result <- this.GetByIndex(index2)
//              index2
//      | Lookup.GE ->
//        let lc = if this.size > 0 then comparer.Compare(key, this.keys.[0]) else 2
//        if lc <= 0 then // key = first key or smaller than the first key
//          result <-  this.GetByIndex(0)
//          0
//        else
//          let index = this.IndexOfKey(key)
//          if index >= 0 && index < this.Count then
//            result <-  this.GetByIndex(index) // equal
//            index
//          else
//            let index2 = ~~~index
//            if index2 >= this.Count then // there are no elements larger than key
//              -2
//            else //  it is the index of the first element that is larger than value
//              result <-   this.GetByIndex(index2)
//              index2
//      | _ -> raise (ApplicationException("Wrong lookup direction"))
//    finally
//      exitLockIf syncRoot entered
//
//
//  member this.TryFind(k:'K, direction:Lookup, [<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
//    res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
//    let tr = this.TryFindWithIndex(k, direction)
//    if (fst tr) >= 0 then
//        res <- snd tr
//        true
//    else
//        false
//
//  /// Return true if found exact key
//  member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
//    let entered = enterLockIf syncRoot  this.isSynchronized
//    try
//      // first/last optimization
//      if this.size = 0 then
//        value <- Unchecked.defaultof<'V>
//        false
//      else
//        let lc = comparer.Compare(key, this.keys.[this.size-1]) 
//        if lc = 0 then // key = last key
//          value <- this.values.[this.size-1]
//          true
//        else   
//          let index = this.IndexOfKey(key)
//          if index >= 0 then
//            value <- this.values.[index]
//            true
//          else
//            value <- Unchecked.defaultof<'V>
//            false
//    finally
//      exitLockIf syncRoot entered
//
//
//  member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
//    try
//      res <- this.First
//      true
//    with
//    | _ -> 
//      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
//      false
//            
//  member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
//    try
//      res <- this.Last
//      true
//    with
//    | _ -> 
//      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
//      false
//
//  member this.GetCursor() = this.GetROOMCursor() :> ICursor<'K,'V>
//  member private this.GetROOMCursor() : MapCursor<'K,'V> =
//    let index = ref -1
//    let pVersion = ref version
//    let currentKey : 'K ref = ref Unchecked.defaultof<'K>
//    let currentValue : 'V ref = ref Unchecked.defaultof<'V>
//    { new MapCursor<'K,'V>(this) with
//      override p.Current with get() = KeyValuePair(currentKey.Value, currentValue.Value)
//      override p.MoveNext() = 
//        let entered = enterLockIf syncRoot  this.isSynchronized
//        try
//          if pVersion.Value <> version then
//            // fallback to default pointer behavior
//            pVersion := version // update state to new version
//            if index.Value < 0 then p.MoveFirst()
//            else
//              let position, kvp = this.TryFindWithIndex(currentKey.Value, Lookup.GT)
//              if position > 0 then
//                index := position
//                currentKey := kvp.Key
//                currentValue := kvp.Value
//                true
//              else  // not found
//                index := this.Count + 1
//                currentKey := Unchecked.defaultof<'K>
//                currentValue := Unchecked.defaultof<'V>
//                false
//          else
//            if index.Value < (this.Count - 1) then
//              index := !index + 1
//              currentKey := this.keys.[index.Value]
//              currentValue := this.values.[index.Value]
//              true
//            else
//              index := this.Count + 1
//              currentKey := Unchecked.defaultof<'K>
//              currentValue := Unchecked.defaultof<'V>
//              false
//        finally
//          exitLockIf syncRoot entered
//
//      override p.MovePrevious() = 
//        let entered = enterLockIf syncRoot  this.isSynchronized
//        try
//          if pVersion.Value <> version then
//            // fallback to default pointer behavior
//            pVersion := version // update state to new version
//            if index.Value < 0 then p.MoveLast()
//            else
//              let position, kvp = this.TryFindWithIndex(currentKey.Value, Lookup.LT)
//              if position > 0 then
//                index := position
//                currentKey := kvp.Key
//                currentValue := kvp.Value
//                true
//              else  // not found
//                index := this.Count + 1
//                currentKey := Unchecked.defaultof<'K>
//                currentValue := Unchecked.defaultof<'V>
//                false
//          else
//            if index.Value >= 1 then
//              index := index.Value - 1
//              currentKey := this.keys.[index.Value]
//              currentValue := this.values.[index.Value]
//              true
//            else
//              index := this.Count + 1
//              currentKey := Unchecked.defaultof<'K>
//              currentValue := Unchecked.defaultof<'V>
//              false
//        finally
//          exitLockIf syncRoot entered
//
//      override p.MoveAt(key:'K, lookup:Lookup) = 
//        let entered = enterLockIf syncRoot  this.isSynchronized
//        try
//          let position, kvp = this.TryFindWithIndex(key, lookup)
//          if position >= 0 then
//            index := position
//            currentKey := kvp.Key
//            currentValue := kvp.Value
//            true
//          else
//            index := this.Count + 1
//            currentKey := Unchecked.defaultof<'K>
//            currentValue := Unchecked.defaultof<'V>
//            false
//        finally
//          exitLockIf syncRoot entered
//
//      override p.MoveFirst() = 
//        let entered = enterLockIf syncRoot  this.isSynchronized
//        try
//          if this.size > 0 then
//            index := 0
//            currentKey := this.keys.[index.Value]
//            currentValue := this.values.[index.Value]
//            true
//          else
//            index := this.Count + 1
//            currentKey := Unchecked.defaultof<'K>
//            currentValue := Unchecked.defaultof<'V>
//            false
//        finally
//          exitLockIf syncRoot entered
//
//      override p.MoveLast() = 
//        let entered = enterLockIf syncRoot  this.isSynchronized
//        try
//          if this.size > 0 then
//            index := this.Count - 1
//            currentKey := this.keys.[index.Value]
//            currentValue := this.values.[index.Value]
//            true
//          else
//            index := this.Count + 1
//            currentKey := Unchecked.defaultof<'K>
//            currentValue := Unchecked.defaultof<'V>
//            false
//        finally
//          exitLockIf syncRoot entered
//
//      override p.CurrentKey with get() = currentKey.Value
//
//      override p.CurrentValue with get() = currentValue.Value
//
//      override p.Reset() = 
//        pVersion := version // update state to new version
//        index := -1
//        currentKey := Unchecked.defaultof<'K>
//        currentValue := Unchecked.defaultof<'V>
//
//      override p.Dispose() = p.Reset()
//    } 
//
//  /// If size is less than 80% of capacity then reduce capacity to the size
//  member this.TrimExcess() =
//    if this.size < int(float(this.keys.Length) * 0.8) then this.Capacity <- this.size
//
//  //#endregion
//
//
//  //#region Interfaces
//
//  interface IEnumerable with
//    member this.GetEnumerator() = this.GetROOMCursor() :> IEnumerator
//
//  interface IEnumerable<KeyValuePair<'K,'V>> with
//    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
//      this.GetROOMCursor() :> IEnumerator<KeyValuePair<'K,'V>>
//
//  interface ICollection  with
//    member this.SyncRoot = syncRoot
//    member this.CopyTo(array, arrayIndex) =
//      if array = null then raise (ArgumentNullException("array"))
//      if arrayIndex < 0 || arrayIndex > array.Length then raise (ArgumentOutOfRangeException("arrayIndex"))
//      if array.Length - arrayIndex < this.Count then raise (ArgumentException("ArrayPlusOffTooSmall"))
//      for index in 0..this.size do
//        let kvp = KeyValuePair(this.keys.[index], this.values.[index])
//        array.SetValue(kvp, arrayIndex + index)
//    member this.Count = this.Count
//    member this.IsSynchronized with get() =  this.isSynchronized
//
//  interface IDictionary<'K,'V> with
//    member this.Count = this.Count
//    member this.IsReadOnly with get() = this.IsReadOnly
//    member this.Item
//      with get key = this.Item(key)
//      and set key value = this.[key] <- value
//    member this.Keys with get() = this.Keys :> ICollection<'K>
//    member this.Values with get() = this.Values :> ICollection<'V>
//    member this.Clear() = this.Clear()
//    member this.ContainsKey(key) = this.ContainsKey(key)
//    member this.Contains(kvp:KeyValuePair<'K,'V>) = this.ContainsKey(kvp.Key)
//    member this.CopyTo(array, arrayIndex) =
//      if array = null then raise (ArgumentNullException("array"))
//      if arrayIndex < 0 || arrayIndex > array.Length then raise (ArgumentOutOfRangeException("arrayIndex"))
//      if array.Length - arrayIndex < this.Count then raise (ArgumentException("ArrayPlusOffTooSmall"))
//      for index in 0..this.Count do
//        let kvp = KeyValuePair(this.Keys.[index], this.Values.[index])
//        array.[arrayIndex + index] <- kvp
//    member this.Add(key, value) = this.Add(key, value)
//    member this.Add(kvp:KeyValuePair<'K,'V>) = this.Add(kvp.Key, kvp.Value)
//    member this.Remove(key) = this.Remove(key)
//    member this.Remove(kvp:KeyValuePair<'K,'V>) = this.Remove(kvp.Key)
//    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
//      let index = this.IndexOfKey(key)
//      if index >= 0 then
//        value <- this.GetValueByIndex(index)
//        true
//      else
//        value <- Unchecked.defaultof<'V>
//        false
//
//  interface IReadOnlyOrderedMap<'K,'V> with
//    member this.Comparer with get() = comparer
//    member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
//    member this.GetCursor() = this.GetCursor()
//    member this.IsEmpty = this.size = 0
//    member this.IsIndexed with get() = false
//    member this.IsMutable with get() = true
//    //member this.Count with get() = int this.size
//    member this.First with get() = this.First
//    member this.Last with get() = this.Last
//    member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
//      this.TryFindWithIndex(k, direction, &result) >=0
//    member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
//      try
//        res <- this.First
//        true
//      with
//      | _ -> 
//        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
//        false
//    member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
//      try
//        res <- this.Last
//        true
//      with
//      | _ -> 
//        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
//        false
//    member this.TryGetValue(k, [<Out>] value:byref<'V>) = this.TryGetValue(k, &value)
//    member this.Item with get k = this.Item(k)
//    member this.GetAt(idx:int) = this.values.[idx]
//    member this.Keys with get() = this.keys :> IEnumerable<'K>
//    member this.Values with get() = this.values :> IEnumerable<'V>
//    member this.SyncRoot with get() = syncRoot
//    
//
//  interface IOrderedMap<'K,'V> with
//    member this.Version with get() = int64(this.Version)
//    member this.Count with get() = int64(this.size)
//    member this.Item with get k = this.Item(k) and set (k:'K) (v:'V) = this.[k] <- v
//    member this.Add(k, v) = this.Add(k,v)
//    member this.AddLast(k, v) = this.AddLast(k, v)
//    member this.AddFirst(k, v) = this.AddFirst(k, v)
//    member this.Remove(k) = this.Remove(k)
//    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
//      this.RemoveFirst(&result)
//    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
//      this.RemoveLast(&result)
//    member this.RemoveMany(key:'K,direction:Lookup) = 
//      this.RemoveMany(key, direction)
////    member x.TryFindWithIndex(key: 'K, direction: Lookup, result: byref<KeyValuePair<'K,'V>>): int = 
////      this.TryFindWithIndex(key, direction, &result)
//    member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>, option:AppendOption) =
////      for i in appendMap do
////        this.AddLast(i.Key, i.Value)
//      raise (NotImplementedException("TODO append impl"))
//    
//  interface IImmutableOrderedMap<'K,'V> with
//    member this.Size with get() = int64(this.size)
//    // Immutable addition, returns a new map with added value
//    member this.Add(key, value):IImmutableOrderedMap<'K,'V> =
//      let newMap = this.Clone()
//      newMap.Add(key, value)
//      newMap :> IImmutableOrderedMap<'K,'V>
//    member this.AddFirst(key, value):IImmutableOrderedMap<'K,'V> =
//      let newMap = this.Clone()
//      newMap.AddFirst(key, value)
//      newMap :> IImmutableOrderedMap<'K,'V>
//    member this.AddLast(key, value):IImmutableOrderedMap<'K,'V> =
//      let newMap = this.Clone()
//      newMap.AddLast(key, value)
//      newMap :> IImmutableOrderedMap<'K,'V>
//    member this.Remove(key):IImmutableOrderedMap<'K,'V> =
//      let newMap = this.Clone()
//      newMap.Remove(key) |> ignore
//      newMap :> IImmutableOrderedMap<'K,'V>
//    member this.RemoveLast([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableOrderedMap<'K,'V> =
//      let newMap = this.Clone()
//      newMap.RemoveLast(&value) |> ignore
//      newMap :> IImmutableOrderedMap<'K,'V>
//    member this.RemoveFirst([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableOrderedMap<'K,'V> =
//      let newMap = this.Clone()
//      newMap.RemoveFirst(&value) |> ignore
//      newMap :> IImmutableOrderedMap<'K,'V>
//    member this.RemoveMany(key:'K,direction:Lookup):IImmutableOrderedMap<'K,'V>=
//      let newMap = this.Clone()
//      newMap.RemoveMany(key, direction) |> ignore
//      newMap :> IImmutableOrderedMap<'K,'V>
//
//  //#endregion   
//
//  //#region Constructors
//
//  new() = SortedMap(Dictionary(), 4, Comparer<'K>.Default)
//  new(comparer:IComparer<'K>) = SortedMap(Dictionary(), 4, comparer)
//  new(dictionary:IDictionary<'K,'V>) = SortedMap(dictionary, dictionary.Count, Comparer<'K>.Default)
//  new(capacity:int) = SortedMap(Dictionary(), capacity, Comparer<'K>.Default)
//  new(dictionary:IDictionary<'K,'V>,comparer:IComparer<'K>) = SortedMap(dictionary, dictionary.Count, comparer)
//  new(capacity:int,comparer:IComparer<'K>) = SortedMap(Dictionary(), capacity, comparer)
//
//  static member internal OfKeysAndValues(size:int, keys:'K[],values:'V[]) =
//    let sm = new SortedMap<'K,'V>()
//    sm.size <- size
//    sm.keys <- keys
//    sm.values <- values
//    sm
//  //#endregion
//
//
