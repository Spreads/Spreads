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

namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open Spreads
open Microsoft.FSharp.Control





type CursorAsync<'K,'V,'TCursor when 'TCursor :> ICursor<'K,'V>>(source:Series<'K,'V>, cursorFactory:unit->'TCursor) as this =
    [<DefaultValueAttribute(false)>]
    val mutable private state : 'TCursor
    [<DefaultValueAttribute(false)>]
    val mutable private unusedTcs : TaskCompletionSource<int64>
    [<DefaultValueAttribute(false)>]
    val mutable private cancelledTcs : TaskCompletionSource<Task<bool>>
    [<DefaultValueAttribute(false)>]
    val mutable private token : CancellationToken
    do
      this.state <- cursorFactory()

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNext(ct: CancellationToken): Task<bool> =      
      match this.state.MoveNext() with
      | true -> 
        OptimizationSettings.TraceVerbose("SM_MNA: sync MN true")
        trueTask            
      | false ->
        OptimizationSettings.TraceVerbose("SM_MNA: sync MN false")
        match this.state.Source.IsReadOnly with
        | true -> if this.state.MoveNext() then trueTask else falseTask
        | false ->
          // Task could have multiple awaiters
          let tcs = Volatile.Read(&source.updateTcs)
          // if some cursor already created a tcs and it is not null, we just await for it
          let activeTcs =
            if tcs <> null then tcs
            else
              let newTcs =
                if this.unusedTcs <> null then this.unusedTcs
                else new TaskCompletionSource<int64>()
              let original = Interlocked.CompareExchange(&source.updateTcs, newTcs, null)
              if original = null then
                // newTcs was put to the SM
                // if unusedTcs was not null, newTcs = unusedTcs
                // and unusedTcs went to SM
                this.unusedTcs <- null
                newTcs
              else
                // SM Tcs was already set, we set unusedTcs to itself if 
                // it was not null or to a new Tcs that we have allocated
                this.unusedTcs <- newTcs
                original
          // NB activeTcs is already allocated and we cannot avoid this allocation,
          // however it could be shared among many cursors. When its Task completes,
          // we create a continuation Task that does synchronous and very fast
          // work inside its body, so it is a very small and short-lived object
          // and .NET's GC is best for this.
          // TODO Check if we need to use RunContinuationsAsynchronously from 4.6
          // https://blogs.msdn.microsoft.com/pfxteam/2015/02/02/new-task-apis-in-net-4-6/
          let returnTask = activeTcs.Task.ContinueWith(this.MoveNextContinuation, TaskContinuationOptions.DenyChildAttach)
          if(not ct.CanBeCanceled) then // (ct = CancellationToken.None || ct = Unchecked.defaultof<CancellationToken>)) then
            // hot path
            returnTask.Unwrap()
          else
            // TODO even though this is quite fast and we have a hot path above,
            // we could cache token, check for equality and do registration work once
            this.token <- ct
            this.cancelledTcs <- new TaskCompletionSource<_>()
            let registration = ct.Register(fun _ -> 
                this.cancelledTcs.SetResult(cancelledBoolTask)
              )
            let anyReturn = Task.WhenAny(returnTask, this.cancelledTcs.Task)
            let final = anyReturn.Unwrap().Unwrap()
            registration.Dispose()
            this.token <- Unchecked.defaultof<_>
            final

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNextContinuation(t:Task<int64>): Task<bool> =
      // while this is not null, noone would be able to set a new one
      let original = Volatile.Read(&source.updateTcs)
      if original <> null then
        // one of many cursors will succeed
        Interlocked.CompareExchange(&source.updateTcs, null, original) |> ignore
      match this.state.MoveNext() with
      | true -> trueTask
      | false ->
        match this.state.Source.IsReadOnly with
        // TODO review this line
        // currently it should always be an OOO exception
        | false -> this.MoveNext(this.token)
        | true -> if this.state.MoveNext() then trueTask else falseTask
        
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.Clone() = 
      let mutable entered = false
      try
        entered <- enterWriteLockIf &source.locker true
        let clone = new CursorAsync<'K,'V,'TCursor>(source, cursorFactory)
        clone.state <- this.state
        clone
      finally
        exitWriteLockIf &source.locker entered
      

    member this.Dispose() = this.state.Reset()

    // C#-like methods to avoid casting to ICursor all the time
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.Reset() = this.state.Reset()
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNext():bool = this.state.MoveNext()
    member this.Current with get(): KVP<'K, 'V> = this.state.Current
    member this.Comparer with get() = source.Comparer
    member this.CurrentBatch = this.state.CurrentBatch
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.state.MoveNextBatch(cancellationToken)
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveAt(index:'K, lookup:Lookup) = this.state.MoveAt(index, lookup)
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveFirst():bool = this.state.MoveFirst()
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveLast():bool =  this.state.MoveLast()
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MovePrevious():bool = this.state.MovePrevious()
    member this.CurrentKey with get():'K = this.state.CurrentKey
    member this.CurrentValue with get():'V = this.state.CurrentValue
    member this.Source with get() = source :> ISeries<'K,'V>
    member this.IsContinuous with get() = false
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = source.TryGetValue(key, &value)

    interface IDisposable with
      member this.Dispose() = this.Dispose()

    interface IEnumerator<KVP<'K,'V>> with    
      member this.Reset() = this.state.Reset()
      member this.MoveNext():bool = this.state.MoveNext()
      member this.Current with get(): KVP<'K, 'V> = this.state.Current
      member this.Current with get(): obj = this.state.Current :> obj

    interface IAsyncEnumerator<KVP<'K,'V>> with
      member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 

    interface ICursor<'K,'V> with
      member this.Comparer with get() = source.Comparer
      member this.CurrentBatch = this.state.CurrentBatch
      member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.state.MoveNextBatch(cancellationToken)
      member this.MoveAt(index:'K, lookup:Lookup) = this.state.MoveAt(index, lookup)
      member this.MoveFirst():bool = this.state.MoveFirst()
      member this.MoveLast():bool =  this.state.MoveLast()
      member this.MovePrevious():bool = this.state.MovePrevious()
      member this.CurrentKey with get():'K = this.state.CurrentKey
      member this.CurrentValue with get():'V = this.state.CurrentValue
      member this.Source with get() = source :> ISeries<'K,'V>
      member this.Clone() = this.Clone() :> ICursor<'K,'V>
      member this.IsContinuous with get() = false
      member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = source.TryGetValue(key, &value)








// TODO rename back to MapCursor - this is an original cursor backed by some map, it does not represent series itself
[<AbstractClassAttribute>]
type BaseCursorOld<'K,'V>
  (source:IReadOnlyOrderedMap<'K,'V>) as this =

  // implement default MoveNextAsync logic using only MoveNext
  let isUpdateable = match source with | :? IUpdateable -> true | _ -> false
  let observerStarted = ref false
  //let tcs = ref (TaskCompletionSource<bool>())
  let mutable tcs = (Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create())
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
              if tcs <> Unchecked.defaultof<_> then
                //Debug.WriteLine("B: !tcs <> null")
                if this.MoveNext() then
                  let tcs' = tcs
                  tcs <- Unchecked.defaultof<_>
                  //Console.WriteLine("C: Moved")
                  //let couldSetResult = 
                  (tcs').SetResult(true)
        #if PRERELEASE
                  //Trace.Assert(couldSetResult)
        #endif
                  ()
                // check if the source became immutable
                elif source.IsReadOnly then 
                  //Debug.WriteLine("D")
                  //let couldSetResult = 
                  (tcs).SetResult(false)
                  //Trace.Assert(couldSetResult)
                  cont <- false
              else
                // do nothing, next MoveNext(ct) will try to call MoveNext() and it will return the correct result
                //Console.WriteLine("Not moved")
                ()
            )
            return! completeTcs()
          else
            //Debug.WriteLine("STOP")
            return false // stop the loop
    }

  let updateHandler : OnUpdateHandler = 
    OnUpdateHandler(fun _ ->
        if semaphore.CurrentCount = 0 then semaphore.Release() |> ignore
    )
      
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
        Trace.Assert(source :? IUpdateable)
        (source :?> IUpdateable).remove_OnUpdate(updateHandler)
    )

  abstract member Reset : unit -> unit

  abstract member MoveNext : CancellationToken -> Task<bool>
  override this.MoveNext(ct) =
      match this.MoveNext() with
      | true -> trueTask
      | false ->
        match isUpdateable, source.IsReadOnly with
        | true, false ->
          let upd = source :?> IUpdateable
          if not !observerStarted then 
            upd.add_OnUpdate(updateHandler)
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
  //          trueTask
  //        else
          cancellationToken := ct
          // TODO use interlocked.exchange or whatever to not allocate new one every time, if we return trueTask below
          // interlocked.exchange does not short-circuit and allocates value each time
          //Interlocked.CompareExchange(tcs, TaskCompletionSource(), null) |> ignore
        
//          if !tcs = null then
//            tcs := TaskCompletionSource()
//          tcs.Value.Task
          // NB it looks like the fact that tcs is a struct is important
          lock(sr) (fun _ ->
              if tcs = Unchecked.defaultof<_>  then
                tcs <- Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create() // new TaskCompletionSource<bool>() //
              tcs.Task
          )
        
        | _ -> falseTask // has no values and will never have because is not IUpdateable or IsMutable=false

  abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
  abstract CurrentBatch: ISeries<'K,'V> with get
  abstract Source : ISeries<'K,'V> with get
  override this.Source with get() = source :> ISeries<'K,'V>
  abstract Clone: unit -> ICursor<'K,'V>

  // NB this is now not a part of interface (it was, could be back if needed)
  abstract IsBatch: bool with get
  abstract IsContinuous: bool with get

//  abstract Subscribe: observer:IObserver<KVP<'K,'V>> -> IDisposable
//  override this.Subscribe(observer : IObserver<KVP<'K,'V>>) : IDisposable =
//    match box observer with
//    | :? ISeriesSubscriber<'K, 'V> as seriesSubscriber -> 
//      let seriesSubscription : ISeriesSubscription<'K> = Unchecked.defaultof<_>
//      seriesSubscription :> IDisposable
//    | :? ISubscriber<KVP<'K,'V>> as subscriber -> 
//      let subscription : ISubscription = Unchecked.defaultof<_>
//      subscription :> IDisposable
//    | _ ->
//      // draft: move from current position on every source OnNext
//      let sourceObserver = 
//        { new IObserver<KVP<'K,'V>> with
//            member x.OnNext(kvp) = ()
//            member x.OnCompleted() = observer.OnCompleted()
//            member x.OnError(exn) = observer.OnError(exn)
//        }
//      let sourceSubscription = source.Subscribe(sourceObserver)
//      { new IDisposable with
//          member x.Dispose() = 
//            sourceSubscription.Dispose()
//      }


  interface IDisposable with
    member this.Dispose() = this.Dispose()

  interface IEnumerator<KVP<'K,'V>> with
    member this.Reset() = this.Reset()
    member this.MoveNext():bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V> = this.Current
    member this.Current with get(): obj = this.Current :> obj

  interface IAsyncEnumerator<KVP<'K,'V>> with
    member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 

  interface ICursor<'K,'V> with
    member this.Comparer with get() = this.Comparer
    member this.CurrentBatch: ISeries<'K,'V> = this.CurrentBatch
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


// TODO rename back to MapCursor - this is an original cursor backed by some map, it does not represent series itself
[<AbstractClassAttribute>]
type BaseCursor<'K,'V>(source:Series<'K,'V>) as this =

  let mutable isSubscribed = false
  
  let mutable tcs : AsyncTaskMethodBuilder<bool> = Unchecked.defaultof<_> //(AsyncTaskMethodBuilder<bool>.Create())
  
  let mutable cancellationToken : CancellationToken = Unchecked.defaultof<_>
  let mutable cancellationTokenRegistration : CancellationTokenRegistration = Unchecked.defaultof<_>
      
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
    //source.onUpdateEvent.Publish.RemoveHandler(onUpdateHandler)
    ()

  abstract member Reset : unit -> unit

  abstract member MoveNext : CancellationToken -> Task<bool>
  override this.MoveNext(ct) =
    if this.MoveNext() then trueTask
    elif not source.IsReadOnly then
      #if PRERELEASE
      Trace.Assert((tcs = Unchecked.defaultof<_>), "TCS field must be cleared before setting TCS result")
      #endif
      if not isSubscribed then
//        source.onNextEvent.Publish.AddHandler(onNextHandler)
//        source.onCompletedEvent.Publish.AddHandler(onCompletedHandler)
//        source.onErrorEvent.Publish.AddHandler(onErrorHandler)
        isSubscribed <- true

      // in most cases we have the same CT
      if cancellationToken <> ct && ct <> Unchecked.defaultof<_> && ct <> CancellationToken.None then
        cancellationToken <- ct
        if cancellationTokenRegistration <> Unchecked.defaultof<_> then
          cancellationTokenRegistration.Dispose()
        //cancellationTokenRegistration <- ct.Register(fun _ -> onErrorHandler.Invoke(OperationCanceledException()))
        
      lock source.SyncRoot (fun _ ->
        // OnNext could have been called before tcs is assigned and then there are no more onnexts
        if this.MoveNext() then
          ()
        else
          tcs <- AsyncTaskMethodBuilder<bool>.Create()
      )
      tcs.Task
    else falseTask

  abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
  abstract CurrentBatch: ISeries<'K,'V> with get
  abstract Source : ISeries<'K,'V> with get
  override this.Source with get() = source :> ISeries<'K,'V>
  abstract Clone: unit -> ICursor<'K,'V>

  // NB this is now not a part of interface (it was, could be back if needed)
  abstract IsBatch: bool with get
  abstract IsContinuous: bool with get

//  abstract Subscribe: observer:IObserver<KVP<'K,'V>> -> IDisposable
//  override this.Subscribe(observer : IObserver<KVP<'K,'V>>) : IDisposable =
//    match box observer with
//    | :? ISeriesSubscriber<'K, 'V> as seriesSubscriber -> 
//      let seriesSubscription : ISeriesSubscription<'K> = Unchecked.defaultof<_>
//      seriesSubscription :> IDisposable
//    | :? ISubscriber<KVP<'K,'V>> as subscriber -> 
//      let subscription : ISubscription = Unchecked.defaultof<_>
//      subscription :> IDisposable
//    | _ ->
//      let entered = enterLockIf source.SyncRoot true
//      try
//        // TODO this is rebroadcast, but we need to move this cursor and push moves
//        if source.IsMutable then
//          source.onNextEvent.Publish.AddHandler(OnNextHandler(observer.OnNext))
//          let completedHandler = OnCompletedHandler(fun isCompleted -> if isCompleted then observer.OnCompleted())
//          source.onCompletedEvent.Publish.AddHandler(completedHandler)
//          source.onErrorEvent.Publish.AddHandler(OnErrorHandler(observer.OnError))
//          // unsubscribe does not destroy a cursor and we could re-subscribe and be at the same place
//          { new IDisposable with
//              member x.Dispose() = 
//                source.onNextEvent.Publish.RemoveHandler(OnNextHandler(observer.OnNext))
//                source.onCompletedEvent.Publish.RemoveHandler(completedHandler)
//                source.onErrorEvent.Publish.RemoveHandler(OnErrorHandler(observer.OnError))
//          }
//        else
//          observer.OnCompleted()
//          DummyDisposable.Instance
//      finally
//        exitLockIf source.SyncRoot entered


  interface IDisposable with
    member this.Dispose() = this.Dispose()

  interface IEnumerator<KVP<'K,'V>> with
    member this.Reset() = this.Reset()
    member this.MoveNext():bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V> = this.Current
    member this.Current with get(): obj = this.Current :> obj

  interface IAsyncEnumerator<KVP<'K,'V>> with
    member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 

  interface ICursor<'K,'V> with
    member this.Comparer with get() = this.Comparer
    member this.CurrentBatch = this.CurrentBatch
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
[<ObsoleteAttribute("TODO replace it with direct BaseCursor implementation")>]
type MapCursor<'K,'V>(map:IReadOnlyOrderedMap<'K,'V>) as this =
  inherit BaseCursorOld<'K,'V>(map)
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

  override this.CurrentBatch = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))
  override this.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))

  override this.Source with get() = map :> ISeries<'K,'V>

  override this.Clone() =
    let c = new MapCursor<'K,'V>(map)
    c.currentPosition <- this.currentPosition
    c :> ICursor<'K,'V>

  override this.IsBatch with get() = false
  override this.IsContinuous with get() = false


