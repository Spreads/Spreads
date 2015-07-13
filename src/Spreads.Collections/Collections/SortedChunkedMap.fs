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

// TODO ensure that on every prevBucket change we check its version and 
// set to outer if versions differ
// TODO subscribe to update events on prevBucket and Flush at least every second

[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedChunkedMap<'K,'V when 'K : comparison>
  internal (outerFactory:IComparer<'K>->IOrderedMap<'K, SortedMap<'K,'V>>, comparer:IComparer<'K>, slicer:Func<'K,'K> opt, ?chunkMaxSize:int) as this =
  inherit Series<'K,'V>()
  // TODO serialize size, add a method to calculate size based on outerMap only
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
  let chunkUpperLimit : int = 
    if slicer.IsPresent then 0
    else
      if chunkMaxSize.IsSome then chunkMaxSize.Value
      else OptimizationSettings.SCMDefaultChunkLength
  // TODO (very low) replace outer with MapDeque, see comments in MapDeque.fs
  let outerMap = outerFactory(comparer)

  [<NonSerializedAttribute>]
  let slicer : IKeySlicer<'K> = 
    match slicer with
    | OptionalValue.Present s -> 
        { new IKeySlicer<'K> with
          member x.Hash(k) = s.Invoke k
        }
    | OptionalValue.Missing ->
      { new IKeySlicer<'K> with
          member x.Hash(k) =
            // on x64, limit for LOH is the same 85kb for float[] (e.g. 10000+)
            // on x86, float[1000+] will be in LOH, will need float buffer pool
            // for DT/(float|decimal) 1000 irregular values are 16kb, must be compressed to less than 4kb
            // (page size) but close to it, or a multiple (we lose all remainder to 4kb, so big chunks are efficient for storage)
            // TODO measure typical compressed size of DT/(float|decimal) and adjust the upper limit
            // TODO measure when Yeppp et al. performance on batches stops increasing with batch size increase
            match outerMap with
      //      | :? SortedMap<'K, SortedMap<'K,'V>> as sm -> 
      //        failwith "not implemented: here will be optimized lookup"
            | _ as om ->
            // Fake comparer
            // For each key, lookup LE outer key. If the bucket with this key has size < UPPER, add 
            // new values to this bucket. Else create a new bucket.
              if om.IsEmpty then k
              else
                let ok,kvp = om.TryFind(k, Lookup.LE)
                if ok then
                  // k is larger than the last key and the chunk is big enough
                  Debug.Assert(kvp.Value.size > 0)
                  if comparer.Compare(k,kvp.Value.Last.Key) > 0 && kvp.Value.size >= chunkUpperLimit then k
                  else kvp.Value.keys.[0]
                else k
      }

  [<OnDeserialized>]
  member private this.Init(context:StreamingContext) =
      prevHash <- Unchecked.defaultof<'K>
      prevBucket <- Unchecked.defaultof<SortedMap<'K,'V>>
      prevBucketIsSet  <- false

  member this.Clear() = outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)

  member this.Count
      with get() = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          let mutable size' = 0L
          for okvp in outerMap do
            size' <- size' + (int64 okvp.Value.size)
          size <- size'
          size'
        finally
          exitLockIf this.SyncRoot entered

  member this.IsEmpty
      with get() =
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          outerMap.IsEmpty 
          || not (outerMap.Values |> Seq.exists (fun inner -> inner <> null && inner.size > 0))
        finally
          exitLockIf this.SyncRoot entered

  member internal this.IsSynchronized with get() = isSync and set v = isSync <- v

  member internal this.SyncRoot with get() = outerMap.SyncRoot

  // TODO! there must be a smarter locking strategy at buckets level (instead of syncRoot)
  // 
  member this.Item 
    with get key =
      let hash = slicer.Hash(key)
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
      let hash = slicer.Hash(key)
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
            //prevBucket.Capacity <- prevBucket.Count // trim excess, save changes to modified bucket
            outerMap.[prevHash] <- prevBucket // will store bucket if outer is persistent
          let bucket = 
            let ok, bucketKvp = outerMap.TryFind(hash, Lookup.EQ)
            if ok then 
              bucketKvp.Value
            else
              // TODO (No, BS) in a normal flow, we add a new bucket when a previous one 
              // is full. For regular UnitPeriods (all but ticks) the buckets
              // should be equal in most cases
              // outerMap.Count could be VERY slow, do not do this
              let averageSize = 4L //try size / (int64 outerMap.Count) with | _ -> 4L // 4L in default
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

  // Ensure than current inner map is saved (set) to the outer map
  member this.Flush() =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      if prevBucketIsSet then
        //&& outerMap.[prevHash].Version <> prevBucket.Version then
          //prevBucket.Capacity <- prevBucket.Count // trim excess, save changes to modified bucket
          outerMap.[prevHash] <- prevBucket
    finally
      exitLockIf this.SyncRoot entered

  override x.Finalize() = ()
    // no locking, no-one has a reference to this
//    if prevBucketIsSet &&
//        outerMap.[prevHash].Version <> prevBucket.Version then
//          //prevBucket.Capacity <- prevBucket.Count // trim excess, save changes to modified bucket
//          outerMap.[prevHash] <- prevBucket
    

  override this.GetCursor() : ICursor<'K,'V> =
    this.GetCursor(outerMap.GetCursor(), Unchecked.defaultof<ICursor<'K, 'V>>, true, Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V>>, false)

  member private this.GetCursor(outer:ICursor<'K,SortedMap<'K,'V>>, inner:ICursor<'K,'V>, isReset:bool,currentBatch:IReadOnlyOrderedMap<'K,'V>, isBatch:bool) : ICursor<'K,'V> =
    // TODO
    let nextBatch : Task<IReadOnlyOrderedMap<'K,'V>> ref = ref Unchecked.defaultof<Task<IReadOnlyOrderedMap<'K,'V>>>
    
    let outer = ref outer
    outer.Value.MoveFirst() |> ignore // otherwise initial move is skipped in MoveAt, isReset knows that we haven't started in SHM even when outer is started
    let inner = ref inner
    let isReset = ref isReset
    let currentBatch : IReadOnlyOrderedMap<'K,'V> ref = ref currentBatch
    let isBatch = ref isBatch

    // TODO use inner directly
//    let currentKey : 'K ref = ref inner.Value.CurrentKey // Unchecked.defaultof<'K>
//    let currentValue : 'V ref = ref inner.Value.CurrentValue // Unchecked.defaultof<'V>

    { new MapCursor<'K,'V>(this) with
      override c.Clone() = this.GetCursor(outer.Value.Clone(), inner.Value.Clone(), !isReset, !currentBatch, !isBatch)
      override c.IsBatch with get() = !isBatch
      override c.Current 
        with get() = 
          if !isBatch then invalidOp "Current move is MoveNextBatxhAsync, cannot return a single valule"
          else inner.Value.Current
      override p.MoveNext() = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          if isReset.Value then p.MoveFirst()
          else
            let res = inner.Value.MoveNext() // could pass curent key by ref and save some single-dig %
            if res then
              if !isBatch then isBatch := false
              true
            else
              if outer.Value.MoveNext() then // go to the next bucket
                inner := outer.Value.CurrentValue.GetCursor()
                let res = inner.Value.MoveFirst()
                if res then
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
              isBatch := false
              true
            else
              if outer.Value.MovePrevious() then // go to the previous bucket
                inner := outer.Value.CurrentValue.GetCursor()
                let res = inner.Value.MoveLast()
                if res then
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
          let newHash = slicer.Hash(key)
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

      override p.CurrentKey with get() = inner.Value.CurrentKey

      override p.CurrentValue with get() = inner.Value.CurrentValue

      override p.Reset() = 
        if not !isReset then
          outer.Value.Reset()
          outer.Value.MoveFirst() |> ignore
          inner.Value.Reset()
          inner := Unchecked.defaultof<ICursor<'K, 'V>> // outer.Value.CurrentValue.GetPointer()
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
                isReset := false
                return true
              else return false
            else
              if !isBatch then
                let! couldMove = outer.Value.MoveNext(ct) |> Async.AwaitTask
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
    } :> ICursor<'K,'V> 

  member this.TryFind(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      result <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        
      let hash = slicer.Hash(key)
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
    let hash = slicer.Hash(key)
    let subKey = key
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      // the most common scenario is to hit the previous bucket 
      if prevBucketIsSet && comparer.Compare(hash, prevHash) = 0 then
        prevBucket.Add(subKey, value)
        size <- size + 1L
      else
        if prevBucketIsSet then
          //prevBucket.Capacity <- prevBucket.Count // trim excess
          outerMap.[prevHash]<- prevBucket
        let bucket = 
          let ok, bucketKvp = outerMap.TryFind(hash, Lookup.EQ)
          if ok then 
            bucketKvp.Value.Add(subKey, value)
            bucketKvp.Value
          else
            let newSm = SortedMap(comparer)
            newSm.Add(subKey, value)
            outerMap.[hash]<- newSm
            newSm
        size <- size + 1L
        prevHash <- hash
        prevBucket <-  bucket
        prevBucketIsSet <- true
    finally
      exitLockIf this.SyncRoot entered

  // TODO add last to empty fails
  member this.AddLast(key, value):unit =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      let c =
        if outerMap.Count = 0L then 1
        else comparer.Compare(key, this.Last.Key)
      if c > 0 then
        this.Add(key, value)
      else raise (ArgumentOutOfRangeException("New key is smaller or equal to the largest existing key"))
    finally
      exitLockIf this.SyncRoot entered


  member this.AddFirst(key, value):unit =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      let c = 
        if outerMap.IsEmpty then -1
        else comparer.Compare(key, this.First.Key)
      if c < 0 then 
        this.Add(key, value)
      else raise (ArgumentOutOfRangeException("New key is larger or equal to the smallest existing key"))
    finally
      exitLockIf this.SyncRoot entered

    
  // do not reset prevBucket in any remove method

  // NB first/last optimization is possible, but removes are rare in the primary use case

  member this.Remove(key):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      let hash = slicer.Hash(key)
      let subKey = key          
      let c = comparer.Compare(hash, prevHash)
      if c = 0 && prevBucketIsSet then
        let res = prevBucket.Remove(subKey)
        if res then 
          size <- size - 1L
          if prevBucket.size = 0 then
            outerMap.Remove(prevHash) |> ignore
            prevBucketIsSet <- false
        res
      else
        if prevBucketIsSet then 
          //prevBucket.Capacity <- prevBucket.Count // trim excess 
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
            if prevBucket.size > 0 then
              outerMap.[prevHash]<- prevBucket
            else
              outerMap.Remove(prevHash) |> ignore
              prevBucketIsSet <- false
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
      if outerMap.Count = 0L then false
      else
        let removed =
          match direction with
          | Lookup.EQ -> 
            this.Remove(key)
          | Lookup.LT | Lookup.LE ->
            let hash = slicer.Hash(key)
            let subKey = key
            let hasPivot, pivot = this.TryFind(key, direction)
            if hasPivot then
              let r1 = outerMap.RemoveMany(hash, Lookup.LT)  // strictly LT
              let r2 = outerMap.First.Value.RemoveMany(subKey, direction) // same direction
              if r2 then
                if outerMap.First.Value.size > 0 then
                  outerMap.[outerMap.First.Key] <- outerMap.First.Value // Flush
                else 
                  outerMap.Remove(outerMap.First.Key) |> ignore
              r1 || r2
              // TODO Flush
            else 
              let c = comparer.Compare(key, this.Last.Key)
              if c > 0 then // remove all keys
                this.Clear()
                true
              elif c = 0 then raise (ApplicationException("Impossible condition when hasPivot is false"))
              else false
          | Lookup.GT | Lookup.GE ->
            let hash = slicer.Hash(key)
            let subKey = key
            let hasPivot, pivot = this.TryFind(key, direction)
            if hasPivot then
              if comparer.Compare(key, hash) = 0 && direction = Lookup.GE then
                outerMap.RemoveMany(hash, Lookup.GE) // remove in one go
              else
                let r1 = outerMap.RemoveMany(hash, Lookup.GT)  // strictly GT
                let lastChunk = outerMap.Last.Value
                let r2 = lastChunk.RemoveMany(subKey, direction) // same direction
                outerMap.[outerMap.Last.Key] <- lastChunk // Flush
                r1 || r2
            else 
              let c = comparer.Compare(key, this.First.Key)
              if c < 0 then // remove all keys
                this.Clear()
              elif c = 0 then raise (ApplicationException("Impossible condition when hasPivot is false"))
              else false
          | _ -> failwith "wrong direction"
        if removed then // we have Flushed, when needed for partial bucket change, above - just invalidate cache
          prevBucketIsSet <- false
          prevBucket <- Unchecked.defaultof<_>
        removed
    finally
      exitLockIf this.SyncRoot entered

  member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>, option:AppendOption) : int =
    let hasEqOverlap (old:IReadOnlyOrderedMap<'K,'V>) (append:IReadOnlyOrderedMap<'K,'V>) : bool =
      if comparer.Compare(append.First.Key, old.Last.Key) > 0 then false
      else
        let oldC = old.GetCursor()
        let appC = append.GetCursor();
        let mutable cont = true
        let mutable overlapOk = 
          oldC.MoveAt(append.First.Key, Lookup.EQ) 
            && appC.MoveFirst() 
            && oldC.CurrentKey = appC.CurrentKey
            && Unchecked.equals oldC.CurrentValue appC.CurrentValue
        while overlapOk && cont do
          if oldC.MoveNext() then
            overlapOk <-
              appC.MoveNext() 
              && oldC.CurrentKey = appC.CurrentKey
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
            Debug.Assert(removed)
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
        this.Flush()
        exitLockIf this.SyncRoot entered
      

  //#region Interfaces

  interface IEnumerable with
    member this.GetEnumerator() = this.GetCursor() :> IEnumerator

  interface IEnumerable<KeyValuePair<'K,'V>> with
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
      this.GetCursor() :> IEnumerator<KeyValuePair<'K,'V>>
   

  interface IReadOnlyOrderedMap<'K,'V> with
    member this.Comparer with get() = comparer
    member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.GetCursor() = this.GetCursor()
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
    member this.Count with get() = this.Count
    member this.Item
      with get k = this.Item(k) 
      and set (k:'K) (v:'V) = this.[k] <- v
    
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
    member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>, option:AppendOption) = this.Append(appendMap, option)
  //#endregion

  // x0
  
  new() = 
    let comparer:IComparer<'K> = Comparer<'K>.Default :> IComparer<'K>
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, OptionalValue.Missing)
  
  // x1

  /// In-memory sorted chunked map
  new(comparer:IComparer<'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, OptionalValue.Missing)
  
  /// In-memory sorted chunked map
  new(slicer:Func<'K,'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    let comparer:IComparer<'K> = Comparer<'K>.Default :> IComparer<'K>
    SortedChunkedMap(factory, comparer, OptionalValue(slicer))
  new(chunkMaxSize:int) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    let comparer:IComparer<'K> = Comparer<'K>.Default :> IComparer<'K>
    SortedChunkedMap(factory, comparer, OptionalValue.Missing, chunkMaxSize)

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>) = 
    let comparer:IComparer<'K> = Comparer<'K>.Default :> IComparer<'K>
    SortedChunkedMap(outerFactory.Invoke, comparer, OptionalValue.Missing)
  
  // x2

  /// In-memory sorted chunked map
  new(comparer:IComparer<'K>,slicer:Func<'K,'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, OptionalValue(slicer))
  new(comparer:IComparer<'K>,chunkMaxSize:int) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, OptionalValue.Missing, chunkMaxSize)

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>) = 
    SortedChunkedMap(outerFactory.Invoke, comparer, OptionalValue.Missing)

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,slicer:Func<'K,'K>) = 
    let comparer:IComparer<'K> = Comparer<'K>.Default :> IComparer<'K>
    SortedChunkedMap(outerFactory.Invoke, comparer, OptionalValue(slicer))
  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,chunkMaxSize:int) = 
    let comparer:IComparer<'K> = Comparer<'K>.Default :> IComparer<'K>
    SortedChunkedMap(outerFactory.Invoke, comparer, OptionalValue.Missing, chunkMaxSize)

  // x3

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>,slicer:Func<'K,'K>) = 
    SortedChunkedMap(outerFactory.Invoke, comparer, OptionalValue(slicer))
  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>,chunkMaxSize:int) = 
    SortedChunkedMap(outerFactory.Invoke, comparer, OptionalValue.Missing, chunkMaxSize)