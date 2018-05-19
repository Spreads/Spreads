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

