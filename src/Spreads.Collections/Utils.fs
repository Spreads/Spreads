namespace Spreads

open System
open System.IO
open System.Threading
// TODO clean up this from unused snippets

[<AutoOpen>]
module Utils =
  // locking using use keyword
  //[<ObsoleteAttribute("When performance is *critical*, consider using enter/exit with try/catch because this shortcut allocates new IDisposable")>]
  // TODO(low) this only remains in misc maps, not in the core ones. Replace later 
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
