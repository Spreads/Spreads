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
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks


open Spreads
open Spreads.Collections

// TODO inline (manually) regular keys calculation, the performance hit is visible on benchmark: 45 mops vs 66 mops (or prove that this is due to the math)

// TODO whenever we add last value, the value must be written to the array before size increment
// TODO we must increment  this.orderVersion before any array change
// TODO when not synchronized, a cursor must always throw on  this.orderVersion change, because we change keys/values array??

// TODO settable IsSyncronized with check on every mutation
// TODO Size to Count

// Why regular keys? Because we do not care about daily or hourly data, but there are 1440 (480) minutes in a day (trading hours)
// with the same diff between each consequitive minute. The number is much bigger with seconds and msecs so that
// memory saving is meaningful, while vectorized calculations on values benefit from fast comparison of regular keys.
// Ticks (and seconds for illiquid instruments) are not regular, but they are never equal among different instruments.

// TODOs
// (done, do it again) test rkLast where it is used, possibly it is not updated in some place where an update is needed
// TODO Test binary & JSON.NET (de)serialization
// (done, KeyComparer was slow) regular add and iterate are slower than the default, which should not be the case at least for add (do we check for rkLast equality?)
// TODO tests with regular keys and step > 1, especially for LT/LE/GT/GE lookups
// TODO cursor tests: all directions, call finished cursor more than once should return false not error.
// TODO events as they are kill performance, see http://v2matveev.blogspot.ru/2010/06/f-performance-of-events.html... - even the fast events from the blog
// TODO read only must have internal setter (or forcedReadOnly internal field) that will indicate that the series will never mutate and it is safe to reuse
// keys array, e.g. for mapValues.



