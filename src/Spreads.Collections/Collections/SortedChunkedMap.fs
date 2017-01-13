// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics

open Spreads
open Spreads.Collections


// NB outer map version must be synced with SCM.version

[<AllowNullLiteral>]
type SortedChunkedMapGeneric<'K,'V,'TContainer when 'TContainer :> IOrderedMap<'K,'V>> 
  internal
  (
    outerFactory:IComparer<'K>->IOrderedMap<'K, 'TContainer>,
    innerFactory:int * IComparer<'K>->'TContainer, 
    comparer:IComparer<'K>,
    hasher:IKeyHasher<'K> option, 
    chunkMaxSize:int option) as this=
  inherit Series<'K,'V>()

  let outerMap = outerFactory(comparer)

  let isOuterPersistent, outerAsPersistent = match outerMap with | :? IPersistentObject as pm -> true, pm | _ -> false, Unchecked.defaultof<_>
  

  // TODO serialize size, add a method to calculate size based on outerMap only
//  let mutable size = 0L
  [<DefaultValueAttribute>]
  val mutable internal version : int64


  let mutable prevHash = Unchecked.defaultof<'K>
  let mutable prevBucket = WeakReference<IOrderedMap<'K,'V>>(null)
  let prevBucketIsSet (prevBucket':IOrderedMap<'K,'V> byref) : bool = 
    let hasValue = prevBucket.TryGetTarget(&prevBucket') 
    hasValue && prevBucket' <> null


  [<DefaultValueAttribute>] 
  val mutable isSynchronized : bool
  [<DefaultValueAttribute>] 
  val mutable isReadOnly : bool
  let ownerThreadId : int = Thread.CurrentThread.ManagedThreadId

  [<DefaultValueAttribute>] 
  val mutable internal orderVersion : int64
  [<DefaultValueAttribute>] 
  val mutable internal nextVersion : int64

  /// Non-zero only for the defaul slicing logic. When zero, we do not check for chunk size
  let chunkUpperLimit : int = 
    if hasher.IsSome then 0
    else
      if chunkMaxSize.IsSome && chunkMaxSize.Value > 0 then chunkMaxSize.Value
      else OptimizationSettings.SCMDefaultChunkLength
  
  let mutable id = String.Empty

  // used only when chunkUpperLimit = 0
  let hasher : IKeyHasher<'K> =
    match hasher with
    | Some h -> h
    | None -> Unchecked.defaultof<_>

  // hash for locating existing value
  let existingHash key =
    if chunkUpperLimit = 0 then hasher.Hash(key) 
    else
      //if outerCursor.MoveAt(key, Lookup.LE) then outerCursor.CurrentKey else Unchecked.defaultof<_>
      let mutable h = Unchecked.defaultof<_>
      outerMap.TryFind(key, Lookup.LE, &h) |> ignore
      h.Key

  do
    this.isSynchronized <- false
    this.version <- outerMap.Version
    this.nextVersion <- outerMap.Version



  member this.Clear() : unit =
    if not this.IsEmpty then 
      let removed = outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)
      if removed then outerMap.Version <- outerMap.Version + 1L

  member this.Comparer with get() = comparer

  member this.Count
      with get() = 
        let mutable entered = false
        try
          entered <- enterWriteLockIf &this.Locker this.isSynchronized
          let mutable size' = 0L
          for okvp in outerMap do
            size' <- size' + okvp.Value.Count
          //size <- size'
          size'
        finally
          exitWriteLockIf &this.Locker entered

  member internal this.OuterMap with get() = outerMap
  member internal this.ChunkUpperLimit with get() = chunkUpperLimit
  member internal this.Hasher with get() = hasher

  member this.Version 
    with get() = readLockIf &this.nextVersion &this.version true (fun _ -> this.version)
    and internal set v = 
      let mutable entered = false
      try
        entered <- enterWriteLockIf &this.Locker true
        this.version <- v // NB setter only for deserializer
        this.nextVersion <- v
      finally
        exitWriteLockIf &this.Locker entered

  member this.IsEmpty
      with get() =
        readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> outerMap.IsEmpty)
         

  member this.Complete() =
    let mutable entered = false
    try
      entered <- enterWriteLockIf &this.Locker true
      if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      if not this.isReadOnly then 
          this.isReadOnly <- true
          // immutable doesn't need sync
          this.isSynchronized <- false // TODO the same for SCM
          this.NotifyUpdateTcs()
    finally
      Interlocked.Increment(&this.version) |> ignore
      exitWriteLockIf &this.Locker entered

  override this.IsReadOnly with get() = readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> this.isReadOnly)
  override this.IsIndexed with get() = false

  member this.IsSynchronized 
    with get() = readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> Volatile.Read(&this.isSynchronized))
    and set(synced:bool) =
      let wasSynced = Volatile.Read(&this.isSynchronized)
      readLockIf &this.nextVersion &this.version wasSynced (fun _ ->
        // NB: multiple set of the same value is ok as long as all write method
        // read this.isSynchronized into a local variable before writing
        // (all write methods should use entered var and not touch this.isSynced after entering locks)
        Volatile.Write(&this.isSynchronized, synced)
        if synced && not wasSynced then this.nextVersion <- this.version
      )

  // TODO! there must be a smarter locking strategy at buckets level (instead of syncRoot)
  member this.Item
    // if hasher is set then for each key we have deterministic hash that does not depend on outer map at all
    with get key =
      readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ ->
        if chunkUpperLimit = 0 then // deterministic hash
          let hash = hasher.Hash(key)
          let c = comparer.Compare(hash, prevHash)
          let mutable prevBucket' = Unchecked.defaultof<_>
          if c = 0 && prevBucketIsSet(&prevBucket') then
            prevBucket'.[key] // this could raise keynotfound exeption
          else
            let bucket =
              let mutable bucketKvp = Unchecked.defaultof<_>
              let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
              if ok then
                bucketKvp.Value
              else
                raise (KeyNotFoundException())
            prevHash <- hash
            prevBucket.SetTarget(bucket)
            bucket.[key] // this could raise keynotfound exeption
        else
          // we are inside previous bucket, getter alway should try to get a value from there
          let mutable prevBucket' = Unchecked.defaultof<_>
          if prevBucketIsSet(&prevBucket') && comparer.Compare(key, prevHash) >= 0 && comparer.Compare(key, prevBucket'.Last.Key) <= 0 then
            prevBucket'.[key]
          else
            let mutable kvp = Unchecked.defaultof<_>
            let ok = outerMap.TryFind(key, Lookup.LE, &kvp)
            if ok then
              prevHash <- kvp.Key
              prevBucket.SetTarget(kvp.Value)
              kvp.Value.[key]
            else raise (KeyNotFoundException())
      )

    and set key value =
      let mutable entered = false
      try
        try ()
        finally
          entered <- enterWriteLockIf &this.Locker this.isSynchronized
          if entered then Interlocked.Increment(&this.nextVersion) |> ignore
            
        if chunkUpperLimit = 0 then // deterministic hash
          let hash = hasher.Hash(key)
          let c = comparer.Compare(hash, prevHash)
          let mutable prevBucket' = Unchecked.defaultof<_>
          if c = 0 && prevBucketIsSet(&prevBucket') then
            prevBucket'.[key] <- value
            outerMap.Version <- outerMap.Version + 1L
            this.NotifyUpdateTcs()
          else
            if prevBucketIsSet(&prevBucket') then this.FlushUnchecked()
            let isNew, bucket = 
              let mutable bucketKvp = Unchecked.defaultof<_>
              let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
              if ok then
                false, bucketKvp.Value
              else
                let newSm = innerFactory(4, comparer)
                true, newSm
            bucket.[key] <- value
            if isNew then
              outerMap.[hash] <- bucket
            else
              outerMap.Version <- outerMap.Version + 1L
            //let s2 = bucket.size
            //size <- size + int64(s2 - s1)
            this.NotifyUpdateTcs()
            prevHash <- hash
            prevBucket.SetTarget(bucket)
        else
          // we are inside previous bucket, setter has no choice but to set to this bucket regardless of its size
          let mutable prevBucket' = Unchecked.defaultof<_>
          if prevBucketIsSet(&prevBucket') && comparer.Compare(key, prevHash) >= 0 && comparer.Compare(key, prevBucket'.Last.Key) <= 0 then
            prevBucket'.[key] <- value
            outerMap.Version <- outerMap.Version + 1L
            this.NotifyUpdateTcs()
          else
            let mutable kvp = Unchecked.defaultof<_>
            let ok = outerMap.TryFind(key, Lookup.LE, &kvp)
            if ok &&
              // the second condition here is for the case when we add inside existing bucket, overflow above chunkUpperLimit is inevitable without a separate split logic (TODO?)
              (kvp.Value.Count < int64 chunkUpperLimit || comparer.Compare(key, kvp.Value.Last.Key) <= 0) then
              kvp.Value.[key] <- value
              prevHash <- kvp.Key
              prevBucket.SetTarget kvp.Value
              outerMap.Version <- outerMap.Version + 1L
              this.NotifyUpdateTcs()
            else
              if prevBucketIsSet(&prevBucket') then this.FlushUnchecked()
              // create a new bucket at key
              let newSm = innerFactory(4, comparer)
              newSm.[key] <- value
              #if PRERELEASE
              let v = outerMap.Version
              outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
              Trace.Assert(v + 1L = outerMap.Version, "Outer setter must increment its version")
              #else
              outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
              #endif
              prevHash <- key
              prevBucket.SetTarget newSm
              this.NotifyUpdateTcs()
      finally
        Interlocked.Increment(&this.version) |> ignore
        exitWriteLockIf &this.Locker entered
        #if PRERELEASE
        if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
        #else
        if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
        #endif

  
  member private this.FirstUnsafe
    with get() = 
      if outerMap.IsEmpty then 
        raise (InvalidOperationException("Could not get the first element of an empty map"))
      let bucket = outerMap.First
      bucket.Value.First
      
  member this.First
    with get() = 
      readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> this.FirstUnsafe)

  member private this.LastUnsafe
    with get() = 
      if outerMap.IsEmpty then raise (InvalidOperationException("Could not get the first element of an empty map"))
      let bucket = outerMap.Last
      bucket.Value.Last
      
  member this.Last
    with get() = 
      readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> this.LastUnsafe )

  member private this.FlushUnchecked() =
    let mutable temp = Unchecked.defaultof<_>
    if prevBucket.TryGetTarget(&temp) then
      prevBucket.SetTarget (null)
      temp <- null
    if isOuterPersistent then outerAsPersistent.Flush()

  // Ensure than current inner map is saved (set) to the outer map
  member this.Flush() =
    let mutable entered = false
    try
      entered <- enterWriteLockIf &this.Locker true
      this.FlushUnchecked()
    finally
      exitWriteLockIf &this.Locker entered

  member this.Dispose(disposing) =
    this.Flush()
    if disposing then GC.SuppressFinalize(this)

  override this.Finalize() = this.Dispose(false)

  override this.GetCursor() =
    if Thread.CurrentThread.ManagedThreadId <> ownerThreadId then this.IsSynchronized <- true // NB: via property with locks
    let mutable entered = false
    try
      entered <- enterWriteLockIf &this.Locker this.isSynchronized
      // if source is already read-only, MNA will always return false
      if this.isReadOnly then new SortedChunkedMapGenericCursor<_,_,_>(this) :> ICursor<'K,'V>
      else
        raise (NotImplementedException())
//        let c = new SortedChunkedMapGenericCursorAsync<_,_,_>(this)
//        c :> ICursor<'K,'V>
    finally
      exitWriteLockIf &this.Locker entered

  // .NETs foreach optimization
  member this.GetEnumerator() =
    if Thread.CurrentThread.ManagedThreadId <> ownerThreadId then 
      // NB: via property with locks
      this.IsSynchronized <- true
    readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ ->
      new SortedChunkedMapGenericCursor<_,_,_>(this)
    )

