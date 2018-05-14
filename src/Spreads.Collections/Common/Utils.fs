// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open System.Runtime.CompilerServices

[<AutoOpenAttribute>]
module TestUtils =
  /// run f and measure ops per second
  let perf (count:int64) (message:string) (f:unit -> unit) : unit = // int * int =
    GC.Collect(3, GCCollectionMode.Forced, true)
    let startMem = GC.GetTotalMemory(false)
    let gen0 = GC.CollectionCount(0);
    let gen1 = GC.CollectionCount(1);
    let gen2 = GC.CollectionCount(2);
    let gen3 = GC.CollectionCount(3);
    let sw = Stopwatch.StartNew()
    f()
    sw.Stop()
    let peakMem = GC.GetTotalMemory(false)
    GC.Collect(3, GCCollectionMode.Forced, true)
    let endMem = GC.GetTotalMemory(true)
    let gen0 = GC.CollectionCount(0) - gen0;
    let gen1 = GC.CollectionCount(1) - gen1;
    let gen2 = GC.CollectionCount(2) - gen2;
    let gen3 = GC.CollectionCount(3) - gen3;
    let p = (1000L * count/sw.ElapsedMilliseconds)
    Console.WriteLine(message + ", #{0}, ops: {1}, pm: {2}, em: {3}, g+: {4}", 
      count.ToString(), p.ToString(), ((peakMem - startMem)/count).ToString(), ((endMem - startMem)/count).ToString(), gen0+gen1+gen2+gen3)


