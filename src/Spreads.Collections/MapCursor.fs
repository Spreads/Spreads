namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open Spreads

// TODO rename it to BaseCursor with only abstract method, to not carry currentPosition


[<AbstractClassAttribute>]
type BaseCursor<'K,'V when 'K : comparison>
  (source:IReadOnlyOrderedMap<'K,'V>) as this =

  // implement default MoveNextAsync logic using only MoveNext

  let isUpdateable = match source with | :? IUpdateable<'K,'V> -> true | _ -> false
  let observerStarted = ref false
  let tcs = ref (TaskCompletionSource<bool>())
  let ctr = ref (Unchecked.defaultof<CancellationTokenRegistration>)
  let isWaitingForTcs = ref false // NB used only inside lock on sr
  let sr = Object()
  let updateHandler : UpdateHandler<'K,'V> =
    let impl _ (kvp:KVP<'K,'V>) =
      lock(sr) (fun _ ->
        if (this.Comparer.Compare(kvp.Key, this.CurrentKey) <= 0) then 
          invalidOp "Out of order value. TODO handle it"
          // TODO could be same logic as in MoveNext - reposition to out-of-order value, or could throw

        // right now a client is waiting for a task to complete, there is no more elements in the map
        if !isWaitingForTcs && this.MoveNext() then // NB order 
          (!ctr).Dispose()
          let couldSetResult = (!tcs).TrySetResult(true)
#if PRERELEASE
          Trace.Assert(couldSetResult)
#endif
          ()
        else
          // do nothing, next MoveNext(ct) will try to call MoveNext() and it will return the correct result
          ()
      )
    UpdateHandler(impl)

  abstract Comparer: IComparer<'K> with get
  override this.Comparer with get() = source.Comparer

  abstract MoveAt: index:'K * direction:Lookup -> bool

  abstract MoveFirst: unit -> bool

  abstract MoveLast: unit -> bool

  abstract member MoveNext : unit -> bool
  
  abstract MovePrevious: unit -> bool

  abstract Current:KVP<'K,'V> with get
  override this.Current with get(): KeyValuePair<'K, 'V> = KVP(this.CurrentKey, this.CurrentValue)

  abstract CurrentKey:'K with get

  abstract CurrentValue:'V with get

  abstract Dispose: unit -> unit
  override this.Dispose() =
    lock(sr) (fun _ ->
      if !observerStarted then
        Trace.Assert(source :? IUpdateable<'K,'V>)
        (source :?> IUpdateable<'K,'V>).OnData.RemoveHandler(updateHandler)
    )

  abstract member Reset : unit -> unit

  abstract member MoveNext : CancellationToken -> Task<bool>
  override this.MoveNext(ct) =
    match this.MoveNext() with
    | true -> Task.FromResult(true)      
    | false -> 
      match isUpdateable with
      | true -> 
        let upd = source :?> IUpdateable<'K,'V>
        // lock with the update handler
        // if it is not added yet, it won't be called right after we add it and before we try to movenext after we add it
        lock(sr) (fun _ ->
          if not !observerStarted then 
            upd.OnData.AddHandler updateHandler
            observerStarted := true
          // TODO use interlocked.exchange or whatever to not allocate new one every time, if we return Task.FromResult(true) below
          tcs := TaskCompletionSource()
          // MSDN If this token is already in the canceled state, the delegate will be run immediately and synchronously. Any exception the delegate generates will be propagated out of this method call.
          ctr := ct.Register(Action(fun () -> (!tcs).TrySetCanceled() |> ignore))
          isWaitingForTcs := true
          // we are now subsribed, but update event could have beed triggered after match and before subscribtion
          if this.MoveNext() && not ct.IsCancellationRequested then
            isWaitingForTcs := false
            Task.FromResult(true)
          else
            tcs.Value.Task
        )
      | _ -> Task.FromResult(false) // has no values and will never have because is not IUpdateable
  
  abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
  abstract CurrentBatch: IReadOnlyOrderedMap<'K,'V> with get
  abstract Source : ISeries<'K,'V> with get
  override this.Source with get() = source :> ISeries<'K,'V>
  abstract Clone: unit -> ICursor<'K,'V>

  // NB this is now not a part of interface (it was, could be back if needed)
  abstract IsBatch: bool with get
  abstract IsContinuous: bool with get

  interface IDisposable with
    member this.Dispose() = this.Dispose()

  interface IEnumerator<KVP<'K,'V>> with    
    member this.Reset() = this.Reset()
    member this.MoveNext():bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V> = this.Current
    member this.Current with get(): obj = this.Current :> obj

  interface IAsyncEnumerator<KVP<'K,'V>> with
    member x.Current: KVP<'K, 'V> = this.Current
    member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 

  interface ICursor<'K,'V> with
    member this.Comparer with get() = this.Comparer
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = this.CurrentBatch
    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatchAsync(cancellationToken)
    //member this.IsBatch with get() = this.IsBatch
    member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
    member this.MoveFirst():bool = this.MoveFirst()
    member this.MoveLast():bool =  this.MoveLast()
    member this.MovePrevious():bool = this.MovePrevious()
    member this.CurrentKey with get():'K = this.CurrentKey
    member this.CurrentValue with get():'V = this.CurrentValue
    member this.Source with get() = this.Source
    member this.Clone() = this.Clone()
    member this.IsContinuous with get() = this.IsContinuous
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = source.TryGetValue(key, &value)


