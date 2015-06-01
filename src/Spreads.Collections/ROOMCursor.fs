namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open Spreads


/// Uses IReadOnlyOrderedMap's TryFind method, doesn't know anything about underlying sequence
type ROOMCursor<'K,'V when 'K : comparison>
  (map:IReadOnlyOrderedMap<'K,'V>) as this =

  [<DefaultValue>] 
  val mutable private currentPosition : bool * KeyValuePair<'K,'V>

  let mutable isReset = true

  let isUpdateable = match map with | :? IUpdateable<'K,'V> -> true | _ -> false
  let observerStarted = ref false
  let tcs = ref (TaskCompletionSource<bool>())
  let isWaitingForTcs = ref false
  let sr = Object()

  let updateHandler : UpdateHandler<'K,'V> =
    let impl (o:obj) (kvp:KVP<'K,'V>) =
      lock(sr) (fun _ ->
        // right now a client is waiting for a task to complete, there is no more elements in the map
        if !isWaitingForTcs then
          this.currentPosition <- true, kvp
          (!tcs).TrySetResult(true)  |> ignore
        else
          // do nothing, MoveNextAsync will try call MoveNext() and it will return the correct result
          ()
      )
    UpdateHandler(impl)


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

  member this.MoveNextAsync() =

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
          isWaitingForTcs := true
          tcs.Value.Task
        )
      | _ -> Task.FromResult(false) // has no values and will never have because is not IUpdateable
  

  interface IEnumerator<KVP<'K,'V>> with    
    member this.Reset() = this.Reset()
    member this.Dispose() = this.Dispose()
    member this.MoveNext():bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V> = this.Current
    member this.Current with get(): obj = this.Current :> obj

  interface ICursor<'K,'V> with
    // TODO need some implementation of ROOM to implement the batch
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = raise (NotImplementedException())
    member this.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> = raise (NotImplementedException())
    member this.MoveAtAsync(index:'K, lookup:Lookup) = Task.FromResult(this.MoveAt(index, lookup))
    member this.MoveFirstAsync():Task<bool> = Task.FromResult(this.MoveFirst())
    member this.MoveLastAsync():Task<bool> = Task.FromResult(this.MoveLast())
    member this.MovePreviousAsync():Task<bool> = Task.FromResult(this.MovePrevious())
    member x.Current with get(): KVP<'K,'V> = this.Current
    member this.CurrentKey with get():'K = this.CurrentKey
    member this.CurrentValue with get():'V = this.CurrentValue
    member this.MoveNextAsync(cancellationToken:CancellationToken): Task<bool> = this.MoveNextAsync()
    member this.Source with get() = map :> ISeries<'K,'V>