//  member private this.GetCursorOld(outer:ICursor<'K,'TContainer>, isReset:bool,currentBatch:IReadOnlyOrderedMap<'K,'V>, isBatch:bool) : ICursor<'K,'V> =
//    // TODO
//    let nextBatch : Task<IReadOnlyOrderedMap<'K,'V>> ref = ref Unchecked.defaultof<Task<IReadOnlyOrderedMap<'K,'V>>>
//    
//    let outer = ref outer
//    // Need to move, otherwise initial move is skipped in MoveAt, isReset knows that we haven't started in SCM even when outer is started
//    let mutable inner = if outer.Value.MoveFirst() then try outer.Value.CurrentValue.GetCursor() with | _ -> Unchecked.defaultof<_> else Unchecked.defaultof<_>
//    let isReset = ref isReset
//    let mutable currentBatch : IReadOnlyOrderedMap<'K,'V> = currentBatch
//    let isBatch = ref isBatch
//
//    let observerStarted = ref false
//    let mutable semaphore : SemaphoreSlim = Unchecked.defaultof<_>
//    let mutable are : AutoResetEvent =  Unchecked.defaultof<_>
//    let onNextHandler : OnNextHandler<'K,'V> = 
//      OnNextHandler(fun (kvp:KVP<'K,'V>) ->
//          if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
//      )
//    let onCompletedHandler : OnCompletedHandler = 
//      OnCompletedHandler(fun _ ->
//          if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
//      )
//
//    { new ICursor<'K,'V> with
//        member c.Comparer: IComparer<'K> = comparer
//        member c.Source with get() = this :> ISeries<_,_>
//        member c.IsContinuous with get() = false
//        member c.Clone() =
//          let clone = this.GetCursorOld(outer.Value.Clone(), !isReset, currentBatch, !isBatch)
//          if not !isReset then
//            let moved = clone.MoveAt(c.CurrentKey, Lookup.EQ)
//            if not moved then invalidOp "cannot correctly clone SCM cursor"
//          clone
//        
//        member x.MoveNext(ct: CancellationToken): Task<bool> = 
//          let rec completeTcs(tcs:AsyncTaskMethodBuilder<bool>, token: CancellationToken) : unit = // Task<bool> = 
//            if x.MoveNext() then
//              tcs.SetResult(true)
//            else
//              let semaphoreTask = semaphore.WaitAsync(-1, token)
//              let awaiter = semaphoreTask.GetAwaiter()
//              awaiter.UnsafeOnCompleted(fun _ ->
//                match semaphoreTask.Status with
//                | TaskStatus.RanToCompletion -> 
//                  if x.MoveNext() then tcs.SetResult(true)
//                  elif this.IsReadOnly then tcs.SetResult(false)
//                  else completeTcs(tcs, token)
//                | _ -> failwith "TODO process all task results"
//                ()
//              )
//          match x.MoveNext() with
//          | true -> trueTask            
//          | false ->
//            match this.IsReadOnly with
//            | false ->
//              let upd = this :> IObservableEvents<'K,'V>
//              if not !observerStarted then
//                semaphore <- new SemaphoreSlim(0,Int32.MaxValue)
//                this.onNextEvent.Publish.AddHandler onNextHandler
//                this.onCompletedEvent.Publish.AddHandler onCompletedHandler
//                observerStarted := true
//              let tcs = Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create()
//              let returnTask = tcs.Task
//              completeTcs(tcs, ct)
//              returnTask
//            | _ -> falseTask
//
//        member p.MovePrevious() = 
//          let entered = enterLockIf this.SyncRoot this.IsSynchronized
//          try
//            if isReset.Value then p.MoveLast()
//            else
//              let res = inner.MovePrevious()
//              if res then
//                isBatch := false
//                true
//              else
//                if outer.Value.MovePrevious() then // go to the previous bucket
//                  inner <- outer.Value.CurrentValue.GetCursor()
//                  let res = inner.MoveLast()
//                  if res then
//                    isBatch := false
//                    true
//                  else
//                    raise (ApplicationException("Unexpected - empty bucket")) 
//                else
//                  false
//          finally
//            exitLockIf this.SyncRoot entered
//
//        // TODO (!) review the entire method for edge cases
//        member p.MoveAt(key:'K, direction:Lookup) = 
//          let entered = enterLockIf this.SyncRoot this.IsSynchronized
//          try
//            let res =
//              if chunkUpperLimit = 0 then
//                let hash = hasher.Hash(key)
//                let c = 
//                  if isReset.Value then 2 // <> 0 below
//                  else comparer.Compare(hash, outer.Value.CurrentKey)
//              
//                if c <> 0 then // not in the current bucket, switch bucket
//                  if outer.Value.MoveAt(hash, Lookup.EQ) then // Equal!
//                    inner <- outer.Value.CurrentValue.GetCursor()
//                    inner.MoveAt(key, direction)
//                  else
//                    false
//                else
//                  inner.MoveAt(key, direction)
//              else
//                if outer.Value.MoveAt(key, Lookup.LE) then // LE!
//                  inner <- outer.Value.CurrentValue.GetCursor()
//                  inner.MoveAt(key, direction)
//                else
//                  false
//
//            if res then
//              isReset := false
//              isBatch := false
//              true
//            else
//                match direction with
//                | Lookup.LT | Lookup.LE ->
//                  // look into previous bucket
//                  if outer.Value.MovePrevious() then
//                    inner <- outer.Value.CurrentValue.GetCursor()
//                    let res = inner.MoveAt(key, direction)
//                    if res then
//                      isBatch := false
//                      isReset := false
//                      true
//                    else
//                      p.Reset()
//                      false
//                  else
//                    p.Reset()
//                    false
//                | Lookup.GT | Lookup.GE ->
//                  // look into next bucket
//                  let moved = outer.Value.MoveNext() 
//                  if moved then
//                    inner <- outer.Value.CurrentValue.GetCursor()
//                    let res = inner.MoveAt(key, direction)
//                    if res then
//                      isBatch := false
//                      isReset := false
//                      true
//                    else
//                      p.Reset()
//                      false 
//                  else
//                    p.Reset()
//                    false 
//                | _ -> false // LookupDirection.EQ
//          finally
//            exitLockIf this.SyncRoot entered
//
//        member p.MoveFirst() = 
//          let entered = enterLockIf this.SyncRoot this.IsSynchronized
//          try
//            if this.IsEmpty then false
//            else try p.MoveAt(this.First.Key, Lookup.EQ) with | _ -> false
//          finally
//            exitLockIf this.SyncRoot entered
//
//        member p.MoveLast() = 
//          let entered = enterLockIf this.SyncRoot this.IsSynchronized
//          try
//            if this.IsEmpty then false
//            else p.MoveAt(this.Last.Key, Lookup.EQ)
//          finally
//            exitLockIf this.SyncRoot entered
//
//        // TODO (v.0.2+) We now require that a false move keeps cursors on the same key before unsuccessfull move
//        // Also, calling CurrentKey/Value must never throw, remove try/catch here to avoid muting error that should not happen
//        //
//        // (delete this) NB These are "undefined" when cursor is in invalid state, but they should not thow
//        // Try/catch adds almost no overhead, even compared to null check, in the normal case. Calling these properties before move is 
//        // an application error and should be logged or raise an assertion failure
//        member c.CurrentKey with get() = try inner.CurrentKey with | _ -> Unchecked.defaultof<_>
//        member c.CurrentValue with get() = try inner.CurrentValue with | _ -> Unchecked.defaultof<_>
//        //member c.Current with get() : obj = box c.Current
//        member c.TryGetValue(key: 'K, value: byref<'V>): bool = this.TryGetValue(key, &value)
//
//
//
//        member p.CurrentBatch = 
//          if !isBatch then currentBatch
//          else invalidOp "Current move is single, cannot return a batch"
//
//        member p.MoveNextBatch(ct) =
//          Async.StartAsTask(async {
//            let entered = enterLockIf this.SyncRoot this.IsSynchronized
//            try
//              if isReset.Value then 
//                if outer.Value.MoveFirst() then
//                  currentBatch <- outer.Value.CurrentValue :> IReadOnlyOrderedMap<'K,'V>
//                  isBatch := true
//                  isReset := false
//                  return true
//                else return false
//              else
//                if !isBatch then
//                  let couldMove = outer.Value.MoveNext() // ct |> Async.AwaitTask // NB not async move next!
//                  if couldMove then
//                    currentBatch <- outer.Value.CurrentValue :> IReadOnlyOrderedMap<'K,'V>
//                    isBatch := true
//                    return true
//                  else 
//                    // no batch, but place cursor at the end of the last batch so that move next won't get null reference exception
//                    inner <- outer.Value.CurrentValue.GetCursor()
//                    if not outer.Value.CurrentValue.IsEmpty then inner.MoveLast() |> ignore
//                    return false
//                else
//                  return false
//            finally
//              exitLockIf this.SyncRoot entered
//          }, TaskCreationOptions.None, ct)
//
//      interface IEnumerator<KVP<'K,'V>> with
//        member p.Reset() = 
//          if not !isReset then
//            outer.Value.Reset()
//            outer.Value.MoveFirst() |> ignore
//            //inner.Reset()
//            inner <- Unchecked.defaultof<_>
//            isReset := true
//
//        member p.Dispose() = 
//            p.Reset()
//            if !observerStarted then
//              this.onNextEvent.Publish.RemoveHandler(onNextHandler)
//              this.onCompletedEvent.Publish.RemoveHandler(onCompletedHandler)
//        
//        member p.MoveNext() = 
//          let entered = enterLockIf this.SyncRoot this.IsSynchronized
//          try
//            if isReset.Value then (p :?> ICursor<'K,'V>).MoveFirst()
//            else
//              let res = inner.MoveNext() // could pass curent key by ref and save some single-dig %
//              if res then
//                if !isBatch then isBatch := false
//                true
//              else
//                //let currentKey = inner.CurrentKey
//                if outer.Value.MoveNext() then // go to the next bucket
//                  inner <- outer.Value.CurrentValue.GetCursor()
//                  let res = inner.MoveFirst()
//                  if res then
//                    isBatch := false
//                    true
//                  else
//                    raise (ApplicationException("Unexpected - empty bucket")) 
//                else
//                  //p.MoveAt(currentKey, Lookup.GT)
//                  false
//          finally
//            exitLockIf this.SyncRoot entered
//
//        member c.Current 
//          with get() =
//            if !isBatch then invalidOp "Current move is MoveNextBatxhAsync, cannot return a single valule"
//            else inner.Current
//        member this.Current with get(): obj = this.Current :> obj
//    }
//
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.TryFindTuple(key:'K, direction:Lookup) = 
    let tupleResult =
      let mutable kvp = Unchecked.defaultof<KeyValuePair<'K, 'V>>
        
      let hash = existingHash key
      let c = comparer.Compare(hash, prevHash)

      let res, pair =
        let mutable prevBucket' = Unchecked.defaultof<_>
        if c <> 0 || (not <| prevBucketIsSet(&prevBucket')) then // not in the prev bucket, switch bucket to newHash
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFind(hash, Lookup.EQ, &innerMapKvp)
          if ok then
            prevHash <- hash
            prevBucket.SetTarget (innerMapKvp.Value)
            innerMapKvp.Value.TryFind(key, direction)
          else
            false, Unchecked.defaultof<KeyValuePair<'K, 'V>>
        else
          // TODO null reference when called on empty
          if prevBucketIsSet(&prevBucket') then
            prevBucket'.TryFind(key, direction)
          else false, Unchecked.defaultof<KeyValuePair<'K, 'V>>

      if res then // found in the bucket of key
        ValueTuple<_,_>(true, pair)
      else
        match direction with
        | Lookup.LT | Lookup.LE ->
          // look into previous bucket and take last
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFind(hash, Lookup.LT, &innerMapKvp)
          if ok then
            Trace.Assert(not innerMapKvp.Value.IsEmpty) // if previous was found it shoudn't be empty
            let pair = innerMapKvp.Value.Last
            ValueTuple<_,_>(true, pair)
          else
            ValueTuple<_,_>(false, kvp)
        | Lookup.GT | Lookup.GE ->
          // look into next bucket and take first
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFind(hash, Lookup.GT, &innerMapKvp)
          if ok then
            Trace.Assert(not innerMapKvp.Value.IsEmpty) // if previous was found it shoudn't be empty
            let pair = innerMapKvp.Value.First
            ValueTuple<_,_>(true, pair)
          else
            ValueTuple<_,_>(false, kvp)
        | _ -> ValueTuple<_,_>(false, kvp) // LookupDirection.EQ
    tupleResult

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.TryFindUnchecked(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let tupleResult = this.TryFindTuple(key, direction)
    result <- tupleResult.Value2
    tupleResult.Value1

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.TryFind(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let res() = this.TryFindTuple(key, direction)
    let tupleResult = readLockIf &this.nextVersion &this.version this.isSynchronized res
    result <- tupleResult.Value2
    tupleResult.Value1

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
    try
      res <- this.First
      true
    with
    | _ -> 
      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
      false
          
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]  
  member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
    try
      res <- this.Last
      true
    with
    | _ -> 
      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
      false
        
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.TryGetValue(k, [<Out>] value:byref<'V>) =
    let res() = 
      let mutable kvp = Unchecked.defaultof<_>
      let ok = this.TryFind(k, Lookup.EQ, &kvp)
      if ok then
        ValueTuple<_,_>(true, kvp.Value)
      else ValueTuple<_,_>(false, Unchecked.defaultof<'V>)
    let tupleResult = readLockIf &this.nextVersion &this.version this.isSynchronized res
    value <- tupleResult.Value2
    tupleResult.Value1

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.AddUnchecked(key, value):unit =
    if chunkUpperLimit = 0 then // deterministic hash
      let hash = hasher.Hash(key)
      // the most common scenario is to hit the previous bucket
      let mutable prevBucket' = Unchecked.defaultof<_>
      if prevBucketIsSet(&prevBucket') && comparer.Compare(hash, prevHash) = 0 then
        prevBucket'.Add(key, value)
        outerMap.Version <- outerMap.Version + 1L
        //size <- size + 1L
        this.NotifyUpdateTcs()
      else
        if prevBucketIsSet(&prevBucket') then this.FlushUnchecked()
        let bucket = 
          let mutable bucketKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
          if ok then 
            bucketKvp.Value.Add(key, value)
            bucketKvp.Value
          else
            let newSm = innerFactory(0,comparer)
            newSm.Add(key, value)
            outerMap.Version <- outerMap.Version - 1L
            outerMap.[hash]<- newSm
            newSm
        outerMap.Version <- outerMap.Version + 1L
        //size <- size + 1L
        this.NotifyUpdateTcs()
        prevHash <- hash
        prevBucket.SetTarget bucket
    else
      // we are inside previous bucket, setter has no choice but to set to this bucket regardless of its size
      let mutable prevBucket' = Unchecked.defaultof<_>
      if prevBucketIsSet(&prevBucket') && comparer.Compare(key, prevHash) >= 0 && comparer.Compare(key, prevBucket'.Last.Key) <= 0 then
        prevBucket'.Add(key,value)
        outerMap.Version <- outerMap.Version + 1L
        this.NotifyUpdateTcs()
      else
        let mutable kvp = Unchecked.defaultof<_>
        let ok = outerMap.TryFind(key, Lookup.LE, &kvp)
        if ok &&
          // the second condition here is for the case when we add inside existing bucket, overflow above chunkUpperLimit is inevitable without a separate split logic (TODO?)
          (kvp.Value.Count < int64 chunkUpperLimit || comparer.Compare(key, kvp.Value.Last.Key) <= 0) then
          kvp.Value.Add(key, value)
          prevHash <- kvp.Key
          prevBucket.SetTarget kvp.Value
          outerMap.Version <- outerMap.Version + 1L
          this.NotifyUpdateTcs()
        else
          let mutable prevBucket' = Unchecked.defaultof<_>
          if prevBucketIsSet(&prevBucket') then this.FlushUnchecked()
          // create a new bucket at key
          let newSm = innerFactory(4, comparer)
          newSm.[key] <- value
          #if PRERELEASE
          let v = outerMap.Version
          outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
          Trace.Assert(v + 1L = outerMap.Version, "Outer setter must increment its version")
          #else
          outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
          #endif
          prevHash <- key
          prevBucket.SetTarget newSm
          this.NotifyUpdateTcs()

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.Add(key, value):unit =
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      this.AddUnchecked(key, value)
    finally
      Interlocked.Increment(&this.version) |> ignore
      exitWriteLockIf &this.Locker entered
      #if PRERELEASE
      Trace.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
      #endif

  // TODO add last to empty fails
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.AddLast(key, value):unit =
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      
      let c =
        if outerMap.Count = 0L then 1
        else comparer.Compare(key, this.LastUnsafe.Key)
      if c > 0 then
        this.AddUnchecked(key, value)
      else
        let exn = OutOfOrderKeyException(this.LastUnsafe.Key, key, "New key is smaller or equal to the largest existing key")
        raise (exn)
    finally
      Interlocked.Increment(&this.version) |> ignore
      exitWriteLockIf &this.Locker entered
      #if PRERELEASE
      Trace.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
      #endif

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.AddFirst(key, value):unit =
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      
      let c = 
        if outerMap.IsEmpty then -1
        else comparer.Compare(key, this.FirstUnsafe.Key)
      if c < 0 then 
        this.AddUnchecked(key, value)
      else 
        let exn = OutOfOrderKeyException(this.LastUnsafe.Key, key, "New key is larger or equal to the smallest existing key")
        raise (exn)
    finally
      Interlocked.Increment(&this.version) |> ignore
      exitWriteLockIf &this.Locker entered
      #if PRERELEASE
      Trace.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
      #endif

    
  // do not reset prevBucket in any remove method

  // NB first/last optimization is possible, but removes are rare in the primary use case
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.RemoveUnchecked(key):bool =
    let hash = existingHash key
    let c = comparer.Compare(hash, prevHash)
    let mutable prevBucket' = Unchecked.defaultof<_>
    if c = 0 && prevBucketIsSet(&prevBucket') then
      let res = prevBucket'.Remove(key)
      if res then 
        outerMap.Version <- outerMap.Version + 1L
        //size <- size - 1L
        if prevBucket'.Count = 0L then
          outerMap.Remove(prevHash) |> ignore
          prevBucket.SetTarget null
      res
    else
      let mutable prevBucket' = Unchecked.defaultof<_>
      if prevBucketIsSet(&prevBucket') then this.FlushUnchecked()
      let mutable innerMapKvp = Unchecked.defaultof<_>
      let ok = outerMap.TryFind(hash, Lookup.EQ, &innerMapKvp)
      if ok then
        let bucket = (innerMapKvp.Value)
        prevHash <- hash
        prevBucket.SetTarget bucket
        let res = bucket.Remove(key)
        if res then
          outerMap.Version <- outerMap.Version + 1L
          //size <- size - 1L
          if prevBucket'.Count > 0L then
            outerMap.Version <- outerMap.Version - 1L
            outerMap.[prevHash] <- bucket
          else
            outerMap.Remove(prevHash) |> ignore
            prevBucket.SetTarget null
        res
      else
        false
     
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.Remove(key):bool =
    let mutable removed = false
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      removed <- this.RemoveUnchecked(key)
      removed
    finally
      this.NotifyUpdateTcs()
      if removed then Interlocked.Increment(&this.version) |> ignore
      else Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if PRERELEASE
      Trace.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
      #endif

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.RemoveFirst([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let mutable removed = false
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      
      result <- this.FirstUnsafe
      let ret' = this.Remove(result.Key)
      ret'
    finally
      this.NotifyUpdateTcs()
      if removed then Interlocked.Increment(&this.version) |> ignore
      else Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if PRERELEASE
      Trace.Assert(outerMap.Version = this.version) 
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
      #endif

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let mutable removed = false
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      
      result <- this.LastUnsafe
      let ret' = this.Remove(result.Key)
      ret'
    finally
      this.NotifyUpdateTcs()
      if removed then Interlocked.Increment(&this.version) |> ignore
      else Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if PRERELEASE
      Trace.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
      #endif

  /// Removes all elements that are to `direction` from `key`
  member this.RemoveMany(key:'K,direction:Lookup):bool =
    let mutable removed = false
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      
      let version = outerMap.Version
      let result = 
        if outerMap.Count = 0L then
          // TODO(!) trace assert that they are no set
          prevBucket.SetTarget null
          false
        else
          let removed =
            match direction with
            | Lookup.EQ -> 
              this.Remove(key)
            | Lookup.LT | Lookup.LE ->
              let hash = existingHash key
              let hasPivot, pivot = this.TryFindUnchecked(key, direction)
              if hasPivot then
                let r1 = outerMap.RemoveMany(hash, Lookup.LT)  // strictly LT
                if not outerMap.IsEmpty then
                  let r2 = outerMap.First.Value.RemoveMany(key, direction) // same direction
                  if r2 then
                    if outerMap.First.Value.Count > 0L then
                      outerMap.Version <- outerMap.Version - 1L
                      outerMap.[outerMap.First.Key] <- outerMap.First.Value // Flush
                    else
                      outerMap.Version <- outerMap.Version - 1L
                      outerMap.Remove(outerMap.First.Key) |> ignore
                  r1 || r2
                else r1
              else 
                let c = comparer.Compare(key, this.LastUnsafe.Key)
                if c > 0 then // remove all keys
                  outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)
                elif c = 0 then raise (ApplicationException("Impossible condition when hasPivot is false"))
                else false
            | Lookup.GT | Lookup.GE ->
              let hash = existingHash key
              let subKey = key
              let hasPivot, pivot = this.TryFindUnchecked(key, direction)
              if hasPivot then
                if comparer.Compare(key, hash) = 0 && direction = Lookup.GE then
                  outerMap.RemoveMany(hash, Lookup.GE) // remove in one go
                else
                  let r1 = outerMap.RemoveMany(hash, Lookup.GT)  // strictly GT
                  if not outerMap.IsEmpty then
                    let lastChunk = outerMap.Last.Value
                    let r2 = lastChunk.RemoveMany(subKey, direction) // same direction
                    if lastChunk.IsEmpty then
                      outerMap.Remove(outerMap.Last.Key) |> ignore
                      outerMap.Version <- outerMap.Version - 1L
                    else
                      outerMap.Version <- outerMap.Version - 1L
                      outerMap.[outerMap.Last.Key] <- lastChunk // Flush
                    r1 || r2
                  else r1
              else
                let c = comparer.Compare(key, this.FirstUnsafe.Key)
                if c < 0 then // remove all keys
                  outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)
                elif c = 0 then raise (ApplicationException("Impossible condition when hasPivot is false"))
                else false
            | _ -> failwith "wrong direction"
          // TODO return condition
          //if removed then // we have Flushed, when needed for partial bucket change, above - just invalidate cache
          prevBucket.SetTarget null
          removed
      if result then 
        outerMap.Version <- version + 1L
      else
        outerMap.Version <- version
      removed <- result
      result
    finally
      this.NotifyUpdateTcs()
      if removed then Interlocked.Increment(&this.version) |> ignore 
      else Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if PRERELEASE
      Trace.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
      #endif

  // TODO after checks, should form changed new chunks and use outer append method with rewrite
  // TODO atomic append with single version increase, now it is a sequence of remove/add mutations
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.Append(appendMap:IReadOnlyOrderedMap<'K,'V>, option:AppendOption) : int =
    let hasEqOverlap (old:SortedChunkedMapGeneric<_,_,_>) (append:IReadOnlyOrderedMap<'K,'V>) : bool =
      if comparer.Compare(append.First.Key, old.LastUnsafe.Key) > 0 then false
      else
        let oldC = new SortedChunkedMapGenericCursor<_,_,_>(old, false) :> ICursor<'K,'V>
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
      let mutable entered = false
      try
        try ()
        finally
          entered <- enterWriteLockIf &this.Locker this.isSynchronized
          if entered then Interlocked.Increment(&this.nextVersion) |> ignore

        match option with
        | AppendOption.ThrowOnOverlap ->
          if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
            let mutable c = 0
            for i in appendMap do
              c <- c + 1
              this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
            c
          else 
            let exn = SpreadsException("values overlap with existing")
            raise exn
        | AppendOption.DropOldOverlap ->
          if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
            let mutable c = 0
            for i in appendMap do
              c <- c + 1
              this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
            c
          else
            let removed = this.RemoveMany(appendMap.First.Key, Lookup.GE)
            Trace.Assert(removed)
            let mutable c = 0
            for i in appendMap do
              c <- c + 1
              this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
            c
        | AppendOption.IgnoreEqualOverlap ->
          if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
            let mutable c = 0
            for i in appendMap do
              c <- c + 1
              this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
            c
          else
            let isEqOverlap = hasEqOverlap this appendMap
            if isEqOverlap then
              let appC = appendMap.GetCursor();
              if appC.MoveAt(this.LastUnsafe.Key, Lookup.GT) then
                this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                let mutable c = 1
                while appC.MoveNext() do
                  this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                  c <- c + 1
                c
              else 0
            else 
              let exn = SpreadsException("overlapping values are not equal")
              raise exn
        | AppendOption.RequireEqualOverlap ->
          if outerMap.IsEmpty then
            let mutable c = 0
            for i in appendMap do
              c <- c + 1
              this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
            c
          elif comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
            let exn = SpreadsException("values do not overlap with existing")
            raise exn
          else
            let isEqOverlap = hasEqOverlap this appendMap
            if isEqOverlap then
              let appC = appendMap.GetCursor();
              if appC.MoveAt(this.LastUnsafe.Key, Lookup.GT) then
                this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                let mutable c = 1
                while appC.MoveNext() do
                  this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
                  c <- c + 1
                c
              else 0
            else 
              let exn = SpreadsException("overlapping values are not equal")
              raise exn
        | _ -> failwith "Unknown AppendOption"
      finally
        Interlocked.Increment(&this.version) |> ignore
        this.FlushUnchecked()
        exitWriteLockIf &this.Locker entered
        #if PRERELEASE
        Trace.Assert(outerMap.Version = this.version)
        if entered && this.version <> this.nextVersion then raise (ApplicationException("this.orderVersion <> this.nextVersion"))
        #else
        if entered && this.version <> this.nextVersion then Environment.FailFast("this.orderVersion <> this.nextVersion")
        #endif
    
  member this.Id with get() = id and set(newid) = id <- newid

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
    member this.IsReadOnly with get() = this.IsReadOnly
    member this.First with get() = this.First
    member this.Last with get() = this.Last
    member this.TryFind(k:'K, direction:Lookup, [<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
      let ok, value = this.TryFind(k, direction)
      if ok then
        res <- value
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
    member this.GetAt(idx:int) = this.Skip(Math.Max(0, idx-1)).First().Value
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)

    member this.SyncRoot with get() = this.SyncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Complete() = this.Complete()
    member this.Version with get() = this.Version and set v = this.Version <- v
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

  interface IPersistentOrderedMap<'K,'V> with
    member this.Flush() = this.Flush()
    member this.Dispose() = this.Dispose(true)
    member this.Id with get() = this.Id
  //#endregion

and
  public SortedChunkedMapGenericCursor<'K,'V,'TContainer when 'TContainer :> IOrderedMap<'K,'V>> =
    struct
      val mutable internal source : SortedChunkedMapGeneric<'K,'V,'TContainer>
      val mutable internal outerCursor : ICursor<'K,'TContainer>
      val mutable internal innerCursor : ICursor<'K,'V>
      val mutable internal isBatch : bool
      val mutable internal synced : bool
      new(source:SortedChunkedMapGeneric<'K,'V,'TContainer>, synced) = 
        { source = source; 
          outerCursor = source.OuterMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          isBatch = false;
          synced = synced;
        }
      new(source:SortedChunkedMapGeneric<'K,'V,'TContainer>) = 
        { source = source; 
          outerCursor = source.OuterMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          isBatch = false;
          synced = true;
        }
    end

    member private this.HasValidInner with get() = this.innerCursor <> Unchecked.defaultof<_>
    
    member this.CurrentKey with get() = this.innerCursor.CurrentKey
    member this.CurrentValue with get() = this.innerCursor.CurrentValue
    member this.Current with get() : KVP<'K,'V> = KeyValuePair(this.innerCursor.CurrentKey, this.innerCursor.CurrentValue)

    member this.Comparer: IComparer<'K> = this.source.Comparer
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.source.TryGetValue(key, &value)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNext() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          try
            if (not this.HasValidInner) && (not this.isBatch) then
              if this.outerCursor.MoveFirst() then
                newInner <- this.outerCursor.CurrentValue.GetCursor()
                newInner.MoveFirst()
              else false
            else
              if this.HasValidInner && newInner.MoveNext() then true
              else
                if this.outerCursor.MoveNext() then
                  this.isBatch <- false // if was batch, moved to the first element of the new batch
                  newInner <- this.outerCursor.CurrentValue.GetCursor()
                  newInner.MoveNext()
                else false
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
             raise (new OutOfOrderKeyException<'K>((if this.isBatch then this.outerCursor.CurrentValue.Last.Key else this.innerCursor.CurrentKey), "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion || not this.synced then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    member this.Source: ISeries<'K,'V> = this.source :> ISeries<'K,'V>      
    member this.IsContinuous with get() = false
    member this.CurrentBatch: ISeries<'K,'V> = 
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////

          if this.isBatch then this.outerCursor.CurrentValue :> ISeries<'K,'V>
          else raise (InvalidOperationException("SortedChunkedMapGenericCursor cursor is not at a batch position"))

        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      result

    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> =
      let mutable newIsBatch = this.isBatch

      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          // TODO test & review
          raise (NotImplementedException("TODO test & review"))
          try
            if (not this.HasValidInner || this.isBatch) && this.outerCursor.MoveNext() then
                newIsBatch <- true
                trueTask
            else falseTask
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
            Trace.Assert(this.isBatch)
            raise (new OutOfOrderKeyException<'K>(this.outerCursor.CurrentValue.Last.Key, "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion || not this.synced then doSpin <- false
          else sw.SpinOnce()
      if result.Result then
        this.isBatch <- newIsBatch
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MovePrevious() = 
      let mutable newInner = this.innerCursor

      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          try
            if this.isBatch then
              raise (NotImplementedException("TODO test & review"))
              this.isBatch <- false
              newInner <- this.outerCursor.CurrentValue.GetCursor()
              newInner.MoveLast() |> ignore

            if not this.HasValidInner then
              if this.outerCursor.MoveLast() then
                newInner <- this.outerCursor.CurrentValue.GetCursor()
                newInner.MoveLast()
              else false
            else
              if this.HasValidInner && newInner.MovePrevious() then true
              else
                if this.outerCursor.MovePrevious() then
                  this.isBatch <- false
                  newInner <- this.outerCursor.CurrentValue.GetCursor()
                  newInner.MovePrevious()
                else false
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
             raise (new OutOfOrderKeyException<'K>((if this.isBatch then this.outerCursor.CurrentValue.Last.Key else this.innerCursor.CurrentKey), "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion || not this.synced then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveFirst() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          if this.outerCursor.MoveFirst() then
            newInner <- this.outerCursor.CurrentValue.GetCursor()
            if newInner.MoveFirst() then
              this.isBatch <- false
              true
            else 
              Trace.Fail("If outer moved first then inned must be non-empty")
              false
          else false
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion || not this.synced then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveLast() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          if this.outerCursor.MoveLast() then
            newInner <- this.outerCursor.CurrentValue.GetCursor()
            if newInner.MoveLast() then
              this.isBatch <- false
              true
            else
              Trace.Fail("If outer moved last then inned must be non-empty")
              false
          else false
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion || not this.synced then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveAt(key:'K, direction:Lookup) =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          let res =
            if this.source.ChunkUpperLimit = 0 then
              let hash = this.source.Hasher.Hash(key)
              let c = 
                if not this.HasValidInner then 2 // <> 0 below
                else this.source.Comparer.Compare(hash, this.outerCursor.CurrentKey)
              
              if c <> 0 then // not in the current bucket, switch bucket
                if this.outerCursor.MoveAt(hash, Lookup.EQ) then // Equal!
                  newInner <- this.outerCursor.CurrentValue.GetCursor()
                  newInner.MoveAt(key, direction)
                else
                  false
              else
                newInner.MoveAt(key, direction)
            else
              if this.outerCursor.MoveAt(key, Lookup.LE) then // LE!
                newInner <- this.outerCursor.CurrentValue.GetCursor()
                newInner.MoveAt(key, direction)
              else
                false

          if res then
            this.isBatch <- false
            true
          else
              match direction with
              | Lookup.LT | Lookup.LE ->
                // look into previous bucket
                if this.outerCursor.MovePrevious() then
                  newInner <- this.outerCursor.CurrentValue.GetCursor()
                  let res = newInner.MoveAt(key, direction)
                  if res then
                    this.isBatch <- false
                    true
                  else
                    this.Reset()
                    false
                else
                  this.Reset()
                  false
              | Lookup.GT | Lookup.GE ->
                // look into next bucket
                let moved = this.outerCursor.MoveNext() 
                if moved then
                  newInner <- this.outerCursor.CurrentValue.GetCursor()
                  let res = newInner.MoveAt(key, direction)
                  if res then
                    this.isBatch <- false
                    true
                  else
                    this.Reset()
                    false 
                else
                  this.Reset()
                  false
              | _ -> false // LookupDirection.EQ
      /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion || not this.synced then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result


    member this.Clone() =
      let mutable entered = false
      try
        entered <- enterWriteLockIf &this.source.Locker this.source.isSynchronized
        let clone = new SortedChunkedMapGenericCursor<_,_,_>(this.source)
        clone.MoveAt(this.CurrentKey, Lookup.EQ) |> ignore
        clone
      finally
        exitWriteLockIf &this.source.Locker entered  
      
    member this.Reset() = 
      if this.HasValidInner then this.innerCursor.Dispose()
      this.innerCursor <- Unchecked.defaultof<_>
      this.outerCursor.Reset()

    member this.Dispose() = this.Reset()

    interface IDisposable with
      member this.Dispose() = this.Dispose()

    interface IEnumerator<KVP<'K,'V>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext():bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V> = this.Current
      member this.Current with get(): obj = this.Current :> obj

    interface IAsyncEnumerator<KVP<'K,'V>> with
      member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = 
        if this.source.IsReadOnly then
          if this.MoveNext() then trueTask else falseTask
        else raise (NotSupportedException("Use SortedChunkedMapGenericCursorAsync instead"))

    interface ICursor<'K,'V> with
      member this.Comparer with get() = this.source.Comparer
      member this.CurrentBatch = this.CurrentBatch
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



[<AllowNullLiteral>]
type SortedChunkedMap<'K,'V>
  internal 
  (
    outerFactory:IComparer<'K>->IOrderedMap<'K, SortedMap<'K,'V>>,
    innerFactory:int * IComparer<'K>->SortedMap<'K,'V>, 
    comparer:IComparer<'K>,
    hasher:IKeyHasher<'K> option, 
    chunkMaxSize:int option) =
  inherit SortedChunkedMapGeneric<'K,'V,SortedMap<'K,'V>>(outerFactory, innerFactory, comparer, hasher, chunkMaxSize)


  override this.GetCursor() =
    //if Thread.CurrentThread.ManagedThreadId <> ownerThreadId then this.IsSynchronized <- true // NB: via property with locks
    let mutable entered = false
    try
      entered <- enterWriteLockIf &this.Locker this.isSynchronized
      // if source is already read-only, MNA will always return false
      if this.isReadOnly then new SortedChunkedMapCursor<_,_>(this) :> ICursor<'K,'V>
      else
        let c = new BaseCursorAsync<_,_,_>(this,Func<_>(this.GetEnumerator))
        c :> ICursor<'K,'V>
    finally
      exitWriteLockIf &this.Locker entered

  // .NETs foreach optimization
  member this.GetEnumerator() =
//    if Thread.CurrentThread.ManagedThreadId <> ownerThreadId then 
//      // NB: via property with locks
//      this.IsSynchronized <- true
    readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ ->
      new SortedChunkedMapCursor<_,_>(this)
    )


  // x0
  
  new() = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c, IsSynchronized = false) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, None)
  
  // x1

  /// In-memory sorted chunked map
  new(comparer:IComparer<'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c, IsSynchronized = false) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)),comparer, None, None)
  
  /// In-memory sorted chunked map
  new(hasher:Func<'K,'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c, IsSynchronized = false) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    let hasher = { new IKeyHasher<'K> with
          member x.Hash(k) = hasher.Invoke k
        }
    new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)
  new(chunkMaxSize:int) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c, IsSynchronized = false) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))

  internal new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>) = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, None)
  
  // x2

  /// In-memory sorted chunked map
  new(comparer:IComparer<'K>,hasher:Func<'K,'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c, IsSynchronized = false) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    let hasher = { new IKeyHasher<'K> with
          member x.Hash(k) = hasher.Invoke k
        }
    new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)

  new(comparer:IComparer<'K>,chunkMaxSize:int) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c, IsSynchronized = false) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>) = 
    new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, None)

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,hasher:Func<'K,'K>) = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    let hasher = { new IKeyHasher<'K> with
          member x.Hash(k) = hasher.Invoke k
        }
    new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)
  
  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,chunkMaxSize:int) = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))

  // x3

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>,hasher:Func<'K,'K>) =
    let hasher = { new IKeyHasher<'K> with
          member x.Hash(k) = hasher.Invoke k
        }
    new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)
  
  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>,chunkMaxSize:int) = 
    new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))