/// Uses IReadOnlyOrderedMap's TryFind method, doesn't know anything about underlying sequence
[<ObsoleteAttribute("TODO replace it with BaseCursor")>]
type MapCursor<'K,'V when 'K : comparison>(map:IReadOnlyOrderedMap<'K,'V>) as this =
  inherit BaseCursor<'K,'V>(map)
  [<DefaultValue>] 
  val mutable private currentPosition : bool * KeyValuePair<'K,'V>

  let mutable isReset = true

  override this.MoveAt(index:'K, lookup:Lookup) = 
    isReset <- false
    this.currentPosition <- map.TryFind(index, lookup)
    fst this.currentPosition

  override this.MoveFirst():bool = 
    try
      this.MoveAt(map.First.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  override this.MoveLast():bool =
    try
      this.MoveAt(map.Last.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  override this.MoveNext():bool = 
    if isReset then this.MoveFirst()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.GT)
      fst this.currentPosition
  
  override this.MovePrevious():bool = 
    if isReset then this.MoveLast()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.LT)
      fst this.currentPosition

  override this.Current 
    with get(): KeyValuePair<'K, 'V> = 
      snd this.currentPosition

  override this.CurrentKey with get():'K = this.Current.Key

  override this.CurrentValue with get():'V = this.Current.Value

  override this.Reset() = isReset <- true

  override this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))
  override this.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))

  override this.Source with get() = map :> ISeries<'K,'V>

  override this.Clone() =
    let c = new MapCursor<'K,'V>(map)
    c.currentPosition <- this.currentPosition
    c :> ICursor<'K,'V>

  override this.IsBatch with get() = false
  override this.IsContinuous with get() = false

//  interface IDisposable with
//    member this.Dispose() = this.Dispose()
//
//  interface IEnumerator<KVP<'K,'V>> with    
//    member this.Reset() = this.Reset()
//    member this.MoveNext():bool = this.MoveNext()
//    member this.Current with get(): KVP<'K, 'V> = this.Current
//    member this.Current with get(): obj = this.Current :> obj
//
//  interface IAsyncEnumerator<KVP<'K,'V>> with
//    member x.Current: KVP<'K, 'V> = this.Current
//    member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 
//
//  interface ICursor<'K,'V> with
//    member this.Comparer with get() = map.Comparer
//    // TODO need some implementation of ROOM to implement the batch
//    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = this.CurrentBatch
//    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatchAsync(cancellationToken)
//    //member this.IsBatch with get() = this.IsBatch
//    member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
//    member this.MoveFirst():bool = this.MoveFirst()
//    member this.MoveLast():bool =  this.MoveLast()
//    member this.MovePrevious():bool = this.MovePrevious()
//    member this.CurrentKey with get():'K = this.CurrentKey
//    member this.CurrentValue with get():'V = this.CurrentValue
//    member this.Source with get() = this.Source
//    member this.Clone() = this.Clone()
//    member this.IsContinuous with get() = this.IsContinuous
//    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = map.TryGetValue(key, &value)