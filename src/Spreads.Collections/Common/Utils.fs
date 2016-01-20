(*  
    Copyright (c) 2014-2015 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.
        
    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

namespace Spreads

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Diagnostics

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
    //int p, int((endtMem - startMem)/1024L)
    Console.WriteLine(message + ", #{0}, ops: {1}, pm: {2}, em: {3}, g+: {4}", 
      count.ToString(), p.ToString(), ((peakMem - startMem)/count).ToString(), ((endMem - startMem)/count).ToString(), gen0+gen1+gen2+gen3)
//
//    Console.WriteLine(message + ", #{0}, ops: {1}, \n\r\t pm: {2}, em: {3}, g0: {4}, g1: {5}, g2: {6}, g3: {7}", 
//      count.ToString(), p.ToString(), ((peakMem - startMem)/count).ToString(), ((endMem - startMem)/count).ToString(), gen0, gen1, gen2, gen3)

// TODO clean up this from unused snippets

[<AutoOpen>]
module Utils =
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

  let increment (value:byref<int>) = value <- value + 1

// TODO add back cancellation. this was taken from F#x and stripped from everything that is not needed for MoveNextAsync in cursors

[<AutoOpenAttribute>]
module TaskModule =
  let trueTask = Task.FromResult(true)
  let falseTask = Task.FromResult(false)
//  let inline konst a _ = a

  /// Task result
//  type Result<'T> = 
//      /// Task was canceled
//      | Canceled
//      /// Unhandled exception in task
//      | Error of exn 
//      /// Task completed successfully
//      | Successful of 'T

//  let run (t: unit -> Task<_>) = 
//      try
//          let task = t()
//          task.Result |> Result.Successful
//      with 
//      | :? OperationCanceledException -> Result.Canceled
//      | :? AggregateException as e ->
//          match e.InnerException with
//          | :? TaskCanceledException -> Result.Canceled
//          | _ -> Result.Error e
//      | e -> Result.Error e

//  let toAsync (t: Task<'T>): Async<'T> =
//      let abegin (cb: AsyncCallback, state: obj) : IAsyncResult = 
//          match cb with
//          | null -> upcast t
//          | cb -> 
//              t.ContinueWith(fun (_ : Task<_>) -> cb.Invoke t) |> ignore
//              upcast t
//      let aend (r: IAsyncResult) = 
//          (r :?> Task<'T>).Result
//      Async.FromBeginEnd(abegin, aend)

  /// Transforms a Task's first value by using a specified mapping function.
//  let inline mapWithOptions (token: CancellationToken) (continuationOptions: TaskContinuationOptions) (scheduler: TaskScheduler) f (m: Task<_>) =
//      m.ContinueWith((fun (t: Task<_>) -> f t.Result), token, continuationOptions, scheduler)
//
//  /// Transforms a Task's first value by using a specified mapping function.
//  let inline map f (m: Task<_>) =
//      m.ContinueWith(fun (t: Task<_>) -> f t.Result)

  let inline bind  (f: 'T -> Task<'U>) (m: Task<'T>) =
      if m.IsCompleted then f m.Result
      else
        let tcs = (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create()) // new TaskCompletionSource<_>() // NB do not allocate objects
        let t = tcs.Task
        let awaiter = m.GetAwaiter() // NB this is faster than ContinueWith
        awaiter.OnCompleted(fun _ -> tcs.SetResult(f m.Result))
        t.Unwrap()
        //m.ContinueWith((fun (x: Task<_>) -> f x.Result)).Unwrap()

//  let inline bind (f: 'T -> Task<'U>) (m: Task<'T>) = 
//      m.ContinueWith(fun (x: Task<_>) -> f x.Result).Unwrap()

  let inline returnM a = Task.FromResult(a)

  let inline bindBool  (f: bool -> Task<bool>) (m: Task<bool>) =
      if m.IsCompleted then f m.Result
      else
        let tcs = (Runtime.CompilerServices.AsyncTaskMethodBuilder<_>.Create()) // new TaskCompletionSource<_>() // NB do not allocate objects
        let t = tcs.Task
        let awaiter = m.GetAwaiter() // NB this is faster than ContinueWith
        awaiter.OnCompleted(fun _ -> tcs.SetResult(f m.Result))
        t.Unwrap()

  let inline returnMBool (a:bool) = if a then trueTask else falseTask

//  /// Sequentially compose two actions, passing any value produced by the first as an argument to the second.
//  let inline (>>=) m f = bind f m
//
//  /// Flipped >>=
//  let inline (=<<) f m = bind f m
//
//  /// Sequentially compose two either actions, discarding any value produced by the first
//  let inline (>>.) m1 m2 = m1 >>= (fun _ -> m2)
//
//  /// Left-to-right Kleisli composition
//  let inline (>=>) f g = fun x -> f x >>= g
//
//  /// Right-to-left Kleisli composition
//  //let inline (<=<) x = flip (>=>) x
//
//  /// Promote a function to a monad/applicative, scanning the monadic/applicative arguments from left to right.
//  let inline lift2 f a b = 
//      a >>= fun aa -> b >>= fun bb -> f aa bb |> returnM
//
//  /// Sequential application
//  let inline ap x f = lift2 id f x
//
//  /// Sequential application
//  let inline (<*>) f x = ap x f
//
//  /// Infix map
//  let inline (<!>) f x = map f x
//
//  /// Sequence actions, discarding the value of the first argument.
//  let inline ( *>) a b = lift2 (fun _ z -> z) a b
//
//  /// Sequence actions, discarding the value of the second argument.
//  let inline ( <*) a b = lift2 (fun z _ -> z) a b
    
  type TaskBuilder(?continuationOptions, ?scheduler, ?cancellationToken) =
      let contOptions = defaultArg continuationOptions TaskContinuationOptions.None
      let scheduler = defaultArg scheduler TaskScheduler.Default
      let cancellationToken = defaultArg cancellationToken CancellationToken.None

      member this.Return x = returnM x

      member this.Zero() = returnM()

      member this.ReturnFrom (a: Task<'T>) = a

      member this.Bind(m, f) = bind f m // bindWithOptions cancellationToken contOptions scheduler f m

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
