namespace Spreads.Collections

open System
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks


open Spreads
open Spreads.Collections

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



/// Mutable sorted IOrderedMap<'K,'V> implementation based on SCG.SortedList<'K,'V>
[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedMap<'K,'V when 'K : comparison>
  internal(dictionary:IDictionary<'K,'V> option, capacity:int option, comparer:IComparer<'K> option) as this=
  inherit Series<'K,'V>()

  //#region Main internal constructor
    
  // data fields
  [<DefaultValueAttribute>]
  val mutable internal keys : 'K array
  [<DefaultValueAttribute>]
  val mutable internal values : 'V array
  [<DefaultValueAttribute>] // if size > 2 and keys.Length = 2 then the keys are regular
  val mutable internal size : int
  let mutable version : int = 0 // enumeration doesn't lock but checks version

  // util fields
  [<NonSerializedAttribute>]
  // This type is logically immutable. This field is only mutated during deserialization. 
  let mutable comparer : IComparer<'K> = 
    if comparer.IsNone then Comparer<'K>.Default :> IComparer<'K> //LanguagePrimitives.FastGenericComparer
    else comparer.Value
  [<NonSerializedAttribute>]
  let isKeyReferenceType : bool = not typeof<'K>.IsValueType
  [<NonSerializedAttribute>]
  let mutable cursorCounter : int = 1
  [<NonSerializedAttribute>]
  let mutable rkStep_ : int = 0 // TODO review all usages

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
  [<NonSerializedAttribute>]
  let syncRoot = new Object()
  [<NonSerializedAttribute>]
  let mutable mapKey = ""

  let updateEvent = new Internals.EventV2<UpdateHandler<'K,'V>,KVP<'K,'V>>()

  // helper functions
  let rkGetStep() =
    Debug.Assert(this.size > 1)
    if rkStep_ > 0 then rkStep_
    elif this.size > 1 then
      rkStep_ <- diffCalc.Diff(this.keys.[1], this.keys.[0])
      rkStep_
    else raise (InvalidOperationException("Cannot calculate regular keys step for a single element in a map"))
  let rkKeyAtIndex (idx:int) : 'K = diffCalc.Add(this.keys.[0], idx*rkGetStep())
  let rkIndexOfKey (key:'K) : int =
    Debug.Assert(this.size > 1)
    // TODO this doesn't work for LT/LE/GT/GE TryFind
    let diff = diffCalc.Diff(key, this.keys.[0])
    let modIsOk = diff % rkGetStep() = 0
    let idx = diff/rkGetStep()
//    https://msdn.microsoft.com/en-us/library/2cy9f6wb(v=vs.110).aspx
//    The index of the specified value in the specified array, if value is found.
//    If value is not found and value is less than one or more elements in array, 
//    a negative number which is the bitwise complement of the index of the first 
//    element that is larger than value. If value is not found and value is greater 
//    than any of the elements in array, a negative number which is the bitwise 
//    complement of (the index of the last element plus 1).
    if idx < 0 then
      ~~~0 // -1 for searches, insert will take ~~~
    elif idx >= this.size then
      ~~~this.size
    elif modIsOk then
      idx
    else
      ~~~(idx+1)

  let rkMaterialize () =
    let step = rkGetStep()
    Array.init this.values.Length (fun i -> if i < this.size then diffCalc.Add(this.keys.[0], i*step) else Unchecked.defaultof<'K>)

  do
    let tempCap = if capacity.IsSome then capacity.Value else 1
    this.keys <- Array.zeroCreate (if couldHaveRegularKeys then 2 else tempCap) // regular keys are the first and the second value, their diff is the step
    this.values <- Array.zeroCreate tempCap

    if dictionary.IsSome then
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
        dictionary.Value.Keys.CopyTo(this.keys, 0)
        dictionary.Value.Values.CopyTo(this.values, 0)
        Array.Sort(this.keys, this.values, comparer)
        this.size <- dictionary.Value.Count
        // TODO review
        if couldHaveRegularKeys then // if could be regular based on initial check of comparer type
          let isReg, step, firstArr = this.rkCheckArray this.keys this.size (comparer :?> IKeyComparer<'K>)
          couldHaveRegularKeys <- isReg
          if couldHaveRegularKeys then 
            this.keys <- firstArr
            rkLast <- rkKeyAtIndex (this.size - 1)
  //#endregion

  member private this.rkCheckArray (sortedArray:'K[]) (size:int) (dc:IKeyComparer<'K>) : bool * int * 'K array = 
    if size > sortedArray.Length then raise (ArgumentException("size is greater than sortedArray length"))
    if size < 1 then
      true, 0, [|Unchecked.defaultof<'K>;Unchecked.defaultof<'K>|]
    elif size < 2 then
      true, 0, [|sortedArray.[0];Unchecked.defaultof<'K>|]
    elif size < 3 then 
      true, dc.Diff(sortedArray.[1], sortedArray.[0]), [|sortedArray.[0];sortedArray.[1]|]
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
        true, firstDiff, [|sortedArray.[0];sortedArray.[1]|]
      else
        false, 0, Unchecked.defaultof<'K[]>

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

  member private this.Clone() = new SortedMap<'K,'V>(Some(this :> IDictionary<'K,'V>), None, Some(comparer))

  member internal this.GetKeyByIndex(index) =
    if couldHaveRegularKeys && this.size > 1 then 
      if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
      rkKeyAtIndex index
    else
      this.keys.[index]
    
  member private this.GetPairByIndexUnchecked(index) = //inline
    if couldHaveRegularKeys && this.size > 1 then
      if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
      KeyValuePair(rkKeyAtIndex index, this.values.[index])
    else KeyValuePair(this.keys.[index], this.values.[index]) 
  
  member private this.CompareToFirst (k:'K) =  //inline
    comparer.Compare(k, this.keys.[0]) // keys.[0] is always the first key
  
  member private this.CompareToLast (k:'K) = //inline
    if couldHaveRegularKeys && this.size > 1 then 
      Debug.Assert(rkLast <> Unchecked.defaultof<'K>)
      comparer.Compare(k, rkLast)
    else comparer.Compare(k, this.keys.[this.size-1])

  member private this.EnsureCapacity(min) = 
    let mutable num = this.values.Length * 2 
    if num > 2146435071 then num <- 2146435071
    if num < min then num <- min // either double or min if min > 2xprevious
    this.Capacity <- num


  member private this.Insert(index:int, k, v) = 
    // key is always new, checks are before this method
    // already inside a lock statement in a caller method if synchronized

    if this.size = this.values.Length then this.EnsureCapacity(this.size + 1)
    
    // for values it is alway the same operation
    Debug.Assert(index <= this.size, "index must be <= this.size")
    if index < this.size then Array.Copy(this.values, index, this.values, index + 1, this.size - index);
    this.values.[index] <- v

    // for regular keys must do some math to check if they will remain regular after the insertion
    // treat sizes 1,2(after insertion) as non-regular because they are always both regular and not 
    if couldHaveRegularKeys then
      if this.size > 1 then
        let diff = diffCalc.Diff(k, this.keys.[0])
        let step = rkGetStep()
        let idx = diff / step
        let modIsOk = diff % step = 0
        if modIsOk && idx = -1 then
          this.keys.[1] <- this.keys.[0]
          this.keys.[0] <- k // change first key and size++ at the bottom
          //rkLast is unchanged
        elif modIsOk && idx = this.size then 
          rkLast <- diffCalc.Add(this.keys.[0], (this.size) * step) // do not subtract -1 from size because size++ happens at the end 
        elif modIsOk && idx > -1 && idx < this.size then
          // error for regular keys, this means we insert existing key
          raise (new ApplicationException("Existing key check must be done before insert"))
        else
          // insertting more than 1 away from end or before start, with a hole
          this.keys <- rkMaterialize() 
          couldHaveRegularKeys <- false
      else
        if index < this.size then
          Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
        else rkLast <- k
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
    version <- version + 1
    this.size <- this.size + 1
    if cursorCounter >0 then updateEvent.Trigger(KVP(k,v))
    
  member internal this.IsReadOnly with get() = false

  member internal this.IsSynchronized 
    with get() =  isSynchronized
    and set(synced:bool) = 
      let entered = enterLockIf syncRoot isSynchronized
      isSynchronized <- synced
      exitLockIf syncRoot entered

  member internal this.MapKey with get() = mapKey and set(key:string) = mapKey <- key

  // external world should not care about this
  member internal this.IsRegular with get() = couldHaveRegularKeys
  member internal this.RegularStep with get() = rkGetStep()

  member internal this.SyncRoot with get() = syncRoot
  member internal this.Version with get() = version and set v = version <- v

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
          if couldHaveRegularKeys then
            Debug.Assert(this.keys.Length = 2)
          else
            let kArr : 'K array = Array.zeroCreate c
            Array.Copy(this.keys, 0, kArr, 0, this.size)
            this.keys <- kArr

          let vArr : 'V array = Array.zeroCreate c
          Array.Copy(this.values, 0, vArr, 0, this.size)
          this.values <- vArr
        | _ -> ()
      finally
        exitLockIf syncRoot entered

  member this.Comparer with get() = comparer

  member this.Clear() =
    version <- version + 1
    if couldHaveRegularKeys then
      Debug.Assert(this.keys.Length = 2)
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
          if couldHaveRegularKeys then
            Array.Copy(rkMaterialize(), 0, array, arrayIndex, this.size)
          else
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
                currentKey := 
                  if couldHaveRegularKeys then diffCalc.Add(this.keys.[0], (!index)*rkGetStep()) 
                  else this.keys.[!index]
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
      if couldHaveRegularKeys && this.size > 1 then 
        rkIndexOfKey key
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
      if couldHaveRegularKeys then 
        Debug.Assert(comparer.Compare(rkLast, diffCalc.Add(this.keys.[0], (this.size-1)*rkGetStep())) = 0)
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
          version <- version + 1
          if cursorCounter >0 then updateEvent.Trigger(KVP(k,v))
          lastIdx
        elif lc > 0 then // adding last value, Insert won't copy arrays if enough capacity
          this.Insert(this.size, k, v)
          this.size
        else   
          let index = this.IndexOfKeyUnchecked(k)
          if index >= 0 then // contains key 
            this.values.[index] <- v
            version <- version + 1 
            if cursorCounter >0 then updateEvent.Trigger(KVP(k,v))
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
    try
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
    finally
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
        else raise (ArgumentOutOfRangeException("SortedMap.AddLast: New key is smaller or equal to the largest existing key"))
    finally
      exitLockIf syncRoot entered

  member this.AddFirst(key, value):unit =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if this.size = 0 then
        this.Insert(0, key, value)
      else
        let c = this.CompareToFirst key
        if c = -1 then
            this.Insert(0, key, value)
        else raise (ArgumentOutOfRangeException("SortedMap.AddLast: New key is larger or equal to the smallest existing key"))
    finally
      exitLockIf syncRoot entered
    
  member internal this.RemoveAt(index):unit =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
      let newSize = this.size - 1
      // TODO review, check for off by 1 bugs, could had lost focus at 3 AM
      // keys
      if couldHaveRegularKeys && this.size > 2 then // will have >= 2 after removal
        if index = 0 then
          this.keys.[0] <- (diffCalc.Add(this.keys.[0], rkGetStep())) // change first key to next and size--
          this.keys.[1] <- (diffCalc.Add(this.keys.[0], rkGetStep())) // add step to the new first value
        elif index = newSize then 
          rkLast <- diffCalc.Add(this.keys.[0], (newSize-1)*rkGetStep()) // removing last, only size--
        else 
          // removing within range, creating a hole
          this.keys <- rkMaterialize() 
          couldHaveRegularKeys <- false
      elif couldHaveRegularKeys && this.size = 2 then // will have single value with undefined step
        if index = 0 then
          this.keys.[0] <- this.keys.[1]
          this.keys.[1] <- Unchecked.defaultof<'K>
        elif index = 1 then
          rkLast <- this.keys.[0]
        rkStep_ <- 0

      if not couldHaveRegularKeys || this.size = 1 then
        if index < this.size then
          Array.Copy(this.keys, index + 1, this.keys, index, this.size - index)
        this.keys.[this.size] <- Unchecked.defaultof<'K>
      
      // values
      if index < newSize then
        Array.Copy(this.values, index + 1, this.values, index, this.size - index)
      this.values.[newSize] <- Unchecked.defaultof<'V>

      this.size <- newSize
      version <- version + 1

      if cursorCounter > 0 then 
        // on removal, the next valid value is the previous one and all downstreams must reposition and replay from it
        if index > 0 then updateEvent.Trigger(this.GetPairByIndexUnchecked(index - 1)) // after removal (index - 1) is unchanged
        else updateEvent.Trigger(Unchecked.defaultof<_>)
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

  // TODO first/last optimization
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

  // TODO first/last optimization
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
          if couldHaveRegularKeys then
            this.keys.[0] <- (diffCalc.Add(this.keys.[0], pivotIndex+1))
            if this.size > 1 then 
              this.keys.[1] <- (diffCalc.Add(this.keys.[0], rkGetStep())) 
            else
              this.keys.[1] <- Unchecked.defaultof<'K>
              rkStep_ <- 0
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
        elif pivotIndex >=0 then // remove elements above and including pivot
          this.size <- pivotIndex
          rkLast <- diffCalc.Add(this.keys.[0], (this.size-1)*rkGetStep()) // -1 is correct, the size is updated on the previous line
          if not couldHaveRegularKeys then
            Array.fill this.keys pivotIndex (this.values.Length - pivotIndex) Unchecked.defaultof<'K>
          Array.fill this.values pivotIndex (this.values.Length - pivotIndex) Unchecked.defaultof<'V>
          version <- version + 1
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
  /// Example: (-1) [...current...(-3)...map ...] (-2)
  member internal this.TryFindWithIndex(key:'K,direction:Lookup, [<Out>]result: byref<KeyValuePair<'K, 'V>>) : int = // rkok
    let entered = enterLockIf syncRoot  isSynchronized
    try
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
          result <-  this.GetPairByIndexUnchecked(lastIdx-1) // return item beforelast
          lastIdx - 1
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
            if index2 >= this.Count then // there are no elements larger than key
              result <-  this.GetPairByIndexUnchecked(this.Count - 1)
              this.Count - 1
            elif index2 = 0 then
              -1
            else //  it is the index of the first element that is larger than value
              result <-   this.GetPairByIndexUnchecked(index2 - 1)
              index2 - 1
      | Lookup.GT ->
        let lc = if this.size > 0 then comparer.Compare(key, this.keys.[0]) else 2
        if lc = 0 then // key = first key
          result <-  this.GetPairByIndexUnchecked(1) // return item after first
          1
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

  override this.GetCursor() = this.GetCursor(-1, version, Unchecked.defaultof<'K>, Unchecked.defaultof<'V>)
    
  // TODO replace with a sealed concrete class, there are too many virtual calls and reference cells in the most critical paths like MoveNext
  member internal this.GetCursor(index:int,cursorVersion:int,currentKey:'K, currentValue:'V) =
    //SortedMapCursor(this, index, cursorVersion, currentKey, currentValue) :> ICursor<'K,'V>
    let index = ref index
    let cursorVersion = ref cursorVersion
    let currentKey : 'K ref = ref currentKey
    let currentValue : 'V ref = ref currentValue
    let isBatch = ref false
    { 
    new MapCursor<'K,'V>(this) with
      override p.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = 
        if !index = -1 then this :> IReadOnlyOrderedMap<'K,'V>
        else raise (InvalidOperationException(""))
      override p.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> =
        if !index = -1 then 
          index := this.size
          isBatch := true
          Task.FromResult(true)
        else Task.FromResult(false)
      override p.IsBatch with get() = !isBatch
      override p.Clone() = this.GetCursor(!index,!cursorVersion,!currentKey,!currentValue)
      override p.Current with get() = KeyValuePair(currentKey.Value, currentValue.Value)
      override p.MoveNext() = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          if index.Value = -1 then p.MoveFirst() // first move when index = -1
          elif cursorVersion.Value = version then
            if index.Value < (this.size - 1) then
              index := !index + 1
              currentKey := this.GetKeyByIndex(index.Value)
              currentValue := this.values.[!index]
              true
            else
              index := this.size
              currentKey := Unchecked.defaultof<'K>
              currentValue := Unchecked.defaultof<'V>
              false
          else  // source change
            cursorVersion := version // update state to new version
            let position, kvp = this.TryFindWithIndex(currentKey.Value, Lookup.GT) // reposition cursor after source change
            if position > 0 then
              index := position
              currentKey := kvp.Key
              currentValue := kvp.Value
              true
            else  // not found
              index := this.size
              currentKey := Unchecked.defaultof<'K>
              currentValue := Unchecked.defaultof<'V>
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
              index := this.size // 
              currentKey := Unchecked.defaultof<'K>
              currentValue := Unchecked.defaultof<'V>
              false
          else
            cursorVersion := version // update state to new version
            let position, kvp = this.TryFindWithIndex(currentKey.Value, Lookup.LT)
            if position > 0 then
              index := position
              currentKey := kvp.Key
              currentValue := kvp.Value
              true
            else  // not found
              index :=  this.size
              currentKey := Unchecked.defaultof<'K>
              currentValue := Unchecked.defaultof<'V>
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
            index := this.size
            currentKey := Unchecked.defaultof<'K>
            currentValue := Unchecked.defaultof<'V>
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
            index := this.size
            currentKey := Unchecked.defaultof<'K>
            currentValue := Unchecked.defaultof<'V>
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
            index := this.size
            currentKey := Unchecked.defaultof<'K>
            currentValue := Unchecked.defaultof<'V>
            false
        finally
          exitLockIf syncRoot entered

      override p.CurrentKey with get() = currentKey.Value

      override p.CurrentValue with get() = currentValue.Value

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
        value <- this.values.[index]
        true
      else
        value <- Unchecked.defaultof<'V>
        false

  interface IReadOnlyOrderedMap<'K,'V> with
    member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = this.GetCursor()
    member this.IsEmpty = this.size = 0
    member this.IsIndexed with get() = false
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
    member this.Keys with get() = this.Keys :> IEnumerable<'K>
    member this.Values with get() = this.values :> IEnumerable<'V>
    member this.SyncRoot with get() = syncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Count with get() = int64(this.size)
    member this.Item with get k = this.Item(k) and set (k:'K) (v:'V) = this.[k] <- v
    member this.Add(k, v) = this.Add(k,v)
    member this.AddLast(k, v) = this.AddLast(k, v)
    member this.AddFirst(k, v) = this.AddFirst(k, v)
    member this.Remove(k) = this.Remove(k)
    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.RemoveFirst(&result) |> ignore // TODO why unit not bool?
    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      this.RemoveLast(&result) |> ignore // TODO why unit not bool?
    member this.RemoveMany(key:'K,direction:Lookup) = 
      this.RemoveMany(key, direction) |> ignore
    member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>) =
      // do not need transaction because if the first addition succeeds then all others will be added as well
      for i in appendMap do
        this.AddLast(i.Key, i.Value)
    
  interface IUpdateable<'K,'V> with
    [<CLIEvent>]
    member x.OnData: IEvent<KVP<'K,'V>> = updateEvent.Publish
    

// YAGNI, TODO remove mutable interfaces from immutable maps and vice versa. In case this is needed, construct one from another

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

  //#endregion

  //#region Constructors

  // TODO try resolve KeyComparer for know types
  new() = SortedMap(None, None, None)
  new(dictionary:IDictionary<'K,'V>) = SortedMap(Some(dictionary), Some(dictionary.Count), Some(Comparer<'K>.Default :> IComparer<'K>))
  new(capacity:int) = SortedMap(None, Some(capacity), Some(Comparer<'K>.Default :> IComparer<'K>))

  // do not expose ctors with comparer to public
  internal new(comparer:IComparer<'K>) = SortedMap(None, None, Some(comparer))
  internal new(dictionary:IDictionary<'K,'V>,comparer:IComparer<'K>) = SortedMap(Some(dictionary), Some(dictionary.Count), Some(comparer))
  internal new(capacity:int,comparer:IComparer<'K>) = SortedMap(None, Some(capacity), Some(comparer))

  static member internal OfSortedKeysAndValues(keys:'K[], values:'V[], size:int, comparer:IComparer<'K>, sortChecked:bool) =
    if keys.Length < size then raise (new ArgumentException("Keys array is smaller than provided size"))
    if values.Length < size then raise (new ArgumentException("Values array is smaller than provided size"))
    let sm = new SortedMap<'K,'V>(comparer)
    if sortChecked then
      for i in 1..keys.Length-1 do
        if comparer.Compare(keys.[i-1], keys.[i]) >= 0 then raise (new ArgumentException("Keys are not sorted"))

    if sm.IsRegular then
      let isReg, step, firstArr = sm.rkCheckArray keys size (comparer :?> IKeyComparer<'K>)
      if isReg then
        sm.keys <- firstArr
      else sm.keys <- keys
    else sm.keys <- keys

    sm.size <- size
    sm.values <- values
    sm

  static member OfSortedKeysAndValues(keys:'K[], values:'V[], size:int) =
    let sm = new SortedMap<'K,'V>()
    let comparer = sm.Comparer
    SortedMap.OfSortedKeysAndValues(keys, values, keys.Length, comparer, true)

  static member OfSortedKeysAndValues(keys:'K[], values:'V[]) =
    if keys.Length <> values.Length then raise (new ArgumentException("Keys and values arrays are of different sizes"))
    SortedMap.OfSortedKeysAndValues(keys, values, keys.Length)
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
      System.Linq.Enumerable.SequenceEqual(smA.keys, smB.keys)



and
  SortedMapCursor<'K,'V when 'K : comparison>(sm:SortedMap<'K,'V>,index:int,cursorVersion:int,currentKey:'K, currentValue:'V) =
    //struct
    let mutable index : int = index
    let mutable cursorVersion : int = cursorVersion
    let mutable currentKey : 'K = currentKey
    let mutable currentValue : 'V = currentValue
    let mutable isBatch : bool = false
    let sm:SortedMap<'K,'V> = sm
    //  {index = index; cursorVersion = cursorVersion; currentKey = currentKey;currentValue=currentValue;isBatch=false; sm = sm}
    //end
   
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = 
      if index = -1 then sm :> IReadOnlyOrderedMap<'K,'V>
      else raise (InvalidOperationException(""))
    member this.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> =
      if index = -1 then 
        index <- sm.size
        isBatch <- true
        Task.FromResult(true)
      else Task.FromResult(false)
    member this.IsBatch with get() = isBatch
    member this.Clone() = sm.GetCursor(index,cursorVersion,currentKey,currentValue)
    member this.Current with get() = KeyValuePair(currentKey, currentValue)
    member this.MoveNext() = 
      let entered = enterLockIf sm.SyncRoot  sm.IsSynchronized
      try
        if index = -1 then this.MoveFirst() // first move when index = -1
        elif cursorVersion = sm.Version then
          if index < (sm.size - 1) then
            index <- index + 1
            currentKey <- sm.GetKeyByIndex(index)
            currentValue <- sm.values.[index]
            true
          else
            index <- sm.size
            currentKey <- Unchecked.defaultof<'K>
            currentValue <- Unchecked.defaultof<'V>
            false
        else  // source change
          cursorVersion <- sm.Version // update state to new sm.version
          let position, kvp = sm.TryFindWithIndex(currentKey, Lookup.GT) // reposition cursor after source change
          if position > 0 then
            index <- position
            currentKey <- kvp.Key
            currentValue <- kvp.Value
            true
          else  // not found
            index <- sm.size
            currentKey <- Unchecked.defaultof<'K>
            currentValue <- Unchecked.defaultof<'V>
            false
      finally
        exitLockIf sm.SyncRoot entered

    member this.MovePrevious() = 
      let entered = enterLockIf sm.SyncRoot  sm.IsSynchronized
      try
        if index = -1 then this.MoveLast()  // first move when index = -1
        elif cursorVersion = sm.Version then
          if index > 0 && index < sm.size then
            index <- index - 1
            currentKey <- sm.GetKeyByIndex(index)
            currentValue <- sm.values.[index]
            true
          else
            index <- sm.size // 
            currentKey <- Unchecked.defaultof<'K>
            currentValue <- Unchecked.defaultof<'V>
            false
        else
          cursorVersion <- sm.Version // update state to new sm.version
          let position, kvp = sm.TryFindWithIndex(currentKey, Lookup.LT)
          if position > 0 then
            index <- position
            currentKey <- kvp.Key
            currentValue <- kvp.Value
            true
          else  // not found
            index <- sm.size
            currentKey <- Unchecked.defaultof<'K>
            currentValue <- Unchecked.defaultof<'V>
            false
      finally
        exitLockIf sm.SyncRoot entered

    member this.MoveAt(key:'K, lookup:Lookup) = 
      let entered = enterLockIf sm.SyncRoot  sm.IsSynchronized
      try
        let position, kvp = sm.TryFindWithIndex(key, lookup)
        if position >= 0 then
          index <- position
          currentKey <- kvp.Key
          currentValue <- kvp.Value
          true
        else
          index <- sm.size
          currentKey <- Unchecked.defaultof<'K>
          currentValue <- Unchecked.defaultof<'V>
          false
      finally
        exitLockIf sm.SyncRoot entered

    member this.MoveFirst() = 
      let entered = enterLockIf sm.SyncRoot  sm.IsSynchronized
      try
        if sm.size > 0 then
          index <- 0
          currentKey <- sm.GetKeyByIndex(index)
          currentValue <- sm.values.[index]
          true
        else
          index <- sm.size
          currentKey <- Unchecked.defaultof<'K>
          currentValue <- Unchecked.defaultof<'V>
          false
      finally
        exitLockIf sm.SyncRoot entered

    member this.MoveLast() = 
      let entered = enterLockIf sm.SyncRoot  sm.IsSynchronized
      try
        if sm.size > 0 then
          index <- sm.size - 1
          currentKey <- sm.GetKeyByIndex(index)
          currentValue <- sm.values.[index]
          true
        else
          index <- sm.size
          currentKey <- Unchecked.defaultof<'K>
          currentValue <- Unchecked.defaultof<'V>
          false
      finally
        exitLockIf sm.SyncRoot entered

    member this.MoveNext(ct) = failwith "not implemented"

    member this.CurrentKey with get() = currentKey

    member this.CurrentValue with get() = currentValue

    member this.Reset() = 
      cursorVersion <- sm.Version // update state to new sm.version
      index <- -1
      currentKey <- Unchecked.defaultof<'K>
      currentValue <- Unchecked.defaultof<'V>

    member this.Dispose() = this.Reset()

    interface IDisposable with
      member this.Dispose() = this.Dispose()

    interface IEnumerator<KVP<'K,'V>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext():bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V> = this.Current
      member this.Current with get(): obj = this.Current :> obj

    interface IAsyncEnumerator<KVP<'K,'V>> with
      member this.Current: KVP<'K, 'V> = this.Current
      member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 

    interface ICursor<'K,'V> with
      // TODO need some implementation of ROOM to implement the batch
      member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = this.CurrentBatch
      member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatchAsync(cancellationToken)
      //member this.IsBatch with get() = this.IsBatch
      member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
      member this.MoveFirst():bool = this.MoveFirst()
      member this.MoveLast():bool =  this.MoveLast()
      member this.MovePrevious():bool = this.MovePrevious()
      member this.CurrentKey with get():'K = this.CurrentKey
      member this.CurrentValue with get():'V = this.CurrentValue
      member this.Source with get() = sm :> ISeries<'K,'V>
      member this.Clone() = this.Clone()
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = sm.TryGetValue(key, &value)