[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Utils =
  // locking using use keyword
  //[<ObsoleteAttribute("When performance is *critical*, consider using enter/exit with try/catch because this shortcut allocates new IDisposable")>]
  // TODO (low) this only remains in misc maps, not in the core ones. Replace later 
  let inline makeLock locker =
    let entered = ref false
    try
      System.Threading.Monitor.Enter(locker, entered)
    with
      | _ -> () 
    { new System.IDisposable with  
      member x.Dispose() =
        try
          if !entered then System.Threading.Monitor.Exit(locker) 
        with
        | _ -> ()
    }

  let inline enterLockIf locker (condition:bool) = 
    if condition then System.Threading.Monitor.Enter(locker)
    condition
  let inline exitLockIf locker (condition:bool) = 
    if condition then System.Threading.Monitor.Exit(locker)

  let inline enterWriteLockIf (locker:int byref) (condition:bool) =
    if condition then
      let sw = new SpinWait()
      let mutable cont = true
      while cont do
        if Interlocked.CompareExchange(&locker, 1, 0) = 0 then cont <- false
        else
          sw.SpinOnce()
          #if TRACE_LOCK_FREE
          if sw.Count > 100 then ThrowHelper.ThrowInvalidOperationException("Deadlock in enterWriteLock, debug me!")
          #else
          if sw.NextSpinWillYield then sw.Reset()
          #endif
      true
    else false

  let inline exitWriteLockIf (locker:int byref) (condition:bool) = 
    if condition then 
      #if PRERELEASE
      Trace.Assert((1 = Interlocked.Exchange(&locker, 0)))
      #else
      Interlocked.Exchange(&locker, 0) |> ignore
      #endif

  let inline enterWriteLock (locker:int byref) =
    let sw = new SpinWait()
    let mutable cont = true
    while cont do
      if Interlocked.CompareExchange(&locker, 1, 0) = 0 then cont <- false
      else
        sw.SpinOnce()
        #if TRACE_LOCK_FREE
        if sw.Count > 100 then ThrowHelper.ThrowInvalidOperationException("Deadlock in enterWriteLock, debug me!")
        #else
        if sw.NextSpinWillYield then sw.Reset()
        #endif

  let inline exitWriteLock (locker:int byref) = 
    #if TRACE_LOCK_FREE
    Trace.Assert((1 = Interlocked.Exchange(&locker, 0)))
    #else
    Interlocked.Exchange(&locker, 0) |> ignore
    #endif
      
  let inline readLockIf (nextVersion:int64 byref) (currentVersion:int64 byref) (condition:bool) (f:unit -> 'T) : 'T =
    let mutable value = Unchecked.defaultof<'T>
    let mutable doSpin = true
    let sw = new SpinWait()
    while doSpin do
      let version = if condition then Volatile.Read(&currentVersion) else currentVersion
      value <- f()
      if condition then
        let nextVersion = Volatile.Read(&nextVersion)
        if version = nextVersion then doSpin <- false
        else sw.SpinOnce()
      else doSpin <- false
    value
    
  let inline readLock (nextVersion:int64 byref) (currentVersion:int64 byref) (f:unit -> 'T) : 'T =
    let mutable value = Unchecked.defaultof<'T>
    let mutable doSpin = true
    let sw = new SpinWait()
    while doSpin do
      let version = Volatile.Read(&currentVersion)
      value <- f()
      let nextVersion = Volatile.Read(&nextVersion)
      if version = nextVersion then doSpin <- false
      else sw.SpinOnce()
    value

  let inline increment (value:byref<_>) = value <- value + LanguagePrimitives.GenericOne

  let inline decrement (value:byref<_>) = value <- value - LanguagePrimitives.GenericOne


  

[<AutoOpenAttribute>]
module TaskModule =
  let trueTask = Task.FromResult(true)
  let falseTask = Task.FromResult(false)
  let cancelledBoolTask = 
    let tcs = new TaskCompletionSource<bool>()
    tcs.SetCanceled()
    tcs.Task


  let inline bind (f: 'T -> Task<'U>) (m: Task<'T>) =
    if m.Status = TaskStatus.RanToCompletion then f m.Result
    elif m.IsCanceled then Utils.TaskUtil.FromCanceled<'U>(CancellationToken.None)
    elif m.IsFaulted then Utils.TaskUtil.FromException<'U>(m.Exception)
    else
      let tcs = (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create()) // new TaskCompletionSource<_>() // NB do not allocate objects
      let t = tcs.Task
      let awaiter = m.GetAwaiter() // NB this is faster than ContinueWith
      awaiter.OnCompleted(fun _ -> 
        if m.IsCanceled then tcs.SetException(OperationCanceledException())
        elif m.IsCompleted then tcs.SetResult(f m.Result)
        else tcs.SetException(m.Exception)
        )
      t.Unwrap()

  let inline returnM a = Task.FromResult(a)


  let inline bindBool (f: bool -> Task<bool>) (m: Task<bool>) =
    if m.Status = TaskStatus.RanToCompletion then f m.Result
    elif m.IsCanceled || m.IsFaulted then m
    else
      let tcs = (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create()) // new TaskCompletionSource<_>() // NB do not allocate objects
      let t = tcs.Task
      let awaiter = m.GetAwaiter() // NB this is faster than ContinueWith
      awaiter.OnCompleted(fun _ -> 
        if m.IsCanceled then tcs.SetResult(m)
        elif m.IsCompleted then tcs.SetResult(f m.Result)
        else tcs.SetResult(Utils.TaskUtil.FromException<bool>(m.Exception))
      )
      t.Unwrap()

  let inline returnMBool (a:bool) = if a then trueTask else falseTask

  type TaskBuilder(?continuationOptions, ?scheduler, ?cancellationToken) =
      let contOptions = defaultArg continuationOptions TaskContinuationOptions.None
      let scheduler = defaultArg scheduler TaskScheduler.Default
      let cancellationToken = defaultArg cancellationToken CancellationToken.None

      member inline this.Return x = returnM x

      member inline this.Zero() = returnM()

      member inline this.ReturnFrom (a: Task<'T>) = a

      member inline this.Bind(m, f) = bind f m

      //member this.Bind(m, f) = bindBool f m // bindWithOptions cancellationToken contOptions scheduler f m

      member this.Combine(comp1, comp2) =
          this.Bind(comp1, comp2)

      member this.While(guard, m) =
          let rec whileRec(guard, m) = 
            if not(guard()) then this.Zero() else
                this.Bind(m(), fun () -> whileRec(guard, m))
          whileRec(guard, m)

      member this.While(guardTask:unit->Task<bool>, body) =
        let m = guardTask()
        let onCompleted() =
          this.Bind(body(), fun () -> this.While(guardTask, body))
        if m.Status = TaskStatus.RanToCompletion then 
          onCompleted()
        else
          let tcs =  new TaskCompletionSource<_>() // (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create())
          let t = tcs.Task
          let awaiter = m.GetAwaiter()
          awaiter.OnCompleted(fun _ -> 
            if m.IsFaulted then
              tcs.SetException(m.Exception)
            elif m.IsCanceled then
              tcs.SetCanceled()
            else
              tcs.SetResult(onCompleted())
            )
          t.Unwrap()

      member this.TryFinally(m, compensation) =
          try this.ReturnFrom m
          finally compensation()

      member this.Using(res: #IDisposable, body: #IDisposable -> Task<_>) =
          this.TryFinally(body res, fun () -> match res with null -> () | disp -> disp.Dispose())

      member this.For(sequence: seq<_>, body) =
          this.Using(sequence.GetEnumerator(),
                                fun enum -> this.While(enum.MoveNext, fun () -> body enum.Current))

      member this.Delay (f: unit -> Task<'T>) = f

      member this.Run (f: unit -> Task<'T>) = f()

  let task = TaskBuilder(scheduler = TaskScheduler.Current)
