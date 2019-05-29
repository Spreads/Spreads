// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#nowarn "44"

namespace rec Spreads.Collections
// TODO [x] Simplify monster method TryFindWithIndex (done)
// TODO Test TryFindWithIndex
// TODO Finalize new interfaces, strides are needed in containers most of all
// TODO Reimplement (review + fix) Append
// TODO (low) Pooling and cursor counter to avoid disposing of a SM with outstanding cursors
// TODO Remove try..finally in simple methods such as Add/Set and use some circuit braker in enterLock with Env.FailFast. Methods should bever throw.

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
open Spreads
open Spreads.Buffers
open Spreads.DataTypes
open Spreads.Serialization
open Spreads.Collections
open Spreads.Utils
open Spreads
open Spreads.Collections.Concurrent
open System
open System.Net.Http.Headers

type KVP<'K,'V> = KeyValuePair<'K,'V>

// NB: Why regular keys? Because we do not care about daily or hourly data, but there are 1440 (480) minutes in a day (trading hours)
// with the same diff between each consequitive minute. The number is much bigger with seconds and msecs so that
// memory saving is meaningful, while vectorized calculations on values benefit from fast comparison of regular keys.
// Ticks (and seconds for illiquid instruments) are not regular, but they are never equal among different instruments.

// NB (update 2018) Regular keys are useful for DataStreams (WIP) with sequential ids, but real data is almost never regular. 
// Keep it at least for the sake of blood wasted on making it right.

