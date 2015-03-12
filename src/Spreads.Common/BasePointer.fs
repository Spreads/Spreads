namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading.Tasks
open System.Diagnostics
open Spreads


/// Uses IReadOnlySortedMap's TryFind method, doesn't know anything about underlying sequence
type BasePointer<'K,'V when 'K : comparison>
  (map:IReadOnlySortedMap<'K,'V>) as this =

  [<DefaultValue>] 
  val mutable private currentPosition : bool * KeyValuePair<'K,'V>

  let mutable isReset = true

  let disposable = ref {new IDisposable with member __.Dispose() = ()}
  let observerStarted = ref false
  let observedCount = ref 0
  let isObserverSynced = ref false
  let isCompleted = ref false
  let hasException = ref false
  let exc:Exception ref = ref (Unchecked.defaultof<Exception>)
  let tcs = ref (TaskCompletionSource())
  let isWaitingForTcs = ref false
  let canceledTask() = 
    let res = TaskCompletionSource()
    res.SetCanceled()
    res.Task
  let exceptionTask(e:Exception) = 
    let res = TaskCompletionSource(Unchecked.defaultof<KVP<'K,'V>>)
    res.SetException(e)
    res.Task


//    abstract NextAsync: unit -> Task<KVP<'K,'V>>
//    abstract Source : IReadOnlySortedMap<'K,'V> with get

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
  override this.Dispose() = disposable.Value.Dispose()

  abstract member Reset : unit -> unit
  override this.Reset() = isReset <- true

  member this.MoveGetNextAsync() =
    match this.MoveNext() with
    | true ->
      let kvp = this.Current
      //hasValues := this.MoveNext()
      Task.FromResult(kvp)
    | false -> 
      match map with
      | :? IObservable<KVP<'K,'V>> as obs -> 
        // here hasValues could be stale because we have just learned that series is IObservable 
        // and series could have received a new value
        // first need to subscribe to IObservable, then try to MoveNext right after subscription
        // NB: here the speed is limited by arrival of new values, use locks and don't bother with 
        //     performance (same as in Stream's notifierAgent)
        if not !observerStarted then
          let synchronize(kvp:KVP<'K,'V>) =
            // we must know if observer has missed value
            // untill we know that observer is synced we must lookup the next value
            // and then move back to return the current state (otherwise MoveGetNextAsync will skip values)
            if not !isObserverSynced then
              let couldMove = this.MoveNext()
#if PRERELEASE
              Trace.Assert(couldMove, "OnNext is called after adding a value, must always be true")
#endif
              if this.CurrentKey = kvp.Key then 
                isObserverSynced := true
#if PRERELEASE
                Trace.Assert(this.CurrentKey = kvp.Key && Object.Equals(this.CurrentValue, kvp.Value))
              else Trace.Assert(this.CurrentKey < kvp.Key) // just in case
#endif
              if not !isObserverSynced then // still not synced
                // we know that there is at least one value that the observer has missed -
                // then always increment. On one of the next OnNexts isObserverSynced will be true
                incr observedCount |> ignore 
              // mind offset inside the first if block to always cancel MoveNext step
              this.MovePrevious() |> ignore

          let observer = 
            { new System.IObserver<KVP<'K,'V>> with
              member __.OnNext(kvp) = 
                lock observedCount ( fun _ ->
                  synchronize(kvp)
                  if !isWaitingForTcs then
                    tcs.Value.SetResult(kvp)
                    isWaitingForTcs := false
                  else
                    incr observedCount |> ignore                 
                )
              member __.OnCompleted() = 
                lock observedCount ( fun _ ->
                  // TODO must also check if state is synced, or will lose missed values
                  if !isWaitingForTcs then 
                    tcs.Value.SetCanceled()
                  else
                    // missed value, e.g. subscribed after a value was added, and receved
                    // oncompleted message first
                    let couldMove = this.MoveNext()
                    if couldMove then 
                      this.MovePrevious() |> ignore
                      incr observedCount |> ignore 
                    isCompleted := true
                )
              member __.OnError(e) = 
                lock observedCount ( fun _ ->
                  if !isWaitingForTcs then 
                    tcs.Value.SetException(e)
                  else
                    // missed value, e.g. subscribed after a value was added, and receved
                    // oncompleted message first
                    let couldMove = this.MoveNext()
                    if couldMove then
                      this.MovePrevious() |> ignore
                      incr observedCount |> ignore 
                    hasException := true
                    exc := e  
                )
            }
          disposable := obs.Subscribe(observer)
          observerStarted := true
        lock observedCount ( fun _ ->
          if !observedCount > 0 then
            let couldMove = this.MoveNext()
#if PRERELEASE
            Trace.Assert(couldMove)
#endif
            decr observedCount |> ignore
            Task.FromResult(this.Current)
          else // observedCount = 0L, observer is working on tcs in the next OnNext
            if !isCompleted then canceledTask()
            elif !hasException then exceptionTask(!exc)
            else
              tcs := TaskCompletionSource()
              isWaitingForTcs := true
              tcs.Value.Task // observer populates tcs only for the first value
        )
      | _ -> // has no values and will never have because is not IObservable
        canceledTask()

  interface IPointer<'K,'V> with
    member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
    member this.MoveFirst():bool = this.MoveFirst()
    member this.MoveLast():bool = this.MoveLast()
    member this.MoveNext():bool = this.MoveNext()
    member this.MovePrevious():bool = this.MovePrevious()
    member this.Current with get(): KeyValuePair<'K, 'V> = this.Current
    member this.Current with get(): obj = box (this :> IPointer<'K,'V>).Current
    member this.CurrentKey with get():'K = this.CurrentKey
    member this.CurrentValue with get():'V = this.CurrentValue
    member this.Reset() = this.Reset()
    member this.Dispose() = this.Dispose()
    member this.MoveGetNextAsync(): Task<KVP<'K,'V>> = this.MoveGetNextAsync()
    member this.Source with get() = map