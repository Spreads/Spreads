namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks


type KVP<'K,'V> = KeyValuePair<'K,'V>

[<AutoOpenAttribute>]
module internal InternalExtensions = 
  type Task with
      static member inline FromResult(result: 'T) : Task<'T> = 
        let tcs = new TaskCompletionSource<'T>()
        tcs.SetResult(result)
        tcs.Task