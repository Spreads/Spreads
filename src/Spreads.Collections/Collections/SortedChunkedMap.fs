// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Collections

open System
open System.Collections.Generic
open System.Linq
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open FSharp.Control.Tasks

open Spreads
open Spreads.Collections
open Spreads.Utils

// NB outer map version must be synced with SCM._version

// NB Outer map operations usually have some IO, so no need for nano-optimization here
// One should use just SortedMap<,> if working with in-memory data (especially when SM will be backed by mmaped Memory<>

[<AllowNullLiteral>]
[<AbstractClass>]
type SortedChunkedMapBase<'K,'V> 
  internal 
  (
    outerFactory:Func<KeyComparer<'K>,IMutableSeries<'K, SortedMap<'K,'V>>>,
    innerFactory:Func<int, KeyComparer<'K>,SortedMap<'K,'V>>, 
    comparer:KeyComparer<'K>,
    hasher:Opt<IKeyHasher<'K>>, 
    chunkMaxSize:Opt<int>) as this=
  inherit ContainerSeries<'K,'V, SortedChunkedMapCursor<'K,'V>>()

  let outerMap = outerFactory.Invoke(comparer)

  let mutable prevRHash = Unchecked.defaultof<'K>
  let mutable prevRBucket: SortedMap<'K,'V> = null  

  let mutable prevWHash = Unchecked.defaultof<'K>
  let mutable prevWBucket: SortedMap<'K,'V> = null 

  [<DefaultValueAttribute>]
  val mutable internal innerFactory : Func<int, KeyComparer<'K>,SortedMap<'K,'V>>

  [<DefaultValueAttribute>]
  val mutable internal isReadOnly : bool

  [<DefaultValueAttribute>] 
  val mutable internal orderVersion : int64

  /// Non-zero only for the defaul slicing logic. When zero, we do not check for chunk size
  let chunkUpperLimit : int = 
    if hasher.IsPresent then 0
    else
      if chunkMaxSize.IsPresent && chunkMaxSize.Present > 0 then chunkMaxSize.Present
      else Settings.SCMDefaultChunkLength
  
  let mutable id = String.Empty

  // used only when chunkUpperLimit = 0
  let hasher : IKeyHasher<'K> = if hasher.IsPresent then hasher.Present else Unchecked.defaultof<_>

  do
    this._isSynchronized <- true
    this._version <- outerMap.Version
    this._nextVersion <- outerMap.Version
    this.innerFactory <- innerFactory

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member inline private this.EnterWriteLock() : bool =
    enterWriteLockIf &this.Locker this._isSynchronized

  // hash for locating existing value
  member inline private this.ExistingHashBucket(key) =
    // we return KVP to save TryFind(LE) call for the case when chunkUpperLimit > 0
    if chunkUpperLimit = 0 then
      KVP(hasher.Hash(key), Unchecked.defaultof<_>)
    else
      let mutable h = Unchecked.defaultof<_>
      outerMap.TryFindAt(key, Lookup.LE, &h) |> ignore
      h

  //member this.Clear() : Task = 
  //  let first = this.First
  //  if first.IsPresent then
  //    task {
  //      let! removed = this.TryRemoveMany(first.Present.Key, Lookup.GE)
  //      return ()
  //    } :> Task
  //  else TaskUtil.CompletedTask

  override this.Comparer with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = comparer

  [<Obsolete("This involves deserializetion of all chunks just to get count")>]
  member this.Count
    with get() = 
      let entered = this.EnterWriteLock()
      try
        let mutable size' = 0L
        for okvp in outerMap do
          size' <- size' + int64 okvp.Value.Count
        size'
      finally
        exitWriteLockIf &this.Locker entered

  // TODO remove Obsolete, just make sure these methods are not used inside SCM
  [<Obsolete>]
  member internal __.OuterMap with get() = outerMap
  [<Obsolete>]
  member internal __.ChunkUpperLimit with get() = chunkUpperLimit
  [<Obsolete>]
  member internal __.Hasher with get() = hasher

  member this.Version 
    with get() = readLockIf &this._nextVersion &this._version (not this.isReadOnly) (fun _ -> this._version)
    and internal set v = 
      enterWriteLockIf &this.Locker true |> ignore
      try
        this._version <- v // NB setter only for deserializer
        this._nextVersion <- v
      finally
        exitWriteLockIf &this.Locker true

  member this.Complete() =
    enterWriteLockIf &this.Locker true |> ignore
    try
      //if entered then Interlocked.Increment(&this._nextVersion) |> ignore
      if not this.isReadOnly then 
          this.isReadOnly <- true
          this._isSynchronized <- false
          this.NotifyUpdate(false)
      Task.CompletedTask
    finally
      //Interlocked.Increment(&this._version) |> ignore
      exitWriteLockIf &this.Locker true

  override this.IsCompleted with get() = readLockIf &this._nextVersion &this._version (not this.isReadOnly) (fun _ -> this.isReadOnly)

  override this.IsIndexed with get() = false

  member inline private __.FirstUnchecked
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = 
      let outerFirst = outerMap.First
      if outerFirst.IsMissing then
        Opt<_>.Missing
      else
        outerFirst.Present.Value.First
      
  override this.First
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = 
      readLockIf &this._nextVersion &this._version (not this.isReadOnly) (fun _ -> this.FirstUnchecked)

  member inline private __.LastUnchecked
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : Opt<KeyValuePair<'K,'V>> = 
      let outerLast = outerMap.Last
      if outerLast.IsMissing then
        Opt<_>.Missing
      else
        outerLast.Present.Value.Last
      
  override this.Last
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : Opt<KeyValuePair<'K,'V>> = 
      readLockIf &this._nextVersion &this._version (not this.isReadOnly) (fun _ -> this.LastUnchecked )

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.TryUpdateRBucket(key) : unit =
    let mutable prevBucket' = Unchecked.defaultof<_>
    let foundNewBucket: bool =
      // if hasher is set then for each key we have deterministic hash that does not depend on outer map at all
      if chunkUpperLimit = 0 then // deterministic hash
        let hash = hasher.Hash(key)
        let c = comparer.Compare(hash, prevRHash)
        // either prevBucket is wrong or not set at all (key = default)
        if c <> 0 then outerMap.TryFindAt(hash, Lookup.EQ, &prevBucket')        
        else false
      else
        // we are inside previous bucket, getter alway should try to get a value from there
        if not (prevRBucket <> null && comparer.Compare(key, prevRHash) >= 0 
          && prevRBucket.CompareToLast(key) <= 0) then
          outerMap.TryFindAt(key, Lookup.LE, &prevBucket')
        else false
    if foundNewBucket then // bucket switch
      prevRHash <- prevBucket'.Key
      prevRBucket <- prevBucket'.Value

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member internal this.TryGetValueUnchecked(key, [<Out>]value: byref<'V>) : bool =
    this.TryUpdateRBucket(key)
    // NB if wrong bucket and we couldn't change it above then TGV will just return false
    if prevRBucket <> null then prevRBucket.TryGetValueUnchecked(key, &value) else false

  override this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
    let mutable value' = Unchecked.defaultof<_>
    let inline res() = this.TryGetValueUnchecked(key, &value')
    if readLockIf &this._nextVersion &this._version (not this.isReadOnly) res then
      value <- value'; true
    else false

  override this.GetCursor() =
    let entered = this.EnterWriteLock()
    try
      // if source is already read-only, MNA will always return false
      if this.isReadOnly then new SortedChunkedMapCursor<_,_>(this) :> ICursor<'K,'V>
      else
        let c = new BaseCursorAsync<_,_,_>(Func<_>(this.GetEnumerator))
        c :> ICursor<'K,'V>    
    finally
      exitWriteLockIf &this.Locker entered

  override this.GetContainerCursor() = this.GetEnumerator()

  // .NETs foreach optimization
  member this.GetEnumerator() =
    readLockIf &this._nextVersion &this._version (not this.isReadOnly) (fun _ ->
      new SortedChunkedMapCursor<_,_>(this)
    )
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.TryFindTuple(key:'K, direction:Lookup) : struct (bool * KeyValuePair<'K,'V>) = 
    let tupleResult =
      let mutable kvp = Unchecked.defaultof<KeyValuePair<'K, 'V>>
      
      let hashBucket = this.ExistingHashBucket key
      let hash = hashBucket.Key
      let c = comparer.Compare(hash, prevRHash)

      let res, pair =
        if c <> 0 || (prevRBucket = null) then // not in the prev bucket, switch bucket to newHash
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = 
            if hashBucket.Value <> null then
              innerMapKvp <- hashBucket
              true
            else outerMap.TryFindAt(hash, Lookup.EQ, &innerMapKvp)
          if ok then
            prevRHash <- hash
            prevRBucket <- innerMapKvp.Value
            innerMapKvp.Value.TryFindAt(key, direction)
          else
            false, Unchecked.defaultof<KeyValuePair<'K, 'V>>
        else
          // TODO null reference when called on empty
          if prevRBucket <> null then prevRBucket.TryFindAt(key, direction)
          else false, Unchecked.defaultof<KeyValuePair<'K, 'V>>

      if res then // found in the bucket of key
        struct (true, pair)
      else
        match direction with
        | Lookup.LT | Lookup.LE ->
          // look into previous bucket and take last
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFindAt(hash, Lookup.LT, &innerMapKvp)
          if ok then
            Debug.Assert(innerMapKvp.Value.Last.IsPresent) // if previous was found it shoudn't be empty
            let pair = innerMapKvp.Value.Last.Present
            struct (true, pair)
          else
            struct (false, kvp)
        | Lookup.GT | Lookup.GE ->
          // look into next bucket and take first
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFindAt(hash, Lookup.GT, &innerMapKvp)
          if ok then
            Debug.Assert(innerMapKvp.Value.First.IsPresent) // if previous was found it shoudn't be empty
            let pair = innerMapKvp.Value.First.Present
            struct (true, pair)
          else
            struct (false, kvp)
        | _ -> struct (false, kvp) // LookupDirection.EQ
    tupleResult

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member this.TryFindUnchecked(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let tupleResult = this.TryFindTuple(key, direction)
    let struct (ret0,res0) = tupleResult
    result <- res0
    ret0

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  override this.TryFindAt(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let res() = this.TryFindTuple(key, direction)
    let tupleResult = readLockIf &this._nextVersion &this._version (not this.isReadOnly) res
    let struct (ret0,res0) = tupleResult
    result <- res0
    ret0

  //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
  override this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>).Select(fun kvp -> kvp.Key)

  //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
  override this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>).Select(fun kvp -> kvp.Value)

  //// NB first/last optimization is possible, but removes are rare in the primary use case
  //[<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  //member private this.TryRemoveUnchecked(key) : ValueTask<Opt<KVP<'K,'V>>>  =
  //  let hashBucket = this.ExistingHashBucket key
  //  let hash = hashBucket.Key
  //  let c = comparer.Compare(hash, prevWHash)
    
  //  if c = 0 && prevWBucket <> null then
  //    let res = prevWBucket.TryRemove(key).Result
  //    if res.IsPresent then
  //      Debug.Assert(prevWBucket._version = this._version + 1L, "Verion of the active bucket must much SCM version")
  //      prevWBucket._version <- this._version + 1L
  //      prevWBucket._nextVersion <- this._version + 1L
  //      // NB no outer set here, it must happen on prev bucket switch
  //      if prevWBucket.Count = 0 then
  //        let t = task {
  //          // but here we must notify outer that version has changed
  //          // setting empty bucket will remove it in the outer map
  //          // (implementation detail, but this is internal) 
  //          let! outerSet = outerMap.Set(prevWHash, prevWBucket)
  //          prevWBucket <- null
  //          return Opt.Present(KVP(key,res.Present))
  //        }
  //        ValueTask<Opt<KVP<'K,'V>>>(t)
  //      else
  //        ValueTask<_>(Opt.Present(KVP(key,res.Present)))
  //    else
  //      ValueTask<_>(Opt<_>.Missing)
  //  else
  //    let t = task {
  //      if prevWBucket <> null then 
  //        // store potentially modified active bucket in the outer, including version
  //        do! this.FlushUnchecked()
  //      let mutable innerMapKvp = Unchecked.defaultof<_>
  //      let ok = 
  //        if hashBucket.Value <> Unchecked.defaultof<_> then
  //          innerMapKvp <- hashBucket
  //          true
  //        else outerMap.TryFindAt(hash, Lookup.EQ, &innerMapKvp)
  //      if ok then
  //        let bucket = (innerMapKvp.Value)
  //        prevWHash <- hash
  //        prevWBucket <- bucket
  //        let res = bucket.TryRemove(key).Result
  //        if res.IsPresent then
  //          bucket._version <- this._version + 1L
  //          bucket._nextVersion <- this._version + 1L
  //          // NB empty will be removed, see comment above
  //          let! outerSet = outerMap.Set(prevWHash, bucket)
  //          if bucket.Count = 0 then
  //            prevWBucket <- null
  //        return Opt.Present(KVP(key,res.Present))
  //      else
  //        return Opt<_>.Missing
  //    }
  //    ValueTask<Opt<KVP<'K,'V>>>(t)
     
  //[<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  //member this.TryRemove(key) : ValueTask<Opt<'V>>  =
  //  let t = task {
  //    let entered = this.EnterWriteLock()
  //    if entered then Interlocked.Increment(&this._nextVersion) |> ignore
  //    let mutable removed = false
  //    try
  //      let! res = this.TryRemoveUnchecked(key)
  //      if res.IsPresent then removed <- true
  //      return if res.IsPresent then Opt.Present(res.Present.Value) else Opt<_>.Missing
  //    finally
  //      this.NotifyUpdate(true)
  //      if removed then Interlocked.Increment(&this._version) |> ignore
  //      elif entered then Interlocked.Decrement(&this._nextVersion) |> ignore
  //      exitWriteLockIf &this.Locker entered
  //      #if DEBUG
  //      this.FlushUnchecked()
  //      Debug.Assert((outerMap.Version = this._version))
  //      if entered && this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
  //      #else
  //      if entered && this._version <> this._nextVersion then Environment.FailFast("this._version <> this._nextVersion")
  //      #endif
  //  }
  //  ValueTask<Opt<'V>>(t)


  //[<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  //member this.TryRemoveFirst() : ValueTask<Opt<KVP<'K,'V>>> =
  //  let t = task {
  //    let entered = this.EnterWriteLock()
  //    if entered then Interlocked.Increment(&this._nextVersion) |> ignore
  //    let mutable removed = false
  //    try
  //        let f = this.FirstUnchecked
  //        if f.IsPresent then
  //          let! res = this.TryRemoveUnchecked(f.Present.Key)
  //          if res.IsPresent then removed <- true
  //          return res
  //        else return Opt<_>.Missing
  //    finally
  //      this.NotifyUpdate(true)
  //      if removed then Interlocked.Increment(&this._version) |> ignore
  //      elif entered then Interlocked.Decrement(&this._nextVersion) |> ignore
  //      exitWriteLockIf &this.Locker entered
  //      #if DEBUG
  //      this.FlushUnchecked()
  //      Debug.Assert((outerMap.Version = this._version))
  //      if entered && this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
  //      #else
  //      if entered && this._version <> this._nextVersion then Environment.FailFast("this._version <> this._nextVersion")
  //      #endif
  //  }
  //  ValueTask<Opt<_>>(t)

  //[<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  //member this.TryRemoveLast() : ValueTask<Opt<KVP<'K,'V>>> =
  //  let t = task {
  //    let entered = this.EnterWriteLock()
  //    if entered then Interlocked.Increment(&this._nextVersion) |> ignore
  //    let mutable removed = false
  //    try
  //        let l = this.LastUnchecked
  //        if l.IsPresent then
  //          let! res = this.TryRemoveUnchecked(l.Present.Key)
  //          if res.IsPresent then removed <- true
  //          return res
  //        else return Opt<_>.Missing
  //    finally
  //      this.NotifyUpdate(true)
  //      if removed then Interlocked.Increment(&this._version) |> ignore
  //      elif entered then Interlocked.Decrement(&this._nextVersion) |> ignore
  //      exitWriteLockIf &this.Locker entered
  //      #if DEBUG
  //      this.FlushUnchecked()
  //      Debug.Assert((outerMap.Version = this._version))
  //      if entered && this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
  //      #else
  //      if entered && this._version <> this._nextVersion then Environment.FailFast("this._version <> this._nextVersion")
  //      #endif
  //  }
  //  ValueTask<Opt<_>>(t)
  

  //member private this.TryRemoveManyUnchecked(key:'K, direction:Lookup) : ValueTask<Opt<KVP<'K,'V>>> =
  //  let version = outerMap.Version
  //  let result = 
  //    if outerMap.Last.IsMissing then
  //      Debug.Assert((prevWBucket = null))
  //      prevWBucket <- null
  //      ValueTask<_>(Opt<_>.Missing)
  //    else
  //      let removed =
  //        match direction with
  //        | Lookup.EQ -> 
  //          this.TryRemoveUnchecked(key)
  //        | Lookup.LT | Lookup.LE ->
  //          let t = task {
  //            do! this.FlushUnchecked() // ensure current version is set in the outer map from the active chunk
  //            let mutable tmp = Unchecked.defaultof<_>
  //            if outerMap.TryFindAt(key, Lookup.LE, &tmp) then
  //              let! r1 = tmp.Value.TryRemoveMany(key, direction)
  //              tmp.Value._version <- this._version + 1L
  //              tmp.Value._nextVersion <- this._version + 1L
  //              let! r2 = outerMap.TryRemoveMany(key, tmp.Value, direction)  // NB order matters
  //              if r1.IsPresent || r2 then increment &this.orderVersion
  //              return if r1.IsPresent then r1 else (if r2 then tmp.Value.Last else Opt<_>.Missing)
  //            else return Opt<_>.Missing // no key below the requested key, notjing to delete
  //          }
  //          ValueTask<Opt<KVP<'K,'V>>>(t)
  //        | Lookup.GT | Lookup.GE ->
  //          let t: Task<Opt<KVP<'K,'V>>> = task {
  //            do! this.FlushUnchecked() // ensure current version is set in the outer map from the active chunk
  //            let mutable tmp = Unchecked.defaultof<_>
  //            if outerMap.TryFindAt(key, Lookup.LE, &tmp) then // NB for deterministic hash LE still works
  //              let chunk = tmp.Value;
  //              let! r1 = chunk.TryRemoveMany(key, direction)
  //              chunk._version <- this._version + 1L
  //              chunk._nextVersion <- this._version + 1L
  //              let! r2 = outerMap.TryRemoveMany(key, chunk, direction)
  //              if r1.IsPresent || r2 then increment &this.orderVersion
  //              return if r1.IsPresent then r1 else (if r2 then chunk.Last else Opt<_>.Missing)
  //            else
  //              // TODO the next line is not needed, we remove all items in SCM here and use the chunk to set the version
  //              let firstChunk = outerMap.First.Present.Value
  //              let! r1 = firstChunk.TryRemoveMany(key, direction) // this will remove all items in the chunk
  //              Debug.Assert(firstChunk.Count = 0, "The first chunk must have been completely cleared")
  //              firstChunk._version <- this._version + 1L
  //              firstChunk._nextVersion <- this._version + 1L
  //              let! r2 = outerMap.TryRemoveMany(key, firstChunk, direction)
  //              if r1.IsPresent || r2 then increment &this.orderVersion
  //              return if r1.IsPresent then r1 else (if r2 then firstChunk.Last else Opt<_>.Missing)
  //          }
  //          ValueTask<Opt<KVP<'K,'V>>>(t)
  //        | _ -> failwith "wrong direction"          
  //      removed
  //  result

  ///// Removes all elements that are to `direction` from `key`
  //member this.TryRemoveMany(key:'K, direction:Lookup) : ValueTask<Opt<KVP<'K,'V>>> =
  //  // TODO(low) for non-EQ this is not atomic now, could add a special method for removed in IMutableChunksSeries
  //  // then in its impl it could be done in a transaction. However, this nethod is not used frequently
  //  let t = task {
  //    let entered = this.EnterWriteLock()
  //    if entered then Interlocked.Increment(&this._nextVersion) |> ignore
  //    let mutable removed = false
  //    try      
  //      return! this.TryRemoveManyUnchecked(key, direction)
  //    finally
  //      this.NotifyUpdate(true)
  //      if removed then Interlocked.Increment(&this._version) |> ignore 
  //      elif entered then Interlocked.Decrement(&this._nextVersion) |> ignore
  //      exitWriteLockIf &this.Locker entered
  //      #if DEBUG
  //      Debug.Assert(outerMap.Version = this._version)
  //      if entered && this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
  //      #else
  //      if entered && this._version <> this._nextVersion then Environment.FailFast("this._version <> this._nextVersion")
  //      #endif
  //  }
  //  ValueTask<Opt<_>>(t)

  // TODO after checks, should form changed new chunks and use outer append method with rewrite
  // TODO atomic append with single version increase, now it is a sequence of remove/add mutations
  //[<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  //member this.Append(appendMap:ISeries<'K,'V>, option:AppendOption) : int =
  //  raise (NotImplementedException())
    //let hasEqOverlap (old:SortedChunkedMapBaseGeneric<_,_>) (append:ISeries<'K,'V>) : bool =
    //  if comparer.Compare(append.First.Key, old.LastUnchecked.Key) > 0 then false
    //  else
    //    let oldC = new SortedChunkedMapBaseGenericCursor<_,_>(old, false) :> ICursor<'K,'V>
    //    let appC = append.GetCursor();
    //    let mutable cont = true
    //    let mutable overlapOk = 
    //      oldC.MoveAt(append.First.Key, Lookup.EQ) 
    //        && appC.MoveFirst() 
    //        && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
    //        && Unchecked.equals oldC.CurrentValue appC.CurrentValue
    //    while overlapOk && cont do
    //      if oldC.MoveNext() then
    //        overlapOk <-
    //          appC.MoveNext() 
    //          && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
    //          && Unchecked.equals oldC.CurrentValue appC.CurrentValue
    //      else cont <- false
    //    overlapOk
    //if appendMap.IsEmpty then
    //  0
    //else
    //  let mutable entered = false
    //  let mutable finished = false
    //  try
    //    try ()
    //    finally
    //      entered <- this.EnterWriteLock()
    //      if entered then Interlocked.Increment(&this._nextVersion) |> ignore
    //    let result =
    //      match option with
    //      | AppendOption.ThrowOnOverlap ->
    //        if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
    //          let mutable c = 0
    //          for i in appendMap do
    //            c <- c + 1
    //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
    //          c
    //        else 
    //          let exn = SpreadsException("values overlap with existing")
    //          raise exn
    //      | AppendOption.DropOldOverlap ->
    //        if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
    //          let mutable c = 0
    //          for i in appendMap do
    //            c <- c + 1
    //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
    //          c
    //        else
    //          let removed = this.RemoveMany(appendMap.First.Key, Lookup.GE)
    //          Trace.Assert(removed)
    //          let mutable c = 0
    //          for i in appendMap do
    //            c <- c + 1
    //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
    //          c
    //      | AppendOption.IgnoreEqualOverlap ->
    //        if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
    //          let mutable c = 0
    //          for i in appendMap do
    //            c <- c + 1
    //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
    //          c
    //        else
    //          let isEqOverlap = hasEqOverlap this appendMap
    //          if isEqOverlap then
    //            let appC = appendMap.GetCursor();
    //            if appC.MoveAt(this.LastUnsafe.Key, Lookup.GT) then
    //              this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
    //              let mutable c = 1
    //              while appC.MoveNext() do
    //                this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
    //                c <- c + 1
    //              c
    //            else 0
    //          else 
    //            let exn = SpreadsException("overlapping values are not equal")
    //            raise exn
    //      | AppendOption.RequireEqualOverlap ->
    //        if outerMap.IsEmpty then
    //          let mutable c = 0
    //          for i in appendMap do
    //            c <- c + 1
    //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
    //          c
    //        elif comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
    //          let exn = SpreadsException("values do not overlap with existing")
    //          raise exn
    //        else
    //          let isEqOverlap = hasEqOverlap this appendMap
    //          if isEqOverlap then
    //            let appC = appendMap.GetCursor();
    //            if appC.MoveAt(this.LastUnsafe.Key, Lookup.GT) then
    //              this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
    //              let mutable c = 1
    //              while appC.MoveNext() do
    //                this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
    //                c <- c + 1
    //              c
    //            else 0
    //          else 
    //            let exn = SpreadsException("overlapping values are not equal")
    //            raise exn
    //      | _ -> failwith "Unknown AppendOption"
    //    finished <- true
    //    result
    //  finally
    //    if not finished then Environment.FailFast("SCM.Append must always succeed")
    //    Interlocked.Increment(&this._version) |> ignore
    //    this.FlushUnchecked()
    //    exitWriteLockIf &this.Locker entered
    //    #if DEBUG
    //    Debug.Assert(outerMap.Version = this._version)
    //    if entered && this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
    //    #else
    //    if entered && this._version <> this._nextVersion then Environment.FailFast("this._version <> this._nextVersion")
    //    #endif
    
  member this.Id with get() = id and set(newid) = id <- newid

  // x0
  
  //new() = 
  //  let comparer:KeyComparer<'K> = KeyComparer<'K>.Default
  //  let factory = (fun (c:KeyComparer<'K>) -> new SortedMap<'K,SortedMap<'K,'V>>(c) :> IMutableSeries<'K,SortedMap<'K,'V>>)
  //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, None)
  
  //// x1

  ///// In-memory sorted chunked map
  //new(comparer:IComparer<'K>) = 
  //  let factory = (fun (c:KeyComparer<'K>) -> new SortedMap<'K,SortedMap<'K,'V>>(c) :> IMutableSeries<'K,SortedMap<'K,'V>>)
  //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), KeyComparer<'K>.Create(comparer), None, None)
  
  //new(comparer:KeyComparer<'K>) = 
  //  let factory = (fun (c:KeyComparer<'K>) -> new SortedMap<'K,SortedMap<'K,'V>>(c) :> IMutableSeries<'K,SortedMap<'K,'V>>)
  //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, None)
  

  ///// In-memory sorted chunked map
  //new(hasher:Func<'K,'K>) = 
  //  let factory = (fun (c:KeyComparer<'K>) -> new SortedMap<'K,SortedMap<'K,'V>>(c) :> IMutableSeries<'K,SortedMap<'K,'V>>)
  //  let comparer:KeyComparer<'K> = KeyComparer<'K>.Default
  //  let hasher = { new IKeyHasher<'K> with
  //        member x.Hash(k) = hasher.Invoke k
  //      }
  //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, Some(hasher), None)
  
  //new(chunkMaxSize:int) = 
  //  let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K,SortedMap<'K,'V>>(c) :> IMutableSeries<'K,SortedMap<'K,'V>>)
  //  let comparer = KeyComparer<'K>.Default
  //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, Some(chunkMaxSize))

  //internal new(outerFactory:Func<IComparer<'K>,IMutableSeries<'K, SortedMap<'K,'V>>>) = 
  //  let comparer = KeyComparer<'K>.Default
  //  new SortedChunkedMapBase<_,_>(outerFactory.Invoke, (fun (capacity, comparer) ->let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, None)
  
  //// x2

  ///// In-memory sorted chunked map
  //new(comparer:KeyComparer<'K>,hasher:Func<'K,'K>) = 
  //  let factory = (fun (c:KeyComparer<'K>) -> new SortedMap<'K,SortedMap<'K,'V>>(c) :> IMutableSeries<'K,SortedMap<'K,'V>>)
  //  let hasher = { new IKeyHasher<'K> with
  //        member x.Hash(k) = hasher.Invoke k
  //      }
  //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, Some(hasher), None)

  //new(comparer:KeyComparer<'K>,chunkMaxSize:int) = 
  //  let factory = (fun (c:KeyComparer<'K>) -> new SortedMap<'K,SortedMap<'K,'V>>(c) :> IMutableSeries<'K,SortedMap<'K,'V>>)
  //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, Some(chunkMaxSize))

  //internal new(outerFactory:Func<KeyComparer<'K>,IMutableSeries<'K,SortedMap<'K,'V>>>,comparer:IComparer<'K>) = 
  //  new SortedChunkedMapBase<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), KeyComparer<'K>.Create(comparer), None, None)

  //internal new(outerFactory:Func<KeyComparer<'K>,IMutableSeries<'K,SortedMap<'K,'V>>>,comparer:KeyComparer<'K>) = 
  //  new SortedChunkedMapBase<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, None)

  //internal new(outerFactory:Func<IComparer<'K>,IMutableSeries<'K,SortedMap<'K,'V>>>,hasher:Func<'K,'K>) = 
  //  let comparer = KeyComparer<'K>.Default
  //  let hasher = { new IKeyHasher<'K> with
  //        member x.Hash(k) = hasher.Invoke k
  //      }
  //  new SortedChunkedMapBase<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, Some(hasher), None)
  
  //internal new(outerFactory:Func<IComparer<'K>,IMutableSeries<'K,SortedMap<'K,'V>>>,chunkMaxSize:int) = 
  //  let comparer = KeyComparer<'K>.Default
  //  new SortedChunkedMapBase<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, Some(chunkMaxSize))

  //// x3

  //internal new(outerFactory:Func<KeyComparer<'K>,IMutableSeries<'K,SortedMap<'K,'V>>>,comparer:KeyComparer<'K>,hasher:Func<'K,'K>) =
  //  let hasher = { new IKeyHasher<'K> with
  //        member x.Hash(k) = hasher.Invoke k
  //      }
  //  new SortedChunkedMapBase<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, Some(hasher), None)
  
  //new(outerFactory:Func<KeyComparer<'K>,IMutableSeries<'K,SortedMap<'K,'V>>>,comparer:KeyComparer<'K>,chunkMaxSize:int) = 
  //  new SortedChunkedMapBase<_,_>(outerFactory.Invoke, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, Some(chunkMaxSize))

  //#region Interfaces
    
  interface ISeries<'K,'V> with
    // the rest is in BaseSeries
    member this.Item with get k = this.Item(k)

  //interface IMutableSeries<'K,'V> with
  //  member this.IsAppendOnly with get() = false
  //  member this.Complete() = this.Complete()
  //  member this.Version with get() = this.Version
  //  member this.Count with get() = this.Count
  //  member this.Set(k, v) = this.Set(k,v)
  //  member this.TryAdd(k, v) = this.TryAdd(k,v)
  //  member this.TryAddLast(k, v) = this.TryAddLast(k, v)
  //  member this.TryAddFirst(k, v) = this.TryAddFirst(k, v)
  //  member this.TryRemove(k) = this.TryRemove(k)
  //  member this.TryRemoveFirst() = this.TryRemoveFirst()
  //  member this.TryRemoveLast() = this.TryRemoveLast()
  //  member this.TryRemoveMany(key:'K,direction:Lookup) = this.TryRemoveMany(key, direction) 
  //  member this.TryRemoveMany(key:'K,value:'V, direction:Lookup) = raise (NotSupportedException())
  //  member this.TryAppend(appendMap:ISeries<'K,'V>, option:AppendOption) =
  //    raise (NotImplementedException())

  //interface IPersistentSeries<'K,'V> with
  //  member this.Flush() = this.Flush()
  //  member this.Dispose() = this.DisposeAsync(true) |> ignore // TODO review
  //  member this.Id with get() = this.Id
  //#endregion

and
  public SortedChunkedMapCursor<'K,'V> =
    struct
      val mutable internal source : SortedChunkedMapBase<'K,'V>
      val mutable internal outerCursor : ICursor<'K,SortedMap<'K,'V>>
      val mutable internal innerCursor : SortedMapCursor<'K,'V>
      val mutable internal isBatch : bool
      new(source:SortedChunkedMapBase<'K,'V>) = 
        { source = if source <> Unchecked.defaultof<_> then source else failwith "source is null"; 
          outerCursor = source.OuterMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          isBatch = false;
        }
    end

    member inline private this.HasValidInner with get() = not (obj.ReferenceEquals(this.innerCursor.source, null))
    
    member this.CurrentKey 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = this.innerCursor.CurrentKey

    member this.CurrentValue 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = this.innerCursor.CurrentValue

    member this.Current 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : KVP<'K,'V> = this.innerCursor.current // KeyValuePair(this.innerCursor.CurrentKey, this.innerCursor.CurrentValue)

    member this.Comparer 
      with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get(): KeyComparer<'K> = this.source.Comparer

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] 
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.source.TryGetValue(key, &value)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveNext() =
      let mutable newInner = Unchecked.defaultof<_> 
      let mutable doSwitchInner = false
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable outerMoved = false
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        /////////// Start read-locked code /////////////
      //try
        // here we use copy-by-value of structs
        // newInner <- this.innerCursor
        if (not this.HasValidInner) && (not this.isBatch) then
          if this.outerCursor.MoveFirst() then
            newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
            doSwitchInner <- true
            result <- newInner.MoveFirst()
          else result <- false
        else
          
        // try
          if this.HasValidInner && this.innerCursor.MoveNext() then 
            // doSwitchInner <- true
            result <- true
          else
            // let entered = enterWriteLockIf &this.source.Locker doSpin
            if outerMoved || this.outerCursor.MoveNext() then
              outerMoved <- true
              this.isBatch <- false // if was batch, moved to the first element of the new batch
              newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
              doSwitchInner <- true
              let moved = newInner.MoveNext()
              // if newInner.source.size > 0 && not moved then ThrowHelper.ThrowInvalidOperationException("must move here")
              if moved then 
                result <- true
              else 
                outerMoved <- false; // need to try to move outer again
                result <- false 
            else
              outerMoved <- false
              result <- false
        //finally
            // exitWriteLockIf &this.source.Locker entered
          //with
          //| :? OutOfOrderKeyException<'K> as ooex ->
          //   raise (new OutOfOrderKeyException<'K>((if this.isBatch then this.outerCursor.CurrentValue.Last.Key else this.innerCursor.CurrentKey), "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
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
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////

          if this.isBatch then this.outerCursor.CurrentValue :> ISeries<'K,'V>
          else raise (InvalidOperationException("SortedChunkedMapBaseGenericCursor cursor is not at a batch position"))

        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      result

    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> =
      let mutable newIsBatch = this.isBatch

      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          // TODO test & review
          raise (NotImplementedException("TODO test & review"))
          try
            if (not this.HasValidInner || this.isBatch) && this.outerCursor.MoveNext() then
                newIsBatch <- true
                TaskUtil.TrueTask
            else TaskUtil.FalseTask
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
            Trace.Assert(this.isBatch)
            raise (new OutOfOrderKeyException<'K>(this.outerCursor.CurrentValue.Last.Present.Key, "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result.Result then
        this.isBatch <- newIsBatch
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MovePrevious() = 
      let mutable newInner = Unchecked.defaultof<_> 
      let mutable doSwitchInner = false
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable outerMoved = false
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
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
                entered <- enterWriteLockIf &this.source.Locker this.source._isSynchronized
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
             raise (new OutOfOrderKeyException<'K>((if this.isBatch then this.outerCursor.CurrentValue.Last.Present.Key else this.innerCursor.CurrentKey), "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        if doSwitchInner then this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveFirst() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
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
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveLast() =
      let mutable newInner = this.innerCursor
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
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
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveAt(key:'K, direction:Lookup) =
      let mutable newInner = Unchecked.defaultof<_>
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          let mutable entered = false
          try
            entered <- enterWriteLockIf &this.source.Locker this.source._isSynchronized
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
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result


    member this.MoveNextAsync(cancellationToken:CancellationToken): Task<bool> = 
      if this.source.isReadOnly then
        if this.MoveNext() then TaskUtil.TrueTask else TaskUtil.FalseTask
      else raise (NotSupportedException("Use SortedChunkedMapBaseGenericCursorAsync instead"))

    member this.Clone() =
      let mutable entered = false
      try
        entered <- enterWriteLockIf &this.source.Locker this.source._isSynchronized
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
      member this.MoveNextAsync(cancellationToken:CancellationToken): Task<bool> = 
        this.MoveNextAsync(cancellationToken)
      member this.MoveNextAsync(): Task<bool> = this.MoveNextAsync(CancellationToken.None)
      member this.DisposeAsync() = this.Dispose();Task.CompletedTask



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
    // TODO
      member this.State with get() = raise (NotImplementedException())
      member this.MoveNext(stride, allowPartial) = raise (NotImplementedException())
      member this.MovePrevious(stride, allowPartial) = raise (NotImplementedException())

    interface ISpecializedCursor<'K,'V, SortedChunkedMapCursor<'K,'V>> with
      member this.Initialize() = 
        let c = this.Clone()
        c.Reset()
        c
      member this.Clone() = this.Clone()