// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.CompilerServices

open Spreads

[<Extension>]
type SeriesExtensions () =

//    [<Extension>] //inline
//    static member  Window(source: ISeries<'K,'V>, width:uint32, step:uint32) : Series<'K,Series<'K,'V>> =
//      if width = 0u then raise (ArgumentOutOfRangeException("Width must be positive"))
//      if step = 0u then raise (ArgumentOutOfRangeException("Step must be positive"))
//      CursorSeries(fun _ -> new WindowCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), width, step, false) :> ICursor<'K,Series<'K,'V>>) :> Series<'K,Series<'K,'V>>

//    [<Extension>] // inline
//    static member  Window(source: ISeries<'K,'V>, width:uint32, step:uint32, returnIncomplete:bool) : Series<'K,Series<'K,'V>> = 
//      CursorSeries(fun _ -> new WindowCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), width, step, returnIncomplete) :> ICursor<'K,Series<'K,'V>>) :> Series<'K,Series<'K,'V>>


    // TODO review. WeakRefernce here is wrong, if we pass a struct series to this method
    // it will be boxed and then GCed, WR will die
    /// Enumerates the source into SortedMap<'K,'V> as Series<'K,'V>. Similar to LINQ ToArray/ToList methods.
    //[<Extension>]
    //static member inline Cache(source: ISeries<'K,'V>) : SortedMap<'K,'V> =
    //  let sm = SortedMap()
    //  // NB if caller of Cache no loger uses sm, the task will stop
    //  let wekRef = new WeakReference(sm)
    //  let task = task {
    //      let sm' = (wekRef.Target :?> SortedMap<'K,'V>)
    //      let cursor = source.GetCursor()
    //      while cursor.MoveNext() do
    //        sm'.AddLast(cursor.CurrentKey, cursor.CurrentValue)
    //      // by contract, if MN returned false, cursor stays at the same key andwe could call MNA
    //      let mutable cont = true
    //      while cont do
    //        if wekRef.IsAlive then
    //          let delay = Task.Delay(1000)
    //          let mn = cursor.MoveNext(CancellationToken.None)
    //          let! moved = Task.WhenAny(mn, delay)
    //          if mn.IsCompleted then
    //            let moved = mn.Result
    //            cont <- moved
    //            if moved then (wekRef.Target :?> SortedMap<'K,'V>).AddLast(cursor.CurrentKey, cursor.CurrentValue)
    //        else
    //          cont <- false
    //      #if PRERELEASE
    //          //Trace.WriteLine("ToSortedMap task exited")
    //      #endif
    //      cursor.Dispose()
    //      return 0
    //  }
    //  let runninTask = Task.Run<int>(Func<Task<int>>(fun _ -> task))
    //  sm


    

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


    [<Extension>]
    static member inline Zip<'K,'V,'R when 'K : comparison>(series: Series<'K,'V> array, resultSelector:Func<'K,'V[],'R>) =
      CursorSeries(fun _ -> new ZipNCursor<'K,'V,'R>(resultSelector, series |> Array.map (fun s -> s.GetCursor))  :> ICursor<'K,'R>) :> Series<'K,'R>

    [<Extension>]
    static member inline Zip<'K,'V,'R when 'K : comparison>(series: ISeries<'K,'V> array, resultSelector:Func<'K,'V[],'R>) =
      CursorSeries(fun _ -> new ZipNCursor<'K,'V,'R>(resultSelector, series |> Array.map (fun s -> s.GetCursor))  :> ICursor<'K,'R>) :> Series<'K,'R>


    /// A shortcut for `if not orderedMap.IsEmpty then IMutableSeries.RemoveMany(orderedMap.First.Key, Lookup.GE) else false`
    [<Extension>]
    static member inline RemoveAll<'K,'V when 'K : comparison>(orderedMap: IMutableSeries<'K,'V>) =
      if not orderedMap.IsEmpty then orderedMap.RemoveMany(orderedMap.First.Key, Lookup.GE)
      else false


