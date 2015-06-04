namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Runtime.InteropServices
open System.Runtime.Serialization
open System.Threading
open System.Threading.Tasks
open System.Diagnostics

open Spreads
open Spreads.Collections

// TODO resolve internal IKeyComparer<'K> by type 'K, if it is not available, 
// outerFactory could produce any kind of sorted map. Outer values are set on bucket switch
// We only use .Hash of IKeyComparer

// TODO implement in C#

[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedChunkedMap<'K,'V when 'K : comparison>
  internal(outerFactory:IComparer<'K>->IOrderedMap<'K, SortedMap<'K,'V>>, c:IComparer<'K>) =
  
  [<NonSerializedAttribute>]
  let mutable size = 0L
  [<NonSerializedAttribute>]
  let mutable prevHash  = Unchecked.defaultof<'K>
  [<NonSerializedAttribute>]
  let mutable prevBucket = Unchecked.defaultof<SortedMap<'K,'V>>
  [<NonSerializedAttribute>]
  let mutable prevBucketIsSet  = false
  [<NonSerializedAttribute>]
  let mutable isSync  = false

  [<NonSerializedAttribute>]
  let comparer : IKeyComparer<'K> = c :?> IKeyComparer<'K>
    // TODO logic for fake comparer
    //KeyComparer.GetDefault<'K>()

  // TODO replace outer with MapDeque, see comments in MapDeque.fs
  let outerMap = outerFactory(comparer)


  [<OnDeserialized>]
  member private this.Init(context:StreamingContext) =
      prevHash <- Unchecked.defaultof<'K>
      prevBucket <- Unchecked.defaultof<SortedMap<'K,'V>>
      prevBucketIsSet  <- false

  member this.Clear() = outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)

  member this.Count
      with get() = size

  member this.IsEmpty
      with get() = 
          outerMap.IsEmpty 
          || not (outerMap.Values |> Seq.exists (fun inner -> inner <> null && inner.size > 0))

  member internal this.IsSynchronized with get() = isSync and set v = isSync <- v

  member internal this.SyncRoot with get() = outerMap.SyncRoot

  // TODO! there must be a smarter locking strategy at buckets level (instead of syncRoot)
  // 
  member this.Item 
    with get key =
      let hash = comparer.Hash(key)
      let subKey = key
      let entered = enterLockIf this.SyncRoot this.IsSynchronized
      try
        let c = comparer.Compare(hash, prevHash)
        if c = 0 && prevBucketIsSet then
          prevBucket.[subKey] // this could raise keynotfound exeption
        else
          let bucket =
            let ok, bucketKvp = outerMap.TryFind(hash, Lookup.EQ)
            if ok then
              bucketKvp.Value
            else
              raise (KeyNotFoundException())
          prevHash <- hash
          prevBucket <- bucket
          prevBucketIsSet <- true
          bucket.[subKey] // this could raise keynotfound exeption
      finally
          exitLockIf this.SyncRoot entered
    and set key value =
      let hash = comparer.Hash(key)
      let subKey = key
      let entered = enterLockIf this.SyncRoot this.IsSynchronized
      try
        let c = comparer.Compare(hash, prevHash)
        if c = 0 && prevBucketIsSet then // avoid generic equality and null compare
          let s1 = prevBucket.size
          prevBucket.[subKey] <- value
          let s2 = prevBucket.size
          size <- size + int64(s2 - s1)
        else
          if prevBucketIsSet then
            prevBucket.Capacity <- prevBucket.Count // trim excess, save changes to modified bucket
            outerMap.[prevHash] <- prevBucket // will store bucket if outer is persistent
          let bucket = 
            let ok, bucketKvp = outerMap.TryFind(hash, Lookup.EQ)
            if ok then 
              bucketKvp.Value
            else
              // TODO (!) in a normal flow, we add a new bucket when a previous one 
              // is full. For regular UnitPeriods (all but ticks) the buckets
              // should be equal in most cases
              let averageSize = try size / (int64 outerMap.Size) with | _ -> 4L // 4L in default
              let newSm = SortedMap(int averageSize, comparer)
              //outerMap.[hash] <- newSm do not store on every update, 
              newSm
          let s1 = bucket.size
          bucket.[subKey] <- value
          let s2 = bucket.size
          size <- size + int64(s2 - s1)
          prevHash <- hash
          prevBucket <- bucket
          prevBucketIsSet <- true
      finally
          exitLockIf this.SyncRoot entered

    
  member this.First
    with get() = 
      let entered = enterLockIf this.SyncRoot this.IsSynchronized
      try
        if this.IsEmpty then raise (InvalidOperationException("Could not get the first element of an empty map"))
        let bucket = outerMap.First
        bucket.Value.First
      finally
          exitLockIf this.SyncRoot entered

  member this.Last
    with get() = 
      let entered = enterLockIf this.SyncRoot this.IsSynchronized
      try
        if this.IsEmpty then raise (InvalidOperationException("Could not get the first element of an empty map"))
        let bucket = outerMap.Last
        bucket.Value.Last
      finally
          exitLockIf this.SyncRoot entered

  member private this.GetROOMCursor() : BaseCursor<'K,'V> =
    let outer = ref (outerMap.GetCursor())
    outer.Value.MoveFirst() |> ignore // otherwise initial move is skipped in MoveAt, isReset knows that we haven't started in SHM even when outer is started
    let inner = ref Unchecked.defaultof<BaseCursor<'K, 'V>> // ref (outer.Value.CurrentValue.GetPointer())
    // TODO (perf) pointers must own previous idx/bucket, SHM methods must
    // use *thread local* pointers for all read operations. Currently many readers will
    // conflict by rewriting prevIdx/bucket.

    let currentKey : 'K ref = ref Unchecked.defaultof<'K>
    let currentValue : 'V ref = ref Unchecked.defaultof<'V>
    let isReset = ref true
    let currentBatch : IReadOnlyOrderedMap<'K,'V> ref = ref Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V>>
    let isBatch = ref false
    //let isOuterStarted = ref false


    { new BaseCursor<'K,'V>(this) with
      override c.Current with get() = 
        if !isBatch then invalidOp "Current move is MoveNextBatxhAsync, cannot return a single valule"
        else KeyValuePair(currentKey.Value, currentValue.Value)
      override p.MoveNext() = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          if isReset.Value then p.MoveFirst()
          else
            let res = inner.Value.MoveNext() // could pass curent key by ref and save some single-dig %
            if res then
              currentKey := inner.Value.CurrentKey
              currentValue := inner.Value.CurrentValue
              isBatch := false
              true
            else
              if outer.Value.MoveNext() then // go to the next bucket
                inner := outer.Value.CurrentValue.GetCursor()
                let res = inner.Value.MoveFirst()
                if res then
                  currentKey := inner.Value.CurrentKey
                  currentValue := inner.Value.CurrentValue
                  isBatch := false
                  true
                else
                  raise (ApplicationException("Unexpected - empty bucket")) 
              else
                false
        finally
          exitLockIf this.SyncRoot entered

      override p.MovePrevious() = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          if isReset.Value then p.MoveLast()
          else
            let res = inner.Value.MovePrevious()
            if res then
              currentKey := inner.Value.CurrentKey
              currentValue := inner.Value.CurrentValue
              isBatch := false
              true
            else
              if outer.Value.MovePrevious() then // go to the previous bucket
                inner := outer.Value.CurrentValue.GetCursor()
                let res = inner.Value.MoveLast()
                if res then
                  currentKey := inner.Value.CurrentKey
                  currentValue := inner.Value.CurrentValue
                  isBatch := false
                  true
                else
                  raise (ApplicationException("Unexpected - empty bucket")) 
              else
                false
        finally
          exitLockIf this.SyncRoot entered

      override p.MoveAt(key:'K, direction:Lookup) = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          let newHash = comparer.Hash(key)
          let newSubIdx = key
          let c = comparer.Compare(newHash, outer.Value.CurrentKey)
          let res =
            if c <> 0 || !isReset then // not in the current bucket, switch bucket
              if outer.Value.MoveAt(newHash, Lookup.EQ) then // Equal!
                inner := outer.Value.CurrentValue.GetCursor()
                inner.Value.MoveAt(newSubIdx, direction)
              else
                false
            else
              inner.Value.MoveAt(newSubIdx, direction)
                   
          isReset := false
                    
          if res then
            currentKey := inner.Value.CurrentKey
            currentValue := inner.Value.CurrentValue
            isBatch := false
            true
          else
              match direction with
              | Lookup.LT | Lookup.LE ->
                // look into previous bucket
                if outer.Value.MovePrevious() then
                  inner := outer.Value.CurrentValue.GetCursor()
                  let res = inner.Value.MoveAt(newSubIdx, direction)
                  if res then
                    currentKey := inner.Value.CurrentKey
                    currentValue := inner.Value.CurrentValue
                    isBatch := false
                    true
                  else
                    p.Reset()
                    false
                else
                  p.Reset()
                  false 
              | Lookup.GT | Lookup.GE ->
                // look into next bucket
                if outer.Value.MoveNext() then
                  inner := outer.Value.CurrentValue.GetCursor()
                  let res = inner.Value.MoveAt(newSubIdx, direction)
                  if res then
                    currentKey := inner.Value.CurrentKey
                    currentValue := inner.Value.CurrentValue
                    isBatch := false
                    true
                  else
                    p.Reset()
                    false 
                else
                  p.Reset()
                  false 
              | _ -> false // LookupDirection.EQ
        finally
          exitLockIf this.SyncRoot entered

      override p.MoveFirst() = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          if this.IsEmpty then false
          else p.MoveAt(this.First.Key, Lookup.EQ)
        finally
          exitLockIf this.SyncRoot entered

      override p.MoveLast() = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          if this.IsEmpty then false
          else p.MoveAt(this.Last.Key, Lookup.EQ)
        finally
          exitLockIf this.SyncRoot entered

      override p.CurrentKey with get() = currentKey.Value

      override p.CurrentValue with get() = currentValue.Value

      override p.Reset() = 
        outer.Value.Reset()
        outer.Value.MoveFirst() |> ignore
        inner := Unchecked.defaultof<BaseCursor<'K, 'V>> // outer.Value.CurrentValue.GetPointer()
        currentKey := Unchecked.defaultof<'K>
        currentValue := Unchecked.defaultof<'V>
        isReset := true

      override p.Dispose() = base.Dispose()

      override p.CurrentBatch = 
        if !isBatch then !currentBatch
        else invalidOp "Current move is single, cannot return a batch"

      override p.MoveNextBatchAsync(ct) =
        Async.StartAsTask(async {
          let entered = enterLockIf this.SyncRoot this.IsSynchronized
          try
            if isReset.Value then 
              if outer.Value.MoveFirst() then
                currentBatch := outer.Value.CurrentValue :> IReadOnlyOrderedMap<'K,'V>
                isBatch := true
                return true
              else return false
            else
              if !isBatch then
                let! couldMove = outer.Value.MoveNextAsync(ct) |> Async.AwaitTask
                if couldMove then
                  currentBatch := outer.Value.CurrentValue :> IReadOnlyOrderedMap<'K,'V>
                  isBatch := true
                  return true
                else return false
              else 
                return false
          finally
            exitLockIf this.SyncRoot entered
        }, TaskCreationOptions.None,CancellationToken.None)
    }

  member this.TryFind(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      result <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        
      let hash = comparer.Hash(key)
      let subKey = key
      let c = comparer.Compare(hash, prevHash)

      let res, pair =
        if c <> 0 || (not prevBucketIsSet) then // not in the prev bucket, switch bucket to newHash
          let ok, innerMapKvp = outerMap.TryFind(hash, Lookup.EQ) //.TryGetValue(newHash)
          if ok then
            prevHash <- hash
            prevBucket <- (innerMapKvp.Value)
            prevBucketIsSet <- true
            prevBucket.TryFind(subKey, direction)
          else
            false, Unchecked.defaultof<KeyValuePair<'K, 'V>>
        else
          // TODO null reference when called on empty
          prevBucket.TryFind(subKey, direction)

      if res then // found in the bucket of key
        result <- pair
        true
      else
        match direction with
        | Lookup.LT | Lookup.LE ->
          // look into previous bucket and take last
          let tf = outerMap.TryFind(hash, Lookup.LT)
          if (fst tf) then
            Trace.Assert(not (snd tf).Value.IsEmpty) // if previous was found it shoudn't be empty
            let pair = (snd tf).Value.Last
            result <- pair
            true
          else
            false
        | Lookup.GT | Lookup.GE ->
          // look into next bucket and take first
          let tf = outerMap.TryFind(hash, Lookup.GT)
          if (fst tf) then
            Trace.Assert(not (snd tf).Value.IsEmpty) // if previous was found it shoudn't be empty
            let pair = (snd tf).Value.First
            result <- pair
            true
          else
            false
        | _ -> false // LookupDirection.EQ
    finally
      exitLockIf this.SyncRoot entered

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
    try
      value <- this.Item(k)
      true
    with
    | :? KeyNotFoundException -> 
      value <- Unchecked.defaultof<'V>
      false

  member this.Add(key, value):unit =
    let hash = comparer.Hash(key)
    let subKey = key
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      // the most common scenario is to hit the previous bucket 
      if prevBucketIsSet && comparer.Compare(hash, prevHash) = 0 then
        prevBucket.Add(subKey, value)
        size <- size + 1L
      else
        if prevBucketIsSet then
          prevBucket.Capacity <- prevBucket.Count // trim excess
          outerMap.[prevHash]<- prevBucket
        let bucket = 
          let ok, bucketKvp = outerMap.TryFind(hash, Lookup.EQ)
          if ok then 
            bucketKvp.Value
          else
            let newSm = SortedMap(comparer)
            outerMap.[hash]<- newSm
            newSm
        bucket.Add(subKey, value)
        size <- size + 1L
        prevHash <- hash
        prevBucket <-  bucket
        prevBucketIsSet <- true
    finally
      exitLockIf this.SyncRoot entered

  // TODO add last to empty fails
  member this.AddLast(key, value):unit =
    let hash = comparer.Hash(key)
    let subKey = key
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      let c =
        if outerMap.Size = 0L then 1
        else comparer.Compare(hash, outerMap.Last.Key)
      if c = 0 then // last existing bucket
        if prevBucketIsSet && comparer.Compare(hash, prevHash) <> 0 then // switching from previous bucket
          prevBucket.Capacity <- prevBucket.Count // trim excess
          outerMap.[prevHash]<- prevBucket
        let sm = outerMap.Last.Value
        sm.AddLast(subKey, value)
        size <- size + 1L
        outerMap.[outerMap.Last.Key]<- sm
        prevHash <- hash
        prevBucket <-  outerMap.Last.Value
        prevBucketIsSet <- true
      elif c > 0 then // have to create new bucket for the value
        if prevBucketIsSet then
          prevBucket.Capacity <- prevBucket.Count // trim excess
          outerMap.[prevHash]<- prevBucket
        let bucket = 
            let newSm = SortedMap(comparer)
            outerMap.[hash]<-newSm
            newSm
        bucket.[subKey] <- value // the only value in the new bucket
        size <- size + 1L
        prevHash <- hash
        prevBucket <- bucket
        prevBucketIsSet <- true
      else raise (ArgumentOutOfRangeException("New key is smaller or equal to the largest existing key"))
    finally
      exitLockIf this.SyncRoot entered


  member this.AddFirst(key, value):unit =
    let hash = comparer.Hash(key)
    let subKey = key
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      let c = 
        if outerMap.IsEmpty then -1
        else comparer.Compare(hash, outerMap.First.Key) // avoid generic equality and null compare
      if c = 0 then // first existing bucket
        if prevBucketIsSet && comparer.Compare(hash, prevHash) <> 0 then // switching from previous bucket
          prevBucket.Capacity <- prevBucket.Count // trim excess
          outerMap.[prevHash]<- prevBucket
        let sm = outerMap.First.Value
        sm.AddFirst(subKey, value)
        size <- size + 1L
        outerMap.[outerMap.First.Key]<- sm
        prevHash <- hash
        prevBucket <-  outerMap.First.Value
        prevBucketIsSet <- true
      elif c < 0 then // have to create new bucket for the value
        let bucket = 
          let newSm = SortedMap(comparer)
          outerMap.[hash]<-newSm
          newSm
        bucket.[subKey] <- value // the only value in the new bucket
        size <- size + 1L
        prevHash <- hash
        prevBucket <- bucket
        prevBucketIsSet <- true
      else raise (ArgumentOutOfRangeException("New key is larger or equal to the smallest existing key"))
    finally
      exitLockIf this.SyncRoot entered

    
  // do not reset prevBucket in any remove method

  // NB first/last optimization is possible, but removes are rare in the primary use case

  member this.Remove(key):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      let hash = comparer.Hash(key)
      let subKey = key          
      let c = comparer.Compare(hash, prevHash)
      if c = 0 && prevBucketIsSet then
        let res = prevBucket.Remove(subKey)
        if res then size <- size - 1L
        res
      else
        if prevBucketIsSet then 
          prevBucket.Capacity <- prevBucket.Count // trim excess 
          outerMap.[prevHash]<- prevBucket
        let ok, innerMapKvp = outerMap.TryFind(hash, Lookup.EQ) //.TryGetValue(newHash)
        if ok then 
          let bucket = (innerMapKvp.Value)
          prevHash <- hash
          prevBucket <- bucket
          prevBucketIsSet <- true
          let res = bucket.Remove(subKey)
          if res then 
            size <- size - 1L
            outerMap.[prevHash]<- prevBucket
          res
        else
            false
    finally
      exitLockIf this.SyncRoot entered

  member this.RemoveFirst([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      result <- this.First
      let ret' = this.Remove(result.Key)
      if ret' then size <- size - 1L
      ret'
    finally
      exitLockIf this.SyncRoot entered


  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      result <- this.Last
      let ret' = this.Remove(result.Key)
      if ret' then size <- size - 1L
      ret'
    finally
      exitLockIf this.SyncRoot entered

  /// Removes all elements that are to `direction` from `key`
  member this.RemoveMany(key:'K,direction:Lookup):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      match direction with
      | Lookup.EQ -> 
        this.Remove(key)
      | Lookup.LT | Lookup.LE ->
        let hash = comparer.Hash(key)
        let subKey = key
        let hasPivot, pivot = this.TryFind(key, direction)
        if hasPivot then
          outerMap.RemoveMany(hash, Lookup.LT)  // strictly LT
          outerMap.First.Value.RemoveMany(subKey, direction) // same direction
        else 
          let c = comparer.Compare(key, this.Last.Key)
          if c > 0 then // remove all keys
            this.Clear()
            true
          elif c = 0 then raise (ApplicationException("Impossible condition when hasPivot is false"))
          else false
      | Lookup.GT | Lookup.GE ->
        let hash = comparer.Hash(key)
        let subKey = key
        let hasPivot, pivot = this.TryFind(key, direction)
        if hasPivot then
          outerMap.RemoveMany(hash, Lookup.GT)  // strictly GT
          outerMap.First.Value.RemoveMany(subKey, direction) // same direction
        else 
          let c = comparer.Compare(key, this.First.Key)
          if c < 0 then // remove all keys
            this.Clear()
            true
          elif c = 0 then raise (ApplicationException("Impossible condition when hasPivot is false"))
          else false
      | _ -> failwith "wrong direction"
    finally
      exitLockIf this.SyncRoot entered

  //#region Interfaces

  interface IEnumerable with
    member this.GetEnumerator() = this.GetROOMCursor() :> IEnumerator

  interface IEnumerable<KeyValuePair<'K,'V>> with
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
      this.GetROOMCursor() :> IEnumerator<KeyValuePair<'K,'V>>
   

  interface IReadOnlyOrderedMap<'K,'V> with
    member this.GetAsyncEnumerator() = this.GetROOMCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = this.GetROOMCursor() :> ICursor<'K,'V>
    member this.IsEmpty = this.IsEmpty
    member this.IsIndexed with get() = false
    //member this.Count with get() = size
    member this.First with get() = this.First
    member this.Last with get() = this.Last
    member this.TryFind(k:'K, direction:Lookup, [<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
      let tr = this.TryFind(k, direction)
      if (fst tr) then
        res <- snd tr
        true
      else
        false
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
      let success, v = this.TryGetValue(k)
      if success then
        value <- v
        true
      else false
    member this.Item with get k = this.Item(k)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)

    member this.SyncRoot with get() = this.SyncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Size with get() = int64(size)
    member this.Item
      with get k = this.Item(k) 
      and set (k:'K) (v:'V) = this.[k] <- v
    
    member this.Add(k, v) = this.Add(k,v)
    member this.AddLast(k, v) = this.AddLast(k, v)
    member this.AddFirst(k, v) = this.AddFirst(k, v)
    member this.Remove(k) = this.Remove(k)
    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      let rf = this.RemoveFirst()
      result <- snd rf
      ()
    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      let rl = this.RemoveLast()
      result <- snd rl
      ()
    member this.RemoveMany(key:'K,direction:Lookup) = 
      this.RemoveMany(key, direction) |> ignore
      ()
  //#endregion

  /// In-memory sorted chunked map
  new(comparer:IComparer<'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer)
    