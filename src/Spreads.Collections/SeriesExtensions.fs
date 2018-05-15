// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.CompilerServices

open Spreads
open System.Threading
open System.Threading.Tasks

[<Extension>]
[<Obsolete>]
type SeriesExtensionsAux () =
    [<Extension>]
    static member inline Do(source: ISeries<'K,'V>, action:Action<'K,'V>, maxIterations:int64, token:CancellationToken) : Task<bool> =
      let tcs = Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create()
      let returnTask = tcs.Task
      let cursor = source.GetCursor()
      let maxIterations = if maxIterations = 0L then Int64.MaxValue else maxIterations
      let mutable iterations = 0L
      let rec loop() =
        while cursor.MoveNext() && iterations < maxIterations do
          action.Invoke(cursor.CurrentKey, cursor.CurrentValue)
          iterations <- iterations + 1L
        if iterations < maxIterations then
          let moveTask = cursor.MoveNextAsync(token)
          let awaiter = moveTask.GetAwaiter()
          awaiter.UnsafeOnCompleted(fun _ ->
            match moveTask.Status with
            | TaskStatus.RanToCompletion ->
              if moveTask.Result then
                action.Invoke(cursor.CurrentKey, cursor.CurrentValue)
                iterations <- iterations + 1L
                loop()
              else
                tcs.SetResult(true) // finish on complete
            | TaskStatus.Canceled -> tcs.SetException(OperationCanceledException())
            | TaskStatus.Faulted -> tcs.SetException(moveTask.Exception)
            | _ -> failwith "TODO process all task statuses"
            ()
          )
        else
          tcs.SetResult(false) // finish on iteration
      loop()
      returnTask

    [<Extension>]
    static member inline Do(source: ISeries<'K,'V>, action:Action<'K,'V>, token:CancellationToken) : Task<bool> =
      SeriesExtensionsAux.Do(source, action, 0L, token)

    [<Extension>]
    static member inline Do(source: ISeries<'K,'V>, action:Action<'K,'V>) : Task<bool> =
      SeriesExtensionsAux.Do(source, action, 0L, CancellationToken.None)

    [<Extension>]
    static member inline Fold(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'K,'V,'R>) : 'R = 
      let mutable state = init
      for kvp in source do
        state <- folder.Invoke(state, kvp.Key, kvp.Value)
      state

    [<Extension>]
    static member inline Fold(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'V,'R>) : 'R = 
      let mutable state = init
      for kvp in source do
        state <- folder.Invoke(state, kvp.Value)
      state

    [<Extension>]
    static member inline FoldKeys(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'K,'R>) : 'R = 
      let mutable state = init
      for kvp in source do
        state <- folder.Invoke(state, kvp.Key)
      state

    /// A shortcut for `if not orderedMap.IsEmpty then IMutableSeries.RemoveMany(orderedMap.First.Key, Lookup.GE) else false`
    [<Extension>]
    static member inline RemoveAll<'K,'V when 'K : comparison>(orderedMap: IMutableSeries<'K,'V>) =
      let f = orderedMap.First
      let mutable x = Unchecked.defaultof<_>
      if f.IsPresent then orderedMap.TryRemoveMany(f.Present.Key, Lookup.GE, &x).Result
      else false


