// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#nowarn "44"

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

open System.Data.SqlTypes
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
    chunkMaxSize:Opt<int>) as this=
  inherit ContainerSeries<'K,'V, SortedChunkedMapCursor<'K,'V>>()

  // let this.outerMap = outerFactory.Invoke(comparer)

  let mutable prevRHash = Unchecked.defaultof<'K>
  let mutable prevRBucket: SortedMap<'K,'V> = null  

  [<DefaultValueAttribute>] 
  val mutable internal outerMap : IMutableSeries<'K, SortedMap<'K,'V>>
  
  [<DefaultValueAttribute>]
  val mutable internal innerFactory : Func<int, KeyComparer<'K>,SortedMap<'K,'V>>

  [<DefaultValueAttribute>] 
  val mutable internal orderVersion : int64
 
  let mutable id = String.Empty
  
  do
    this.outerMap <- outerFactory.Invoke(comparer)
    this._isSynchronized <- true
    this._version <- this.outerMap.Version
    this._nextVersion <- this.outerMap.Version
    this.innerFactory <- innerFactory

  override this.Comparer with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = comparer

  [<Obsolete("This involves deserializetion of all chunks just to get count")>]
  member this.Count
    with get() = 
      this.BeforeWrite()
      try
        let mutable size' = 0L
        for okvp in this.outerMap do
          size' <- size' + int64 okvp.Value.Count
        size'
      finally
        this.AfterWrite(false)

  member this.Version
    with get() = readLockIf &this._nextVersion &this._version (not this._isReadOnly) (fun _ -> this._version)
    and internal set v =
      this.BeforeWrite()
      this._version <- v // NB setter only for deserializer
      this._nextVersion <- v
      this.AfterWrite(false)

  override this.IsIndexed with get() = false

  member inline private __.FirstUnchecked
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = 
      let outerFirst = this.outerMap.First
      if outerFirst.IsMissing then
        Opt<_>.Missing
      else
        outerFirst.Present.Value.First
      
  override this.First
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() = 
      readLockIf &this._nextVersion &this._version (not this._isReadOnly) (fun _ -> this.FirstUnchecked)

  member inline private __.LastUnchecked
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : Opt<KeyValuePair<'K,'V>> = 
      let outerLast = this.outerMap.Last
      if outerLast.IsMissing then
        Opt<_>.Missing
      else
        outerLast.Present.Value.Last
      
  override this.Last
    with [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] get() : Opt<KeyValuePair<'K,'V>> = 
      readLockIf &this._nextVersion &this._version (not this._isReadOnly) (fun _ -> this.LastUnchecked )

  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.TryUpdateRBucket(key) : unit =
    let mutable prevBucket' = Unchecked.defaultof<_>
    let foundNewBucket: bool =
      // we are inside previous bucket, getter alway should try to get a value from there
      if not (prevRBucket <> null && comparer.Compare(key, prevRHash) >= 0 
        && prevRBucket.CompareToLast(key) <= 0) then
        this.outerMap.TryFindAt(key, Lookup.LE, &prevBucket')
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
    if readLockIf &this._nextVersion &this._version (not this._isReadOnly) res then
      value <- value'; true
    else false

  override this.GetContainerCursor() = this.GetEnumerator()

  // .NETs foreach optimization
  member this.GetEnumerator() =
    readLockIf &this._nextVersion &this._version (not this._isReadOnly) (fun _ ->
      new SortedChunkedMapCursor<_,_>(this)
    )
  
  [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
  member private this.TryFindTuple(key:'K, direction:Lookup) : struct (bool * KeyValuePair<'K,'V>) = 
    let tupleResult =
      let mutable kvp = Unchecked.defaultof<KeyValuePair<'K, 'V>>
      
      let mutable hashBucket = Unchecked.defaultof<_>
      this.outerMap.TryFindAt(key, Lookup.LE, &hashBucket) |> ignore
      let hash = hashBucket.Key
      let c = comparer.Compare(hash, prevRHash)

      let res, pair =
        if c <> 0 || (prevRBucket = null) then // not in the prev bucket, switch bucket to newHash
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = 
            if hashBucket.Value <> null then
              innerMapKvp <- hashBucket
              true
            else this.outerMap.TryFindAt(hash, Lookup.EQ, &innerMapKvp)
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
          let ok = this.outerMap.TryFindAt(hash, Lookup.LT, &innerMapKvp)
          if ok then
            Debug.Assert(innerMapKvp.Value.Last.IsPresent) // if previous was found it shoudn't be empty
            let pair = innerMapKvp.Value.Last.Present
            struct (true, pair)
          else
            struct (false, kvp)
        | Lookup.GT | Lookup.GE ->
          // look into next bucket and take first
          let mutable innerMapKvp = Unchecked.defaultof<_>
          let ok = this.outerMap.TryFindAt(hash, Lookup.GT, &innerMapKvp)
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
    let tupleResult = readLockIf &this._nextVersion &this._version (not this._isReadOnly) res
    let struct (ret0,res0) = tupleResult
    result <- res0
    ret0

  //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
  override this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>).Select(fun kvp -> kvp.Key)

  //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
  override this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>).Select(fun kvp -> kvp.Value)
    
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

and
  public SortedChunkedMapCursor<'K,'V> =
    struct
      val mutable internal source : SortedChunkedMapBase<'K,'V>
      val mutable internal outerCursor : ICursor<'K,SortedMap<'K,'V>>
      val mutable internal innerCursor : SortedMapCursor<'K,'V>
      // batching is orthogonal feature to the cursor implementation, batch moves should not affect normal moves
      // TODO review & refactor
      val mutable internal batchCursor : ICursor<'K,SortedMap<'K,'V>>
      new(source:SortedChunkedMapBase<'K,'V>) = 
        { source = if source <> Unchecked.defaultof<_> then source else failwith "source is null"; 
          outerCursor = source.outerMap.GetCursor();
          innerCursor = Unchecked.defaultof<_>;
          batchCursor = null;
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

    member this.Source: ISeries<'K,'V> = this.source :> ISeries<'K,'V>      
    
    member this.IsContinuous with get() = false

    member this.IsCompleted with get() = this.source.IsCompleted
    
    member this.AsyncCompleter with get() = this.source :> IAsyncCompleter

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>] 
    member this.TryGetValue(key: 'K, [<Out>]value: byref<'V>): bool = this.source.TryGetValue(key, &value)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MoveNext() =
      let mutable newInner = Unchecked.defaultof<_> 
      // let mutable doSwitchInner = false
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable outerMoved = false
      let mutable sw = new SpinWait()

      while doSpin do

        // here we use copy-by-value of structs, SMCursor is cloneable this way without calling Clone()
        // TODO assignment of doSwitchInner and then check is probably more expensive than always updating on true
        newInner <- this.innerCursor

        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L

        /////////// Start read-locked code /////////////
        if (not this.HasValidInner) then
          if this.outerCursor.MoveFirst() then
            newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
            result <- newInner.MoveFirst()
            if not result then ThrowHelper.ThrowInvalidOperationException("Outer should not have empty chunks")
          else result <- false
        if this.HasValidInner then
          if newInner.MoveNext() then
            result <- true
          else
            if outerMoved || this.outerCursor.MoveNext() then
              
              // this is for spin - if we could get value but versions are not equal and we need to spin
              // then we do not need to move outer again if it was already moved
              outerMoved <- true

              newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
              result <- newInner.MoveNext()
              if not result then ThrowHelper.ThrowInvalidOperationException("Outer should not have empty chunks")
            else
              outerMoved <- false
              result <- false

        /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        // if doSwitchInner then 
        this.innerCursor <- newInner
      result

    member private this.CurrentBatch =
      if this.batchCursor = null then null
      else  this.batchCursor.CurrentValue

    member this.MoveNextBatch(noAsync: bool): ValueTask<bool> =
      if this.batchCursor = null then 
        this.batchCursor <- this.source.outerMap.GetCursor()
      if noAsync then new ValueTask<bool>(this.batchCursor.MoveNext())
      else this.batchCursor.MoveNextAsync()

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
    member this.MovePrevious() = 
      let mutable newInner = Unchecked.defaultof<_> 
      let mutable doSwitchInner = false
      let mutable result = Unchecked.defaultof<_>
      let mutable doSpin = true
      let mutable outerMoved = false
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          try
            // here we use copy-by-value of structs
            newInner <- this.innerCursor
            if (not this.HasValidInner) then
              if this.outerCursor.MoveLast() then
                newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                doSwitchInner <- true
                newInner.MoveLast()
              else false
            else
              let mutable entered = false
              try
                this.source.BeforeWrite()
                if this.HasValidInner && newInner.MovePrevious() then 
                  doSwitchInner <- true
                  true
                else
                  if outerMoved || this.outerCursor.MovePrevious() then
                    outerMoved <- true
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    doSwitchInner <- true
                    if newInner.MovePrevious() then true
                    else outerMoved <- false; false // need to try to move outer again
                  else
                    outerMoved <- false
                    false
              finally
                this.source.AfterWrite(false)
          with
          | :? OutOfOrderKeyException<'K> as ooex ->
             raise (new OutOfOrderKeyException<'K>(this.innerCursor.CurrentKey, "SortedMap order was changed since last move. Catch OutOfOrderKeyException and use its CurrentKey property together with MoveAt(key, Lookup.GT) to recover."))
            
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
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          if this.outerCursor.MoveFirst() then
            newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
            if newInner.MoveFirst() then
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
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          if this.outerCursor.MoveLast() then
            newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
            if newInner.MoveLast() then
              true
            else
              ThrowHelper.ThrowInvalidOperationException("If outer moved last then inned must be non-empty")
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
      let mutable sw = new SpinWait()
      while doSpin do
        doSpin <- this.source._isSynchronized
        let version = if doSpin then Volatile.Read(&this.source._version) else 0L
        result <-
        /////////// Start read-locked code /////////////
          let mutable entered = false
          try
            this.source.BeforeWrite()
            newInner <- this.innerCursor
            let res =
              if this.outerCursor.MoveAt(key, Lookup.LE) then // LE!
                newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                newInner.MoveAt(key, direction)
              else
                false

            if res then
              true
            else
                match direction with
                | Lookup.LT | Lookup.LE ->
                  // look into previous bucket
                  if this.outerCursor.MovePrevious() then
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    newInner.MoveAt(key, direction)
                  else
                    false
                | Lookup.GT | Lookup.GE ->
                  // look into next bucket
                  let moved = this.outerCursor.MoveNext() 
                  if moved then
                    newInner <- new SortedMapCursor<'K,'V>(this.outerCursor.CurrentValue)
                    newInner.MoveAt(key, direction)
                  else
                    false
                | _ -> false // LookupDirection.EQ
          finally
            this.source.AfterWrite(false)
      /////////// End read-locked code /////////////
        if doSpin then
          let nextVersion = Volatile.Read(&this.source._nextVersion)
          if version = nextVersion then doSpin <- false
          else sw.SpinOnce()
      if result then
        //if this.HasValidInner then this.innerCursor.Dispose()
        this.innerCursor <- newInner
      result

    member this.MoveNextAsync(): ValueTask<bool> = 
      if this.source._isReadOnly then
        if this.MoveNext() then new ValueTask<bool>(true) else ValueTask<bool>(false)
      else raise (NotSupportedException("Use SortedChunkedMapBaseGenericCursorAsync instead"))

    member this.Clone() =
      let mutable entered = false
      try
        this.source.BeforeWrite()
        let mutable clone = this
        clone.source <- this.source
        clone.outerCursor <- this.outerCursor.Clone()
        clone.innerCursor <- this.innerCursor.Clone()
        clone.batchCursor <- if this.batchCursor = null then null else this.batchCursor.Clone()
        clone
      finally
        this.source.AfterWrite(false)
      
    member this.Reset() =
      if this.HasValidInner then this.innerCursor.Dispose()
      this.innerCursor <- Unchecked.defaultof<_>
      this.outerCursor.Reset()
      if this.batchCursor <> null then this.batchCursor.Reset()

    member this.Dispose() = this.Reset()

    member this.Initialize() =
        let c = this.Clone()
        c.Reset()
        c

    interface IDisposable with
      member this.Dispose() = this.Dispose()

    interface IEnumerator<KVP<'K,'V>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext():bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V> = KeyValuePair(this.innerCursor.CurrentKey, this.innerCursor.CurrentValue)
      member this.Current with get(): obj = this.Current :> obj

    interface IAsyncEnumerator<KVP<'K,'V>> with
      member this.MoveNextAsync(): ValueTask<bool> = this.MoveNextAsync()
      member this.DisposeAsync() = this.Dispose();Task.CompletedTask

    interface IAsyncBatchEnumerator<KVP<'K,'V>> with
      member this.MoveNextBatch(noAsync: bool): ValueTask<bool> = this.MoveNextBatch(noAsync)
      member this.CurrentBatch with get(): IEnumerable<KVP<'K, 'V>> = this.CurrentBatch :> IEnumerable<KVP<'K, 'V>>
      
    interface ICursor<'K,'V> with
      member this.Comparer with get() = this.Comparer
      member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
      member this.MoveFirst():bool = this.MoveFirst()
      member this.MoveLast():bool =  this.MoveLast()
      member this.MoveNext():bool = this.MoveNext()
      member this.MovePrevious():bool = this.MovePrevious()
      member this.CurrentKey with get() = this.CurrentKey
      member this.CurrentValue with get() = this.CurrentValue
      member this.Source with get() = this.source :> ISeries<'K,'V>
      member this.Clone() = this.Clone() :> ICursor<'K,'V>
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.source.TryGetValue(key, &value)
    // TODO
      member this.State with get() = raise (NotImplementedException())
      member this.MoveNext(stride, allowPartial) = raise (NotImplementedException())
      member this.MovePrevious(stride, allowPartial) = raise (NotImplementedException())

    interface ISpecializedCursor<'K,'V, SortedChunkedMapCursor<'K,'V>> with
      member this.Initialize() = this.Initialize()
      member this.Clone() = this.Clone()
      member this.IsIndexed with get() = false
      member this.IsCompleted with get() = this.IsCompleted
      member this.AsyncCompleter with get() = this.AsyncCompleter
      member this.Source with get() = new Series<_,_,_>(this)