/// Mutable sorted IOrderedMap<'K,'V> implementation based similar to SCG.SortedList<'K,'V>
[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedMap<'K,'V>
  internal(dictionary:IDictionary<'K,'V> option, capacity:int option, comparerOpt:IComparer<'K> option) as this=
  inherit Series<'K,'V>()
  
  // data fields
  [<DefaultValueAttribute>] 
  val mutable internal version : int // enumeration doesn't lock but checks this.version
  [<DefaultValueAttribute>]
  val mutable internal size : int
  // TODO isMutable as field
  [<DefaultValueAttribute>]
  val mutable internal keys : 'K array
  [<DefaultValueAttribute>]
  val mutable internal values : 'V array



  /// used for cursors, incremented on any out of order data change that require a cursor either to throw to to recover with repositioning
  [<NonSerializedAttribute>]
  [<DefaultValueAttribute>] 
  val mutable orderVersion : int
  // util fields
  [<NonSerializedAttribute>]
  // This type is logically immutable. This field is only mutated during deserialization. 
  let mutable comparer : IComparer<'K> = 
    if comparerOpt.IsNone || Comparer<'K>.Default.Equals(comparerOpt.Value) then
      let kc = KeyComparer.GetDefault<'K>()
      if kc = Unchecked.defaultof<_> then Comparer<'K>.Default :> IComparer<'K> 
      else kc
    else comparerOpt.Value // do not try to replace with KeyComparer if a comparer was given
  [<NonSerializedAttribute>]
  let isKeyReferenceType : bool = not typeof<'K>.IsValueType
  [<NonSerializedAttribute>]
  [<Obsolete("rename to observer counter, also check if event do this automatically")>]
  let mutable cursorCounter : int = 1 // TODO either delete this or add decrement to cursor disposal
  [<NonSerializedAttribute>]
  let mutable rkStep_ : int64 = 0L // TODO review all usages

  // TODO use IDC resolution via KeyHelper.diffCalculators here or befor ctor
  [<NonSerializedAttribute>]
  let mutable couldHaveRegularKeys : bool = comparer :? IKeyComparer<'K>
  [<NonSerializedAttribute>]
  let mutable diffCalc : IKeyComparer<'K> =  // TODO review all usages, could have missed rkGetStep()
    if couldHaveRegularKeys then comparer :?> IKeyComparer<'K> 
    else Unchecked.defaultof<IKeyComparer<'K>>
  // TODO off by one in remove not checked, in insert is OK
  [<NonSerializedAttribute>]
  let mutable rkLast = Unchecked.defaultof<'K>
  [<NonSerializedAttribute>]
  let mutable isSynchronized : bool = false
  // NB it is serializable, but stored as sign bit of version in out default serialization
  let mutable isMutable : bool = true
  [<NonSerializedAttribute>]
  let syncRoot = new Object()
  [<NonSerializedAttribute>]
  let mutable mapKey = ""


  do
    let tempCap = if capacity.IsSome then capacity.Value else 1
    if dictionary.IsNone || dictionary.Value.Count = 0 then // otherwise we will set them in dict processing part
      this.keys <- OptimizationSettings.ArrayPool.TakeBuffer (if couldHaveRegularKeys then 2 else tempCap) // regular keys are the first and the second value, their diff is the step
    this.values <- OptimizationSettings.ArrayPool.TakeBuffer tempCap

    if dictionary.IsSome && dictionary.Value.Count > 0 then
      match dictionary.Value with
      | :? SortedMap<'K,'V> as map ->
        let entered = enterLockIf map.SyncRoot map.IsSynchronized
        try
          couldHaveRegularKeys <- map.IsRegular
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
        let tempKeys = 
//          if dictionary.Value.Count = 1 then 
//            [|(dictionary.Value.Keys |> Seq.toArray).[0]; Unchecked.defaultof<'K>|] // TODO this is very ugly, just to fix an edge case bug
//          else
            dictionary.Value.Keys |> Seq.toArray
        dictionary.Value.Values.CopyTo(this.values, 0)
        Array.Sort(tempKeys, this.values, comparer)
        this.size <- dictionary.Value.Count

        // TODO review
        if couldHaveRegularKeys && this.size > 1 then // if could be regular based on initial check of comparer type
          let isReg, step, firstArr = this.rkCheckArray tempKeys this.size (comparer :?> IKeyComparer<'K>)
          couldHaveRegularKeys <- isReg
          if couldHaveRegularKeys then 
            this.keys <- firstArr
            OptimizationSettings.ArrayPool.ReturnBuffer tempKeys |> ignore
            rkLast <- this.rkKeyAtIndex (this.size - 1)
          else
            this.keys <- tempKeys
        else
          this.keys <- tempKeys
        
      


#if FX_NO_BINARY_SERIALIZATION
#else
  [<System.Runtime.Serialization.OnSerializingAttribute>]
  member __.OnSerializing(context: System.Runtime.Serialization.StreamingContext) =
    ignore(context)

  [<System.Runtime.Serialization.OnDeserializedAttribute>]
  member __.OnDeserialized(context: System.Runtime.Serialization.StreamingContext) =
    ignore(context)
    // TODO check if we have a default special IKeyComparer for the type
    comparer <- Comparer<'K>.Default :> IComparer<'K> //LanguagePrimitives.FastGenericComparer<'K>
    // TODO assign all fields that are marked with NonSerializable
    if this.size > this.keys.Length then // regular keys
      rkLast <- this.GetKeyByIndex(this.size - 1)

#endif


  //#region Private & Internal members

  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.rkGetStep() =
    #if PRERELEASE
    Trace.Assert(this.size > 1)
    #endif
    if rkStep_ > 0L then rkStep_
    elif this.size > 1 then
      rkStep_ <- diffCalc.Diff(this.keys.[1], this.keys.[0])
      rkStep_
    else raise (InvalidOperationException("Cannot calculate regular keys step for a single element in a map or an empty map"))
  
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.rkKeyAtIndex (idx:int) : 'K = diffCalc.Add(this.keys.[0], (int64 idx)*this.rkGetStep())
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.rkIndexOfKey (key:'K) : int =
    Trace.Assert(this.size > 1)
    // TODO this doesn't work for LT/LE/GT/GE TryFind

    let diff = diffCalc.Diff(key, this.keys.[0])
    let step = this.rkGetStep()
    let idxL : int64 = (diff / step)
    let modulo = (diff - step * idxL)
    let idx = idxL
//    https://msdn.microsoft.com/en-us/library/2cy9f6wb(v=vs.110).aspx
//    The index of the specified value in the specified array, if value is found.
//    If value is not found and value is less than one or more elements in array, 
//    a negative number which is the bitwise complement of the index of the first 
//    element that is larger than value. If value is not found and value is greater 
//    than any of the elements in array, a negative number which is the bitwise 
//    complement of (the index of the last element plus 1).

    // TODO test it for diff <> step, bug-prone stuff here
    if modulo = 0L then
      if idx < 0L then
        ~~~0 // -1 for searches, insert will take ~~~
      elif idx >= int64 this.size then
        ~~~this.size
      else
        int idx // > 0 and < size => always withing int range
//      elif modIsOk then
//        idx
//      else
//        ~~~(idx+1)
    else
      if idx <= 0L && diff < 0L then
        ~~~0 // -1 for searches, insert will take ~~~
      elif idx >= int64 this.size then
        ~~~this.size
      else
        ~~~((int idx)+1)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.rkMaterialize () =
    let step = this.rkGetStep()
    Array.init this.values.Length (fun i -> if i < this.size then diffCalc.Add(this.keys.[0], (int64 i)*step) else Unchecked.defaultof<'K>)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.rkCheckArray (sortedArray:'K[]) (size:int) (dc:IKeyComparer<'K>) : bool * int * 'K array = 
    if size > sortedArray.Length then raise (ArgumentException("size is greater than sortedArray length"))
    if size < 1 then
      true, 0, [|Unchecked.defaultof<'K>;Unchecked.defaultof<'K>|]
    elif size < 2 then
      true, 0, [|sortedArray.[0];Unchecked.defaultof<'K>|]
    elif size < 3 then 
      true, int <| dc.Diff(sortedArray.[1], sortedArray.[0]), [|sortedArray.[0];sortedArray.[1]|]
    else
      let firstDiff = dc.Diff(sortedArray.[1], sortedArray.[0])
      let mutable isReg = true
      let mutable n = 2
      while isReg && n < size do
        let newDiff = dc.Diff(sortedArray.[n], sortedArray.[n-1])
        if newDiff <> firstDiff then
          isReg <- false
        n <- n + 1
      if isReg then
        true, int firstDiff, [|sortedArray.[0];sortedArray.[1]|]
      else
        false, 0, Unchecked.defaultof<'K[]>

  member private this.Clone() = new SortedMap<'K,'V>(Some(this :> IDictionary<'K,'V>), None, Some(comparer))
  member private this.SetRkLast(rkl) = rkLast <- rkl

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member internal this.GetKeyByIndexUnchecked(index) =
    if couldHaveRegularKeys && this.size > 1 then this.rkKeyAtIndex index
    else this.keys.[index]

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member internal this.GetKeyByIndex(index) =
    if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
    this.GetKeyByIndexUnchecked(index)
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.GetPairByIndexUnchecked(index) = //inline
    if couldHaveRegularKeys && this.size > 1 then
      if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
      KeyValuePair(this.rkKeyAtIndex index, this.values.[index])
    else KeyValuePair(this.keys.[index], this.values.[index]) 
  
  member private this.CompareToFirst (k:'K) =  //inline
    comparer.Compare(k, this.keys.[0]) // keys.[0] is always the first key
  
  member private this.CompareToLast (k:'K) = //inline
    if couldHaveRegularKeys && this.size > 1 then 
      Debug.Assert(not <| Unchecked.equals rkLast Unchecked.defaultof<'K>)
      comparer.Compare(k, rkLast)
    else comparer.Compare(k, this.keys.[this.size-1])

  member private this.EnsureCapacity(min) = 
    let mutable num = this.values.Length * 2 
    if num > 2146435071 then num <- 2146435071
    if num < min then num <- min // either double or min if min > 2xprevious
    this.Capacity <- num


  member private this.Insert(index:int, k, v) = 
    if not isMutable then invalidOp "SortedMap is not mutable"
    // key is always new, checks are before this method
    // already inside a lock statement in a caller method if synchronized

    let mutable keepVersion = false
    if this.size = this.values.Length then this.EnsureCapacity(this.size + 1)
    
    // for values it is alway the same operation
    Trace.Assert(index <= this.size, "index must be <= this.size")
    if index < this.size then Array.Copy(this.values, index, this.values, index + 1, this.size - index);
    this.values.[index] <- v

    // for regular keys must do some math to check if they will remain regular after the insertion
    // treat sizes 1,2(after insertion) as non-regular because they are always both regular and not 
    if couldHaveRegularKeys then
      if this.size > 1 then
        let step = this.rkGetStep()
        if comparer.Compare(diffCalc.Add(rkLast, step), k) = 0 then
          // adding next regular, only rkLast changes
          rkLast <- k
          keepVersion <- true
        elif comparer.Compare(diffCalc.Add(this.keys.[0], -step), k) = 0 then
          this.keys.[1] <- this.keys.[0]
          this.keys.[0] <- k // change first key and size++ at the bottom
          //rkLast is unchanged
        else
          let diff = diffCalc.Diff(k, this.keys.[0])
//          let mutable rem = 0L
//          let idxL : int64 = Math.DivRem(diff, step, &rem) //(diff / step)
//          let modIsOk = rem = 0L // (diff - step * idxL) = 0L
          let idxL : int64 = (diff / step)
          let modIsOk = (diff - step * idxL) = 0L // gives 13% boost for add compared to diff % step
          let idx = int idxL 
          if modIsOk && idx > -1 && idx < this.size then
            // error for regular keys, this means we insert existing key
            raise (new ApplicationException("Existing key check must be done before insert"))
          else
            // insertting more than 1 away from end or before start, with a hole
            this.keys <- this.rkMaterialize() 
            couldHaveRegularKeys <- false
      else
        if index < this.size then
          Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
        else 
          keepVersion <- true
          rkLast <- k
        this.keys.[index] <- k
        
    // couldHaveRegularKeys could be set to false inside the previous block even if it was true before
    if not couldHaveRegularKeys then
      if index < this.size then
        Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
      else keepVersion <- true
      this.keys.[index] <- k
      // do not check if could regularize back, it is very rare 
      // that an irregular becomes a regular one, and such check is always done on
      // bucket switch in SHM (TODO really? check) and before serialization
      // the 99% use case is when we load data from a sequential stream or deserialize a map with already regularized keys
    if not keepVersion then this.orderVersion <- this.orderVersion + 1
    this.size <- this.size + 1
    this.version <- this.version + 1
    if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(k,v))
    
  member this.Complete() =
    let entered = enterLockIf syncRoot true
    try
      if isMutable then 
          isMutable <- false
          // immutable doesn't need sync
          isSynchronized <- false // TODO the same for SCM
          if cursorCounter > 0 then this.onCompletedEvent.Trigger(true)
    finally
      exitLockIf syncRoot entered

  member internal this.IsMutable with get() = isMutable
  override this.IsReadOnly with get() = not isMutable
  override this.IsIndexed with get() = false

  member this.IsSynchronized 
    with get() = isSynchronized
    and set(synced:bool) = 
      let entered = enterLockIf syncRoot isSynchronized
      isSynchronized <- synced
      exitLockIf syncRoot entered

  member internal this.MapKey with get() = mapKey and set(key:string) = mapKey <- key

  // external world should not care about this
  // TODO internal
  member this.IsRegular with get() = couldHaveRegularKeys and private set (v) = couldHaveRegularKeys <- v
  member this.RegularStep with get() = this.rkGetStep()
  member this.SyncRoot with get() = syncRoot
  member this.Version with get() = this.version and internal set v = this.version <- v // NB setter only for deserializer

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
          if not isMutable then invalidOp "SortedMap is not mutable"
          if couldHaveRegularKeys then
            Trace.Assert(this.keys.Length = 2)
          else
            let kArr : 'K array = OptimizationSettings.ArrayPool.TakeBuffer(c)
            Array.Copy(this.keys, 0, kArr, 0, this.size)
            let toReturn = this.keys
            this.keys <- kArr
            OptimizationSettings.ArrayPool.ReturnBuffer(toReturn) |> ignore
            

          let vArr : 'V array = OptimizationSettings.ArrayPool.TakeBuffer(c)
          Array.Copy(this.values, 0, vArr, 0, this.size)
          let toReturn = this.values
          this.values <- vArr
          OptimizationSettings.ArrayPool.ReturnBuffer(toReturn) |> ignore
        | _ -> ()
      finally
        exitLockIf syncRoot entered

  member this.Comparer with get() = comparer

  member this.Clear() =
    this.version <- this.version + 1
    this.orderVersion <- this.orderVersion + 1
    if couldHaveRegularKeys then
      Trace.Assert(this.keys.Length = 2)
      Array.Clear(this.keys, 0, 2)
    else
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
          if couldHaveRegularKeys && this.size > 2 then
            Array.Copy(this.rkMaterialize(), 0, array, arrayIndex, this.size)
          else
            Array.Copy(this.keys, 0, array, arrayIndex, this.size)
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
              if index.Value < this.size then
                currentKey := 
                  if couldHaveRegularKeys && this.size > 1 then diffCalc.Add(this.keys.[0], (int64 !index)*this.rkGetStep()) 
                  else this.keys.[!index]
                index := index.Value + 1
                true
              else
                index := this.size + 1
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
        member x.Count with get() = this.size
        member x.IsReadOnly with get() = true
        member x.Item 
          with get index : 'V = this.values.[index]
          and set index value = raise (NotSupportedException("Values collection is read-only"))
        member x.Add(k) = raise (NotSupportedException("Values colelction is read-only"))
        member x.Clear() = raise (NotSupportedException("Values colelction is read-only"))
        member x.Contains(value) = this.ContainsValue(value)
        member x.CopyTo(array, arrayIndex) = 
          Array.Copy(this.values, 0, array, arrayIndex, this.size)
        member x.IndexOf(value:'V) = this.IndexOfValue(value)
        member x.Insert(index, value) = raise (NotSupportedException("Values collection is read-only"))
        member x.Remove(value:'V) = raise (NotSupportedException("Values collection is read-only"))
        member x.RemoveAt(index:int) = raise (NotSupportedException("Values collection is read-only"))
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
              if index.Value < this.size then
                currentValue := this.values.[index.Value]
                index := index.Value + 1
                true
              else
                index := this.size + 1
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

  member internal this.IndexOfKeyUnchecked(key:'K) : int =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if couldHaveRegularKeys && this.size > 1 then 
        this.rkIndexOfKey key
      else 
        Array.BinarySearch(this.keys, 0, this.size, key, comparer)
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
      if couldHaveRegularKeys && this.size > 1 then 
        Trace.Assert(comparer.Compare(rkLast, diffCalc.Add(this.keys.[0], (int64 (this.size-1))*this.rkGetStep())) = 0)
        KeyValuePair(rkLast, this.values.[this.size - 1])
      else KeyValuePair(this.keys.[this.size - 1], this.values.[this.size - 1])

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
            let lc = this.CompareToLast key
            if lc < 0 then
              let index = this.IndexOfKeyUnchecked(key)
              if index >= 0 then
                this.values.[index]
              else
                raise (KeyNotFoundException())
            elif lc = 0 then // key = last key
              this.values.[this.size-1]
            else raise (KeyNotFoundException())              
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
        let lc = this.CompareToLast k
        if lc = 0 then // key = last key
          this.values.[lastIdx] <- v
          this.version <- this.version + 1
          this.orderVersion <-  this.orderVersion + 1
          if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(k,v))
          lastIdx
        elif lc > 0 then // adding last value, Insert won't copy arrays if enough capacity
          this.Insert(this.size, k, v)
          this.size
        else
          let index = this.IndexOfKeyUnchecked(k)
          if index >= 0 then // contains key 
            this.values.[index] <- v
            this.version <- this.version + 1
            this.orderVersion <- this.orderVersion + 1
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
      // last optimization gives near 2x performance boost
      let lc = this.CompareToLast key
      if lc = 0 then // key = last key
          raise (ArgumentException("SortedMap.Add: key already exists: " + key.ToString()))
      elif lc > 0 then // adding last value, Insert won't copy arrays if enough capacity
          this.Insert(this.size, key, value)
      else
          let index = this.IndexOfKeyUnchecked(key)
          if index >= 0 then // contains key 
              raise (ArgumentException("SortedMap.Add: key already exists: " + key.ToString()))
          else
              this.Insert(~~~index, key, value)
    //finally
    exitLockIf syncRoot entered

  member this.AddLast(key, value):unit =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if this.size = 0 then
        this.Insert(0, key, value)
      else
        let c = this.CompareToLast key
        if c > 0 then 
          this.Insert(this.size, key, value)
        else 
          let exn = OutOfOrderKeyException(this.Last.Key, key, "SortedMap.AddLast: New key is smaller or equal to the largest existing key")
          this.onErrorEvent.Trigger(exn)
          raise (exn)
    finally
      exitLockIf syncRoot entered

  member this.AddFirst(key, value):unit =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if this.size = 0 then
        this.Insert(0, key, value)
      else
        let c = this.CompareToFirst key
        if c < 0 then
            this.Insert(0, key, value)
        else 
          let exn = OutOfOrderKeyException(this.Last.Key, key, "SortedMap.AddLast: New key is larger or equal to the smallest existing key")
          this.onErrorEvent.Trigger(exn)
          raise (exn)
    finally
      exitLockIf syncRoot entered
    
  member internal this.RemoveAt(index):unit =
    let entered = enterLockIf syncRoot isSynchronized
    try
      if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
      let newSize = this.size - 1
      // TODO review, check for off by 1 bugs, could had lost focus at 3 AM
      // keys
      if couldHaveRegularKeys && this.size > 2 then // will have >= 2 after removal
        if index = 0 then
          this.keys.[0] <- (diffCalc.Add(this.keys.[0], this.rkGetStep())) // change first key to next and size--
          this.keys.[1] <- (diffCalc.Add(this.keys.[0], this.rkGetStep())) // add step to the new first value
        elif index = newSize then 
          rkLast <- diffCalc.Add(this.keys.[0], (int64 (newSize-1))*this.rkGetStep()) // removing last, only size--
        else
          // removing within range,  creating a hole
          this.keys <- this.rkMaterialize()
          couldHaveRegularKeys <- false
      elif couldHaveRegularKeys && this.size = 2 then // will have single value with undefined step
        if index = 0 then
          this.keys.[0] <- this.keys.[1]
          this.keys.[1] <- Unchecked.defaultof<'K>
        elif index = 1 then
          rkLast <- this.keys.[0]
        rkStep_ <- 0L


      if not couldHaveRegularKeys || this.size = 1 then
        if index < this.size then
          Array.Copy(this.keys, index + 1, this.keys, index, newSize - index) // this.size
        this.keys.[newSize] <- Unchecked.defaultof<'K>
      
      // values
      if index < newSize then
        Array.Copy(this.values, index + 1, this.values, index, newSize - index) //this.size
      this.values.[newSize] <- Unchecked.defaultof<'V>

      this.size <- newSize
      this.version <- this.version + 1
      this.orderVersion <-  this.orderVersion + 1

      if cursorCounter > 0 then
        this.onErrorEvent.Trigger(NotImplementedException("TODO remove should trigger a special exception"))
        // on removal, the next valid value is the previous one and all downstreams must reposition and replay from it
//        if index > 0 then this.onNextEvent.Trigger(this.GetPairByIndexUnchecked(index - 1)) // after removal (index - 1) is unchanged
//        else this.onNextEvent.Trigger(Unchecked.defaultof<_>)
    finally
      exitLockIf syncRoot entered

  member this.Remove(key):bool =
    let entered = enterLockIf syncRoot isSynchronized
    try
      if not isMutable then invalidOp "SortedMap is not mutable"
      let index = this.IndexOfKey(key)
      if index >= 0 then this.RemoveAt(index)
      index >= 0
    finally
      exitLockIf syncRoot entered

  // TODO why not just remove from idx 0?
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

  // TODO why not just remove from idx (size - 1)?
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
      if not isMutable then invalidOp "SortedMap is not mutable"
      if this.size = 0 then false
      else
        let mutable kvp = Unchecked.defaultof<_>
        let pivotIndex = this.TryFindWithIndex(key, direction, &kvp)
        // pivot should be removed, after calling TFWI pivot is always inclusive
        match direction with
        | Lookup.EQ -> this.Remove(key)
        | Lookup.LT | Lookup.LE ->
          if pivotIndex = -1 then // pivot is not here but to the left, keep all elements
            false
          elif pivotIndex >=0 then // remove elements below pivot and pivot
            this.size <- this.size - (pivotIndex + 1)
            this.version <- this.version + 1
            this.orderVersion <-  this.orderVersion + 1
            if couldHaveRegularKeys then
              this.keys.[0] <- (diffCalc.Add(this.keys.[0], int64 (pivotIndex+1)))
              if this.size > 1 then 
                this.keys.[1] <- (diffCalc.Add(this.keys.[0], this.rkGetStep())) 
              else
                this.keys.[1] <- Unchecked.defaultof<'K>
                rkStep_ <- 0L
            else
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
          elif pivotIndex >= 0 then // remove elements above and including pivot
            this.size <- pivotIndex
            if couldHaveRegularKeys then
              if this.size > 1 then
                rkLast <- diffCalc.Add(this.keys.[0], (int64 (this.size-1))*this.rkGetStep()) // -1 is correct, the size is updated on the previous line
              else
                this.keys.[1] <- Unchecked.defaultof<'K>
                rkStep_ <- 0L
                if this.size = 1 then rkLast <- this.keys.[0] 
                else rkLast <- Unchecked.defaultof<_>
            if not couldHaveRegularKeys then
              Array.fill this.keys pivotIndex (this.values.Length - pivotIndex) Unchecked.defaultof<'K>
            Array.fill this.values pivotIndex (this.values.Length - pivotIndex) Unchecked.defaultof<'V>
            this.version <- this.version + 1
            this.orderVersion <-  this.orderVersion + 1
            this.Capacity <- this.size
            true
          else
            raise (ApplicationException("wrong result of TryFindWithIndex with GT/GE direction"))
        | _ -> failwith "wrong direction"
    finally
      this.onErrorEvent.Trigger(NotImplementedException("TODO remove should trigger a special exception"))
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
          if this.size > 0 && this.CompareToLast(key) = 0 then // key = last key
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
          let lc = if this.size > 0 then this.CompareToLast(key) else -2
          if lc = 0 then // key = last key
            if this.size > 1 then
              result <-  this.GetPairByIndexUnchecked(lastIdx-1) // return item beforelast
              lastIdx - 1
            else -1
          elif lc > 0 then // key greater than the last
            result <-  this.GetPairByIndexUnchecked(lastIdx) // return the last item 
            lastIdx
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
          let lc = if this.size > 0 then this.CompareToLast(key) else -2
          if lc >= 0 then // key = last key or greater than the last key
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
    let mutable kvp = Unchecked.defaultof<_>
    let idx = this.TryFindWithIndex(k, direction, &kvp)
    if idx >= 0 then
        res <- kvp
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
        let lc = this.CompareToLast key
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

  override this.GetCursor() =
    if isMutable then
      this.GetCursor(-1,  this.orderVersion, Unchecked.defaultof<'K>, Unchecked.defaultof<'V>) :> ICursor<'K,'V>
    else new SortedMapCursor<'K,'V>(this) :> ICursor<'K,'V>
  // foreach optimization
  member this.GetEnumerator() = new SortedMapCursor<'K,'V>(this)

  member internal this.GetSMCursor() = new SortedMapCursor<'K,'V>(this)

  // for foreach optimization
  // TODO(?) replace with a mutable struct, like in SCG.SortedList<T>, there are too many virtual calls and reference cells in the most critical paths like MoveNext
  // NB Object expression with ref cells are surprisingly fast compared to a custom class or even a struct
  member internal this.GetCursor(index:int,cursorVersion:int,currentKey:'K, currentValue:'V) =
    let mutable index = index
    let mutable cursorVersion = cursorVersion
    let mutable currentKey : 'K = currentKey
    let mutable currentValue : 'V = currentValue
    let mutable isBatch = false

    let observerStarted = ref false
    let mutable semaphore : SemaphoreSlim = Unchecked.defaultof<_>
    let onNextHandler : OnNextHandler<'K,'V> = 
      OnNextHandler(fun (kvp:KVP<'K,'V>) ->
          if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
      )
    let onCompletedHandler : OnCompletedHandler = 
      OnCompletedHandler(fun _ ->
          if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
      )
      
    { 
      new ICursor<'K,'V> with
        member x.Comparer: IComparer<'K> = this.Comparer
        member x.TryGetValue(key: 'K, value: byref<'V>): bool = this.TryGetValue(key, &value)
        member x.MoveNext(ct: CancellationToken): Task<bool> = 
          let rec completeTcs(tcs:AsyncTaskMethodBuilder<bool>, token: CancellationToken) : unit = // Task<bool> = 
            if x.MoveNext() then
              tcs.SetResult(true)
            else
              let semaphoreTask = semaphore.WaitAsync(-1, token)
              let awaiter = semaphoreTask.GetAwaiter()
              awaiter.UnsafeOnCompleted(fun _ ->
                match semaphoreTask.Status with
                | TaskStatus.RanToCompletion -> 
                  if x.MoveNext() then tcs.SetResult(true)
                  elif not this.IsMutable then tcs.SetResult(false)
                  else completeTcs(tcs, token)
                | _ -> failwith "TODO process all task results"
                ()
              )
          match x.MoveNext() with
          | true -> trueTask            
          | false ->
            match this.IsMutable with
            | true ->
              let upd = this :> IObservableEvents<'K,'V>
              if not !observerStarted then
                semaphore <- new SemaphoreSlim(0,Int32.MaxValue)
                this.onNextEvent.Publish.AddHandler onNextHandler
                this.onCompletedEvent.Publish.AddHandler onCompletedHandler
                observerStarted := true
              let tcs = Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create()
              let returnTask = tcs.Task
              completeTcs(tcs, ct)
              returnTask
            | _ -> falseTask

        member x.Source: ISeries<'K,'V> = this :> ISeries<'K,'V>
      
        member this.IsContinuous with get() = false
        member p.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = 
          let entered = enterLockIf syncRoot  isSynchronized
          try
            // TODO! how to do this correct for mutable case. Looks like impossible without copying
            if isBatch then
              Trace.Assert((index = this.size - 1))
              Trace.Assert(not this.IsMutable)
              this :> IReadOnlyOrderedMap<'K,'V>
            else raise (InvalidOperationException("SortedMap cursor is not at a batch position"))
          finally
            exitLockIf syncRoot entered
        member p.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> =
          let entered = enterLockIf syncRoot  isSynchronized
          try
            if (not this.IsMutable) && (index = -1) then
              index <- this.size - 1 // at the last element of the batch
              currentKey <- this.GetKeyByIndex(index)
              currentValue <- this.values.[index]
              isBatch <- true
              trueTask
            else falseTask
          finally
            exitLockIf syncRoot entered

        member p.Clone() = this.GetCursor(index,cursorVersion, p.CurrentKey, p.CurrentValue) //!currentKey,!currentValue)
      
        member p.MovePrevious() = 
          let entered = enterLockIf syncRoot  isSynchronized
          try
            if index = -1 then p.MoveLast()  // first move when index = -1
            elif cursorVersion =  this.orderVersion then
              if index > 0 && index < this.size then
                index <- index - 1
                currentKey <- this.GetKeyByIndex(index)
                currentValue <- this.values.[index]
                true
              else
                //p.Reset()
                false
            else
              cursorVersion <-  this.orderVersion // update state to new this.version
              let mutable kvp = Unchecked.defaultof<_>
              let position = this.TryFindWithIndex(p.CurrentKey, Lookup.LT, &kvp) //currentKey.Value
              if position > 0 then
                index <- position
                currentKey <- kvp.Key
                currentValue <- kvp.Value
                true
              else  // not found
                //p.Reset()
                false
          finally
            exitLockIf syncRoot entered

        member p.MoveAt(key:'K, lookup:Lookup) = 
          let entered = enterLockIf syncRoot  isSynchronized
          try
            let mutable kvp = Unchecked.defaultof<_>
            let position = this.TryFindWithIndex(key, lookup, &kvp)
            if position >= 0 then
              index <- position
              currentKey <- kvp.Key
              currentValue <- kvp.Value
              true
            else
              p.Reset()
              false
          finally
            exitLockIf syncRoot entered

        member p.MoveFirst() = 
          let entered = enterLockIf syncRoot  isSynchronized
          try
            if this.size > 0 then
              index <- 0
              currentKey <- this.GetKeyByIndex(index)
              currentValue <- this.values.[index]
              true
            else
              p.Reset()
              false
          finally
            exitLockIf syncRoot entered

        member p.MoveLast() = 
          let entered = enterLockIf syncRoot  isSynchronized
          try
            if this.size > 0 then
              index <- this.size - 1
              currentKey <- this.GetKeyByIndex(index)
              currentValue <- this.values.[index]
              true
            else
              p.Reset()
              false
          finally
            exitLockIf syncRoot entered

        member p.CurrentKey with get() = currentKey

        member p.CurrentValue with get() = currentValue

      interface IEnumerator<KVP<'K,'V>> with
        member p.Current with get() : KVP<'K,'V> = KeyValuePair(currentKey, currentValue)
        member p.Current with get() : obj = box p.Current
        member p.MoveNext() = 
          let entered = enterLockIf syncRoot isSynchronized
          try
            if cursorVersion =  this.orderVersion then
              if index < (this.size - 1) then
                index <- index + 1
                // TODO!! (perf) regular keys were supposed to speed up things, not to slow down by 50%! 
                currentKey <- this.GetKeyByIndexUnchecked(index)
                currentValue <- this.values.[index]
                true
              else
                //p.Reset() // NB! Do not reset cursor on false MoveNext
                false
            else  // source change
              cursorVersion <- this.orderVersion // update state to new this.version
              let mutable kvp = Unchecked.defaultof<_>
              let position = this.TryFindWithIndex(currentKey, Lookup.GT, &kvp) // reposition cursor after source change //currentKey.Value
              if position > 0 then
                index <- position
                currentKey <- kvp.Key
                currentValue <- kvp.Value
                true
              else  // not found
                //p.Reset() // NB! Do not reset cursor on false MoveNext
                false
          finally
            exitLockIf syncRoot entered
        member p.Reset() = 
          cursorVersion <-  this.orderVersion // update state to new this.version
          index <- -1
          currentKey <- Unchecked.defaultof<'K>
          currentValue <- Unchecked.defaultof<'V>

        member p.Dispose() = 
          p.Reset()
          if !observerStarted then
            this.onNextEvent.Publish.RemoveHandler(onNextHandler)
            this.onCompletedEvent.Publish.RemoveHandler(onCompletedHandler)
    }

  member this.GetAt(idx:int) = if idx >= 0 && idx < this.size then this.values.[idx] else raise (ArgumentOutOfRangeException("idx", "Idx is out of range in SortedMap GetAt method."))

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
    member this.IsReadOnly with get() = this.IsReadOnly
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
    member this.GetAt(idx:int) = this.GetAt(idx:int)
    member this.Keys with get() = this.Keys :> IEnumerable<'K>
    member this.Values with get() = this.values :> IEnumerable<'V>
    member this.SyncRoot with get() = syncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Complete() = this.Complete()
    member this.Version with get() = int64(this.Version)
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
    // TODO atomic append with single version increase, now it is a sequence of remove/add mutations
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
    
  //#endregion

  //#region Constructors

  // TODO try resolve KeyComparer for know types
  new() = SortedMap(None, None, None)
  new(dictionary:IDictionary<'K,'V>) = SortedMap(Some(dictionary), Some(dictionary.Count), Some(Comparer<'K>.Default :> IComparer<'K>))
  new(minimumCapacity:int) = SortedMap(None, Some(minimumCapacity), Some(Comparer<'K>.Default :> IComparer<'K>))

  // do not expose ctors with comparer to public
  internal new(comparer:IComparer<'K>) = SortedMap(None, None, Some(comparer))
  internal new(dictionary:IDictionary<'K,'V>,comparer:IComparer<'K>) = SortedMap(Some(dictionary), Some(dictionary.Count), Some(comparer))
  internal new(minimumCapacity:int,comparer:IComparer<'K>) = SortedMap(None, Some(minimumCapacity), Some(comparer))

  static member internal OfSortedKeysAndValues(keys:'K[], values:'V[], size:int, comparer:IComparer<'K>, sortChecked:bool, isAlreadyRegular) =
    if keys.Length < size && not isAlreadyRegular then raise (new ArgumentException("Keys array is smaller than provided size"))
    if values.Length < size then raise (new ArgumentException("Values array is smaller than provided size"))
    let sm = new SortedMap<'K,'V>(comparer)
    if sortChecked then
      for i in 1..size-1 do
        if comparer.Compare(keys.[i-1], keys.[i]) >= 0 then raise (new ArgumentException("Keys are not sorted"))

    // at this point IsRegular means could be regular
    if sm.IsRegular && not isAlreadyRegular then
      let isReg, step, firstArr = sm.rkCheckArray keys size (sm.Comparer :?> IKeyComparer<'K>)
      if isReg then
        sm.keys <- firstArr
      else 
        sm.IsRegular <- false
        sm.keys <- keys
    elif sm.IsRegular && isAlreadyRegular then
      Trace.Assert(keys.Length = 2)
      sm.keys <- keys
    elif not sm.IsRegular && not isAlreadyRegular then
      sm.IsRegular <- false
      sm.keys <- keys
    else raise (InvalidOperationException("Keys are marked as already regular, but comparer doesn't cupport regular keys"))
    
    sm.size <- size
    sm.values <- values
    if sm.size > 0 then sm.SetRkLast(sm.GetKeyByIndex(sm.size - 1))
    sm

  static member OfSortedKeysAndValues(keys:'K[], values:'V[], size:int) =
    let sm = new SortedMap<'K,'V>()
    let comparer = sm.Comparer
    SortedMap.OfSortedKeysAndValues(keys, values, size, comparer, true, false)

  static member OfSortedKeysAndValues(keys:'K[], values:'V[]) =
    if keys.Length <> values.Length then raise (new ArgumentException("Keys and values arrays are of different sizes"))
    SortedMap.OfSortedKeysAndValues(keys, values, values.Length)
  //#endregion


  // TODO move to extensions
  /// Checks if keys of two maps are equal
  static member internal KeysAreEqual(smA:SortedMap<'K,_>,smB:SortedMap<'K,_>) : bool =
    if not (smA.size = smB.size) then false
    elif smA.IsRegular && smB.IsRegular 
      && smA.RegularStep = smB.RegularStep
      && smA.Comparer.Equals(smB.Comparer) // TODO test custom comparer equality, custom comparers must implement equality
      && smA.Comparer.Compare(smA.keys.[0], smB.keys.[0]) = 0 then // if steps are equal we could skip checking second elements in keys
      true
    else
      // this is very slow to be used in any "optimization", should use BytesExtensions.UnsafeCompare
      System.Linq.Enumerable.SequenceEqual(smA.keys, smB.keys)

and
  public SortedMapCursor<'K,'V> =
    struct
      val public source : SortedMap<'K,'V>
      val mutable index : int
      val mutable currentKey : 'K
      val mutable currentValue: 'V
      val mutable cursorVersion : int
      val mutable isBatch : bool

      new(source:SortedMap<'K,'V>) = 
        { source = source; 
          index = -1;
          currentKey = Unchecked.defaultof<_>;
          currentValue = Unchecked.defaultof<_>;
          cursorVersion = source.orderVersion;
          isBatch = false;
        }
    end 

    member this.Comparer: IComparer<'K> = this.source.Comparer
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.source.TryGetValue(key, &value)
    member this.MoveNext() =
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.cursorVersion =  this.source.orderVersion then
          if this.index < (this.source.size - 1) then
            this.index <- this.index + 1
            // TODO!! (perf) regular keys were supposed to speed up things, not to slow down by 50%! 
            this.currentKey <- this.source.GetKeyByIndexUnchecked(this.index)
            this.currentValue <- this.source.values.[this.index]
            true
          else
            false // NB! Do not reset cursor on false MoveNext
        else  // source change
          this.cursorVersion <- this.source.orderVersion // update state to new this.source.version
          let mutable kvp = Unchecked.defaultof<_>
          let position = this.source.TryFindWithIndex(this.currentKey, Lookup.GT, &kvp) // reposition cursor after source change //currentKey.Value
          if position > 0 then
            this.index <- position
            this.currentKey <- kvp.Key
            this.currentValue <- kvp.Value
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
          Trace.Assert(this.index = this.source.size - 1)
          Trace.Assert(not this.source.IsMutable)
          this.source :> IReadOnlyOrderedMap<'K,'V>
        else raise (InvalidOperationException("SortedMap cursor is not at a batch position"))
      finally
        exitLockIf this.source.SyncRoot entered

    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> =
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if (not this.source.IsMutable) && (this.index = -1) then
          this.index <- this.source.size - 1 // at the last element of the batch
          this.currentKey <- this.source.GetKeyByIndex(this.index)
          this.currentValue <- this.source.values.[this.index]
          this.isBatch <- true
          trueTask
        else falseTask
      finally
        exitLockIf this.source.SyncRoot entered

    member this.Clone() = 
      let mutable clone = new SortedMapCursor<'K,'V>(this.source)
      clone.index <- this.index
      clone.currentKey <- this.currentKey
      clone.currentValue <- this.CurrentValue
      clone.isBatch <- this.isBatch
      clone

    member this.MovePrevious() = 
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.index = -1 then this.MoveLast()  // first move when index = -1
        elif this.cursorVersion =  this.source.orderVersion then
          if this.index > 0 && this.index < this.source.size then
            this.index <- this.index - 1
            this.currentKey <- this.source.GetKeyByIndex(this.index)
            this.currentValue <- this.source.values.[this.index]
            true
          else
            //p.Reset()
            false
        else
          this.cursorVersion <-  this.source.orderVersion // update state to new this.source.version
          let mutable kvp = Unchecked.defaultof<_>
          let position = this.source.TryFindWithIndex(this.currentKey, Lookup.LT, &kvp) //currentKey.Value
          if position > 0 then
            this.index <- position
            this.currentKey <- kvp.Key
            this.currentValue <- kvp.Value
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
          this.currentKey <- kvp.Key
          this.currentValue <- kvp.Value
          true
        else
          this.Reset()
          false
      finally
        exitLockIf this.source.SyncRoot entered

    member this.MoveFirst() = 
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.source.size > 0 then
          this.index <- 0
          this.currentKey <- this.source.GetKeyByIndex(this.index)
          this.currentValue <- this.source.values.[this.index]
          true
        else
          this.Reset()
          false
      finally
        exitLockIf this.source.SyncRoot entered

    member this.MoveLast() = 
      let entered = enterLockIf this.source.SyncRoot this.source.IsSynchronized
      try
        if this.source.size > 0 then
          this.index <- this.source.size - 1
          this.currentKey <- this.source.GetKeyByIndex(this.index)
          this.currentValue <- this.source.values.[this.index]
          true
        else
          this.Reset()
          false
      finally
        exitLockIf this.source.SyncRoot entered

    member this.CurrentKey with get() = this.currentKey
    member this.CurrentValue with get() = this.currentValue
    member this.Current with get() : KVP<'K,'V> = KeyValuePair(this.currentKey, this.currentValue)

    member this.Reset() = 
      this.cursorVersion <-  this.source.orderVersion
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
