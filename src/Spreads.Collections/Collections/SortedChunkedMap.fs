(*  
    Copyright (c) 2014-2016 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
        
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Runtime.Serialization
open System.Threading
open System.Threading.Tasks
open System.Diagnostics

open Spreads
open Spreads.Collections

// TODO ensure that on every prevBucket change we check its version and set to outer if versions differ
// TODO subscribe to update events on prevBucket and Flush at least every second
// TODO do not flush unchanged buckets, e.g. when chunkSize = 1 we flush twice
// TODO IsMutable property
// TODO (low) name is bad, but it is rarely used directly, but rather as Series or IOrderedMap
// TODO (perf) SM cursor as struct, and here work with its members directly, not via interfaces call


[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedChunkedMap<'K,'V> 
  internal (outerFactory:IComparer<'K>->IOrderedMap<'K, SortedMap<'K,'V>>, comparer:IComparer<'K>, slicer:Func<'K,'K> option, ?chunkMaxSize:int) as this =
  inherit Series<'K,'V>()

  // TODO (low) replace outer with MapDeque, see comments in MapDeque.fs
  let outerMap = outerFactory(comparer)

  // TODO serialize size, add a method to calculate size based on outerMap only
//  [<NonSerializedAttribute>]
//  let mutable size = 0L
//  [<NonSerializedAttribute>]
//  let mutable version = outerMap.Version
  [<NonSerializedAttribute>]
  let mutable prevHash  = Unchecked.defaultof<'K>
  [<NonSerializedAttribute>]
  let mutable prevBucket = Unchecked.defaultof<SortedMap<'K,'V>>
  [<NonSerializedAttribute>]
  let mutable prevBucketIsSet  = false
  [<NonSerializedAttribute>]
  let mutable flushedVersion = outerMap.Version
  [<NonSerializedAttribute>]
  let mutable isMutable : bool = true
  [<NonSerializedAttribute>]
  let mutable cursorCounter : int = 1 // TODO either delete this or add decrement to cursor disposal
  [<NonSerializedAttribute>]
  let mutable isSync  = true
  [<NonSerializedAttribute>]
  let chunkUpperLimit : int = 
    if slicer.IsSome then 0
    else
      if chunkMaxSize.IsSome then chunkMaxSize.Value
      else OptimizationSettings.SCMDefaultChunkLength
  
  let mutable id = String.Empty
  
  [<NonSerializedAttribute>]
  let slicer : IKeySlicer<'K> = 
    match slicer with
    | Some s -> 
        { new IKeySlicer<'K> with
          member x.Hash(k) = s.Invoke k
        }
    | None ->
      { new IKeySlicer<'K> with
          member x.Hash(k) =
            match outerMap with
      //      | :? SortedMap<'K, SortedMap<'K,'V>> as sm -> 
      //        failwith "not implemented: here will be optimized lookup"
            | _ as om ->
            // Fake comparer
            // For each key, lookup LE outer key. If the bucket with this key has size < UPPER, add 
            // new values to this bucket. Else create a new bucket.
              if om.IsEmpty then k
              else
                let mutable kvp = Unchecked.defaultof<_>
                let ok = om.TryFind(k, Lookup.LE, &kvp)
                if ok then
                  // k is larger than the last key and the chunk is big enough
                  //Trace.Assert(kvp.Value.IsEmpty)
                  if kvp.Value.IsEmpty 
                    || (comparer.Compare(k,kvp.Value.Last.Key) > 0 && kvp.Value.size >= chunkUpperLimit) then k
                  else kvp.Key // NB! there was a bug: .Value.keys.[0] -- outer hash key could be smaller that the first key of its inner map
                else k
      }

  [<OnDeserialized>]
  member private this.Init(context:StreamingContext) =
      prevHash <- Unchecked.defaultof<'K>
      prevBucket <- Unchecked.defaultof<SortedMap<'K,'V>>
      prevBucketIsSet  <- false

  member this.Clear() : unit =
    if not this.IsEmpty then 
      let removed = outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)
      if removed then outerMap.Version <- outerMap.Version + 1L

  member this.Comparer with get() = comparer

  member this.Count
      with get() = 
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          let mutable size' = 0L
          for okvp in outerMap do
            size' <- size' + (int64 okvp.Value.size)
          //size <- size'
          size'
        finally
          exitLockIf this.SyncRoot entered

  member this.Version with get() = outerMap.Version

  member this.IsEmpty
      with get() =
        let entered = enterLockIf this.SyncRoot this.IsSynchronized
        try
          outerMap.IsEmpty
          //|| not (outerMap.Values |> Seq.exists (fun inner -> inner <> null && inner.size > 0))
        finally
          exitLockIf this.SyncRoot entered

  member this.Complete() = 
    if isMutable then 
        isMutable <- false
        if cursorCounter > 0 then 
          this.onCompletedEvent.Trigger(true)
  member internal this.IsMutable with get() = isMutable
  override this.IsReadOnly with get() = not isMutable
  override this.IsIndexed with get() = false

  member this.IsSynchronized with get() = isSync and set v = isSync <- v

  member this.SyncRoot with get() = outerMap.SyncRoot

  // TODO! there must be a smarter locking strategy at buckets level (instead of syncRoot)
  // 
  member this.Item 
    with get key =
      let entered = enterLockIf this.SyncRoot this.IsSynchronized
      let hash = slicer.Hash(key)
      let subKey = key
      try
        let c = comparer.Compare(hash, prevHash)
        if c = 0 && prevBucketIsSet then
          prevBucket.[subKey] // this could raise keynotfound exeption
        else
          let bucket =
            let mutable bucketKvp = Unchecked.defaultof<_>
            let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
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
      let entered = enterLockIf this.SyncRoot this.IsSynchronized
      let hash = slicer.Hash(key)
      let subKey = key
      try
        let c = comparer.Compare(hash, prevHash)
        if c = 0 && prevBucketIsSet then // avoid generic equality and null compare
          let s1 = prevBucket.size
          prevBucket.[subKey] <- value
          outerMap.Version <- outerMap.Version + 1L
          let s2 = prevBucket.size
          //size <- size + int64(s2 - s1)
          if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(key,value))
        else
          if prevBucketIsSet then
            outerMap.Version <- outerMap.Version - 1L // set operation increments version, here not needed
            //prevBucket.Capacity <- prevBucket.Count // trim excess, save changes to modified bucket
            outerMap.[prevHash] <- prevBucket // will store bucket if outer is persistent
          let isNew, bucket = 
            let mutable bucketKvp = Unchecked.defaultof<_>
            let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
            if ok then
              false, bucketKvp.Value
            else
              // outerMap.Count could be VERY slow, do not do this
              let averageSize = 4L //try size / (int64 outerMap.Count) with | _ -> 4L // 4L in default
              let newSm = new SortedMap<'K,'V>(int averageSize, comparer)
              newSm.IsSynchronized <- this.IsSynchronized
              true, newSm
          //let s1 = bucket.size
          bucket.[subKey] <- value
          if isNew then
            outerMap.Version <- outerMap.Version - 1L // set operation increments version, here not needed
            outerMap.[hash] <- bucket
          outerMap.Version <- outerMap.Version + 1L
          //let s2 = bucket.size
          //size <- size + int64(s2 - s1)
          if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(key,value))
          prevHash <- hash
          prevBucket <- bucket
          prevBucketIsSet <- true
      finally
          exitLockIf this.SyncRoot entered

    
  member this.First
    with get() = 
      let entered = enterLockIf this.SyncRoot this.IsSynchronized
      try
        if this.IsEmpty then 
          raise (InvalidOperationException("Could not get the first element of an empty map"))
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
      if prevBucketIsSet && flushedVersion <> outerMap.Version then
        //&& outerMap.[prevHash].Version <> prevBucket.Version then
          //prevBucket.Capacity <- prevBucket.Count // trim excess, save changes to modified bucket
          outerMap.Version <- outerMap.Version - 1L // set operation increments version, here not needed
          outerMap.[prevHash] <- prevBucket
          flushedVersion <- outerMap.Version
    finally
      exitLockIf this.SyncRoot entered

  override x.Finalize() =
    // TODO check if flushed already
    let flushed = false
    // no locking, no-one has a reference to this
    if prevBucketIsSet && not flushed then
      //&& outerMap.[prevHash].Version <> prevBucket.Version then
          //prevBucket.Capacity <- prevBucket.Count // trim excess, save changes to modified bucket
          outerMap.Version <- outerMap.Version - 1L // set operation increments version, here not needed
          outerMap.[prevHash] <- prevBucket
    

  override this.GetCursor() : ICursor<'K,'V> =
    this.GetCursor(outerMap.GetCursor(), true, Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V>>, false)

  member private this.GetCursor(outer:ICursor<'K,SortedMap<'K,'V>>, isReset:bool,currentBatch:IReadOnlyOrderedMap<'K,'V>, isBatch:bool) : ICursor<'K,'V> =
    // TODO
    let nextBatch : Task<IReadOnlyOrderedMap<'K,'V>> ref = ref Unchecked.defaultof<Task<IReadOnlyOrderedMap<'K,'V>>>
    
    let outer = ref outer
    outer.Value.MoveFirst() |> ignore // otherwise initial move is skipped in MoveAt, isReset knows that we haven't started in SHM even when outer is started
    let mutable inner : SortedMapCursor<'K,'V> =  Unchecked.defaultof<_> //outer.Value.CurrentValue.GetSMCursor()
    let isReset = ref isReset
    let mutable currentBatch : IReadOnlyOrderedMap<'K,'V> = currentBatch
    let isBatch = ref isBatch

    let observerStarted = ref false
    let mutable semaphore : SemaphoreSlim = Unchecked.defaultof<_>
    let mutable are : AutoResetEvent =  Unchecked.defaultof<_>
    let onNextHandler : OnNextHandler<'K,'V> = 
      OnNextHandler(fun (kvp:KVP<'K,'V>) ->
          if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
      )
    let onCompletedHandler : OnCompletedHandler = 
      OnCompletedHandler(fun _ ->
          if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
      )

    { new ICursor<'K,'V> with
        member c.Comparer: IComparer<'K> = comparer
        member c.Source with get() = this :> ISeries<_,_>
        member c.IsContinuous with get() = false
        member c.Clone() = 
          let clone = this.GetCursor(outer.Value.Clone(), !isReset, currentBatch, !isBatch)
          if not !isReset then
            let moved = clone.MoveAt(c.CurrentKey, Lookup.EQ)
            if not moved then invalidOp "cannot correctly clone SCM cursor"
          clone
        
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

        member p.MovePrevious() = 
          let entered = enterLockIf this.SyncRoot this.IsSynchronized
          try
            if isReset.Value then p.MoveLast()
            else
              let res = inner.MovePrevious()
              if res then
                isBatch := false
                true
              else
                if outer.Value.MovePrevious() then // go to the previous bucket
                  inner <- outer.Value.CurrentValue.GetSMCursor()
                  let res = inner.MoveLast()
                  if res then
                    isBatch := false
                    true
                  else
                    raise (ApplicationException("Unexpected - empty bucket")) 
                else
                  false
          finally
            exitLockIf this.SyncRoot entered

        // TODO (!) review the entire method for edge cases
        member p.MoveAt(key:'K, direction:Lookup) = 
          let entered = enterLockIf this.SyncRoot this.IsSynchronized
          try
            let newHash = slicer.Hash(key)
            let newSubIdx = key
            let c = 
              if isReset.Value then 2 // <> 0 below
              else comparer.Compare(newHash, outer.Value.CurrentKey)
            let res =
              if c <> 0 then // not in the current bucket, switch bucket
                if outer.Value.MoveAt(newHash, Lookup.EQ) then // Equal!
                  inner <- outer.Value.CurrentValue.GetSMCursor()
                  inner.MoveAt(newSubIdx, direction)
                else
                  false
              else
                #if PRERELEASE
                Trace.Assert(not <| Unchecked.equals inner Unchecked.defaultof<SortedMapCursor<'K,'V>>)
                #endif
                inner.MoveAt(newSubIdx, direction)
                   
            if res then
              isReset := false
              isBatch := false
              true
            else
                match direction with
                | Lookup.LT | Lookup.LE ->
                  // look into previous bucket
                  if outer.Value.MovePrevious() then
                    inner <- outer.Value.CurrentValue.GetSMCursor()
                    let res = inner.MoveAt(newSubIdx, direction)
                    if res then
                      isBatch := false
                      isReset := false
                      true
                    else
                      p.Reset()
                      false
                  else
                    p.Reset()
                    false 
                | Lookup.GT | Lookup.GE ->
                  // look into next bucket
                  let moved = outer.Value.MoveNext() 
                  if moved then
                    inner <- outer.Value.CurrentValue.GetSMCursor()
                    let res = inner.MoveAt(newSubIdx, direction)
                    if res then
                      isBatch := false
                      isReset := false
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

        member p.MoveFirst() = 
          let entered = enterLockIf this.SyncRoot this.IsSynchronized
          try
            if this.IsEmpty then false
            else p.MoveAt(this.First.Key, Lookup.EQ)
          finally
            exitLockIf this.SyncRoot entered

        member p.MoveLast() = 
          let entered = enterLockIf this.SyncRoot this.IsSynchronized
          try
            if this.IsEmpty then false
            else p.MoveAt(this.Last.Key, Lookup.EQ)
          finally
            exitLockIf this.SyncRoot entered

        // TODO (v.0.2+) We now require that a false move keeps cursors on the same key before unsuccessfull move
        // Also, calling CurrentKey/Value must never throw, remove try/catch here to avoid muting error that should not happen
        //
        // (delete this) NB These are "undefined" when cursor is in invalid state, but they should not thow
        // Try/catch adds almost no overhead, even compared to null check, in the normal case. Calling these properties before move is 
        // an application error and should be logged or raise an assertion failure
        member c.CurrentKey with get() = try inner.CurrentKey with | _ -> Unchecked.defaultof<_>
        member c.CurrentValue with get() = try inner.CurrentValue with | _ -> Unchecked.defaultof<_>
        //member c.Current with get() : obj = box c.Current
        member c.TryGetValue(key: 'K, value: byref<'V>): bool = this.TryGetValue(key, &value)



        member p.CurrentBatch = 
          if !isBatch then currentBatch
          else invalidOp "Current move is single, cannot return a batch"

        member p.MoveNextBatch(ct) =
          Async.StartAsTask(async {
            let entered = enterLockIf this.SyncRoot this.IsSynchronized
            try
              if isReset.Value then 
                if outer.Value.MoveFirst() then
                  currentBatch <- outer.Value.CurrentValue :> IReadOnlyOrderedMap<'K,'V>
                  isBatch := true
                  isReset := false
                  return true
                else return false
              else
                if !isBatch then
                  let couldMove = outer.Value.MoveNext() // ct |> Async.AwaitTask // NB not async move next!
                  if couldMove then
                    currentBatch <- outer.Value.CurrentValue :> IReadOnlyOrderedMap<'K,'V>
                    isBatch := true
                    return true
                  else 
                    // no batch, but place cursor at the end of the last batch so that move next won't get null reference exception
                    inner <- outer.Value.CurrentValue.GetSMCursor()
                    if not outer.Value.CurrentValue.IsEmpty then inner.MoveLast() |> ignore
                    return false
                else
                  return false
            finally
              exitLockIf this.SyncRoot entered
          }, TaskCreationOptions.None, ct)

      interface IEnumerator<KVP<'K,'V>> with
        member p.Reset() = 
          if not !isReset then
            outer.Value.Reset()
            outer.Value.MoveFirst() |> ignore
            //inner.Reset()
            inner <- Unchecked.defaultof<_>
            isReset := true

        member p.Dispose() = 
            p.Reset()
            if !observerStarted then
              this.onNextEvent.Publish.RemoveHandler(onNextHandler)
              this.onCompletedEvent.Publish.RemoveHandler(onCompletedHandler)
        
        member p.MoveNext() = 
          let entered = enterLockIf this.SyncRoot this.IsSynchronized
          try
            if isReset.Value then (p :?> ICursor<'K,'V>).MoveFirst()
            else
              let res = inner.MoveNext() // could pass curent key by ref and save some single-dig %
              if res then
                if !isBatch then isBatch := false
                true
              else
                let currentKey = inner.CurrentKey
                if outer.Value.MoveNext() then // go to the next bucket
                  inner <- outer.Value.CurrentValue.GetSMCursor()
                  let res = inner.MoveFirst()
                  if res then
                    isBatch := false
                    true
                  else
                    raise (ApplicationException("Unexpected - empty bucket")) 
                else
                  //p.MoveAt(currentKey, Lookup.GT)
                  false
          finally
            exitLockIf this.SyncRoot entered

        member c.Current 
          with get() =
            if !isBatch then invalidOp "Current move is MoveNextBatxhAsync, cannot return a single valule"
            else inner.Current
        member this.Current with get(): obj = this.Current :> obj
    }

  member this.TryFind(key:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      result <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        
      let hash = slicer.Hash(key)
      let subKey = key
      let c = comparer.Compare(hash, prevHash)

      let res, pair =
        if c <> 0 || (not prevBucketIsSet) then // not in the prev bucket, switch bucket to newHash
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFind(hash, Lookup.EQ, &innerMapKvp)
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
    let mutable kvp = Unchecked.defaultof<_>
    let ok = this.TryFind(k, Lookup.EQ, &kvp)
    if ok then
      value <- kvp.Value
      true
    else false

  member this.Add(key, value):unit =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    let hash = slicer.Hash(key)
    let subKey = key
    try
      // the most common scenario is to hit the previous bucket 
      if prevBucketIsSet && comparer.Compare(hash, prevHash) = 0 then
        prevBucket.Add(subKey, value)
        outerMap.Version <- outerMap.Version + 1L
        //size <- size + 1L
        if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(key,value))
      else
        if prevBucketIsSet then
          //prevBucket.Capacity <- prevBucket.Count // trim excess
          outerMap.Version <- outerMap.Version - 1L
          outerMap.[prevHash] <- prevBucket
        let bucket = 
          let mutable bucketKvp = Unchecked.defaultof<_>
          let ok = outerMap.TryFind(hash, Lookup.EQ, &bucketKvp)
          if ok then 
            bucketKvp.Value.Add(subKey, value)
            bucketKvp.Value
          else
            let newSm = new SortedMap<'K,'V>(comparer)
            newSm.IsSynchronized <- this.IsSynchronized
            newSm.Add(subKey, value)
            outerMap.Version <- outerMap.Version - 1L
            outerMap.[hash]<- newSm
            newSm
        outerMap.Version <- outerMap.Version + 1L
        //size <- size + 1L
        if cursorCounter > 0 then this.onNextEvent.Trigger(KVP(key,value))
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
      else
        let exn = OutOfOrderKeyException(this.Last.Key, key, "New key is smaller or equal to the largest existing key")
        this.onErrorEvent.Trigger(exn)
        raise (exn)
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
      else 
        let exn = OutOfOrderKeyException(this.Last.Key, key, "New key is larger or equal to the smallest existing key")
        this.onErrorEvent.Trigger(exn)
        raise (exn)
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
          outerMap.Version <- outerMap.Version + 1L
          //size <- size - 1L
          if prevBucket.size = 0 then
            outerMap.Remove(prevHash) |> ignore
            prevBucketIsSet <- false
        res
      else
        if prevBucketIsSet then 
          //prevBucket.Capacity <- prevBucket.Count // trim excess 
          outerMap.Version <- outerMap.Version - 1L
          outerMap.[prevHash]<- prevBucket
        let mutable innerMapKvp = Unchecked.defaultof<_>
        let ok = outerMap.TryFind(hash, Lookup.EQ, &innerMapKvp)
        if ok then 
          let bucket = (innerMapKvp.Value)
          prevHash <- hash
          prevBucket <- bucket
          prevBucketIsSet <- true
          let res = bucket.Remove(subKey)
          if res then
            outerMap.Version <- outerMap.Version + 1L
            //size <- size - 1L
            if prevBucket.size > 0 then
              outerMap.Version <- outerMap.Version - 1L
              outerMap.[prevHash]<- prevBucket
            else
              outerMap.Remove(prevHash) |> ignore
              prevBucketIsSet <- false
          res
        else
            false
    finally
      this.onErrorEvent.Trigger(NotImplementedException("TODO remove should trigger a special exception"))
      exitLockIf this.SyncRoot entered

  member this.RemoveFirst([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      result <- this.First
      let ret' = this.Remove(result.Key)
      ret'
    finally
      exitLockIf this.SyncRoot entered


  member this.RemoveLast([<Out>]result: byref<KeyValuePair<'K, 'V>>):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      result <- this.Last
      let ret' = this.Remove(result.Key)
      ret'
    finally
      exitLockIf this.SyncRoot entered

  /// Removes all elements that are to `direction` from `key`
  member this.RemoveMany(key:'K,direction:Lookup):bool =
    let entered = enterLockIf this.SyncRoot this.IsSynchronized
    try
      let version = outerMap.Version
      let result = 
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
                    outerMap.Version <- outerMap.Version - 1L
                    outerMap.[outerMap.First.Key] <- outerMap.First.Value // Flush
                  else 
                    outerMap.Remove(outerMap.First.Key) |> ignore
                r1 || r2
                // TODO Flush
              else 
                let c = comparer.Compare(key, this.Last.Key)
                if c > 0 then // remove all keys
                  outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)
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
                  if lastChunk.IsEmpty then
                    outerMap.Remove(outerMap.Last.Key) |> ignore
                  else
                    outerMap.Version <- outerMap.Version - 1L
                    outerMap.[outerMap.Last.Key] <- lastChunk // Flush
                  r1 || r2
              else 
                let c = comparer.Compare(key, this.First.Key)
                if c < 0 then // remove all keys
                  outerMap.RemoveMany(outerMap.First.Key, Lookup.GE)
                elif c = 0 then raise (ApplicationException("Impossible condition when hasPivot is false"))
                else false
            | _ -> failwith "wrong direction"
          if removed then // we have Flushed, when needed for partial bucket change, above - just invalidate cache
            prevBucketIsSet <- false
            prevBucket <- Unchecked.defaultof<_>
          removed
      if result then 
        outerMap.Version <- version + 1L
      else
        outerMap.Version <- version
      result
    finally
      this.onErrorEvent.Trigger(NotImplementedException("TODO remove should trigger a special exception"))
      exitLockIf this.SyncRoot entered

  // TODO after checks, should form changed new chunks and use outer append method with rewrite
  // TODO atomic append with single version increase, now it is a sequence of remove/add mutations
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
          else 
            let exn = SpreadsException("values overlap with existing")
            this.onErrorEvent.Trigger(exn)
            raise exn
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
            else 
              let exn = SpreadsException("overlapping values are not equal")
              this.onErrorEvent.Trigger(exn)
              raise exn
        | AppendOption.RequireEqualOverlap ->
          if this.IsEmpty then
            let mutable c = 0
            for i in appendMap do
              c <- c + 1
              this.AddLast(i.Key, i.Value) // TODO Add last when fixed flushing
            c
          elif comparer.Compare(appendMap.First.Key, this.Last.Key) > 0 then
            let exn = SpreadsException("values do not overlap with existing")
            this.onErrorEvent.Trigger(exn)
            raise exn
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
            else 
              let exn = SpreadsException("overlapping values are not equal")
              this.onErrorEvent.Trigger(exn)
              raise exn
        | _ -> failwith "Unknown AppendOption"
      finally
        this.Flush()
        exitLockIf this.SyncRoot entered
    
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
    member this.IsReadOnly with get() = not this.IsMutable
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
    member this.GetAt(idx:int) = this.Skip(Math.Max(0, idx-1)).First().Value
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
    [<ObsoleteAttribute("Naive impl, optimize if used often")>]
    member this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)

    member this.SyncRoot with get() = this.SyncRoot
    

  interface IOrderedMap<'K,'V> with
    member this.Complete() = this.Complete()
    member this.Version with get() = int64(this.Version) and set v = raise (NotSupportedException())
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
    member this.Dispose() = this.Flush()
    member this.Id with get() = this.Id
  //#endregion

  // x0
  
  new() = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, None)
  
  // x1

  /// In-memory sorted chunked map
  new(comparer:IComparer<'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, None)
  
  /// In-memory sorted chunked map
  new(slicer:Func<'K,'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    SortedChunkedMap(factory, comparer, Some(slicer))
  new(chunkMaxSize:int) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    SortedChunkedMap(factory, comparer, None, chunkMaxSize)

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>) = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    SortedChunkedMap(outerFactory.Invoke, comparer, None)
  
  // x2

  /// In-memory sorted chunked map
  new(comparer:IComparer<'K>,slicer:Func<'K,'K>) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, Some(slicer))
  new(comparer:IComparer<'K>,chunkMaxSize:int) = 
    let factory = (fun (c:IComparer<'K>) -> new SortedMap<'K, SortedMap<'K,'V>>(c) :> IOrderedMap<'K, SortedMap<'K,'V>>)
    SortedChunkedMap(factory, comparer, None, chunkMaxSize)

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>) = 
    SortedChunkedMap(outerFactory.Invoke, comparer, None)

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,slicer:Func<'K,'K>) = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    SortedChunkedMap(outerFactory.Invoke, comparer, Some(slicer))
  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,chunkMaxSize:int) = 
    let comparer:IComparer<'K> = KeyComparer.GetDefault<'K>()
    SortedChunkedMap(outerFactory.Invoke, comparer, None, chunkMaxSize)

  // x3

  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>,slicer:Func<'K,'K>) = 
    SortedChunkedMap(outerFactory.Invoke, comparer, Some(slicer))
  new(outerFactory:Func<IComparer<'K>,IOrderedMap<'K, SortedMap<'K,'V>>>,comparer:IComparer<'K>,chunkMaxSize:int) = 
    SortedChunkedMap(outerFactory.Invoke, comparer, None, chunkMaxSize)