and
  public SortedChunkedMapCursor<'K,'V> =
    struct
      val mutable internal source : SortedChunkedMap<'K,'V>
      val mutable internal outerCursor : ICursor<'K,SortedMap<'K,'V>>
      val mutable internal innerCursor : SortedMapCursor<'K,'V>
      val mutable internal isBatch : bool
      new(source:SortedChunkedMap<'K,'V>) = 
        { source = source; 
          outerCursor = source.OuterMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          isBatch = false;
        }
    end

    member private this.HasValidInner with get() = this.innerCursor.source <> Unchecked.defaultof<_>
    
    member this.CurrentKey with get() = this.innerCursor.CurrentKey
    member this.CurrentValue with get() = this.innerCursor.CurrentValue
    member this.Current with get() : KVP<'K,'V> = KeyValuePair(this.innerCursor.CurrentKey, this.innerCursor.CurrentValue)

    member this.Comparer: IComparer<'K> = this.source.Comparer
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.source.TryGetValue(key, &value)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNext() =
      let mutable newInner = Unchecked.defaultof<_> 
      let mutable doSwitchInner = false
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable outerMoved = false
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          try
            // here we use copy-by-value of structs
            newInner <- this.innerCursor
            if (not this.HasValidInner) && (not this.isBatch) then
              if this.outerCursor.MoveFirst() then
                newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                doSwitchInner <- true
                newInner.MoveFirst()
              else false
            else
              let mutable entered = false
              try
                entered <- enterWriteLockIf &this.source.Locker this.source.isSynchronized
                if this.HasValidInner && newInner.MoveNext() then 
                  doSwitchInner <- true
                  true
                else
                  if outerMoved || this.outerCursor.MoveNext() then
                    outerMoved <- true
                    this.isBatch <- false // if was batch, moved to the first element of the new batch
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    doSwitchInner <- true
                    let moved = newInner.MoveNext()
                    if newInner.source.Count > 0 && not moved then failwith "must move here"
                    if moved then true
                    else outerMoved <- false; false // need to try to move outer again
                  else
                    outerMoved <- false
                    false
              finally
                exitWriteLockIf &this.source.Locker entered
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
             raise (new OutOfOrderKeyException<'K>((if this.isBatch then this.outerCursor.CurrentValue.Last.Key else this.innerCursor.CurrentKey), "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        if doSwitchInner then this.innerCursor <- newInner
      result

    member this.Source: ISeries<'K,'V> = this.source :> ISeries<'K,'V>      
    member this.IsContinuous with get() = false
    member this.CurrentBatch: ISeries<'K,'V> = 
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////

          if this.isBatch then this.outerCursor.CurrentValue :> ISeries<'K,'V>
          else raise (InvalidOperationException("SortedChunkedMapGenericCursor cursor is not at a batch position"))

        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      result

    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> =
      let mutable newIsBatch = this.isBatch

      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          // TODO test & review
          raise (NotImplementedException("TODO test & review"))
          try
            if (not this.HasValidInner || this.isBatch) && this.outerCursor.MoveNext() then
                newIsBatch <- true
                trueTask
            else falseTask
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
            Trace.Assert(this.isBatch)
            raise (new OutOfOrderKeyException<'K>(this.outerCursor.CurrentValue.Last.Key, "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result.Result then
        this.isBatch <- newIsBatch
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MovePrevious() = 
      let mutable newInner = Unchecked.defaultof<_> 
      let mutable doSwitchInner = false
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable outerMoved = false
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          try
            // here we use copy-by-value of structs
            newInner <- this.innerCursor
            if (not this.HasValidInner) && (not this.isBatch) then
              if this.outerCursor.MoveLast() then
                newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                doSwitchInner <- true
                newInner.MoveLast()
              else false
            else
              let mutable entered = false
              try
                entered <- enterWriteLockIf &this.source.Locker this.source.isSynchronized
                if this.HasValidInner && newInner.MovePrevious() then 
                  doSwitchInner <- true
                  true
                else
                  if outerMoved || this.outerCursor.MovePrevious() then
                    outerMoved <- true
                    this.isBatch <- false // if was batch, moved to the first element of the new batch
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    doSwitchInner <- true
                    if newInner.MovePrevious() then true
                    else outerMoved <- false; false // need to try to move outer again
                  else
                    outerMoved <- false
                    false
              finally
                exitWriteLockIf &this.source.Locker entered
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
             raise (new OutOfOrderKeyException<'K>((if this.isBatch then this.outerCursor.CurrentValue.Last.Key else this.innerCursor.CurrentKey), "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        if doSwitchInner then this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveFirst() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          if this.outerCursor.MoveFirst() then
            newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
            if newInner.MoveFirst() then
              this.isBatch <- false
              true
            else 
              Trace.Fail("If outer moved first then inned must be non-empty")
              false
          else false
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveLast() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          if this.outerCursor.MoveLast() then
            newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
            if newInner.MoveLast() then
              this.isBatch <- false
              true
            else
              Trace.Fail("If outer moved last then inned must be non-empty")
              false
          else false
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveAt(key:'K, direction:Lookup) =
      let mutable newInner = Unchecked.defaultof<_>
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else this.source.orderVersion
        result <-
        /////////// Start read-locked code /////////////
          let mutable entered = false
          try
            entered <- enterWriteLockIf &this.source.Locker this.source.isSynchronized
            newInner <- this.innerCursor
            let res =
              if this.source.ChunkUpperLimit = 0 then
                let hash = this.source.Hasher.Hash(key)
                let c = 
                  if not this.HasValidInner then 2 // <> 0 below
                  else this.source.Comparer.Compare(hash, this.outerCursor.CurrentKey)
              
                if c <> 0 then // not in the current bucket, switch bucket
                  if this.outerCursor.MoveAt(hash, Lookup.EQ) then // Equal!
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    newInner.MoveAt(key, direction)
                  else
                    false
                else
                  newInner.MoveAt(key, direction)
              else
                if this.outerCursor.MoveAt(key, Lookup.LE) then // LE!
                  newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                  newInner.MoveAt(key, direction)
                else
                  false

            if res then
              this.isBatch <- false
              true
            else
                match direction with
                | Lookup.LT | Lookup.LE ->
                  // look into previous bucket
                  if this.outerCursor.MovePrevious() then
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    let res = newInner.MoveAt(key, direction)
                    if res then
                      this.isBatch <- false
                      true
                    else
                      this.Reset()
                      false
                  else
                    this.Reset()
                    false
                | Lookup.GT | Lookup.GE ->
                  // look into next bucket
                  let moved = this.outerCursor.MoveNext() 
                  if moved then
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    let res = newInner.MoveAt(key, direction)
                    if res then
                      this.isBatch <- false
                      true
                    else
                      this.Reset()
                      false 
                  else
                    this.Reset()
                    false
                | _ -> false // LookupDirection.EQ
          finally
            exitWriteLockIf &this.source.Locker entered
      /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source.nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result


    member this.Clone() =
      let mutable entered = false
      try
        entered <- enterWriteLockIf &this.source.Locker this.source.isSynchronized
        let mutable clone = this
        clone.source <- this.source
        clone.outerCursor <- this.outerCursor.Clone()
        clone.innerCursor <- this.innerCursor.Clone()
        clone.isBatch <- this.innerCursor.isBatch
        clone
      finally
        exitWriteLockIf &this.source.Locker entered
      
    member this.Reset() = 
      if this.HasValidInner then this.innerCursor.Dispose()
      this.innerCursor <- Unchecked.defaultof<_>
      this.outerCursor.Reset()

    member this.Dispose() = this.Reset()

    interface IDisposable with
      member this.Dispose() = this.Dispose()

    interface IEnumerator<KVP<'K,'V>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext():bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V> = KeyValuePair(this.innerCursor.CurrentKey, this.innerCursor.CurrentValue)
      member this.Current with get(): obj = this.Current :> obj

    interface IAsyncEnumerator<KVP<'K,'V>> with
      member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = 
        if this.source.IsReadOnly then
          if this.MoveNext() then trueTask else falseTask
        else raise (NotSupportedException("Use SortedChunkedMapGenericCursorAsync instead"))

    interface ICursor<'K,'V> with
      member this.Comparer with get() = this.source.Comparer
      member this.CurrentBatch = this.CurrentBatch
      member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatch(cancellationToken)
      member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
      member this.MoveFirst():bool = this.MoveFirst()
      member this.MoveLast():bool =  this.MoveLast()
      member this.MovePrevious():bool = this.MovePrevious()
      member this.CurrentKey with get() = this.innerCursor.CurrentKey
      member this.CurrentValue with get() = this.innerCursor.CurrentValue
      member this.Source with get() = this.source :> ISeries<'K,'V>
      member this.Clone() = this.Clone() :> ICursor<'K,'V>
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.source.TryGetValue(key, &value)


//and
//  internal SortedChunkedMapCursorAsync<'K,'V>(source:SortedChunkedMap<'K,'V>) as this =
//    [<DefaultValueAttribute(false)>]
//    val mutable private state : SortedChunkedMapCursor<'K,'V>
//    [<DefaultValueAttribute(false)>]
//    val mutable private semaphore : SemaphoreSlim
//    [<DefaultValueAttribute(false)>]
//    val mutable private onUpdateHandler : OnUpdateHandler
//
//    // NB Async cursors are supposed to be long-lived, therefore we opt for fatter 
//    // cursor object with these fields but avoid closure allocation in the callback.
//    [<DefaultValueAttribute(false)>]
//    val mutable private semaphoreTask : Task<bool>
//    [<DefaultValueAttribute(false)>]
//    val mutable private tcs : AsyncTaskMethodBuilder<bool>
//    [<DefaultValueAttribute(false)>]
//    val mutable private token: CancellationToken
//    [<DefaultValueAttribute(false)>]
//    val mutable private callbackAction: Action
//
//    do
//      this.state <- new SortedChunkedMapCursor<'K,'V>(source)
//
//    override this.Finalize() = this.Dispose()
//     
//    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
//    member private this.CompleteTcsCallback() =
//      OptimizationSettings.TraceVerbose("SM_MNA: awaiter on completed")
//      match this.semaphoreTask.Status with
//      | TaskStatus.RanToCompletion -> 
//        if not this.semaphoreTask.Result then 
//          OptimizationSettings.TraceVerbose("semaphore timeout " + this.state.source.subscribersCounter.ToString() + " " + this.state.source.isReadOnly.ToString())
//        if this.state.MoveNext() then
//          OptimizationSettings.TraceVerbose("SM_MNA: awaiter on completed MN true")
//          this.tcs.SetResult(true)
//        elif this.state.source.isReadOnly then 
//          OptimizationSettings.TraceVerbose("SM_MNA: awaiter on completed immutable")
//          this.tcs.SetResult(false)
//        else
//          OptimizationSettings.TraceVerbose("SM_MNA: recursive calling completeTcs")
//          this.CompleteTcs()
//      | _ -> failwith "TODO process all task results, e.g. cancelled"
//      ()
//
//    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
//    member private this.CompleteTcs() : unit =
//      if this.state.MoveNext() then
//          OptimizationSettings.TraceVerbose("SM_MNA: MN inside completeTcs")
//          this.tcs.SetResult(true)
//        else
//          OptimizationSettings.TraceVerbose("SM_MNA: waiting on semaphore")
//          // NB this.source.isReadOnly could be set to true right before semaphore.WaitAsync call
//          // and we will never get signal after that
//          let mutable entered = false
//          try
//            entered <- enterWriteLockIf &this.state.source.locker this.state.source.isSynchronized
//            if this.state.source.isReadOnly then
//              if this.state.MoveNext() then this.tcs.SetResult(true) else this.tcs.SetResult(false)
//            else
//              this.semaphoreTask <- this.semaphore.WaitAsync(500, this.token)
//              let awaiter = this.semaphoreTask.GetAwaiter()
//              // TODO profiler says this allocates. This is because we close over the three variables.
//              // We could turn them into mutable fields and then closure could be allocated just once.
//              // But then the cursor will become heavier.
//              awaiter.UnsafeOnCompleted(this.callbackAction)
//          finally
//            exitWriteLockIf &this.state.source.locker entered
//
//    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
//    member this.MoveNext(ct: CancellationToken): Task<bool> =      
//      match this.state.MoveNext() with
//      | true -> 
//        OptimizationSettings.TraceVerbose("SM_MNA: sync MN true")
//        trueTask            
//      | false ->
//        OptimizationSettings.TraceVerbose("SM_MNA: sync MN false")
//        match this.state.source.isReadOnly with
//        | false ->
//          let sw = SpinWait()
//          let mutable doSpin = true
//          let mutable spinCount = 0
//          // spin 10 times longer than default SpinWait implementation
//          while doSpin && not this.state.source.isReadOnly && not ct.IsCancellationRequested && spinCount < 100 do
//            OptimizationSettings.TraceVerbose("SM_MNA: spinning")
//            doSpin <- (not <| this.state.MoveNext()) 
//            if doSpin then
//              if sw.NextSpinWillYield then 
//                increment &spinCount
//                sw.Reset()
//              sw.SpinOnce()
//          if not doSpin then // exited loop due to successful MN, not due to sw.NextSpinWillYield
//            OptimizationSettings.TraceVerbose("SM_MNA: spin wait success")
//            trueTask
//          elif this.state.source.isReadOnly then
//            if this.state.MoveNext() then trueTask else falseTask
//          elif ct.IsCancellationRequested then
//            raise (OperationCanceledException(ct))
//          else
//            // NB expect huge amount of idle tasks. Spinning on all of them is questionable.
//            //failwith "TODO exit spinning on some condition"
//            if this.onUpdateHandler = Unchecked.defaultof<_> then
//              let mutable entered = false
//              try
//                entered <- enterWriteLockIf &this.state.source.locker this.state.source.isSynchronized
//                this.semaphore <- new SemaphoreSlim(0,Int32.MaxValue)
//                this.onUpdateHandler <- OnUpdateHandler(fun _ ->
//                    if this.semaphore.CurrentCount <> Int32.MaxValue then this.semaphore.Release() |> ignore
//                )
//                this.state.source.onUpdateEvent.Publish.AddHandler this.onUpdateHandler
//                Interlocked.Increment(&this.state.source.subscribersCounter) |> ignore
//              finally
//                exitWriteLockIf &this.state.source.locker entered
//            if this.state.MoveNext() then trueTask
//            else
//              this.tcs <- Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create()
//              let returnTask = this.tcs.Task
//              OptimizationSettings.TraceVerbose("SM_MNA: calling completeTcs")
//              this.token <- ct
//              if this.callbackAction = Unchecked.defaultof<_> then this.callbackAction <- Action(this.CompleteTcsCallback)
//              this.CompleteTcs()
//              returnTask
//        | _ -> if this.state.MoveNext() then trueTask else falseTask
//      
//    member this.Clone() = 
//      let mutable entered = false
//      try
//        entered <- enterWriteLockIf &this.state.source.locker this.state.source.isSynchronized
//        let clone = new SortedChunkedMapCursorAsync<'K,'V>(this.state.source)
//        let mutable cloneState = this.state
//        cloneState.source <- this.state.source
//        cloneState.outerCursor <- this.state.outerCursor.Clone()
//        cloneState.innerCursor <- this.state.innerCursor.Clone()
//        cloneState.isBatch <- this.state.isBatch
//        clone.state <- cloneState
//        clone
//      finally
//        exitWriteLockIf &this.state.source.locker entered
//      
//    member this.Dispose() = this.Dispose(true)
//    member this.Dispose(disposing:bool) = 
//      this.state.Reset()
//      if this.onUpdateHandler <> Unchecked.defaultof<_> then
//        Interlocked.Decrement(&this.state.source.subscribersCounter) |> ignore
//        this.state.source.onUpdateEvent.Publish.RemoveHandler(this.onUpdateHandler)
//        this.semaphore.Dispose()
//        this.semaphoreTask <- Unchecked.defaultof<_>
//        this.token <- Unchecked.defaultof<_>
//        this.tcs <- Unchecked.defaultof<_>
//      if disposing then GC.SuppressFinalize(this)
//
//    // C#-like methods to avoid casting to ICursor all the time
//    member this.Reset() = this.state.Reset()
//    member this.MoveNext():bool = this.state.MoveNext()
//    member this.Current with get(): KVP<'K, 'V> = this.state.Current
//    member this.Comparer with get() = this.state.source.Comparer
//    member this.CurrentBatch = this.state.CurrentBatch
//    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.state.MoveNextBatch(cancellationToken)
//    member this.MoveAt(index:'K, lookup:Lookup) = this.state.MoveAt(index, lookup)
//    member this.MoveFirst():bool = this.state.MoveFirst()
//    member this.MoveLast():bool =  this.state.MoveLast()
//    member this.MovePrevious():bool = this.state.MovePrevious()
//    member this.CurrentKey with get():'K = this.state.CurrentKey
//    member this.CurrentValue with get():'V = this.state.CurrentValue
//    member this.Source with get() = this.state.source :> ISeries<'K,'V>
//    member this.IsContinuous with get() = false
//    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.state.source.TryGetValue(key, &value)
//
//    interface IDisposable with
//      member this.Dispose() = this.Dispose()
//
//    interface IEnumerator<KVP<'K,'V>> with    
//      member this.Reset() = this.state.Reset()
//      member this.MoveNext():bool = this.state.MoveNext()
//      member this.Current with get(): KVP<'K, 'V> = this.state.Current
//      member this.Current with get(): obj = this.state.Current :> obj
//
//    interface IAsyncEnumerator<KVP<'K,'V>> with
//      member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 
//
//    interface ICursor<'K,'V> with
//      member this.Comparer with get() = this.state.source.Comparer
//      member this.CurrentBatch = this.state.CurrentBatch
//      member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.state.MoveNextBatch(cancellationToken)
//      member this.MoveAt(index:'K, lookup:Lookup) = this.state.MoveAt(index, lookup)
//      member this.MoveFirst():bool = this.state.MoveFirst()
//      member this.MoveLast():bool =  this.state.MoveLast()
//      member this.MovePrevious():bool = this.state.MovePrevious()
//      member this.CurrentKey with get():'K = this.state.CurrentKey
//      member this.CurrentValue with get():'V = this.state.CurrentValue
//      member this.Source with get() = this.state.source :> ISeries<'K,'V>
//      member this.Clone() = this.Clone() :> ICursor<'K,'V>
//      member this.IsContinuous with get() = false
//      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.state.source.TryGetValue(key, &value)
//
