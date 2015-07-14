namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open Spreads


/// Uses IReadOnlyOrderedMap's TryFind method, doesn't know anything about underlying sequence
type MapCursor<'K,'V when 'K : comparison>
  (map:IReadOnlyOrderedMap<'K,'V>) as this =
  //inherit Cursor<'K,'V>()
  [<DefaultValue>] 
  val mutable private currentPosition : bool * KeyValuePair<'K,'V>

  let mutable isReset = true

  let isUpdateable = match map with | :? IUpdateable<'K,'V> -> true | _ -> false
  let observerStarted = ref false
  let tcs = ref (TaskCompletionSource<bool>())
  let ctr = ref (Unchecked.defaultof<CancellationTokenRegistration>)
  let isWaitingForTcs = ref false
  let sr = Object()

  let updateHandler : UpdateHandler<'K,'V> =
    let impl _ (kvp:KVP<'K,'V>) =
      lock(sr) (fun _ ->
        // right now a client is waiting for a task to complete, there is no more elements in the map
        if !isWaitingForTcs then
          this.currentPosition <- true, kvp
          (!ctr).Dispose()
          (!tcs).TrySetResult(true)  |> ignore
        else
          // do nothing, MoveNextAsync will try call MoveNext() and it will return the correct result
          ()
      )
    UpdateHandler(impl)
  let cancelHandler = fun () -> (!tcs).TrySetCanceled() |> ignore

  abstract MoveAt: index:'K * direction:Lookup -> bool
  override this.MoveAt(index:'K, lookup:Lookup) = 
    isReset <- false
    this.currentPosition <- map.TryFind(index, lookup)
    fst this.currentPosition

  abstract MoveFirst: unit -> bool
  override this.MoveFirst():bool = 
    try
      this.MoveAt(map.First.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  abstract MoveLast: unit -> bool
  override this.MoveLast():bool =
    try
      this.MoveAt(map.Last.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  abstract member MoveNext : unit -> bool
  override this.MoveNext():bool = 
    if isReset then this.MoveFirst()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.GT)
      fst this.currentPosition
  
  abstract MovePrevious: unit -> bool
  override this.MovePrevious():bool = 
    if isReset then this.MoveLast()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.LT)
      fst this.currentPosition

  abstract Current:KVP<'K,'V> with get
  override this.Current 
    with get(): KeyValuePair<'K, 'V> = 
      snd this.currentPosition

  abstract CurrentKey:'K with get
  override this.CurrentKey with get():'K = this.Current.Key

  abstract CurrentValue:'V with get
  override this.CurrentValue with get():'V = this.Current.Value

  abstract Dispose: unit -> unit
  override this.Dispose() =
    if !observerStarted then
      Debug.Assert(map :? IUpdateable<'K,'V>)
      (map :?> IUpdateable<'K,'V>).OnData.RemoveHandler(updateHandler)

  abstract member Reset : unit -> unit
  override this.Reset() = isReset <- true

  abstract member MoveNext : CancellationToken -> Task<bool>
  override this.MoveNext(ct) =
    match this.MoveNext() with
    | true -> Task.FromResult(true)      
    | false -> 
      match isUpdateable with
      | true -> 
        let upd = map :?> IUpdateable<'K,'V>
        lock(sr) (fun _ ->
          if not !observerStarted then 
            upd.OnData.AddHandler updateHandler
            observerStarted := true
          tcs := TaskCompletionSource()
          ctr := ct.Register(Action(cancelHandler))
          isWaitingForTcs := true
          tcs.Value.Task
        )
      | _ -> Task.FromResult(false) // has no values and will never have because is not IUpdateable
  
  abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
  abstract CurrentBatch: IReadOnlyOrderedMap<'K,'V> with get
  override this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))
  override this.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))

  abstract Source : ISeries<'K,'V> with get
  override this.Source with get() = map :> ISeries<'K,'V>

  abstract Clone: unit -> ICursor<'K,'V>
  override this.Clone() =
    let c = new MapCursor<'K,'V>(map)
    c.currentPosition <- this.currentPosition
    c :> ICursor<'K,'V>

  abstract IsBatch: bool with get
  override this.IsBatch with get() = false
  abstract IsContinuous: bool with get
  override this.IsContinuous with get() = false

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
    member this.Comparer with get() = map.Comparer
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
    member this.Source with get() = this.Source
    member this.Clone() = this.Clone()
    member this.IsContinuous with get() = this.IsContinuous
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = 
      let entered = enterLockIf syncRoot isSynchronized
        try
        map.TryGetValue(key, &value)