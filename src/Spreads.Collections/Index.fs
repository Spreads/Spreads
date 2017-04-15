// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Collections

open System
open System.Linq
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Runtime.CompilerServices
open System.Reflection

open Spreads
open Spreads.Buffers
open Spreads.Serialization
open Spreads.Collections


// NB: Why regular keys? Because we do not care about daily or hourly data, but there are 1440 (480) minutes in a day (trading hours)
// with the same diff between each consequitive minute. The number is much bigger with seconds and msecs so that
// memory saving is meaningful, while vectorized calculations on values benefit from fast comparison of regular keys.
// Ticks (and seconds for illiquid instruments) are not regular, but they are never equal among different instruments.


[<AllowNullLiteral>]
[<DebuggerTypeProxy(typeof<IDictionaryDebugView<_,_>>)>]
[<DebuggerDisplay("Count = {Count}")>]
type Index<'K>
  internal(keys:'K[] opt, size:int opt, comparerOpt:KeyComparer<'K> opt) as this=

  // data fields
  [<DefaultValueAttribute>]
  val mutable internal version : int64
  [<DefaultValueAttribute>]
  val mutable internal size : int
  [<DefaultValueAttribute>]
  val mutable internal keys : 'K array

//  [<DefaultValueAttribute>] 
//  val mutable internal orderVersion : int64
//  [<DefaultValueAttribute>] 
//  val mutable internal nextVersion : int64
  
  // util fields
  [<DefaultValueAttribute>]
  val mutable internal syncRoot : obj

  let comparer : KeyComparer<'K> = 
    if comparerOpt.IsMissing || Comparer<'K>.Default.Equals(comparerOpt.Present) then
      KeyComparer<'K>.Default
    else comparerOpt.Present // do not try to replace with KeyComparer if a comparer was given


  [<DefaultValueAttribute>] 
  val mutable isKeyReferenceType : bool
  
  let mutable couldHaveRegularKeys : bool = comparer.IsDiffable
  
  // TODO Remove this
  let mutable diffCalc : KeyComparer<'K> = comparer
  
  let mutable rkStep_ : int64 = 0L
  let mutable rkLast = Unchecked.defaultof<'K>

  do
    this.isKeyReferenceType <- not <| typeof<'K>.GetTypeInfo().IsValueType

    if not(keys.BothPresentOrMissing(size)) then raise (ArgumentException("Keys and size must be both either missing or set"))
    if keys.IsPresent && uint32 size.Present > uint32 keys.Present.Length then raise (ArgumentOutOfRangeException("Wrong size"))

    let tempCap = if size.IsPresent then size.Present else 2

    this.keys <- 
      if keys.IsPresent then keys.Present
      elif couldHaveRegularKeys then 
        // regular keys are the first and the second value, their diff is the step
        // NB: Buffer pools could return a buffer greater than the requested length,
        // but for regular keys we alway need a fixed-length array of size 2, so we allocate a new one.
        // TODO wrap the corefx buffer and for len = 2 use a special self-adjusting ObjectPool, because these 
        // arrays are not short-lived and could accumulate in gen 1+ easily.
        Array.zeroCreate 2
      else BufferPool<'K>.Rent(tempCap)


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
  member private this.rkKeyAtIndex (idx:int) : 'K =
    let step = this.rkGetStep()
    diffCalc.Add(this.keys.[0], (int64 idx) * step)
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.rkIndexOfKey (key:'K) : int =
    #if PRERELEASE
    Trace.Assert(this.size > 1)
    #endif

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
      else int idx
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
    // current pool implementation could return any power of 2 above the size, not always the next
    let capacity = Utils.BitUtil.FindNextPositivePowerOfTwo(this.size)
    let keys = BufferPool<'K>.Rent(capacity, true)
    for i in 0..this.size-1 do
      keys.[i] <- diffCalc.Add(this.keys.[0], (int64 i)*step)
    keys

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

  // need this for the SortedMapCursor
  member private this.SetRkLast(rkl) = rkLast <- rkl
  member private this.Clone() = 
    let keys = BufferPool<'K>.Rent(this.keys.Length, true)
    new Index<'K>(Present(keys), Present(this.size), Present(comparer))

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member internal this.GetKeyByIndexUnchecked(index) =
    if couldHaveRegularKeys && this.size > 1 then this.rkKeyAtIndex index
    else this.keys.[index]

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member internal this.GetKeyByIndex(index) =
    if uint32 index >= uint32 this.size then raise (ArgumentOutOfRangeException("index"))
    this.GetKeyByIndexUnchecked(index)
   
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.CompareToFirst (k:'K) =
    comparer.Compare(k, this.keys.[0]) // keys.[0] is always the first key even for regular keys
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.CompareToLast (k:'K) =
    if couldHaveRegularKeys && this.size > 1 then
      #if PRERELEASE
      Trace.Assert(not <| Unchecked.equals rkLast Unchecked.defaultof<'K>)
      #endif
      comparer.Compare(k, rkLast)
    else comparer.Compare(k, this.keys.[this.size-1])

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.EnsureCapacity(min) = 
    let mutable num = this.keys.Length * 2 
    if num > 2146435071 then num <- 2146435071
    if num < min then num <- min // either double or min if min > 2xprevious
    this.SetCapacity(num)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.SetCapacity(value) =
    match value with
    | c when c = this.keys.Length -> ()
    | c when c < this.size -> raise (ArgumentOutOfRangeException("Small capacity"))
    | c when c > 0 ->      
      // first, take new buffers. this could cause out-of-memory
      let kArr : 'K[] = 
        if couldHaveRegularKeys then
          Trace.Assert(this.keys.Length = 2)
          Unchecked.defaultof<_>
        else
          BufferPool<_>.Rent(c)
      if not couldHaveRegularKeys then
        Array.Copy(this.keys, 0, kArr, 0, this.size)
        let toReturn = this.keys
        this.keys <- kArr
        BufferPool<_>.Return(toReturn, true) |> ignore
    | _ -> ()

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  //[<ReliabilityContractAttribute(Consistency.MayCorruptInstance, Cer.MayFail)>]
  member private this.Insert(index:int, k:'K) =
    // key is always new, checks are before this method
    // already inside a lock statement in a caller method if synchronized
   
    #if PRERELEASE
    Trace.Assert(index <= this.size, "index must be <= this.size")
    #endif
    // for regular keys must do some math to check if they will remain regular after the insertion
    // treat sizes 1,2(after insertion) as non-regular because they are always both regular and not 
    if couldHaveRegularKeys then
      if this.size > 1 then
        let step = this.rkGetStep()
        if comparer.Compare(diffCalc.Add(rkLast, step), k) = 0 then
          // adding next regular, only rkLast changes
          rkLast <- k
        elif comparer.Compare(diffCalc.Add(this.keys.[0], -step), k) = 0 then
          this.keys.[1] <- this.keys.[0]
          this.keys.[0] <- k // change first key and size++ at the bottom
          //rkLast is unchanged
        else
          let diff = diffCalc.Diff(k, this.keys.[0])
          let idxL : int64 = (diff / step)
          let modIsOk = (diff - step * idxL) = 0L // gives 13% boost for add compared to diff % step
          let idx = int idxL 
          if modIsOk && idx > -1 && idx < this.size then
            // error for regular keys, this means we insert existing key
            let msg = "Existing key check must be done before insert. SortedMap code is wrong."
            Environment.FailFast(msg, new ApplicationException(msg))            
          else
            // insertting more than 1 away from end or before start, with a hole
            this.keys <- this.rkMaterialize() 
            couldHaveRegularKeys <- false
      else
        if index < this.size then
          Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
        else 
          rkLast <- k
        this.keys.[index] <- k
        
    // couldHaveRegularKeys could be set to false inside the previous block even if it was true before
    if not couldHaveRegularKeys then
      if index < this.size then
        Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
      this.keys.[index] <- k
      // do not check if could regularize back, it is very rare 
      // that an irregular becomes a regular one, and such check is always done on
      // bucket switch in SHM (TODO really? check) and before serialization
      // the 99% use case is when we load data from a sequential stream or deserialize a map with already regularized keys
    this.size <- this.size + 1


  member this.IsIndexed with get() = false

  member this.IsRegular 
    with get() = couldHaveRegularKeys
    and private set (v) = couldHaveRegularKeys <- v

  member this.RegularStep 
    with get() = try this.rkGetStep() with | _ -> 0L

  member this.Version 
    with get() = this.version
    and internal set v =
      this.version <- v // NB setter only for deserializer

  //#endregion


  //#region Public members



  member this.Capacity
    with get() = this.keys.Length
    and set(value) = this.SetCapacity(value)

  member this.Comparer with get() = comparer

  member this.Clear() =
    if couldHaveRegularKeys then
      Trace.Assert(this.keys.Length = 2)
      Array.Clear(this.keys, 0, 2)
    else
      Array.Clear(this.keys, 0, this.size)
    this.size <- 0

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
        member x.GetEnumerator() : IEnumerator<'K> = this.GetEnumerator()
          
      }

  member this.ContainsKey(key) = this.IndexOfKey(key) >= 0


  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member internal this.IndexOfKeyUnchecked(key:'K) : int =
    if couldHaveRegularKeys && this.size > 1 then this.rkIndexOfKey key
    else Array.BinarySearch(this.keys, 0, this.size, key, comparer)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.IndexOfKey(key:'K) : int =
    if this.isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then raise (ArgumentNullException("key"))
    this.IndexOfKeyUnchecked(key)
    
  member this.First
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>] get() =
        if this.size = 0 then raise (InvalidOperationException("Could not get the first element of an empty map"))
        this.keys.[0]
      
  member this.Last
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>] get() =
        if this.size = 0 then raise (InvalidOperationException("Could not get the last element of an empty map"))
        if couldHaveRegularKeys && this.size > 1 then
          Trace.Assert(comparer.Compare(rkLast, diffCalc.Add(this.keys.[0], (int64 (this.size-1))*this.rkGetStep())) = 0)
          rkLast
        else this.keys.[this.size - 1]
  
  member this.Item
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>] get index : 'K =
        this.GetKeyByIndex(index)
      and
        [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
        set index k =
          if uint32 index >= uint32 this.size then raise (ArgumentOutOfRangeException("index"))
          this.keys.[index] <- k
    
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.RemoveAt(index):unit =
    if uint32 index >= uint32 this.size then raise (ArgumentOutOfRangeException("index"))
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
      
    this.size <- newSize

//  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
//  member this.Remove(key): bool =
//    if this.isKeyReferenceType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then raise (ArgumentNullException("key"))
//    let index = this.IndexOfKeyUnchecked(key)
//    if index >= 0 then 
//      this.RemoveAt(index)
//    index >= 0
   

  /// Removes all elements that are to `direction` from `key`
  member this.RemoveMany(index:int,direction:Lookup) : bool =
    let mutable removed = false
    if this.size = 0 then false
    else
      match direction with
      | Lookup.EQ -> 
        if index >= 0 then 
          this.RemoveAt(index)
          removed <- true
          true
        else false
      | Lookup.LT | Lookup.LE ->
        if index = -1 then // pivot is not here but to the left, keep all elements
          false
        elif index >=0 then // remove elements below pivot and pivot
          this.size <- this.size - (index + 1)
            
          if couldHaveRegularKeys then
            this.keys.[0] <- (diffCalc.Add(this.keys.[0], int64 (index + 1)))
            if this.size > 1 then 
              this.keys.[1] <- (diffCalc.Add(this.keys.[0], this.rkGetStep())) 
            else
              this.keys.[1] <- Unchecked.defaultof<'K>
              rkStep_ <- 0L
          else
            Array.Copy(this.keys, index + 1, this.keys, 0, this.size) // move this.values to 

          removed <- true
          true
        else
          raise (ApplicationException("wrong result of TryFindWithIndex with LT/LE direction"))
      | Lookup.GT | Lookup.GE ->
        if index = -2 then // pivot is not here but to the right, keep all elements
          false
        elif index >= 0 then // remove elements above and including pivot
          this.size <- index
          if couldHaveRegularKeys then
            if this.size > 1 then
              rkLast <- diffCalc.Add(this.keys.[0], (int64 (this.size-1))*this.rkGetStep()) // -1 is correct, the size is updated on the previous line
            else
              this.keys.[1] <- Unchecked.defaultof<'K>
              rkStep_ <- 0L
              if this.size = 1 then rkLast <- this.keys.[0] 
              else rkLast <- Unchecked.defaultof<_>
          this.SetCapacity(this.size)
          removed <- true
          true
        else
          raise (ApplicationException("wrong result of TryFindWithIndex with GT/GE direction"))
      | _ -> failwith "wrong direction"

    
  /// Returns the index of found KeyValuePair or a negative value:
  /// -1 if the non-found key is smaller than the first key
  /// -2 if the non-found key is larger than the last key
  /// -3 if the non-found key is within the key range (for EQ direction only)
  /// -4 empty
  /// Example: (-1) [...current...(-3)...map ...] (-2)
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member internal this.TryFindWithIndex(key:'K, direction:Lookup, [<Out>]result: byref<'K>) : int =
    if this.size = 0 then -4
    else
      match direction with
      | Lookup.EQ ->
        let lastIdx = this.size-1
        if this.size > 0 && this.CompareToLast(key) = 0 then // key = last key
          result <- 
            if couldHaveRegularKeys && this.size > 1 then
              #if PRERELEASE
              Trace.Assert(comparer.Compare(rkLast, diffCalc.Add(this.keys.[0], (int64 (this.size-1))*this.rkGetStep())) = 0)
              #endif
              rkLast
            else this.keys.[this.size - 1]
          lastIdx
        else
          let index = this.IndexOfKeyUnchecked(key)
          if index >= 0 then
            result <-  this.GetKeyByIndexUnchecked(index)
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
            result <- this.GetKeyByIndexUnchecked(lastIdx - 1) // return item beforelast
            lastIdx - 1
          else -1
        elif lc > 0 then // key greater than the last
          result <- this.GetKeyByIndexUnchecked(lastIdx) // return the last item 
          lastIdx
        else
          let index = this.IndexOfKeyUnchecked(key)
          if index > 0 then
            result <- this.GetKeyByIndexUnchecked(index - 1)
            index - 1
          elif index = 0 then
              -1 // 
          else
            let index2 = ~~~index
            if index2 >= this.Count then // there are no elements larger than key
              result <- this.GetKeyByIndexUnchecked(this.Count - 1) // last element is the one that LT key
              this.Count - 1
            elif index2 = 0 then
              -1
            else //  it is the index of the first element that is larger than value
              result <- this.GetKeyByIndexUnchecked(index2 - 1)
              index2 - 1
      | Lookup.LE ->
        let lastIdx = this.size-1
        let lc = if this.size > 0 then this.CompareToLast(key) else -2
        if lc >= 0 then // key = last key or greater than the last key
          result <- this.GetKeyByIndexUnchecked(lastIdx)
          lastIdx
        else
          let index = this.IndexOfKeyUnchecked(key)
          if index >= 0 then
            result <- this.GetKeyByIndexUnchecked(index) // equal
            index
          else
            let index2 = ~~~index
            if index2 >= this.size then // there are no elements larger than key
              result <- this.GetKeyByIndexUnchecked(this.size - 1)
              this.size - 1
            elif index2 = 0 then
              -1
            else //  it is the index of the first element that is larger than value
              result <- this.GetKeyByIndexUnchecked(index2 - 1)
              index2 - 1
      | Lookup.GT ->
        let lc = if this.size > 0 then comparer.Compare(key, this.keys.[0]) else 2
        if lc = 0 then // key = first key
          if this.size > 1 then
            result <- this.GetKeyByIndexUnchecked(1) // return item after first
            1
          else -2 // cannot get greater than a single value when k equals to it
        elif lc < 0 then
          result <- this.GetKeyByIndexUnchecked(0) // return first
          0
        else
          let index = this.IndexOfKeyUnchecked(key)
          if index >= 0 && index < this.Count - 1 then
            result <- this.GetKeyByIndexUnchecked(index + 1)
            index + 1
          elif index >= this.Count - 1 then
            -2
          else
            let index2 = ~~~index
            if index2 >= this.Count then // there are no elements larger than key
              -2
            else //  it is the index of the first element that is larger than value
              result <- this.GetKeyByIndexUnchecked(index2)
              index2
      | Lookup.GE ->
        let lc = if this.size > 0 then comparer.Compare(key, this.keys.[0]) else 2
        if lc <= 0 then // key = first key or smaller than the first key
          result <- this.GetKeyByIndexUnchecked(0)
          0
        else
          let index = this.IndexOfKeyUnchecked(key)
          if index >= 0 && index < this.Count then
            result <- this.GetKeyByIndexUnchecked(index) // equal
            index
          else
            let index2 = ~~~index
            if index2 >= this.Count then // there are no elements larger than key
              -2
            else //  it is the index of the first element that is larger than value
              result <- this.GetKeyByIndexUnchecked(index2)
              index2
      | _ -> raise (ApplicationException("Wrong lookup direction"))



  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.TryGetFirst([<Out>] res: byref<'K>) = 
    try
      res <- this.First
      true
    with
    | _ -> 
      res <- Unchecked.defaultof<_>
      false
           
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>] 
  member this.TryGetLast([<Out>] res: byref<'K>) = 
    try
      res <- this.Last
      true
    with
    | _ -> 
      res <- Unchecked.defaultof<_>
      false


  member this.GetEnumerator() =
    let index = ref 0
    let eVersion = ref this.version
    let currentKey : 'K ref = ref Unchecked.defaultof<'K>
    { new IEnumerator<'K> with
      member e.Current with get() = currentKey.Value
      member e.Current with get() = box e.Current
      member e.MoveNext() =
        let nextIndex = index.Value + 1
        if eVersion.Value <> this.version then
          raise (InvalidOperationException("Collection changed during enumeration"))
        if index.Value < this.size then
          currentKey := 
            if couldHaveRegularKeys && this.size > 1 then diffCalc.Add(this.keys.[0], (int64 !index)*this.rkGetStep()) 
            else this.keys.[!index]
          index := nextIndex
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


  member private this.Dispose(disposing:bool) =
    if not couldHaveRegularKeys then BufferPool<_>.Return(this.keys, true) |> ignore
    if disposing then GC.SuppressFinalize(this)
  
  member this.Dispose() = this.Dispose(true)
  override this.Finalize() = this.Dispose(false)

  member internal this.SyncRoot 
    with get() = 
      if this.syncRoot = null then
        System.Threading.Interlocked.CompareExchange(ref this.syncRoot, new Object(), null) |> ignore
      this.syncRoot

  //#endregion


  //#region Interfaces


  interface IDisposable with
    member this.Dispose() = this.Dispose(true)

  interface IEnumerable with
    member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator

  interface IEnumerable<'K> with
    member this.GetEnumerator() : IEnumerator<'K> = this.GetEnumerator()

  interface ICollection  with
    member this.SyncRoot = this.SyncRoot
    member this.CopyTo(array, arrayIndex) = this.Keys.CopyTo(array :?> 'K[], arrayIndex)
    member this.Count = this.Count
    member this.IsSynchronized with get() = false

     
  //#endregion

  /// Checks if keys of two indexes are equal
  static member internal KeysAreEqual(smA:Index<'K>,smB:Index<'K>) : bool =
    if not (smA.size = smB.size) then false
    elif smA.IsRegular && smB.IsRegular 
      && smA.RegularStep = smB.RegularStep
      && smA.Comparer.Equals(smB.Comparer) // TODO test custom comparer equality, custom comparers must implement equality
      && smA.Comparer.Compare(smA.keys.[0], smB.keys.[0]) = 0 then // if steps are equal we could skip checking second elements in keys
      true
    else
      // this is very slow to be used in any "optimization", should use BytesExtensions.UnsafeCompare
      System.Linq.Enumerable.SequenceEqual(smA.keys, smB.keys)

