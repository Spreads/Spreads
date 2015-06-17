namespace Spreads.Collections

open System
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections


/// Thread-unsafe (unsynced) array-based sorted map
/// Implemented as SCG's SortedList with additional methods
[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedMap<'K,'V when 'K : comparison>
  internal(dictionary:IDictionary<'K,'V> option, capacity:int option, comparer:IComparer<'K> option) as this=
  inherit Series<'K,'V>()
  //#region Main private constructor
    
  // data fields
  [<DefaultValueAttribute>]
  val mutable internal keys : 'K array // = [||]
  [<DefaultValueAttribute>]
  val mutable internal values : 'V array //= [||]
  [<DefaultValueAttribute>]
  val mutable internal size : int //= 0
  [<VolatileFieldAttribute>] // enumeration doesn't locks but checks versions
  let mutable version = 0

  // util fields
  let comparer : IComparer<'K> = 
    if comparer.IsNone then Comparer<'K>.Default :> IComparer<'K> //LanguagePrimitives.FastGenericComparer
    else comparer.Value
  [<NonSerializedAttribute>]
  let tyK = typeof<'K>
  [<NonSerializedAttribute>]
  let tyV = typeof<'V>
  [<NonSerializedAttribute>]
  let isKeyReferenceType : bool = not tyK.IsValueType

  // TODO use IDC resolution via KeyHelper.diffCalculators here or befor ctor
  [<NonSerializedAttribute>]
  let mutable isKeyDiffable : bool = comparer :? IKeyComparer<'K>
  [<NonSerializedAttribute>]
  let mutable diffCalc : IKeyComparer<'K> = 
    if isKeyDiffable then comparer :?> IKeyComparer<'K> 
    else Unchecked.defaultof<IKeyComparer<'K>>

  [<NonSerializedAttribute;VolatileFieldAttribute>]
  let mutable isRegularKeys : bool = isKeyDiffable
  // TODO off by one in remove not checked, in insert is OK
  let mutable rkLast = Unchecked.defaultof<'K>
  let rkKeyAtIndex idx = diffCalc.Add(this.keys.[0], idx)
  let rkIndexOfKey key = diffCalc.Diff(key, this.keys.[0])
  let rkWillRemainRegular newKey = 
    if this.size = 0 then true
    else 
      //      0 || 4    1,2,3
      let diff = diffCalc.Diff(newKey, this.keys.[0]) 
      diff <= this.size && diff >= -1
  let rkMaterialize () =
    Array.init this.values.Length (fun i -> if i < this.size then diffCalc.Add(this.keys.[0], i) else Unchecked.defaultof<'K>)
  

  [<NonSerializedAttribute;VolatileFieldAttribute>]
  let mutable isSynchronized : bool = false // TODO? safer to set true by default
  [<NonSerializedAttribute>]
  let syncRoot = new Object()

  [<NonSerializedAttribute>]
  let mutable mapKey = ""


  do
    let tempCap = if capacity.IsSome then capacity.Value else 1
    this.keys <- Array.zeroCreate (if isRegularKeys then 1 else tempCap)
    this.values <- Array.zeroCreate tempCap

    if dictionary.IsSome then
      match dictionary.Value with
      | :? SortedMap<'K,'V> as map ->
        let entered = enterLockIf map.SyncRoot map.IsSynchronized
        try
          isRegularKeys <- map.IsRegular
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
        if isKeyDiffable then
          isRegularKeys <- KeyHelper.isRegular this.keys this.size (comparer :?> IKeyComparer<'K>)
        if isRegularKeys then 
          this.keys <- [|this.keys.[0]|]
          // TODO buffer pool for regular/irregular conversions

  //#endregion

  //#region Private & Internal members

  member private this.Clone() = new SortedMap<'K,'V>(Some(this :> IDictionary<'K,'V>), None, Some(comparer))

  member private this.GetKeyByIndex(index) =  // rkok
    if isRegularKeys then rkKeyAtIndex index
    else
      if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
      this.keys.[index]
    
  member private this.GetValueByIndex(index) =  // rkok
    if index < 0 || index >= this.size then raise (ArgumentOutOfRangeException("index"))
    this.values.[index]

  member  private this.GetPairByIndexUnchecked(index) = // rkok //inline
    if isRegularKeys then 
      KeyValuePair(rkKeyAtIndex index, this.values.[index])
    else KeyValuePair(this.keys.[index], this.values.[index]) 
  
  member  private this.CompareToFirst (k:'K) =  //inline
     comparer.Compare(k, this.keys.[0])
  
  member  private this.CompareToLast (k:'K) = //inline
     if isRegularKeys then comparer.Compare(k, rkLast) 
     else comparer.Compare(k, this.keys.[this.size-1])

  // key is always new, checks are before this method
  member private this.Insert(index:int, k, v) = // rkok
    if this.size = this.values.Length then 
          this.EnsureCapacity(this.size + 1)

    if index < this.size then
        Array.Copy(this.values, index, this.values, index + 1, this.size - index);

    if isRegularKeys then
      if this.size = 0 then 
        this.keys.[0] <- k
        rkLast <- k
      else
        let diff = diffCalc.Diff(k, this.keys.[0]) 
        if diff = -1 then this.keys.[0] <- k // change first key and size++
        elif diff = this.size then 
          rkLast <- diffCalc.Add(this.keys.[0], this.size) // append, only size++ at the end 
        elif diff > -1 && diff < this.size then
          // error for regular keys, this means we insert existing key
          raise (new ApplicationException("Existing key check must be done before insert"))
        else 
          // insertting more than 1 away from end or before start, with a hole
          this.keys <- rkMaterialize() 
          isRegularKeys <- false
    if not isRegularKeys then
      if index < this.size then
        Array.Copy(this.keys, index, this.keys, index + 1, this.size - index);
      this.keys.[index] <- k
      // do not check if could regularize back, it is very rare 
      // that an irregular becomes a regular one, and such check is always done on
      // bucket switch in SHM and defore serialization
    this.values.[index] <- v
    version <- version + 1
    this.size <- this.size + 1

  member private this.EnsureCapacity(min) = // rkok
    let mutable num = this.values.Length * 2 
    if num > 2146435071 then num <- 2146435071
    if num < min then num <- min
    this.Capacity <- num
    
  member internal this.IsReadOnly with get() = false

  member internal this.IsSynchronized 
    with get() =  isSynchronized
    and set(synced:bool) = 
      let entered = enterLockIf syncRoot  isSynchronized
      isSynchronized <- synced
      exitLockIf syncRoot entered

  member internal this.MapKey with get() = mapKey and set(key:string) = mapKey <- key

  /// <summary>
  /// Check if keys could be represented as regular and compress them if they could
  /// </summary>
  member internal this.CheckRegular() =
    if isKeyDiffable then
      isRegularKeys <- KeyHelper.isRegular this.keys this.size (comparer :?> IKeyComparer<'K>)
    if isRegularKeys then 
      this.keys <- [|this.keys.[0]|]

  member internal this.IsRegular with get() = isRegularKeys

  member internal this.SyncRoot with get() = syncRoot

  member internal this.Version with get() = version and set v = version <- v


  //#endregion


  //#region Public members

  member this.Capacity // rkok
    with get() = this.values.Length
    and set(value) =
      let entered = enterLockIf syncRoot  isSynchronized
      try
        match value with
        | c when c = this.values.Length -> ()
        | c when c < this.size -> raise (ArgumentOutOfRangeException("Small capacity"))
        | c when c > 0 -> 
          if isRegularKeys then
            Debug.Assert(this.keys.Length <= 1)
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

  member this.Clear() = // rkok
    version <- version + 1
    if isRegularKeys then
      Debug.Assert(this.keys.Length = 1)
      Array.Clear(this.keys, 0, 1)
    else
      Array.Clear(this.keys, 0, this.size)
    Array.Clear(this.values, 0, this.size)
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
          if isRegularKeys then
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
                  if isRegularKeys then diffCalc.Add(this.keys.[0], !index) 
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
          with get index : 'V = this.GetValueByIndex(index)
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

  member internal this.IndexOfKeyUnchecked(key:'K) : int = //rkok
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if isRegularKeys then
        let idx = rkIndexOfKey key
        if idx < 0 then ~~~0
        elif idx >= this.size then ~~~this.size
        else idx
      else
        Array.BinarySearch(this.keys, 0, this.size, key, comparer)
    finally
      exitLockIf syncRoot entered

  member this.IndexOfKey(key:'K) : int = //rkok
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
      if isRegularKeys then KeyValuePair(this.keys.[0], this.values.[0])
      else KeyValuePair(this.keys.[0], this.values.[0])

  member this.Last
    with get() =
      if this.size = 0 then raise (InvalidOperationException("Could not get the last element of an empty map"))
      if isRegularKeys then KeyValuePair(rkLast, this.values.[this.size - 1])
      else KeyValuePair(this.keys.[this.size - 1], this.values.[this.size - 1])

  member this.Item // rkok
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
  member internal this.SetWithIndex(k, v) = // rkok
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
          lastIdx
        elif lc = 1 then // adding last value, Insert won't copy arrays if enough capacity
          this.Insert(this.size, k, v)
          this.size
        else   
          let index = this.IndexOfKeyUnchecked(k)
          if index >= 0 then // contains key 
            this.values.[index] <- v
            version <- version + 1 
            index          
          else
            this.Insert(~~~index, k, v)
            ~~~index
    finally
      exitLockIf syncRoot entered

  // In-place (mutable) addition
  member this.Add(key, value) : unit =  // rkok
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
        elif lc = 1 then // adding last value, Insert won't copy arrays if enough capacity
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
        if c = 1 then 
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
    
  member public this.RemoveAt(index):unit = // rkok
    let entered = enterLockIf syncRoot  isSynchronized
    try
      if index < 0 || index >= this.size then
          raise (ArgumentOutOfRangeException("index"))
      this.size <- this.size - 1 // NB do not -1 below
      // keys
      if isRegularKeys then
        if index = 0 then this.keys.[0] <- (diffCalc.Add(this.keys.[0], 1)) // change first key to next and size--
        elif index = this.size then 
          rkLast <- diffCalc.Add(this.keys.[0], this.size-1) // removing last, only size--
        else 
          // removing within range, creating a hole
          this.keys <- rkMaterialize() 
          isRegularKeys <- false
      if not isRegularKeys then
        if index < this.size then
          Array.Copy(this.keys, index + 1, this.keys, index, this.size - index)
        this.keys.[this.size] <- Unchecked.defaultof<'K>
      // values
      if index < this.size then
        Array.Copy(this.values, index + 1, this.values, index, this.size - index)
      this.values.[this.size] <- Unchecked.defaultof<'V>

      version <- version + 1
    finally
      exitLockIf syncRoot entered

  member this.Remove(key):bool =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      let index = this.IndexOfKey(key)
      if index >= 0 then this.RemoveAt(index)
      index >= 0
    finally
      exitLockIf syncRoot entered

  // TODO first/last optimization
  member this.RemoveFirst([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      result <- this.First
      this.Remove(result.Key)
    finally
      exitLockIf syncRoot entered

  // TODO first/last optimization
  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let entered = enterLockIf syncRoot  isSynchronized
    try
      result <-this.Last
      this.Remove(result.Key)
    finally
      exitLockIf syncRoot entered

  /// Removes all elements that are to `direction` from `key`
  member this.RemoveMany(key:'K,direction:Lookup):bool = // rkok
    let entered = enterLockIf syncRoot  isSynchronized
    try
      let pivot = this.TryFindWithIndex(key, direction)
      let pivotIndex = fst pivot
      // pivot should be removed, after calling TFWI pivot is always inclusive
      match direction with
      | Lookup.EQ -> this.Remove(key)
      | Lookup.LT | Lookup.LE ->
        if pivotIndex = -1 then // pivot is not here but to the left, keep all elements
          false
        elif pivotIndex >=0 then // remove elements below pivot and pivot
          this.size <- this.size - (pivotIndex + 1)
          version <- version + 1
          if isRegularKeys then
            this.keys.[0] <- (diffCalc.Add(this.keys.[0], pivotIndex+1)) 
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
          rkLast <- diffCalc.Add(this.keys.[0], this.size-1)
          if not isRegularKeys then
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
        elif lc = 1 then // key greater than the last
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
        elif lc = -1 then
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
    let tr = this.TryFindWithIndex(k, direction)
    if (fst tr) >= 0 then
        res <- snd tr
        true
    else
        false

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

  override this.GetCursor()  = this.GetCursor(-1, version, Unchecked.defaultof<'K>, Unchecked.defaultof<'V>)
    
  member private this.GetCursor(index:int,pVersion:int,currentKey:'K, currentValue:'V)=
    let index = ref index
    let pVersion = ref pVersion
    let currentKey : 'K ref = ref currentKey
    let currentValue : 'V ref = ref currentValue
    { 
    new MapCursor<'K,'V>(this) with
      override p.Clone() = this.GetCursor(!index,!pVersion,!currentKey,!currentValue)
      override p.Current with get() = KeyValuePair(currentKey.Value, currentValue.Value)
      override p.MoveNext() = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          if pVersion.Value <> version then
            // fallback to default pointer behavior
            pVersion := version // update state to new version
            if index.Value < 0 then p.MoveFirst()
            else
              let position, kvp = this.TryFindWithIndex(currentKey.Value, Lookup.GT)
              if position > 0 then
                index := position
                currentKey := kvp.Key
                currentValue := kvp.Value
                true
              else  // not found
                index := this.Count + 1
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>
                false
          else
            if index.Value < (this.Count - 1) then
              index := !index + 1
              currentKey := 
                if isRegularKeys then diffCalc.Add(this.keys.[0], !index) 
                else this.keys.[!index]
              currentValue := this.values.[!index]
              true
            else
              index := this.Count + 1
              currentKey := Unchecked.defaultof<'K>
              currentValue := Unchecked.defaultof<'V>
              false
        finally
          exitLockIf syncRoot entered

      override p.MovePrevious() = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          if pVersion.Value <> version then
            // fallback to default pointer behavior
            pVersion := version // update state to new version
            if index.Value < 0 then p.MoveLast()
            else
              let position, kvp = this.TryFindWithIndex(currentKey.Value, Lookup.LT)
              if position > 0 then
                index := position
                currentKey := kvp.Key
                currentValue := kvp.Value
                true
              else  // not found
                index := this.Count + 1
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>
                false
          else
            if index.Value >= 1 then
              index := index.Value - 1
              currentKey := this.GetKeyByIndex(index.Value)
              currentValue := this.values.[index.Value]
              true
            else
              index := this.Count + 1
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
            index := this.Count + 1
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
            index := this.Count + 1
            currentKey := Unchecked.defaultof<'K>
            currentValue := Unchecked.defaultof<'V>
            false
        finally
          exitLockIf syncRoot entered

      override p.MoveLast() = 
        let entered = enterLockIf syncRoot  isSynchronized
        try
          if this.size > 0 then
            index := this.Count - 1
            currentKey := this.GetKeyByIndex(index.Value)
            currentValue := this.values.[index.Value]
            true
          else
            index := this.Count + 1
            currentKey := Unchecked.defaultof<'K>
            currentValue := Unchecked.defaultof<'V>
            false
        finally
          exitLockIf syncRoot entered

      override p.CurrentKey with get() = currentKey.Value

      override p.CurrentValue with get() = currentValue.Value

      override p.Reset() = 
        pVersion := version // update state to new version
        index := -1
        currentKey := Unchecked.defaultof<'K>
        currentValue := Unchecked.defaultof<'V>

      override p.Dispose() = p.Reset()
    } :> ICursor<'K,'V>

  /// If size is less than 80% of capacity then reduce capacity to the size
  member this.TrimExcess() =
    if this.size < int(float(this.values.Length) * 0.8) then this.Capacity <- this.size

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
        value <- this.GetValueByIndex(index)
        true
      else
        value <- Unchecked.defaultof<'V>
        false

  interface IReadOnlyOrderedMap<'K,'V> with
    member this.GetAsyncEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = this.GetCursor()
    member this.IsEmpty = this.size = 0
    member this.IsIndexed with get() = false
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
    member this.Keys with get() = this.Keys :> IEnumerable<'K>
    member this.Values with get() = this.values :> IEnumerable<'V>
    member this.SyncRoot with get() = syncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Size with get() = int64(this.size)
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
//    member x.TryFindWithIndex(key: 'K, direction: Lookup, result: byref<KeyValuePair<'K,'V>>): int = 
//      this.TryFindWithIndex(key, direction, &result)

    
    
  interface IImmutableOrderedMap<'K,'V> with
    member this.Size with get() = int64(this.size)
    // Immutable addition, returns a new map with added value
    member this.Add(key, value):IImmutableOrderedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.Add(key, value)
      newMap :> IImmutableOrderedMap<'K,'V>
    member this.AddFirst(key, value):IImmutableOrderedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.AddFirst(key, value)
      newMap :> IImmutableOrderedMap<'K,'V>
    member this.AddLast(key, value):IImmutableOrderedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.AddLast(key, value)
      newMap :> IImmutableOrderedMap<'K,'V>
    member this.Remove(key):IImmutableOrderedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.Remove(key) |> ignore
      newMap :> IImmutableOrderedMap<'K,'V>
    member this.RemoveLast([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableOrderedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.RemoveLast(&value) |> ignore
      newMap :> IImmutableOrderedMap<'K,'V>
    member this.RemoveFirst([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableOrderedMap<'K,'V> =
      let newMap = this.Clone()
      newMap.RemoveFirst(&value) |> ignore
      newMap :> IImmutableOrderedMap<'K,'V>
    member this.RemoveMany(key:'K,direction:Lookup):IImmutableOrderedMap<'K,'V>=
      let newMap = this.Clone()
      newMap.RemoveMany(key, direction) |> ignore
      newMap :> IImmutableOrderedMap<'K,'V>

  //#endregion   

  //#region Constructors

  new() = SortedMap(None, None, None)
  new(comparer:IComparer<'K>) = SortedMap(None, None, Some(comparer))
  new(dictionary:IDictionary<'K,'V>) = SortedMap(Some(dictionary), Some(dictionary.Count), Some(Comparer<'K>.Default :> IComparer<'K>))
  new(capacity:int) = SortedMap(None, Some(capacity), Some(Comparer<'K>.Default :> IComparer<'K>))
  new(dictionary:IDictionary<'K,'V>,comparer:IComparer<'K>) = SortedMap(Some(dictionary), Some(dictionary.Count), Some(comparer))
  new(capacity:int,comparer:IComparer<'K>) = SortedMap(None, Some(capacity), Some(comparer))

  // TODO move size to end
  [<ObsoleteAttribute>]
  static member OfKeysAndValues(size:int, keys:'K[], values:'V[]) =
    let sm = new SortedMap<'K,'V>()
    sm.size <- size
    sm.keys <- keys
    sm.values <- values
    sm

  static member OfSortedKeysAndValues(keys:'K[], values:'V[], size:int) =
    if keys.Length < size then raise (new ArgumentException("Keys array is smaller than provided size"))
    if values.Length < size then raise (new ArgumentException("Values array is smaller than provided size"))
    let sm = new SortedMap<'K,'V>()
    let comparer = sm.Comparer
    for i in 1..keys.Length-1 do
      if comparer.Compare(keys.[i-1], keys.[i]) >= 0 then raise (new ArgumentException("Keys are not sorted"))
    sm.size <- size
    sm.keys <- keys
    sm.values <- values
    sm

  static member OfSortedKeysAndValues(keys:'K[], values:'V[]) =
    if keys.Length <> values.Length then raise (new ArgumentException("Keys and values arrays are of different sizes"))
    SortedMap.OfSortedKeysAndValues(keys, values, keys.Length)



  static member internal KeysAreEqual(smA:SortedMap<'K,_>,smB:SortedMap<'K,_>) =
    if not (smA.size = smB.size) then false
    elif smA.IsRegular && smB.IsRegular 
      && smA.Comparer.Equals(smB.Comparer) // TODO test custom comparer equality
      && smA.Comparer.Compare(smA.keys.[0], smB.keys.[0]) = 0 then
      true
    else
      System.Linq.Enumerable.SequenceEqual(smA.keys, smB.keys)
      // TODO memcmp for key arrays from SpreadsDB, or unsafe implementation from SO
      // TODO make array equality a part of MathProvider
      //false
  //#endregion

