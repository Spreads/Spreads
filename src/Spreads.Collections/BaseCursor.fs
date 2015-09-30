namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open Spreads


[<AbstractClassAttribute>]
type BaseCursor<'K,'V>
  (source:IReadOnlyOrderedMap<'K,'V>) as this =

  // implement default MoveNextAsync logic using only MoveNext

  let isUpdateable = match source with | :? IUpdateable<'K,'V> -> true | _ -> false
  let observerStarted = ref false
  let tcs = ref (TaskCompletionSource<bool>())
  // TODO use CT while waiting on semaphore
  let cancellationToken = ref CancellationToken.None
  let sr = Object()
  let semaphore = new SemaphoreSlim(0,Int32.MaxValue)
  let taskCompleter = ref Unchecked.defaultof<Task<bool>>
  let rec completeTcs() : Task<bool> = 
    task {
          let mutable cont = true
          let waitTask = semaphore.WaitAsync(!cancellationToken).ContinueWith(fun _ -> true)
          let! couldProceed = waitTask
          //Debug.WriteLine("A")
          if cont && couldProceed && !observerStarted && waitTask.IsCompleted then
            lock(sr) (fun _ ->
              // right now a client is waiting for a task to complete, there are no more elements in the map
              if !tcs <> null then
                //Debug.WriteLine("B: !tcs <> null")
                if this.MoveNext() then
                  let tcs' = !tcs
                  tcs := null
                  //Debug.WriteLine("C: Moved")
                  let couldSetResult = (tcs').TrySetResult(true)
        #if PRERELEASE
                  Trace.Assert(couldSetResult)
        #endif
                  ()
                // check if the source became immutable
                elif not source.IsMutable then 
                  //Debug.WriteLine("D")
                  let couldSetResult = (!tcs).TrySetResult(false)
                  Trace.Assert(couldSetResult)
                  cont <- false
              else
                // do nothing, next MoveNext(ct) will try to call MoveNext() and it will return the correct result
                ()
            )
            return! completeTcs()
          else
            Debug.WriteLine("STOP")
            return false // stop the loop
    }

  let updateHandler : UpdateHandler<'K,'V> = 
    UpdateHandler(fun _ (kvp:KVP<'K,'V>) ->
         
//        if (this.Comparer.Compare(kvp.Key, this.CurrentKey) < 0) then 
//          invalidOp "Out of order value. TODO handle it"
          // TODO could be same logic as in MoveNext - reposition to out-of-order value, or could throw
//        if kvp = Unchecked.defaultof<_> then
//          lock(sr) (fun _ ->
//            if !tcs <> null then (!tcs).TrySetResult(false) |> ignore
//          )
        //el
        if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
    )
      
    //UpdateHandler(impl)

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
    | true -> 
      Task.FromResult(true)      
    | false ->
      match isUpdateable, source.IsMutable with
      | true, true ->
        let upd = source :?> IUpdateable<'K,'V>
        if not !observerStarted then 
          upd.OnData.AddHandler updateHandler
          observerStarted := true
          taskCompleter := completeTcs()

        // TODO why spinning doesn't add performance?
//        let mutable moved = this.MoveNext()
////        if not moved then
////          let spinCountMax = 100
////          let mutable spinCount = 0
////          while spinCount < spinCountMax do
////            if this.MoveNext() then 
////              spinCount <- spinCountMax
////              moved <- true
////            else spinCount <- spinCount + 1
//        if moved && not ct.IsCancellationRequested then
//          //isWaitingForTcs := false
//          Task.FromResult(true)
//        else
        cancellationToken := ct
        // TODO use interlocked.exchange or whatever to not allocate new one every time, if we return Task.FromResult(true) below
        // interlocked.exchange does not short-circuit and allocates value each time
        //Interlocked.CompareExchange(tcs, TaskCompletionSource(), null) |> ignore
        lock(sr) (fun _ ->
            if !tcs = null then
              tcs := TaskCompletionSource()
            tcs.Value.Task
        )
        
      | _ -> Task.FromResult(false) // has no values and will never have because is not IUpdateable or IsMutable=false
  
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
type MapCursor<'K,'V>(map:IReadOnlyOrderedMap<'K,'V>) as this =
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


