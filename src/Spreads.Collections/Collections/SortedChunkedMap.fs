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
open Spreads.Utils

// NB outer map version must be synced with SCM.version

[<AllowNullLiteral>]
[<AbstractClass>]
type SortedChunkedMapGeneric<'K,'V> 
  internal 
  (
    outerFactory:KeyComparer<'K>->IMutableChunksSeries<'K, 'V, SortedMap<'K,'V>>,
    innerFactory:int * KeyComparer<'K>->SortedMap<'K,'V>, 
    comparer:KeyComparer<'K>,
    hasher:IKeyHasher<'K> option, 
    chunkMaxSize:int option) as this=
  inherit ContainerSeries<'K,'V, SortedChunkedMapCursor<'K,'V>>()

  let outerMap = outerFactory(comparer)

  [<DefaultValueAttribute>]
  val mutable internal version : int64

  let mutable prevHash = Unchecked.defaultof<'K>
  // TODO this is temp replacement of WeakReference, with the same signature
  // WR is too complicated. If we want to release active bucket, just call Flush
  let mutable prevBucket = StrongReference<SortedMap<'K,'V>>(null)
  let prevBucketIsSet (prevBucket':SortedMap<'K,'V> byref) : bool =
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
  let existingHashBucket key =
    // we return KVP to save TryFind(LE) call for the case when chunkUpperLimit > 0
    if chunkUpperLimit = 0 then
      KVP(hasher.Hash(key), Unchecked.defaultof<_>)
    else
      let mutable h = Unchecked.defaultof<_>
      outerMap.TryFind(key, Lookup.LE, &h) |> ignore
      h

  do
    this.isSynchronized <- false
    this.version <- outerMap.Version
    this.nextVersion <- outerMap.Version

  member this.Clear() : unit = this.RemoveMany(this.First.Key, Lookup.GE) |> ignore

  override this.Comparer with get() = comparer

  member this.Count
      with get() = 
        let mutable entered = false
        try
          entered <- enterWriteLockIf &this.Locker this.isSynchronized
          let mutable size' = 0L
          for okvp in outerMap do
            size' <- size' + int64 okvp.Value.Count
          //size <- size'
          size'
        finally
          exitWriteLockIf &this.Locker entered

  member internal this.OuterMap with get() = outerMap
  member internal this.ChunkUpperLimit with get() = chunkUpperLimit
  member internal this.Hasher with get() = hasher

  member this.Version 
    with get() = readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> this.version)
    and internal set v = 
      let mutable entered = false
      try
        entered <- enterWriteLockIf &this.Locker true
        this.version <- v // NB setter only for deserializer
        this.nextVersion <- v
      finally
        exitWriteLockIf &this.Locker entered

  override this.IsEmpty
      with get() =
        readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> outerMap.IsEmpty)
         

  member this.Complete() =
    let mutable entered = false
    try
      entered <- enterWriteLockIf &this.Locker true
      //if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      if not this.isReadOnly then 
          this.isReadOnly <- true
          // immutable doesn't need sync
          this.isSynchronized <- false // TODO the same for SCM
          this.NotifyUpdate(false)
    finally
      //Interlocked.Increment(&this.version) |> ignore
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
          let bucketIsSet = prevBucketIsSet(&prevBucket')
          if c = 0 && bucketIsSet then
            prevBucket'.[key] // this could raise keynotfound exeption
          else
            // bucket switch
            if bucketIsSet then this.FlushUnchecked()
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
      #if DEBUG
      let mutable finished = false
      #endif
      try
        try ()
        finally
          entered <- enterWriteLockIf &this.Locker this.isSynchronized
          if entered then Interlocked.Increment(&this.nextVersion) |> ignore
            
        if chunkUpperLimit = 0 then // deterministic hash
          let hash = hasher.Hash(key)
          let c = comparer.Compare(hash, prevHash)
          let mutable prevBucket' = Unchecked.defaultof<_>
          let bucketIsSet = prevBucketIsSet(&prevBucket')
          if c = 0 && bucketIsSet then
            Debug.Assert(prevBucket'.version = this.version)
            prevBucket'.[key] <- value
            this.NotifyUpdate(true)
          else
            // bucket switch
            if bucketIsSet then this.FlushUnchecked()
            let isNew, bucket = 
              let mutable bucketKvp = Unchecked.defaultof<_>
              let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
              if ok then
                false, bucketKvp.Value
              else
                let newSm = innerFactory(0, comparer)
                true, newSm
            bucket.version <- this.version // NB old bucket could have stale version, update for both cases
            bucket.nextVersion <- this.version
            bucket.[key] <- value
            if isNew then
              outerMap.[hash] <- bucket
              Debug.Assert(bucket.version = outerMap.Version, "Outer setter must update its version")
            this.NotifyUpdate(true)
            prevHash <- hash
            prevBucket.SetTarget(bucket)
        else
          let mutable prevBucket' = Unchecked.defaultof<_>
          let bucketIsSet = prevBucketIsSet(&prevBucket')
          if bucketIsSet && comparer.Compare(key, prevHash) >= 0 && comparer.Compare(key, prevBucket'.Last.Key) <= 0 then
            // we are inside previous bucket, setter has no choice but to set to this bucket regardless of its size
            Debug.Assert(prevBucket'.version = this.version)
            prevBucket'.[key] <- value
            this.NotifyUpdate(true)
          else
            let mutable kvp = Unchecked.defaultof<_>
            let ok = outerMap.TryFind(key, Lookup.LE, &kvp)
            if ok &&
              // the second condition here is for the case when we add inside existing bucket, overflow above chunkUpperLimit is inevitable without a separate split logic (TODO?)
              (kvp.Value.size < chunkUpperLimit || comparer.Compare(key, kvp.Value.Last.Key) <= 0) then
              if comparer.Compare(prevHash, kvp.Key) <> 0 then 
                // switched active bucket
                this.FlushUnchecked()
                // if add fails later, it is ok to update the stale version to this.version
                kvp.Value.version <- this.version
                kvp.Value.nextVersion <- this.version
                prevHash <- kvp.Key
                prevBucket.SetTarget kvp.Value
              Debug.Assert(kvp.Value.version = this.version)
              kvp.Value.[key] <- value
              this.NotifyUpdate(true)
            else
              if bucketIsSet then this.FlushUnchecked()
              // create a new bucket at key
              let newSm = innerFactory(0, comparer)
              newSm.version <- this.version
              newSm.nextVersion <- this.version
              newSm.[key] <- value
              #if DEBUG
              let v = outerMap.Version
              outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
              Debug.Assert(v + 1L = outerMap.Version, "Outer setter must increment its version")
              #else
              outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
              #endif
              prevHash <- key
              prevBucket.SetTarget newSm
              this.NotifyUpdate(true)
        #if DEBUG
        finished <- true
        #endif
      finally
        Interlocked.Increment(&this.version) |> ignore
        exitWriteLockIf &this.Locker entered
        #if DEBUG
        if not finished then Environment.FailFast("SCM.Item set must always succeed")
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
      
  override this.First
    with get() = 
      readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> this.FirstUnsafe)

  member private this.LastUnsafe
    with get() = 
      if outerMap.IsEmpty then raise (InvalidOperationException("Could not get the first element of an empty map"))
      let bucket = outerMap.Last
      bucket.Value.Last
      
  override this.Last
    with get() = 
      readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ -> this.LastUnsafe )

  member private this.FlushUnchecked() =
    let mutable temp = Unchecked.defaultof<_>
    if prevBucket.TryGetTarget(&temp) && temp.version <> outerMap.Version then
      // ensure the version of current bucket is saved in outer
      Debug.Assert(temp.version = this.version, "TODO review/test, this must be true? RemoveMany doesn't use prev bucket, review logic there")
      temp.version <- this.version
      temp.nextVersion <- this.version
      outerMap.[prevHash] <- temp
      temp <- null
      outerMap.Flush()
    else
      Debug.Assert(outerMap.Version = this.version)
      () // nothing to flush
    prevBucket.SetTarget (null) // release active bucket so it can be GCed

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
      if this.isReadOnly then new SortedChunkedMapGenericCursor<_,_>(this) :> ICursor<'K,'V>
      else
        raise (NotImplementedException()) // implemented below for non-generic SCM
    finally
      exitWriteLockIf &this.Locker entered

  // .NETs foreach optimization
  member this.GetEnumerator() =
    if Thread.CurrentThread.ManagedThreadId <> ownerThreadId then 
      // NB: via property with locks
      this.IsSynchronized <- true
    readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ ->
      new SortedChunkedMapGenericCursor<_,_>(this)
    )
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.TryFindTuple(key:'K, direction:Lookup) = 
    let tupleResult =
      let mutable kvp = Unchecked.defaultof<KeyValuePair<'K, 'V>>
      
      let hashBucket = existingHashBucket key
      let hash = hashBucket.Key
      let c = comparer.Compare(hash, prevHash)

      let res, pair =
        let mutable prevBucket' = Unchecked.defaultof<_>
        if c <> 0 || (not <| prevBucketIsSet(&prevBucket')) then // not in the prev bucket, switch bucket to newHash
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = 
            if hashBucket.Value <> Unchecked.defaultof<_> then
              innerMapKvp <- hashBucket
              true
            else outerMap.TryFind(hash, Lookup.EQ, &innerMapKvp)
          if ok then
            // bucket switch
            this.FlushUnchecked()
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
    result <- tupleResult.Item2
    tupleResult.Item1

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  override this.TryFind(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let res() = this.TryFindTuple(key, direction)
    let tupleResult = readLockIf &this.nextVersion &this.version this.isSynchronized res
    result <- tupleResult.Item2
    tupleResult.Item1

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  override this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
    try
      res <- this.First
      true
    with
    | _ -> 
      res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
      false
          
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]  
  override this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
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
    value <- tupleResult.Item2
    tupleResult.Item1

  //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
  override this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>).Select(fun kvp -> kvp.Key)
  //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
  override this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>).Select(fun kvp -> kvp.Value)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.AddUnchecked(key, value):unit =
    if chunkUpperLimit = 0 then // deterministic hash
      let hash = hasher.Hash(key)
      // the most common scenario is to hit the previous bucket
      let mutable prevBucket' = Unchecked.defaultof<_>
      let bucketIsSet = prevBucketIsSet(&prevBucket')
      if bucketIsSet && comparer.Compare(hash, prevHash) = 0 then
        Debug.Assert(prevBucket'.version = this.version)
        prevBucket'.Add(key, value)
        this.NotifyUpdate(true)
      else
        // bucket switch
        if bucketIsSet then this.FlushUnchecked()
        let bucket = 
          let mutable bucketKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
          if ok then
            bucketKvp.Value.version <- this.version
            bucketKvp.Value.nextVersion <- this.version
            bucketKvp.Value.Add(key, value)
            bucketKvp.Value
          else
            let newSm = innerFactory(0, comparer)
            newSm.version <- this.version
            newSm.nextVersion <- this.version
            newSm.Add(key, value)
            outerMap.[hash]<- newSm
            newSm
        this.NotifyUpdate(true)
        prevHash <- hash
        prevBucket.SetTarget bucket
    else
      let mutable prevBucket' = Unchecked.defaultof<_>
      let bucketIsSet = prevBucketIsSet(&prevBucket')
      if bucketIsSet && comparer.Compare(key, prevHash) >= 0 && comparer.Compare(key, prevBucket'.Last.Key) <= 0 then
        // we are inside previous bucket, setter has no choice but to set to this bucket regardless of its size
        Debug.Assert(prevBucket'.version = this.version)
        prevBucket'.Add(key,value)
        this.NotifyUpdate(true)
      else
        let mutable kvp = Unchecked.defaultof<_>
        let ok = outerMap.TryFind(key, Lookup.LE, &kvp)
        if ok &&
          // the second condition here is for the case when we add inside existing bucket, overflow above chunkUpperLimit is inevitable without a separate split logic (TODO?)
          (kvp.Value.size < chunkUpperLimit || comparer.Compare(key, kvp.Value.Last.Key) <= 0) then
          if comparer.Compare(prevHash, kvp.Key) <> 0 || not bucketIsSet then 
            // switched active bucket
            this.FlushUnchecked()
            // if add fails later, it is ok to update the stale version to this.version
            kvp.Value.version <- this.version
            kvp.Value.nextVersion <- this.version
            prevHash <- kvp.Key
            prevBucket.SetTarget kvp.Value
          Debug.Assert(kvp.Value.version = this.version)
          kvp.Value.Add(key, value)
          this.NotifyUpdate(true)
        else
          if bucketIsSet then this.FlushUnchecked()
          // create a new bucket at key
          let newSm = innerFactory(0, comparer)
          newSm.version <- this.version
          newSm.nextVersion <- this.version
          newSm.[key] <- value
          #if DEBUG
          let v = outerMap.Version
          outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
          Debug.Assert(v + 1L = outerMap.Version, "Outer setter must increment its version")
          #else
          outerMap.[key] <- newSm // outerMap.Version is incremented here, set non-empty bucket only
          #endif
          prevHash <- key
          prevBucket.SetTarget newSm
          this.NotifyUpdate(true)

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.Add(key, value):unit =
    let mutable entered = false
    let mutable added = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      this.AddUnchecked(key, value)
      added <- true
    finally
      if added then Interlocked.Increment(&this.version) |> ignore elif entered then Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if DEBUG
      // TODO in debug this will make any storage unusable, add tests instead
      //this.FlushUnchecked() 
      //Trace.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
      #endif

  // TODO add last to empty fails
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.AddLast(key, value):unit =
    let mutable entered = false
    let mutable added = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      
      let c =
        if outerMap.IsEmpty then 1
        else comparer.Compare(key, this.LastUnsafe.Key)
      if c > 0 then
        this.AddUnchecked(key, value)
      else
        let exn = OutOfOrderKeyException(this.LastUnsafe.Key, key, "New key is smaller or equal to the largest existing key")
        raise (exn)
      added <- true
    finally
      if added then Interlocked.Increment(&this.version) |> ignore elif entered then Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if DEBUG
      // TODO in debug this will make any storage unusable, add tests instead
      //this.FlushUnchecked() 
      //Debug.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
      #endif

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.AddFirst(key, value):unit =
    let mutable entered = false
    let mutable added = false
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
      added <- true
    finally
      if added then Interlocked.Increment(&this.version) |> ignore elif entered then Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if DEBUG
      //Debug.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
      #endif

    
  // NB first/last optimization is possible, but removes are rare in the primary use case
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member private this.RemoveUnchecked(key):bool =
    let hashBucket = existingHashBucket key
    let hash = hashBucket.Key
    let c = comparer.Compare(hash, prevHash)
    let mutable prevBucket' = Unchecked.defaultof<_>
    let prevBucketIsSet = prevBucketIsSet(&prevBucket')
    if c = 0 && prevBucketIsSet then
      let res = prevBucket'.Remove(key)
      if res then
        Debug.Assert(prevBucket'.version = this.version + 1L, "Verion of the active bucket must much SCM version")
        prevBucket'.version <- this.version + 1L
        prevBucket'.nextVersion <- this.version + 1L
        // NB no outer set here, it must happen on prev bucket switch
        if prevBucket'.Count = 0 then
          // but here we must notify outer that version has changed
          // setting empty bucket will remove it in the outer map
          // (implementation detail, but this is internal) 
          outerMap.[prevHash] <- prevBucket'
          prevBucket.SetTarget null
      res
    else
      if prevBucketIsSet then 
        // store potentially modified active bucket in the outer, including version
        this.FlushUnchecked()
      let mutable innerMapKvp = Unchecked.defaultof<_>
      let ok = 
        if hashBucket.Value <> Unchecked.defaultof<_> then
          innerMapKvp <- hashBucket
          true
        else outerMap.TryFind(hash, Lookup.EQ, &innerMapKvp)
      if ok then
        let bucket = (innerMapKvp.Value)
        prevHash <- hash
        prevBucket.SetTarget bucket
        let res = bucket.Remove(key)
        if res then
          bucket.version <- this.version + 1L
          bucket.nextVersion <- this.version + 1L
          // NB empty will be removed, see comment above
          outerMap.[prevHash] <- bucket
          if bucket.Count = 0 then
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
      if removed then increment &this.orderVersion
      removed
    finally
      this.NotifyUpdate(true)
      if removed then Interlocked.Increment(&this.version) |> ignore
      elif entered then Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if DEBUG
      this.FlushUnchecked()
      Debug.Assert((outerMap.Version = this.version))
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
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
      removed <- this.RemoveUnchecked(result.Key)
      removed
    finally
      this.NotifyUpdate(true)
      if removed then Interlocked.Increment(&this.version) |> ignore
      elif entered then Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if DEBUG
      this.FlushUnchecked()
      Debug.Assert((outerMap.Version = this.version)) 
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
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
      removed <- this.RemoveUnchecked(result.Key)
      removed
    finally
      this.NotifyUpdate(true)
      if removed then Interlocked.Increment(&this.version) |> ignore
      elif entered then Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if DEBUG
      this.FlushUnchecked()
      Debug.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
      #endif

  /// Removes all elements that are to `direction` from `key`
  member this.RemoveMany(key:'K,direction:Lookup):bool =
    // TODO(low) for non-EQ this is not atomic now, could add a special method for removed in IMutableChunksSeries
    // then in its impl it could be done in a transaction. However, this nethod is not used frequently
    let mutable removed = false
    let mutable entered = false
    try
      try ()
      finally
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        if entered then Interlocked.Increment(&this.nextVersion) |> ignore
      
      let version = outerMap.Version
      let result = 
        if outerMap.IsEmpty then
          #if DEBUG
          let mutable temp = Unchecked.defaultof<_>
          Debug.Assert(not <| prevBucketIsSet(&temp), "there must be no active bucket for empty outer")
          #endif
          prevBucket.SetTarget null
          false
        else
          let removed =
            match direction with
            | Lookup.EQ -> 
              this.Remove(key)
            | Lookup.LT | Lookup.LE ->
              this.FlushUnchecked() // ensure current version is set in the outer map from the active chunk
              let mutable tmp = Unchecked.defaultof<_>
              if outerMap.TryFind(key, Lookup.LE, &tmp) then
                let r = tmp.Value.RemoveMany(key, direction)
                tmp.Value.version <- this.version + 1L
                tmp.Value.nextVersion <- this.version + 1L
                outerMap.RemoveMany(key, tmp.Value, direction) || r // NB order matters
              else false // no key below the requested key, notjing to delete
            | Lookup.GT | Lookup.GE ->
              this.FlushUnchecked() // ensure current version is set in the outer map from the active chunk
              let mutable tmp = Unchecked.defaultof<_>
              if outerMap.TryFind(key, Lookup.LE, &tmp) then // NB for deterministic hash LE still works
                let chunk = tmp.Value;
                let r1 = chunk.RemoveMany(key, direction)
                chunk.version <- this.version + 1L
                chunk.nextVersion <- this.version + 1L
                let r2 = outerMap.RemoveMany(key, chunk, direction)
                r1 || r2
              else
                // TODO the next line is not needed, we remove all items in SCM here and use the chunk to set the version
                let firstChunk = outerMap.First.Value
                let r1 = firstChunk.RemoveMany(key, direction) // this will remove all items in the chunk
                Debug.Assert(firstChunk.Count = 0, "The first chunk must have been completely cleared")
                firstChunk.version <- this.version + 1L
                firstChunk.nextVersion <- this.version + 1L
                let r2 = outerMap.RemoveMany(key, firstChunk, direction)
                r1 || r2
            | _ -> failwith "wrong direction"          
          removed
      removed <- result
      if removed then increment &this.orderVersion
      result
    finally
      this.NotifyUpdate(true)
      if removed then Interlocked.Increment(&this.version) |> ignore 
      elif entered then Interlocked.Decrement(&this.nextVersion) |> ignore
      exitWriteLockIf &this.Locker entered
      #if DEBUG
      Debug.Assert(outerMap.Version = this.version)
      if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
      #else
      if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
      #endif

  // TODO after checks, should form changed new chunks and use outer append method with rewrite
  // TODO atomic append with single version increase, now it is a sequence of remove/add mutations
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
  member this.Append(appendMap:IReadOnlySeries<'K,'V>, option:AppendOption) : int =
    let hasEqOverlap (old:SortedChunkedMapGeneric<_,_>) (append:IReadOnlySeries<'K,'V>) : bool =
      if comparer.Compare(append.First.Key, old.LastUnsafe.Key) > 0 then false
      else
        let oldC = new SortedChunkedMapGenericCursor<_,_>(old, false) :> ICursor<'K,'V>
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
      let mutable finished = false
      try
        try ()
        finally
          entered <- enterWriteLockIf &this.Locker this.isSynchronized
          if entered then Interlocked.Increment(&this.nextVersion) |> ignore
        let result =
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
        finished <- true
        result
      finally
        if not finished then Environment.FailFast("SCM.Append must always succeed")
        Interlocked.Increment(&this.version) |> ignore
        this.FlushUnchecked()
        exitWriteLockIf &this.Locker entered
        #if DEBUG
        Debug.Assert(outerMap.Version = this.version)
        if entered && this.version <> this.nextVersion then raise (ApplicationException("this.version <> this.nextVersion"))
        #else
        if entered && this.version <> this.nextVersion then Environment.FailFast("this.version <> this.nextVersion")
        #endif
    
  member this.Id with get() = id and set(newid) = id <- newid

  //#region Interfaces
    
  interface IReadOnlySeries<'K,'V> with
    // the rest is in BaseSeries
    member this.Item with get k = this.Item(k)

  interface IMutableSeries<'K,'V> with
    member this.Complete() = this.Complete()
    member this.Version with get() = this.Version
    member this.Count with get() = this.Count
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
    member this.Append(appendMap:IReadOnlySeries<'K,'V>, option:AppendOption) = this.Append(appendMap, option)

  interface IPersistentSeries<'K,'V> with
    member this.Flush() = this.Flush()
    member this.Dispose() = this.Dispose(true)
    member this.Id with get() = this.Id
  //#endregion

and
  public SortedChunkedMapGenericCursor<'K,'V> =
    struct
      val mutable internal source : SortedChunkedMapGeneric<'K,'V>
      val mutable internal outerCursor : ICursor<'K,SortedMap<'K,'V>>
      val mutable internal innerCursor : ICursor<'K,'V>
      val mutable internal isBatch : bool
      val mutable internal synced : bool
      internal new(source:SortedChunkedMapGeneric<'K,'V>, synced) = 
        { source = source; 
          outerCursor = source.OuterMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          isBatch = false;
          synced = synced;
        }
      internal new(source:SortedChunkedMapGeneric<'K,'V>) = 
        { source = source; 
          outerCursor = source.OuterMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          isBatch = false;
          synced = true;
        }
    end

    member inline private this.HasValidInner with get() = not (obj.ReferenceEquals(this.innerCursor, null))
    
    member this.CurrentKey with get() = this.innerCursor.CurrentKey
    member this.CurrentValue with get() = this.innerCursor.CurrentValue
    member this.Current with get() : KVP<'K,'V> = KeyValuePair(this.innerCursor.CurrentKey, this.innerCursor.CurrentValue)

    member this.Comparer: KeyComparer<'K> = this.source.Comparer
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.source.TryGetValue(key, &value)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNext() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
    member this.CurrentBatch: IReadOnlySeries<'K,'V> = 
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
        result <-
        /////////// Start read-locked code /////////////

          if this.isBatch then this.outerCursor.CurrentValue :> IReadOnlySeries<'K,'V>
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          // TODO test & review
          raise (NotImplementedException("TODO test & review"))
          try
            // TODO should await outer MNA after false MN
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
                    false
                else
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
                    false 
                else
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
        let clone = new SortedChunkedMapGenericCursor<_,_>(this.source)
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
      member this.Source with get() = this.source :> IReadOnlySeries<'K,'V>
      member this.Clone() = this.Clone() :> ICursor<'K,'V>
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.source.TryGetValue(key, &value)


and
  [<AllowNullLiteral>]
  SortedChunkedMap<'K,'V>
    internal 
    (
      outerFactory:KeyComparer<'K>->IMutableChunksSeries<'K,'V, SortedMap<'K,'V>>,
      innerFactory:int * KeyComparer<'K>->SortedMap<'K,'V>, 
      comparer:KeyComparer<'K>,
      hasher:IKeyHasher<'K> option, 
      chunkMaxSize:int option) =
    inherit SortedChunkedMapGeneric<'K,'V>(outerFactory, innerFactory, comparer, hasher, chunkMaxSize)

    override this.GetCursor() =
      //if Thread.CurrentThread.ManagedThreadId <> ownerThreadId then this.IsSynchronized <- true // NB: via property with locks
      let mutable entered = false
      try
        entered <- enterWriteLockIf &this.Locker this.isSynchronized
        // if source is already read-only, MNA will always return false
        if this.isReadOnly then new SortedChunkedMapCursor<_,_>(this) :> ICursor<'K,'V>
        else
          let c = new BaseCursorAsync<_,_,_>(Func<_>(this.GetEnumerator))
          c :> ICursor<'K,'V>
      finally
        exitWriteLockIf &this.Locker entered

    override this.GetContainerCursor() = this.GetEnumerator()

    // .NETs foreach optimization - return a struct
    member this.GetEnumerator() =
  //    if Thread.CurrentThread.ManagedThreadId <> ownerThreadId then 
  //      // NB: via property with locks
  //      this.IsSynchronized <- true
      readLockIf &this.nextVersion &this.version this.isSynchronized (fun _ ->
        new SortedChunkedMapCursor<_,_>(this)
      )

    // x0
  
    new() = 
      let comparer:KeyComparer<'K> = KeyComparer<'K>.Default
      let factory = (fun (c:KeyComparer<'K>) -> new ChunksContainer<'K,'V>(c, false) :> IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>)
      new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, None)
  
    // x1

    /// In-memory sorted chunked map
    new(comparer:IComparer<'K>) = 
      let factory = (fun (c:KeyComparer<'K>) -> new ChunksContainer<'K,'V>(c, false) :> IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>)
      new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), KeyComparer<'K>.Create(comparer), None, None)
  
    new(comparer:KeyComparer<'K>) = 
      let factory = (fun (c:KeyComparer<'K>) -> new ChunksContainer<'K,'V>(c, false) :> IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>)
      new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, None)
  

    /// In-memory sorted chunked map
    new(hasher:Func<'K,'K>) = 
      let factory = (fun (c:KeyComparer<'K>) -> new ChunksContainer<'K,'V>(c, false) :> IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>)
      let comparer:KeyComparer<'K> = KeyComparer<'K>.Default
      let hasher = { new IKeyHasher<'K> with
            member x.Hash(k) = hasher.Invoke k
          }
      new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)
  
    new(chunkMaxSize:int) = 
      let factory = (fun (c:IComparer<'K>) -> new ChunksContainer<'K,'V>(c, false) :> IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>)
      let comparer = KeyComparer<'K>.Default
      new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))

    internal new(outerFactory:Func<IComparer<'K>,IMutableChunksSeries<'K,'V, SortedMap<'K,'V>>>) = 
      let comparer = KeyComparer<'K>.Default
      new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, None)
  
    // x2

    /// In-memory sorted chunked map
    new(comparer:KeyComparer<'K>,hasher:Func<'K,'K>) = 
      let factory = (fun (c:KeyComparer<'K>) -> new ChunksContainer<'K,'V>(c, false) :> IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>)
      let hasher = { new IKeyHasher<'K> with
            member x.Hash(k) = hasher.Invoke k
          }
      new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)

    new(comparer:KeyComparer<'K>,chunkMaxSize:int) = 
      let factory = (fun (c:KeyComparer<'K>) -> new ChunksContainer<'K,'V>(c, false) :> IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>)
      new SortedChunkedMap<_,_>(factory, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))

    internal new(outerFactory:Func<KeyComparer<'K>,IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>>,comparer:IComparer<'K>) = 
      new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), KeyComparer<'K>.Create(comparer), None, None)

    internal new(outerFactory:Func<KeyComparer<'K>,IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>>,comparer:KeyComparer<'K>) = 
      new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, None)

    internal new(outerFactory:Func<IComparer<'K>,IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>>,hasher:Func<'K,'K>) = 
      let comparer = KeyComparer<'K>.Default
      let hasher = { new IKeyHasher<'K> with
            member x.Hash(k) = hasher.Invoke k
          }
      new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)
  
    internal new(outerFactory:Func<IComparer<'K>,IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>>,chunkMaxSize:int) = 
      let comparer = KeyComparer<'K>.Default
      new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))

    // x3

    internal new(outerFactory:Func<KeyComparer<'K>,IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>>,comparer:KeyComparer<'K>,hasher:Func<'K,'K>) =
      let hasher = { new IKeyHasher<'K> with
            member x.Hash(k) = hasher.Invoke k
          }
      new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, Some(hasher), None)
  
    new(outerFactory:Func<KeyComparer<'K>,IMutableChunksSeries<'K,'V,SortedMap<'K,'V>>>,comparer:KeyComparer<'K>,chunkMaxSize:int) = 
      new SortedChunkedMap<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> new SortedMap<'K,'V>(capacity, comparer)), comparer, None, Some(chunkMaxSize))


and
  public SortedChunkedMapCursor<'K,'V> =
    struct
      val mutable internal source : SortedChunkedMap<'K,'V>
      val mutable internal outerCursor : ICursor<'K,SortedMap<'K,'V>>
      val mutable internal innerCursor : SortedMapCursor<'K,'V>
      val mutable internal isBatch : bool
      new(source:SortedChunkedMap<'K,'V>) = 
        { source = if source <> Unchecked.defaultof<_> then source else failwith "source is null"; 
          outerCursor = source.OuterMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          isBatch = false;
        }
    end

    member inline private this.HasValidInner with get() = not (obj.ReferenceEquals(this.innerCursor.source, null))
    
    member this.CurrentKey with get() = this.innerCursor.CurrentKey
    member this.CurrentValue with get() = this.innerCursor.CurrentValue
    member this.Current with get() : KVP<'K,'V> = KeyValuePair(this.innerCursor.CurrentKey, this.innerCursor.CurrentValue)

    member this.Comparer: KeyComparer<'K> = this.source.Comparer
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
    member this.CurrentBatch: IReadOnlySeries<'K,'V> = 
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source.isSynchronized
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
        result <-
        /////////// Start read-locked code /////////////

          if this.isBatch then this.outerCursor.CurrentValue :> IReadOnlySeries<'K,'V>
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
        let version = if doSpin then Volatile.Read(&this.source.version) else 0L
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
                      false
                  else
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
                      false 
                  else
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
      this.source.Flush() // 
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
      member this.Source with get() = this.source :> IReadOnlySeries<'K,'V>
      member this.Clone() = this.Clone() :> ICursor<'K,'V>
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.source.TryGetValue(key, &value)

    interface ISpecializedCursor<'K,'V, SortedChunkedMapCursor<'K,'V>> with
      member this.Initialize() = 
        let c = this.Clone()
        c.Reset()
        c
      member this.Clone() = this.Clone()