/// Mutable sorted thread-safe IMutableSeries<'K,'V> implementation similar to SCG.SortedList<'K,'V>
[<AllowNullLiteral>]
[<Sealed>]
[<DebuggerTypeProxy(typeof<IDictionaryDebugView<_,_>>)>]
[<DebuggerDisplay("SortedMap: Count = {Count}")>]
type SortedMap<'K,'V>
  internal(dictionary:Opt<IDictionary<'K,'V>>, capacity:Opt<int>, comparerOpt:Opt<KeyComparer<'K>>) =
  inherit ContainerSeries<'K,'V, SortedMapCursor<'K,'V>>()

  static let smallArrayPool : ObjectPool<'K[]> = ObjectPool(fun _ -> Array.zeroCreate 2)
  static let empty = lazy (let sm = new SortedMap<'K,'V>() in sm.Complete().Wait();sm)

  let comparer : KeyComparer<'K> = 
    if comparerOpt.IsMissing || KeyComparer<'K>.Default.Equals(comparerOpt.Present) then
      KeyComparer<'K>.Default
    else 
      comparerOpt.Present // do not try to replace with KeyComparer if a comparer was given
  
  let mutable couldHaveRegularKeys : bool = comparer.IsDiffable

  // data fields
  [<DefaultValueAttribute>]
  val mutable internal size : int

  // A small hook to avoid outer orderVersion increment in SCM.
  // This is simpler than a special child class (also SM is sealed)
  // and incurres only a null check  
  [<DefaultValueAttribute>]
  val mutable internal keepOrderVersionDelegate : Func<'V,'V,bool>

  let initCapacity = 
    if capacity.IsPresent && capacity.Present > 16 then
      BitUtil.FindNextPositivePowerOfTwo(capacity.Present) 
    else 16

  let mutable keys : 'K array = 
      if couldHaveRegularKeys then 
        // regular keys are the first and the second value, their diff is the step
        // NB: Buffer pools could return a buffer greater than the requested length,
        // but for regular keys we always need a fixed-length array of size 2, so we allocate a new one.
        // TODO wrap the corefx buffer and for len = 2 use a special self-adjusting ObjectPool, because these 
        // arrays are not short-lived and could accumulate in gen 1+ easily.
        smallArrayPool.Allocate() // Array.zeroCreate 2
      else BufferPool<'K>.Rent(initCapacity) 

  let mutable values : 'V array = BufferPool<'V>.Rent(initCapacity)

  [<DefaultValueAttribute>] 
  val mutable internal orderVersion : int64

  [<DefaultValueAttribute>] 
  val mutable private rkStep_ : int64

  [<DefaultValueAttribute>] 
  val mutable private rkLast: 'K

  //[<DefaultValueAttribute>] 
  //val mutable internal isReadOnly : bool

  [<DefaultValueAttribute>] 
  val mutable private mapKey: string

  // TODO buffer management similar to OwnedArray
  [<DefaultValueAttribute>] 
  val mutable internal subscriberCount : int
  [<DefaultValueAttribute>] 
  val mutable internal isReadyToDispose : int

  //do
  //  if dictionary.IsPresent && dictionary.Present.Count > 0 then
  //    match dictionary.Present with
  //    // TODO SCM
  //    | :? SortedMap<'K,'V> as map ->
  //      if map.IsCompleted then Trace.TraceWarning("TODO: reuse arrays of immutable map")
  //      let mutable entered = false
  //      try
  //        entered <- enterWriteLockIf &map.Locker true
  //        couldHaveRegularKeys <- map.IsRegular
  //        this.SetCapacity(map.size)
  //        this.size <- map.size
  //        Array.Copy(map.keys, 0, keys, 0, map.size)
  //        Array.Copy(map.values, 0, values, 0, map.size)
  //      finally
  //        exitWriteLockIf &map.Locker entered
  //    | _ ->
  //      // TODO ICollection interface to IMutableSeries
  //      let locked, sr = match dictionary.Present with | :? ICollection as col -> col.IsSynchronized, col.SyncRoot | _ -> false, null
  //      let entered = enterLockIf sr locked
  //      try
  //        if capacity.IsPresent && capacity.Present < dictionary.Present.Count then 
  //          raise (ArgumentException("capacity is less then dictionary this.size"))
  //        else
  //          this.SetCapacity(dictionary.Present.Count)
        
  //        let tempKeys = BufferPool<_>.Rent(dictionary.Present.Keys.Count)
  //        dictionary.Present.Keys.CopyTo(tempKeys, 0)
  //        dictionary.Present.Values.CopyTo(values, 0)
  //        // NB IDictionary guarantees there is no duplicates
  //        Array.Sort(tempKeys, values, 0, dictionary.Present.Keys.Count, comparer)
  //        this.size <- dictionary.Present.Count

  //        if couldHaveRegularKeys && this.size > 1 then // if could be regular based on initial check of comparer type
  //          let isReg, step, regularKeys = this.rkCheckArray tempKeys this.size (comparer)
  //          couldHaveRegularKeys <- isReg
  //          if couldHaveRegularKeys then 
  //            keys <- regularKeys
  //            BufferPool<_>.Return(tempKeys, true) |> ignore
  //            this.rkLast <- this.rkKeyAtIndex (this.size - 1)
  //          else
  //            keys <- tempKeys
  //        else
  //          keys <- tempKeys
  //      finally
  //        exitLockIf sr entered
        

  
  //#region Private & Internal members

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline private this.rkGetStep() =
    #if DEBUG
    Trace.Assert(this.size > 1)
    #endif
    if this.rkStep_ > 0L then this.rkStep_
    elif this.size > 1 then
      this.rkStep_ <- comparer.Diff(keys.[1], keys.[0])
      this.rkStep_
    else
      ThrowHelper.ThrowInvalidOperationException("Cannot calculate regular keys step for a single element in a map or an empty map")
      0L
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.rkKeyAtIndex (idx:int) : 'K =
    let step = this.rkGetStep()
    comparer.Add(keys.[0], (int64 idx) * step)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline private this.rkIndexOfKey (key:'K) : int =
    #if DEBUG
    Trace.Assert(this.size > 1)
    #endif

    let diff = comparer.Diff(key, keys.[0])
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

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.rkMaterialize () =
    let step = this.rkGetStep()
    let arr = BufferPool<'K>.Rent(values.Length)
    for i = 0 to values.Length - 1 do
      arr.[i] <- comparer.Add(keys.[0], (int64 i)*step)
    arr

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
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
  member inline private this.SetRkLast(rkl) = this.rkLast <- rkl
  
  member private this.Clone() = new SortedMap<'K,'V>(Opt.Present(this :> IDictionary<'K,'V>), Opt<_>.Missing, Opt.Present(comparer))

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline internal this.CheckNull(key) =
    if not TypeHelper<'K>.IsValueType && EqualityComparer<'K>.Default.Equals(key, Unchecked.defaultof<'K>) then 
      ThrowHelper.ThrowArgumentNullException("key")
    
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline internal this.GetKeyByIndexUnchecked(index) =
    if couldHaveRegularKeys && this.size > 1 then this.rkKeyAtIndex index
    else keys.[index]

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline internal this.GetKeyByIndex(index) =
    if uint32 index >= uint32 this.size then raise (ArgumentOutOfRangeException("index"))
    this.GetKeyByIndexUnchecked(index)
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline internal this.GetPairByIndexUnchecked(index) =
    if couldHaveRegularKeys && this.size > 1 then
      Debug.Assert(uint32 index < uint32 this.size, "Index must be checked before calling GetPairByIndexUnchecked")
      KeyValuePair(this.rkKeyAtIndex index, values.[index])
    else KeyValuePair(keys.[index], values.[index]) 
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline private this.CompareToFirst (k:'K) =
    Debug.Assert(this.size > 0)
    comparer.Compare(k, keys.[0]) // keys.[0] is always the first key even for regular keys
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline internal this.CompareToLast (k:'K) =
    if couldHaveRegularKeys && this.size > 1 then
      Debug.Assert(not <| Unchecked.equals this.rkLast Unchecked.defaultof<'K>)
      comparer.Compare(k, this.rkLast)
    else
      Debug.Assert(this.size > 0)
      comparer.Compare(k, keys.[this.size-1])

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.EnsureCapacity(min) = 
    let mutable num = values.Length * 2 
    if num > 2146435071 then num <- 2146435071
    if num < min then num <- min // either double or min if min > 2xprevious
    this.SetCapacity(num)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  //[<ReliabilityContractAttribute(Consistency.MayCorruptInstance, Cer.MayFail)>]
  member private this.Insert(index:int, k, v) =
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
    // key is always new, checks are before this method
    Debug.Assert(index >= 0)
    // already inside a lock statement in a caller method if synchronized
   
    if this.size = values.Length || this.size = keys.Length then 
      this.EnsureCapacity(this.size + 1)
    Debug.Assert(index <= this.size, "index must be <= this.size")
    
    // for values it is alway the same operation
    if index < this.size then Array.Copy(values, index, values, index + 1, this.size - index);
    values.[index] <- v
    // for regular keys must do some math to check if they will remain regular after the insertion
    // treat sizes 1,2(after insertion) as non-regular because they are always both regular and not 
    if couldHaveRegularKeys then
      if this.size > 1 then
        let step = this.rkGetStep()
        if comparer.Compare(comparer.Add(this.rkLast, step), k) = 0 then
          // adding next regular, only rkLast changes
          this.rkLast <- k
        elif comparer.Compare(comparer.Add(keys.[0], -step), k) = 0 then
          keys.[1] <- keys.[0]
          keys.[0] <- k // change first key and size++ at the bottom
          //rkLast is unchanged
        else
          let diff = comparer.Diff(k, keys.[0])
          let idxL : int64 = (diff / step)
          let modIsOk = (diff - step * idxL) = 0L // gives 13% boost for add compared to diff % step
          let idx = int idxL 
          if modIsOk && idx > -1 && idx < this.size then
            // error for regular keys, this means we insert existing key
            let msg = "Existing key check must be done before insert. SortedMap code is wrong."
            ThrowHelper.FailFast(msg)
          else
            // insertting more than 1 away from end or before start, with a hole
            let toFree = keys
            keys <- this.rkMaterialize()
            smallArrayPool.Free(toFree)
            couldHaveRegularKeys <- false
      else
        if index < this.size then
          Array.Copy(keys, index, keys, index + 1, this.size - index);
        else 
          this.rkLast <- k
        keys.[index] <- k
        
    // couldHaveRegularKeys could be set to false inside the previous block even if it was true before
    if not couldHaveRegularKeys then
      if index < this.size then
        Array.Copy(keys, index, keys, index + 1, this.size - index);
      keys.[index] <- k
      // do not check if could regularize back, it is very rare 
      // that an irregular becomes a regular one, and such check is always done on
      // bucket switch in SHM (TODO really? check) and before serialization
      // the 99% use case is when we load data from a sequential stream or deserialize a map with already regularized keys
    this.size <- this.size + 1

  member this.Complete() : Task = this.DoComplete()
  
  override this.IsIndexed with get() = false

  member this.IsSynchronized with get() = this._isSynchronized
    

  member internal this.MapKey with get() = this.mapKey and set(key:string) = this.mapKey <- key

  member this.IsRegular
    with get() = readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ -> couldHaveRegularKeys) 
    and private set (v) = couldHaveRegularKeys <- v

  member this.RegularStep 
    with get() = 
      readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ -> try this.rkGetStep() with | _ -> 0L)

  member this.Version 
    with get() = readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ -> this._version)
    and internal set v = 
      this.BeforeWrite()
      this._version <- v // NB setter only for deserializer
      this._nextVersion <- v
      this.AfterWrite(false)

  //#endregion

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.SetCapacity(value) =
    match value with
    | c when c = values.Length && c = keys.Length -> ()
    | c when c < this.size -> ThrowHelper.ThrowArgumentOutOfRangeException("capacity")
    | c when c > 0 -> 
      if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
      
      try
        // first, take new buffers. this could cause out-of-memory
        let kArr : 'K array = 
          if c > keys.Length then
            if couldHaveRegularKeys then
              // Trace.Assert(keys.Length = 2)
              Unchecked.defaultof<_>
            else
              BufferPool<_>.Rent(c)
           else keys
        let vArr : 'V array = 
          if c > values.Length then
            BufferPool<_>.Rent(c)
          else values
      
        if not couldHaveRegularKeys && not (obj.ReferenceEquals(kArr, keys)) then
          Array.Copy(keys, 0, kArr, 0, this.size)
          let toReturn = keys
          keys <- kArr
          BufferPool<_>.Return(toReturn, true) |> ignore

        if not (obj.ReferenceEquals(vArr, values)) then
          Array.Copy(values, 0, vArr, 0, this.size)
          let toReturn = values
          values <- vArr
          BufferPool<_>.Return(toReturn, true) |> ignore
      with
      // NB see enterWriteLockIf comment and https://github.com/dotnet/corefx/issues/1345#issuecomment-147569967
      // If we were able to get new arrays without OOM but got some out-of-band exception during
      // copying, then we corrupted state and should die
      | _ as ex -> Environment.FailFast(ex.Message, ex)
    | _ -> ()

  //#region Public members

  member this.Capacity
    with get() = 
      readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ ->
        values.Length
      )
    and set(value) =
      let mutable entered = false
      this.BeforeWrite()
      this.SetCapacity(value)
      this.AfterWrite(false)

  override __.Comparer with get() = comparer

  member inline internal this.ComparerInlined with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = comparer

  member inline internal this.KeysArr 
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = keys
    and [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] set arr = keys <- arr
  
  member inline internal this.ValuesArr 
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = values
    and [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] set arr = values <- arr

  member this.Clear() =
    this.BeforeWrite();
    try
      if couldHaveRegularKeys then
        // Trace.Assert(keys.Length = 2)
        Array.Clear(keys, 0, 2)
      else
        Array.Clear(keys, 0, this.size)
      Array.Clear(values, 0, this.size)
      this.size <- 0
      increment &this.orderVersion
    finally
      this.AfterWrite(true);

  member this.Count
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = this.size

  override this.Keys 
    with get() =
      { new IEnumerable<'K> with
          member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
          member x.GetEnumerator() : IEnumerator<'K> =
            let mutable c : SortedMapCursor<'K,'V> = this.GetContainerCursor();
            { new IEnumerator<'K> with
                member __.Current with get() = c.CurrentKey
                member __.Current with get() : obj = c.CurrentKey :> obj
                member __.MoveNext() = c.MoveNext()
                member __.Reset() = c.Reset()
                member __.Dispose() = c.Dispose()
            }
      }

  override this.Values
    with get() =
      { new IEnumerable<'V> with
          member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
          member x.GetEnumerator() : IEnumerator<'V> =
            let mutable c : SortedMapCursor<'K,'V> = this.GetContainerCursor();
            { new IEnumerator<'V> with
                member __.Current with get() = c.CurrentValue
                member __.Current with get() : obj = c.CurrentValue :> obj
                member __.MoveNext() = c.MoveNext()
                member __.Reset() = c.Reset()
                member __.Dispose() = c.Dispose()
            }
      }

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.ContainsKey(key) = this.IndexOfKey(key) >= 0

  member this.ContainsValue(value) = this.IndexOfValue(value) >= 0

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline internal this.IndexOfKeyUnchecked(key:'K) : int =
    if couldHaveRegularKeys && this.size > 1 then this.rkIndexOfKey key
    else comparer.BinarySearch(keys, 0, this.size, key)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.IndexOfKey(key:'K) : int =
    this.CheckNull(key)
    readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ ->
      this.IndexOfKeyUnchecked(key)
    )

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.IndexOfValue(value:'V) : int =
    readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ ->
      let mutable res = 0
      let mutable found = false
      let valueComparer = Comparer<'V>.Default;
      while not found do
          if valueComparer.Compare(value,values.[res]) = 0 then
              found <- true
          else res <- res + 1
      if found then res else -1
    )


  override this.First
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() =
      readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ ->
        this.FirstUnchecked
      )

  member private this.FirstUnchecked
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() =
      if this.size > 0 then 
        Opt.Present(KeyValuePair(keys.[0], values.[0]))
      else Opt.Missing
      
  override this.Last
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() =
      readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ ->
        this.LastUnchecked
      )

  member internal this.LastUnchecked
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() =
      if couldHaveRegularKeys && this.size > 1 then
        Trace.Assert(comparer.Compare(this.rkLast, comparer.Add(keys.[0], (int64 (this.size-1))*this.rkGetStep())) = 0)
        Opt.Present(KeyValuePair(this.rkLast, values.[this.size - 1]))
      elif this.size > 0 then Opt.Present(KeyValuePair(keys.[this.size - 1], values.[this.size - 1]))
      else Opt.Missing

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member internal this.TryGetValueUnchecked(key, [<Out>]value: byref<'V>) : bool =
    let index = this.IndexOfKeyUnchecked(key)
    if index >= 0 then
      value <- values.[index]
      true
    else false
    
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  override this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
    this.CheckNull(key)
    let mutable value' = Unchecked.defaultof<_>
    let inline res() = this.TryGetValueUnchecked(key, &value')
    if readLockIf &this._nextVersion &this._version this._isSynchronized res then
      value <- value'; true
    else false
      
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member internal this.SetOrAddUnchecked(k, v, overwrite:bool) : ValueTuple<bool, bool> =
    #if DEBUG
    try
    #endif
      let mutable keepOrderVersion = false
      let mutable added = false
    
      if this.size = 0 then
        this.Insert(0, k, v)
        keepOrderVersion <- true
        added <- true
      else
        if this.CompareToLast(k) > 0 then // adding last value, Insert won't copy arrays if enough capacity
          this.Insert(this.size, k, v)
          keepOrderVersion <- true
          added <- true
        else
          let index = this.IndexOfKeyUnchecked(k)
          if index >= 0 then // contains key
            Debug.Assert(index < this.size)
            if overwrite then
              if this.keepOrderVersionDelegate <> null then 
                let previous = values.[index]
                keepOrderVersion <- this.keepOrderVersionDelegate.Invoke(previous, v)
              values.[index] <- v
          else
            this.Insert(~~~index, k, v)
            added <- true
      struct(added, keepOrderVersion)
    #if DEBUG
    with | _ -> ThrowHelper.FailFast("SM.SetOrAddUnchecked should never throw"); Unchecked.defaultof<_>
    #endif


  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member internal this.SetOrAdd(key, value, overwrite: bool) : Task<bool> =
    this.CheckNull(key)
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
    
    this.BeforeWrite()

    let struct(added, keepOrderVersion) = 
      this.SetOrAddUnchecked(key, value, overwrite)
    if not keepOrderVersion then increment(&this.orderVersion)
    this.AfterWrite(overwrite || added)

    #if DEBUG
    if this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
    #endif
    if added then TaskUtil.TrueTask else TaskUtil.FalseTask

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.Set(key, value) : Task<bool> = this.SetOrAdd(key, value, true)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.TryAdd(key, value) : Task<bool> = this.SetOrAdd(key, value, false)

  // NB this is for ctor pattern with IEnumerable
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.Add(key, value) : unit =
    let added = this.TryAdd(key, value).Result
    if not added then ThrowHelper.ThrowArgumentException("Key already exists");
      
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.TryAddLast(key, value) : Task<bool> =
    this.CheckNull(key)
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
        
    this.BeforeWrite()

    let mutable added = false
    
    if this.size = 0 then
      this.Insert(0, key, value)
      added <- true
    else
      let c = this.CompareToLast key
      if c > 0 then 
        this.Insert(this.size, key, value)
        added <- true
      
    this.AfterWrite(added)

    #if DEBUG
    if this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
    #endif

    if added then TaskUtil.TrueTask else TaskUtil.FalseTask

//  // TODO lockless AddLast for temporary Append implementation
//  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
//  member private this.AddLastUnchecked(key, value) : unit =
//      if this.size = 0 then
//        this.Insert(0, key, value)
//      else
//        let c = this.CompareToLast key
//        if c > 0 then 
//          this.Insert(this.size, key, value)
//        else
//          ThrowHelper.ThrowOutOfOrderKeyException(this.LastUnchecked.Present.Key, "SortedMap.AddLast: New key is smaller or equal to the largest existing key")
//
//
//  member this.TryAppend(appendMap:ISeries<'K,'V>, [<Out>] result: byref<int64>, option:AppendOption) =
//      let hasEqOverlap (old:ISeries<'K,'V>) (append:ISeries<'K,'V>) : bool =
//        if comparer.Compare(append.First.Present.Key, old.Last.Present.Key) > 0 then false
//        else
//          let oldC = old.GetCursor()
//          let appC = append.GetCursor();
//          let mutable cont = true
//          let mutable overlapOk = 
//            oldC.MoveAt(append.First.Present.Key, Lookup.EQ) 
//              && appC.MoveFirst() 
//              && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
//              && Unchecked.equals oldC.CurrentValue appC.CurrentValue
//          while overlapOk && cont do
//            if oldC.MoveNext() then
//              overlapOk <-
//                appC.MoveNext() 
//                && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
//                && Unchecked.equals oldC.CurrentValue appC.CurrentValue
//            else cont <- false
//          overlapOk
//      if appendMap.First.IsMissing then
//        0
//      else
//        let mutable entered = false
//        try
//          entered <- enterWriteLockIf &this.Locker this._isSynchronized
//          match option with
//          | AppendOption.RejectOnOverlap ->
//            if this.size = 0 || comparer.Compare(appendMap.First.Present.Key, this.LastUnchecked.Present.Key) > 0 then
//              let mutable c = 0
//              for i in appendMap do
//                c <- c + 1
//                this.AddLastUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
//              c
//            else invalidOp "values overlap with existing"
//          | AppendOption.DropOldOverlap ->
//            if this.size = 0 || comparer.Compare(appendMap.First.Present.Key, this.LastUnchecked.Present.Key) > 0 then
//              let mutable c = 0
//              for i in appendMap do
//                c <- c + 1
//                this.AddLastUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
//              c
//            else
//              let removed = this.RemoveMany(appendMap.First.Key, Lookup.GE)
//              Trace.Assert(removed)
//              let mutable c = 0
//              for i in appendMap do
//                c <- c + 1
//                this.AddLastUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
//              c
//          | AppendOption.IgnoreEqualOverlap ->
//            if this.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnchecked.Key) > 0 then
//              let mutable c = 0
//              for i in appendMap do
//                c <- c + 1
//                this.AddLastUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
//              c
//            else
//              let isEqOverlap = hasEqOverlap this appendMap
//              if isEqOverlap then
//                let appC = appendMap.GetCursor();
//                if appC.MoveAt(this.LastUnchecked.Key, Lookup.GT) then
//                  this.AddLastUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
//                  let mutable c = 1
//                  while appC.MoveNext() do
//                    this.AddLastUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
//                    c <- c + 1
//                  c
//                else 0
//              else invalidOp "overlapping values are not equal" // TODO unit test
//          | AppendOption.RequireEqualOverlap ->
//            if this.IsEmpty then
//              let mutable c = 0
//              for i in appendMap do
//                c <- c + 1
//                this.AddLastUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
//              c
//            elif comparer.Compare(appendMap.First.Key, this.LastUnchecked.Key) > 0 then
//              invalidOp "values do not overlap with existing"
//            else
//              let isEqOverlap = hasEqOverlap this appendMap
//              if isEqOverlap then
//                let appC = appendMap.GetCursor();
//                if appC.MoveAt(this.LastUnchecked.Key, Lookup.GT) then
//                  this.AddLastUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
//                  let mutable c = 1
//                  while appC.MoveNext() do
//                    this.AddLastUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
//                    c <- c + 1
//                  c
//                else 0
//              else invalidOp "overlapping values are not equal" // TODO unit test
//          | _ -> failwith "Unknown AppendOption"
//        finally
//          exitWriteLockIf &this.Locker entered

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.TryAddFirst(key, value) : Task<bool> =
    this.CheckNull(key)
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
      
    this.BeforeWrite()

    let mutable added = false
    let mutable keepOrderVersion = false
    
    try
      if this.size = 0 then
        this.Insert(0, key, value)
        keepOrderVersion <- true
        added <- true
      else
        let c = this.CompareToFirst key
        if c < 0 then
            this.Insert(0, key, value)
            added <- true
      if added then TaskUtil.TrueTask else TaskUtil.FalseTask   
    finally
      if not keepOrderVersion then increment(&this.orderVersion)
      this.AfterWrite(added)
      
      #if DEBUG
      if this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
      #endif
    
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.RemoveAt(index)  =
    let result = this.GetPairByIndexUnchecked(index)
    if uint32 index >= uint32 this.size then ThrowHelper.ThrowArgumentOutOfRangeException("index")
    let newSize = this.size - 1
    // TODO review, check for off by 1 bugs, could had lost focus at 3 AM
    // keys
    if couldHaveRegularKeys && this.size > 2 then // will have >= 2 after removal
      if index = 0 then
        keys.[0] <- (comparer.Add(keys.[0], this.rkGetStep())) // change first key to next and size--
        keys.[1] <- (comparer.Add(keys.[0], this.rkGetStep())) // add step to the new first value
      elif index = newSize then 
        this.rkLast <- comparer.Add(keys.[0], (int64 (newSize-1))*this.rkGetStep()) // removing last, only size--
      else
        // removing within range,  creating a hole
        keys <- this.rkMaterialize()
        couldHaveRegularKeys <- false
    elif couldHaveRegularKeys && this.size = 2 then // will have single value with undefined step
      if index = 0 then
        keys.[0] <- keys.[1]
        keys.[1] <- Unchecked.defaultof<'K>
      elif index = 1 then
        this.rkLast <- keys.[0]
      this.rkStep_ <- 0L

    if not couldHaveRegularKeys || this.size = 1 then
      if index < this.size then
        Array.Copy(keys, index + 1, keys, index, newSize - index) // this.size
      keys.[newSize] <- Unchecked.defaultof<'K>
      
    // values
    if index < newSize then
      Array.Copy(values, index + 1, values, index, newSize - index) //this.size
      
    values.[newSize] <- Unchecked.defaultof<'V>
    this.size <- newSize
    result
    
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.TryRemove(key) : ValueTask<Opt<'V>>  =
    this.CheckNull(key)
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
    
    let mutable removed = false
    this.BeforeWrite()
    
    try
      let index = this.IndexOfKeyUnchecked(key)
      if index >= 0 then 
        let result = this.RemoveAt(index).Value
        increment &this.orderVersion
        removed <- true
        ValueTask<_>(Opt.Present(result))
      else ValueTask<_>(Opt<_>.Missing)
    finally
      this.AfterWrite(removed)
      
      #if DEBUG
      if this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
      #endif


  member this.TryRemoveFirst() : ValueTask<Opt<KVP<'K,'V>>>  =
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
    let mutable removed = false
    this.BeforeWrite()
    try
      if this.size > 0 then
        let result = this.RemoveAt(0)
        increment &this.orderVersion
        removed <- true
        ValueTask<_>(Opt.Present(result))
      else ValueTask<_>(Opt<_>.Missing)
    finally
      this.AfterWrite(removed)
      #if DEBUG
      if this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
      #endif


  member this.TryRemoveLast() : ValueTask<Opt<KVP<'K,'V>>> =
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")
    let mutable removed = false
    this.BeforeWrite()
    try
      if this.size > 0 then
        let result = this.RemoveAt(this.size - 1)
        increment &this.orderVersion
        removed <- true
        ValueTask<_>(Opt.Present(result))
      else ValueTask<_>(Opt<_>.Missing)
    finally
      this.AfterWrite(removed)
      #if DEBUG
      if this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
      #endif

  /// Removes all elements that are to `direction` from `key`
  member this.TryRemoveMany(key:'K, direction:Lookup) : ValueTask<Opt<KVP<'K,'V>>> =
    this.CheckNull(key)
    if this._isReadOnly then ThrowHelper.ThrowInvalidOperationException("SortedMap is read-only")

    let mutable removed = false
    this.BeforeWrite()
    try
      if this.size = 0 then ValueTask<_>(Opt<_>.Missing)
      else
        let mutable result = Unchecked.defaultof<_>
        let pivotIndex = this.FindIndexAt(key, direction)
        let mutable result =
          if pivotIndex >= 0 && pivotIndex < this.size then
            this.GetPairByIndexUnchecked(pivotIndex)
          else Unchecked.defaultof<_>
        // pivot should be removed, after calling TFWI pivot is always inclusive
        match direction with
        | Lookup.EQ -> 
          let index = this.IndexOfKeyUnchecked(key)
          if index >= 0 then 
            result <- this.RemoveAt(index)
            increment &this.orderVersion
            removed <- true
            ValueTask<_>(Opt.Present(result))
          else ValueTask<_>(Opt<_>.Missing)
        | Lookup.LT | Lookup.LE ->
          if pivotIndex = -1 then // pivot is not here but to the left, keep all elements
            ValueTask<_>(Opt<_>.Missing)
          elif pivotIndex >=0 then // remove elements below pivot and pivot
            this.size <- this.size - (pivotIndex + 1)
            
            if couldHaveRegularKeys then
              keys.[0] <- (comparer.Add(keys.[0], int64 (pivotIndex+1)))
              if this.size > 1 then 
                keys.[1] <- (comparer.Add(keys.[0], this.rkGetStep())) 
              else
                keys.[1] <- Unchecked.defaultof<'K>
                this.rkStep_ <- 0L
            else
              Array.Copy(keys, pivotIndex + 1, keys, 0, this.size) // move values to 
              Array.fill keys this.size (values.Length - this.size) Unchecked.defaultof<'K>

            Array.Copy(values, pivotIndex + 1, values, 0, this.size)
            Array.fill values this.size (values.Length - this.size) Unchecked.defaultof<'V>
            
            increment &this.orderVersion
            removed <- true
            ValueTask<_>(Opt.Present(result))
          else
            ThrowHelper.ThrowInvalidOperationException("wrong result of TryFindWithIndex with LT/LE direction");Unchecked.defaultof<_>
        | Lookup.GT | Lookup.GE ->
          if pivotIndex = -2 then // pivot is not here but to the right, keep all elements
            ValueTask<_>(Opt<_>.Missing)
          elif pivotIndex >= 0 then // remove elements above and including pivot
            this.size <- pivotIndex
            if couldHaveRegularKeys then
              if this.size > 1 then
                this.rkLast <- comparer.Add(keys.[0], (int64 (this.size-1))*this.rkGetStep()) // -1 is correct, the size is updated on the previous line
              else
                keys.[1] <- Unchecked.defaultof<'K>
                this.rkStep_ <- 0L
                if this.size = 1 then this.rkLast <- keys.[0] 
                else this.rkLast <- Unchecked.defaultof<_>
            if not couldHaveRegularKeys then
              Array.fill keys pivotIndex (values.Length - pivotIndex) Unchecked.defaultof<'K>
            Array.fill values pivotIndex (values.Length - pivotIndex) Unchecked.defaultof<'V>
            this.SetCapacity(this.size)
            increment &this.orderVersion
            removed <- true
            ValueTask<_>(Opt.Present(result))
          else
            ThrowHelper.ThrowInvalidOperationException("Wrong result of TryFindWithIndex with GT/GE direction"); Unchecked.defaultof<_>
        | _ -> ThrowHelper.ThrowInvalidOperationException("Invalid direction"); Unchecked.defaultof<_> //
    finally
      this.AfterWrite(removed)
      #if DEBUG
      if this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
      #endif
    
  /// Returns the index of found KeyValuePair or a negative value:
  /// -1 if the non-found key is smaller than the first key
  /// -2 if the non-found key is larger than the last key
  /// -3 if the non-found key is within the key range (for EQ direction only)
  /// -4 empty
  /// Example: (-1) [...current...(-3)...map ...] (-2)
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member internal this.FindIndexAt(key:'K, direction:Lookup) : int32 =
    if this.size = 0 then -4
    // the only optimization: return last item if requesting LE with key above the last one
    elif direction = Lookup.LE && this.CompareToLast(key) >= 0 then 
      this.size - 1
    else
      let mutable idx = this.IndexOfKeyUnchecked(key);
      // adjust idx
      if idx >= 0 then
        if direction = Lookup.LT then 
          idx <- idx - 1
        elif direction = Lookup.GT then
          idx <- idx + 1
        else () // for LE/EQ/GE we are fine with existing key
      else
        idx <- ~~~idx
        if direction = Lookup.EQ then
          idx <- -3
        elif direction = Lookup.LE || direction = Lookup.LT then
          idx <- idx - 1
        else () // for GE/GT ~~~ is already right
      if idx >= this.size then idx <- -2
      idx


  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.TryFindAtUnchecked(key:'K, direction:Lookup, [<Out>]value: byref<KeyValuePair<'K, 'V>>) : bool =
    let idx = this.FindIndexAt(key, direction)
    if idx >= 0 && idx < this.size then
      value <- this.GetPairByIndexUnchecked(idx)
      true
    else false

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  override this.TryFindAt(key:'K, direction:Lookup, [<Out>]value: byref<KeyValuePair<'K, 'V>>) : bool =
    // NB *very* hot path, manual inlining of read lock logic
    let mutable result = Unchecked.defaultof<_>
    let mutable doSpin = true
    let sw = new SpinWait()
    while doSpin do
      doSpin <- this._isSynchronized
      let version = if doSpin then Volatile.Read(&this._version) else 0L
      result <- 
        let idx = this.FindIndexAt(key, direction)
        if idx >= 0 && idx < this.size then
          value <- this.GetPairByIndexUnchecked(idx)
          true
        else false
      if doSpin then
        let nextVersion = Volatile.Read(&this._nextVersion)
        if version = nextVersion then doSpin <- false
        else sw.SpinOnce()
      else doSpin <- false
    result

  override this.GetContainerCursor() = this.GetEnumerator()

  // .NETs foreach pattern optimization must return struct
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.GetEnumerator() : SortedMapCursor<_,_> =
    readLockIf &this._nextVersion &this._version (not this._isReadOnly) (fun _ ->
          new SortedMapCursor<'K,'V>(this)
    )

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  override this.TryGetAt(idx:int64, [<Out>]value: byref<KeyValuePair<'K, 'V>>) : bool =
    let mutable kvp: KeyValuePair<'K, 'V> = Unchecked.defaultof<_>
    let result = readLockIf &this._nextVersion &this._version this._isSynchronized (fun _ ->
      if idx >= 0L && idx < int64(this.size) then 
       kvp <- KVP( this.GetKeyByIndexUnchecked(int32(idx)), values.[int32(idx)])
       true 
      else false
    )
    value <- kvp
    result

  /// Make the capacity equal to the size
  member this.TrimExcess() = this.Capacity <- this.size

  override this.Dispose(disposing:bool) =
    if BufferPoolRetainedMemoryHelper<'V>.IsRetainedMemory then BufferPoolRetainedMemoryHelper<'V>.DisposeRetainedMemory(values, 0, this.size)
    
    // TODO control that we do not try to return wrong buffers
    try
      if not couldHaveRegularKeys then BufferPool<_>.Return(keys, true) |> ignore
      BufferPool<_>.Return(values, true) |> ignore
    with | _ -> ()
    if disposing then GC.SuppressFinalize(this)
    base.Dispose(disposing)
  
  member this.Dispose() = this.Dispose(true)
  
  override this.Finalize() = this.Dispose(false)

  //#endregion


  //#region Interfaces

  interface IDisposable with
    member this.Dispose() = this.Dispose(true)

  interface IEnumerable with
    member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator

  interface IEnumerable<KeyValuePair<'K,'V>> with
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
      this.GetEnumerator() :> IEnumerator<KeyValuePair<'K,'V>>


  interface IDictionary<'K,'V> with
    member this.Count = this.Count
    member this.IsReadOnly with get() = this.IsCompleted
    member this.Item 
      with get key = this.Item(key)
      and set key value = this.Set(key, value) |> ignore
    member this.Keys with get() = this.Keys :?> ICollection<'K>
    member this.Values with get() = this.Values :?> ICollection<'V>
    member this.Clear() = this.Clear()
    member this.ContainsKey(key) = this.ContainsKey(key)
    member this.Contains(kvp:KeyValuePair<'K,'V>) = this.ContainsKey(kvp.Key)
    member this.CopyTo(array, arrayIndex) =
      if array = null then raise (ArgumentNullException("array"))
      if arrayIndex < 0 || arrayIndex > array.Length then raise (ArgumentOutOfRangeException("arrayIndex"))
      if array.Length - arrayIndex < this.Count then raise (ArgumentException("ArrayPlusOffTooSmall"))
      let count = this.Count
      let ov = this.orderVersion
      for index in 0..count do
        if ov <> this.orderVersion then ThrowHelper.ThrowInvalidOperationException("Collection changed during enumeration")
        let kvp = KeyValuePair(keys.[index], values.[index])
        array.[arrayIndex + index] <- kvp
      
    member this.Add(key, value) = 
      if not (this.TryAdd(key, value).Result) then ThrowHelper.ThrowInvalidOperationException("Key already exists") 
    member this.Add(kvp:KeyValuePair<'K,'V>) = 
      if not (this.TryAdd(kvp.Key, kvp.Value).Result) then ThrowHelper.ThrowInvalidOperationException("Key already exists") 
    member this.Remove(key) = let opt = this.TryRemove(key).Result in opt.IsPresent
    member this.Remove(kvp:KeyValuePair<'K,'V>) = let opt = this.TryRemove(kvp.Key).Result in opt.IsPresent
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.TryGetValue(key, &value)
    
  interface IMutableSeries<'K,'V> with
    member this.Complete() = this.Complete()
    member this.Version with get() = this.Version
    member this.Count with get() = int64(this.size)
    member this.IsAppendOnly with get() = false
    member this.Set(k, v) = this.Set(k,v)
    member this.TryAdd(k, v) = this.TryAdd(k,v)
    member this.TryAddLast(k, v) = this.TryAddLast(k, v)
    member this.TryAddFirst(k, v) = this.TryAddFirst(k, v)
    member this.TryRemove(k) = this.TryRemove(k)
    member this.TryRemoveFirst() = this.TryRemoveFirst()
    member this.TryRemoveLast() = this.TryRemoveLast()
    member this.TryRemoveMany(key:'K, direction:Lookup) = this.TryRemoveMany(key, direction)
    member this.TryRemoveMany(key, keyChunk, direction) =
      match direction with
      | Lookup.EQ -> 
        this.Set(key, keyChunk) |> ignore
        TaskUtil.TrueTask
      | Lookup.LT | Lookup.LE -> 
        let r = this.TryRemoveMany(key, Lookup.LT).Result
        this.Set(key, keyChunk) |> ignore
        if r.IsPresent then TaskUtil.TrueTask else TaskUtil.FalseTask
      | Lookup.GT | Lookup.GE -> 
        let r = this.TryRemoveMany(key, Lookup.GT).Result
        this.Set(key, keyChunk) |> ignore
        if r.IsPresent then TaskUtil.TrueTask else TaskUtil.FalseTask
      | _ -> Unchecked.defaultof<_>

    // TODO move to type memeber, check if ISeries is SM and copy arrays in one go
    // TODO atomic append with single version increase, now it is a sequence of remove/add mutations
    member this.TryAppend(appendMap:ISeries<'K,'V>, option:AppendOption) =
      raise (NotImplementedException())

  //#endregion

  //#region Constructors


  new() = new SortedMap<_,_>(Opt<_>.Missing, Opt<_>.Missing, Opt<_>.Missing)
  new(dictionary:IDictionary<'K,'V>) = new SortedMap<_,_>(Opt.Present(dictionary), Opt.Present(dictionary.Count), Opt<_>.Missing)
  new(minimumCapacity:int) = new SortedMap<_,_>(Opt<_>.Missing, Opt.Present(minimumCapacity), Opt<_>.Missing)

  // do not expose ctors with comparer to public
  internal new(comparer:IComparer<'K>) = new SortedMap<_,_>(Opt<_>.Missing, Opt<_>.Missing, Opt.Present(KeyComparer<'K>.Create(comparer)))
  internal new(dictionary:IDictionary<'K,'V>,comparer:IComparer<'K>) = new SortedMap<_,_>(Opt.Present(dictionary), Opt.Present(dictionary.Count), Opt.Present(KeyComparer<'K>.Create(comparer)))
  internal new(minimumCapacity:int,comparer:IComparer<'K>) = new SortedMap<_,_>(Opt<_>.Missing, Opt.Present(minimumCapacity), Opt.Present(KeyComparer<'K>.Create(comparer)))

  internal new(comparer:KeyComparer<'K>) = new SortedMap<_,_>(Opt<_>.Missing, Opt<_>.Missing, Opt.Present(comparer))
  internal new(dictionary:IDictionary<'K,'V>,comparer:KeyComparer<'K>) = new SortedMap<_,_>(Opt.Present(dictionary), Opt.Present(dictionary.Count), Opt.Present(comparer))
  internal new(minimumCapacity:int,comparer:KeyComparer<'K>) = new SortedMap<_,_>(Opt<_>.Missing, Opt.Present(minimumCapacity), Opt.Present(comparer))

  static member internal OfSortedKeysAndValues(keys:'K[], values:'V[], size:int, comparer:IComparer<'K>, doCheckSorted:bool, isAlreadyRegular) =
    if keys.Length < size && not isAlreadyRegular then raise (new ArgumentException("Keys array is smaller than provided size"))
    if values.Length < size then raise (new ArgumentException("Values array is smaller than provided size"))
    let sm = new SortedMap<'K,'V>(comparer)
    if doCheckSorted then
      for i in 1..size-1 do
        if comparer.Compare(keys.[i-1], keys.[i]) >= 0 then raise (new ArgumentException("Keys are not sorted"))

    // at this point IsRegular means could be regular
    if sm.IsRegular && not isAlreadyRegular then
      let isReg, step, firstArr = sm.rkCheckArray keys size sm.Comparer
      if isReg then
        sm.KeysArr <- firstArr
      else 
        sm.IsRegular <- false
        sm.KeysArr <- keys
    elif sm.IsRegular && isAlreadyRegular then
      Trace.Assert(keys.Length >= 2)
      sm.KeysArr <- keys
    elif not sm.IsRegular && not isAlreadyRegular then
      sm.IsRegular <- false
      sm.KeysArr <- keys
    else raise (InvalidOperationException("Keys are marked as already regular, but comparer doesn't cupport regular keys"))
    
    sm.size <- size
    sm.ValuesArr <- values
    if sm.size > 0 then sm.SetRkLast(sm.GetKeyByIndexUnchecked(sm.size - 1))
    sm

  /// Create a SortedMap using the first `size` elements of the provided keys and values.
  static member OfSortedKeysAndValues(keys:'K[], values:'V[], size:int, comparer:IComparer<'K>) =
    if comparer = Unchecked.defaultof<_> then raise (ArgumentNullException("comparer"))
    else SortedMap.OfSortedKeysAndValues(keys, values, size, comparer, true, false)

  /// Create a SortedMap using the first `size` elements of the provided keys and values, with default comparer.
  static member OfSortedKeysAndValues(keys:'K[], values:'V[], size:int) =
    let comparer =
      KeyComparer<'K>.Default
    SortedMap.OfSortedKeysAndValues(keys, values, size, comparer, true, false)

  static member OfSortedKeysAndValues(keys:'K[], values:'V[]) =
    if keys.Length <> values.Length then raise (new ArgumentException("Keys and values arrays are of different sizes"))
    SortedMap.OfSortedKeysAndValues(keys, values, values.Length)

  //#endregion


  /// Checks if keys of two maps are equal
  //static member internal KeysAreEqual(smA:SortedMap<'K,_>,smB:SortedMap<'K,_>) : bool =
  //  if not (smA.size = smB.size) then false
  //  elif smA.IsRegular && smB.IsRegular 
  //    && smA.RegularStep = smB.RegularStep
  //    && smA.Comparer.Equals(smB.Comparer) // TODO test custom comparer equality, custom comparers must implement equality
  //    && smA.Comparer.Compare(smA.keys.[0], smB.keys.[0]) = 0 then // if steps are equal we could skip checking second elements in keys
  //    true
  //  else
  //    // TODO this is very slow to be used in any "optimization", should use BytesExtensions.UnsafeCompare
  //    System.Linq.Enumerable.SequenceEqual(smA.keys, smB.keys)

  static member Empty = empty.Value

[<StructLayout(LayoutKind.Sequential, Pack = 1)>]
type public SortedMapCursor<'K,'V> =
    struct
      val internal source : SortedMap<'K,'V>
      val mutable internal current : KVP<'K,'V>
      val mutable internal index : int
      val mutable internal cursorVersion : int16
      val mutable internal isBatch : bool
      val mutable internal doSpin : bool
      [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
      new(source:SortedMap<'K,'V>) = 
        { source = source;
          index = -1;
          current = Unchecked.defaultof<_>;
          cursorVersion = int16 source.orderVersion;
          isBatch = false;
          doSpin = source._isSynchronized
        }
    end

    member this.CurrentKey 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : 'K = this.current.Key

    member this.CurrentValue 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : 'V = this.current.Value

    member this.Current 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : KVP<'K,'V> = this.current

    member this.Source 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get(): ISeries<'K,'V> = this.source :> ISeries<'K,'V>    
    
    member this.IsContinuous with get() = false

    member this.IsCompleted with get() = Volatile.Read(&this.source._isReadOnly)
    
    member this.AsyncCompleter with get() = this.source :> IAsyncCompleter

    member this.Comparer
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : KeyComparer<'K> = this.source.ComparerInlined
    
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.source.TryGetValue(key, &value)
        
    [<MethodImplAttribute(MethodImplOptions.NoInlining)>]
    member this.ThrowOnOrderVersionChange() : unit =
        // source order change
        this.index <- -1
        let key = this.current.Key
        this.current <- Unchecked.defaultof<_>
        ThrowHelper.ThrowOutOfOrderKeyException(key, "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover.")

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveNext() : bool =
      this.index <- this.index + 1
      let mutable doSpin = this.doSpin;
      let mutable version = if doSpin then Volatile.Read(&this.source._version) else 0L
      
      if this.index < this.source.size then
        let mutable sw = new SpinWait()
        let mutable newC = this.source.GetPairByIndexUnchecked(this.index)
        while doSpin do
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then 
            doSpin <- false
          else    
            sw.SpinOnce()
            version <- Volatile.Read(&this.source._version)
            if this.index < this.source.size then
              newC <- this.source.GetPairByIndexUnchecked(this.index)
            else
              // NB this is only possible if size decreased but that changes 
              // this.source.orderVersion and operation will throw on the next section below
              doSpin <- false 

        if this.cursorVersion <> int16 this.source.orderVersion then this.ThrowOnOrderVersionChange()
        this.current <- newC
        true
      else
        // NB It's slightly chaper to set index on the happy path and recover here
        // If happy path throws than the cursor is in invalid state (calling Current/Key/Value is undefined)
        // but we have CurrentKey stored inside the exception and could recover with MoveAt
        this.index <- this.index - 1
        false

    member private this.MoveNextBatch(): ValueTask<bool> =
      // only once  
      if this.isBatch then new ValueTask<bool>(false)
      else
        this.isBatch <- true
        new ValueTask<bool>(true)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MovePrevious() = 
      let mutable newIndex = this.index
      let mutable newC = Unchecked.defaultof<_>
      //let mutable newKey = this.currentKey
      //let mutable newValue = this.currentValue

      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          if this.index = -1 then
            if this.source.size > 0 then
              this.cursorVersion <- int16 this.source.orderVersion
              newIndex <- this.source.size - 1
              newC <- this.source.GetPairByIndexUnchecked(newIndex)
              //newKey <- this.source.GetKeyByIndexUnchecked(newIndex)
              //newValue <- this.source.values.[newIndex]
              true
            else
              false
          elif this.cursorVersion = int16 this.source.orderVersion then
            if this.index > 0 && this.index < this.source.size then
              newIndex <- this.index - 1
              newC <- this.source.GetPairByIndexUnchecked(newIndex)
              //newKey <- this.source.GetKeyByIndexUnchecked(newIndex)
              //newValue <- this.source.values.[newIndex]
              true
            else
              false
          else
            ThrowHelper.ThrowOutOfOrderKeyException(this.current.Key, "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.LT) to recover.")
            false
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        this.index <- newIndex
        this.current <- newC
        //this.currentKey <- newKey
        //this.currentValue <- newValue
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveAt(key:'K, lookup:Lookup) =
      let mutable newIndex = this.index
      let mutable newC = Unchecked.defaultof<_>
      //let mutable newKey = this.currentKey
      //let mutable newValue = this.currentValue
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          // leave it for mutations, optimize TryFindAt
          let position = this.source.FindIndexAt(key, lookup)
          if position >= 0 then
            newC <- this.source.GetPairByIndexUnchecked(position)
            this.cursorVersion <- int16 this.source.orderVersion
            newIndex <- position
            //newKey <- kvp.Key
            //newValue <- kvp.Value
            true
          else
            false
      /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        this.index <- newIndex
        this.current <- newC
        //this.currentKey <- newKey
        //this.currentValue <- newValue
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveFirst() =
      let mutable newIndex = this.index
      let mutable newC = Unchecked.defaultof<_>
      //let mutable newKey = this.currentKey
      //let mutable newValue = this.currentValue
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          if this.source.size > 0 then
            this.cursorVersion <- int16 this.source.orderVersion
            newIndex <- 0
            newC <- this.source.GetPairByIndexUnchecked(newIndex)
            //newKey <- this.source.GetKeyByIndexUnchecked(newIndex)
            //newValue <- this.source.values.[newIndex]
            true
          else
            false
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        this.index <- newIndex
        this.current <- newC
        //this.currentKey <- newKey
        //this.currentValue <- newValue
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveLast() =
      let mutable newIndex = this.index
      let mutable newC = Unchecked.defaultof<_>
      //let mutable newKey = this.currentKey
      //let mutable newValue = this.currentValue
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          if this.source.size > 0 then
            this.cursorVersion <- int16 this.source.orderVersion
            newIndex <- this.source.size - 1
            newC <- this.source.GetPairByIndexUnchecked(newIndex)
            //newKey <- this.source.GetKeyByIndexUnchecked(newIndex)
            //newValue <- this.source.values.[newIndex]
            true
          else
            false
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        this.index <- newIndex
        this.current <- newC
        //this.currentKey <- newKey
        //this.currentValue <- newValue
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveNextAsync(): ValueTask<bool> =
      if this.source._isReadOnly then
        if this.MoveNext() then new ValueTask<bool>(true) else new ValueTask<bool>(false)
      else ThrowHelper.ThrowNotSupportedException("Use an async cursor wrapper instead");new ValueTask<bool>(false)

    member this.DisposeAsync() = this.Dispose();ValueTask(Task.CompletedTask)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.Clone() = 
      let mutable copy = this
      copy

    member this.Reset() : unit = 
      this.cursorVersion <- int16 this.source.orderVersion
      this.current <- Unchecked.defaultof<_>
      //this.currentKey <- Unchecked.defaultof<'K>
      //this.currentValue <- Unchecked.defaultof<'V>
      this.index <- -1

    member this.Dispose(): unit = this.Reset()

    interface IDisposable with
      member this.Dispose() = this.Dispose()
    
    interface IEnumerator with 
      member this.Current with get(): obj = this.Current :> obj
      member this.Reset() = this.Reset()
      member this.MoveNext():bool = this.MoveNext()
      
    interface IEnumerator<KVP<'K,'V>> with    
      member this.Current with get(): KVP<'K, 'V> = this.Current
            
    interface IAsyncEnumerator<KVP<'K,'V>> with
      member this.DisposeAsync() = this.DisposeAsync()
      member this.MoveNextAsync(): ValueTask<bool> = this.MoveNextAsync()
      
    interface IAsyncBatchEnumerator<KVP<'K,'V>> with
      member this.MoveNextBatchAsync(noAsync: bool): ValueTask<bool> = this.MoveNextBatch()
      member this.CurrentBatch
        with get(): IEnumerable<KVP<'K, 'V>> = 
          if this.isBatch then
            this.source :> IEnumerable<KVP<'K, 'V>> 
          else Unchecked.defaultof<_>
      
    interface ICursor<'K,'V> with
      member this.Comparer with get() = this.Comparer
      member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
      member this.MoveFirst():bool = this.MoveFirst()
      member this.MoveLast():bool =  this.MoveLast()
      member this.MoveNext():bool = this.MoveNext()
      member this.MovePrevious():bool = this.MovePrevious()
      member this.CurrentKey with get():'K = this.CurrentKey
      member this.CurrentValue with get():'V = this.CurrentValue
      member this.Source with get() = this.source :> ISeries<'K,'V>
      member this.Clone() = this.Clone() :> ICursor<'K,'V>
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>] value: byref<'V>) = this.TryGetValue(key, &value)
      // TODO
      member this.State with get() = raise (NotImplementedException())
      member this.MoveNext(stride, allowPartial) = raise (NotImplementedException())
      member this.MovePrevious(stride, allowPartial) = raise (NotImplementedException())
      
    interface ISpecializedCursor<'K,'V, SortedMapCursor<'K,'V>> with
      member this.Initialize() = 
        let c = this.Clone()
        c.Reset()
        c
      member this.Clone() = this.Clone()
      member this.IsIndexed with get() = false
      member this.IsCompleted with get() = this.IsCompleted
      member this.AsyncCompleter with get() = this.AsyncCompleter
      member this.Source with get() = new Series<_,_,_>(this)