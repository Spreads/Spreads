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
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks

open Spreads
open Spreads.Collections
open System.Threading


// TODO Ensure extensions on ISeries but return series, to keep operators working

[<Extension>]
type ISeriesExtensions () =

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
          let moveTask = cursor.MoveNext(token)
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
      ISeriesExtensions.Do(source, action, 0L, token)

    [<Extension>]
    static member inline Do(source: ISeries<'K,'V>, action:Action<'K,'V>) : Task<bool> =
      ISeriesExtensions.Do(source, action, 0L, CancellationToken.None)