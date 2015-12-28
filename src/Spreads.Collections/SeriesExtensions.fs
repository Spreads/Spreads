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
type SeriesExtensions () =
    /// Wraps any series into CursorSeries that implements only the IReadOnlyOrderedMap interface. Use it to prevent 
    /// any accidental mutations on the source series (e.g. as compared to using IROOM interface of SortedMap)
    [<Extension>]
    static member inline ReadOnly(source: ISeries<'K,'V>) : Series<'K,'V> = 
      CursorSeries(fun _ -> source.GetCursor()) :> Series<'K,'V>

    // TODO AsyncOnly

    [<Extension>]
    static member  Repeat(source: ISeries<'K,'V>) : Series<'K,'V> = 
      CursorSeries(fun _ -> new RepeatCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor)) :> ICursor<_,_> ) :> Series<'K,'V>


    /// TODO check if input cursor is MapValuesCursor or FilterValuesCursor cursor and repack them into
    /// a single mapFilter cursor with nested funcs. !!! Check if this gives any per gain !!! 
    [<Extension>]
    static member Map(source: ISeries<'K,'V>, mapFunc:Func<'V,'V2>) : Series<'K,'V2> =
#if PRERELEASE // we could switch off this optimization in prerelease builds
      if OptimizationSettings.CombineFilterMapDelegates then
#endif
        match source with
        | :? ICanMapSeriesValues<'K,'V> as s -> s.Map(mapFunc)
        | _ ->
          CursorSeries(fun _ -> new BatchMapValuesCursor<'K,'V,'V2>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> ICursor<_,_> ) :> Series<'K,'V2>
#if PRERELEASE
      else CursorSeries(fun _ -> new BatchMapValuesCursor<'K,'V,'V2>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> ICursor<_,_> ) :> Series<'K,'V2>
#endif
//    [<Extension>]
//    static member inline Map(source: Series<'K,'V>, mapFunc:Func<'K,'K>) : Series<'K,'V> =
//      CursorSeries(fun _ -> new MapKeysCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member Filter(source: ISeries<'K,'V>, filterFunc:Func<'V,bool>) : Series<'K,'V> = 
       CursorSeries(fun _ -> new FilterValuesCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), filterFunc) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member FilterMap(source: ISeries<'K,'V>, filterFunc:Func<'K,'V,bool>, mapper:Func<'V,'V2>) : Series<'K,'V2> = 
       CursorSeries(fun _ -> new FilterMapCursor<'K,'V,'V2>(Func<ICursor<'K,'V>>(source.GetCursor), filterFunc, mapper) :> ICursor<'K,'V2>) :> Series<'K,'V2>
      //inline

    [<Extension>]
    static member Filter(source: ISeries<'K,'V>, filterFunc:Func<'K,'V,bool>) : Series<'K,'V> = 
       CursorSeries(fun _ -> new FilterMapCursor<'K,'V,'V>(Func<ICursor<'K,'V>>(source.GetCursor), filterFunc, idFunc) :> ICursor<'K,'V>) :> Series<'K,'V>

    /// Fill missing values with the given value
    [<Extension>]
    static member Fill(source: ISeries<'K,'V>, fillValue:'V) : Series<'K,'V> = 
      CursorSeries(fun _ -> new FillCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), fillValue) :> ICursor<_,_> ) :> Series<'K,'V>

    [<Extension>]
    static member Lag(source: ISeries<'K,'V>, lag:uint32) : Series<'K,'V> = 
      CursorSeries(fun _ -> new LagCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), lag) :> ICursor<'K,'V>) :> Series<'K,'V>

    /// Apply zipCurrentPrev function to current and lagged values (in this order, current is the first) and return the result
    [<Extension>]
    static member ZipLagSlow(source: ISeries<'K,'V>, lag:uint32, zipCurrentPrev:Func<'V,'V,'R>) : Series<'K,'R> = 
      new ZipLagCursorSlow<'K,'V,'R>(Func<ICursor<'K,'V>>(source.GetCursor), lag, zipCurrentPrev) :> Series<'K,'R>


    /// Apply zipCurrentPrev function to current and lagged values (in this order, current is the first) and return the result
    [<Extension>]
    static member ZipLag(source: ISeries<'K,'V>, lag:uint32, zipCurrentPrev:Func<'V,'V,'R>) : Series<'K,'R> = 
      CursorSeries(fun _ -> new ZipLagCursor<'K,'V,'R>(Func<ICursor<'K,'V>>(source.GetCursor), lag, zipCurrentPrev) :> ICursor<'K,'R>) :> Series<'K,'R>


//    [<Extension>]
//    static member inline Diff(source: ISeries<'K,'V>) : Series<'K,'V> = 
//      CursorSeries(fun _ -> new LagMapCursor<'K,'V,'V>(Func<ICursor<'K,'V>>(source.GetCursor), 1u, fun c p -> c - p) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>] //inline
    static member  Window(source: ISeries<'K,'V>, width:uint32, step:uint32) : Series<'K,Series<'K,'V>> =
      if width = 0u then raise (ArgumentOutOfRangeException("Width must be positive"))
      if step = 0u then raise (ArgumentOutOfRangeException("Step must be positive"))
      CursorSeries(fun _ -> new WindowCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), width, step, false) :> ICursor<'K,Series<'K,'V>>) :> Series<'K,Series<'K,'V>>

    [<Extension>] // inline
    static member  Window(source: ISeries<'K,'V>, width:uint32, step:uint32, returnIncomplete:bool) : Series<'K,Series<'K,'V>> = 
      CursorSeries(fun _ -> new WindowCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), width, step, returnIncomplete) :> ICursor<'K,Series<'K,'V>>) :> Series<'K,Series<'K,'V>>


    /// Enumerates the source into SortedMap<'K,'V> as Series<'K,'V>. Similar to LINQ ToArray/ToList methods.
    [<Extension>]
    static member inline Cache(source: ISeries<'K,'V>) : SortedMap<'K,'V> =
      let sm = SortedMap()
      // NB if called of Cache no loger uses sm, the tasl will stop
      let wekRef = new WeakReference(sm)
      let task = task {
          let sm' = (wekRef.Target :?> SortedMap<'K,'V>)
          let cursor = source.GetCursor()
          while cursor.MoveNext() do
            sm'.AddLast(cursor.CurrentKey, cursor.CurrentValue)
          // by contract, if MN returned false, cursor stays at the same key andwe could call MNA
          let mutable cont = true
          while cont do
            if wekRef.IsAlive then
              let delay = Task.Delay(1000)
              let mn = cursor.MoveNext(CancellationToken.None)
              let! moved = Task.WhenAny(mn, delay)
              if mn.IsCompleted then
                let moved = mn.Result
                cont <- moved
                if moved then (wekRef.Target :?> SortedMap<'K,'V>).AddLast(cursor.CurrentKey, cursor.CurrentValue)
            else
              cont <- false
          #if PRERELEASE
              //Trace.WriteLine("ToSortedMap task exited")
          #endif
          cursor.Dispose()
          return 0
      }
      let runninTask = Task.Run<int>(Func<Task<int>>(fun _ -> task))
      sm

    // TODO async fold that returns only when source is complete

    // TODO Do should return task that returns when the source is complete or token was cancelled
    // called of Do decides wether await or not on it

    /// Invoke action on each key/value sequentially
    [<Extension>]
    static member inline Do(source: ISeries<'K,'V>, action:Action<'K,'V>, token:CancellationToken) : unit =
      let task = task {
          let cursor = source.GetCursor()
          while cursor.MoveNext() do
            action.Invoke(cursor.CurrentKey, cursor.CurrentValue)
          let mutable moved = true
          while moved && not token.IsCancellationRequested do
              let! moved' =  cursor.MoveNext(token)
              moved <- moved'
              if moved then
                action.Invoke(cursor.CurrentKey, cursor.CurrentValue)
          cursor.Dispose()
          return 0
      }
      let runninTask = Task.Run<int>(Func<Task<int>>(fun _ -> task))
      ()

    [<Extension>]
    static member inline Do(source: ISeries<'K,'V>, action:Action<'K,'V>) : unit =
      SeriesExtensions.Do(source, action, CancellationToken.None)

    /// Enumerates the source into SortedMap<'K,'V> as Series<'K,'V>. Similar to LINQ ToArray/ToList methods.
    [<Extension>]
    static member inline ToSortedMap(source: ISeries<'K,'V>) : SortedMap<'K,'V> =
      let sm = SortedMap()
      let cursor = source.GetCursor()
      while cursor.MoveNext() do
        sm.AddLast(cursor.CurrentKey, cursor.CurrentValue)
      sm

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
    static member Scan(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'K,'V,'R>) : Series<'K,'R> = 
      CursorSeries(fun _ -> new ScanCursor<'K,'V,'R>(Func<ICursor<'K,'V>>(source.GetCursor), init, folder) :> ICursor<'K,'R>) :> Series<'K,'R>
      
    [<Extension>]
    static member inline Range(source: ISeries<'K,'V>, startKey:'K, endKey:'K) : Series<'K,'V> = 
      CursorSeries(fun _ -> new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), Some(startKey), Some(endKey), None, None) :> ICursor<'K,'V>) :> Series<'K,'V>
      
    [<Extension>]
    static member inline After(source: ISeries<'K,'V>, startKey:'K, lookup:Lookup) : Series<'K,'V> = 
      CursorSeries(fun _ -> new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), Some(startKey), None, Some(lookup), None) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Before(source: ISeries<'K,'V>, endKey:'K, lookup:Lookup) : Series<'K,'V> = 
      CursorSeries(fun _ -> new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), None, Some(endKey), None, Some(Lookup.GT)) :> ICursor<'K,'V>) :> Series<'K,'V>
    

    [<Extension>]
    static member inline Zip(source: ISeries<'K,'V>, other: ISeries<'K,'V2>, mapFunc:Func<'K,'V,'V2,'R>) : Series<'K,'R> =
      // TODO! check this type stuff
      if typeof<'V> = typeof<'V2> then
        let mapFunc2 = (box mapFunc) :?> Func<'K,'V,'V,'R>
        CursorSeries(fun _ -> new ZipNCursor<'K,'V,'R>(Func<'K,'V[],'R>(fun k varr -> mapFunc2.Invoke(k, varr.[0], varr.[1])), [|source;(box other) :?> ISeries<'K,'V>|] |> Array.map (fun s -> s.GetCursor))  :> ICursor<'K,'R>) :> Series<'K,'R>
      else
        CursorSeries(fun _ -> new Zip2Cursor<'K,'V,'V2,'R>(Func<ICursor<'K,'V>>(source.GetCursor), Func<ICursor<'K,'V2>>(other.GetCursor), mapFunc) :> ICursor<'K,'R>) :> Series<'K,'R>

    [<Extension>]
    static member inline Zip(source: ISeries<'K,'V>, other: ISeries<'K,'V2>, mapFunc:Func<'V,'V2,'R>) : Series<'K,'R> =
      // TODO! check this type stuff
      if typeof<'V> = typeof<'V2> then
        let mapFunc2 = (box mapFunc) :?> Func<'V,'V,'R>
        CursorSeries(fun _ -> new ZipNCursor<'K,'V,'R>(Func<'K,'V[],'R>(fun k varr -> mapFunc2.Invoke(varr.[0], varr.[1])), [|source;(box other) :?> ISeries<'K,'V>|] |> Array.map (fun s -> s.GetCursor))  :> ICursor<'K,'R>) :> Series<'K,'R>
      else
        CursorSeries(fun _ -> new Zip2Cursor<'K,'V,'V2,'R>(Func<ICursor<'K,'V>>(source.GetCursor), Func<ICursor<'K,'V2>>(other.GetCursor), fun k v1 v2 -> mapFunc.Invoke(v1, v2)) :> ICursor<'K,'R>) :> Series<'K,'R>


    [<Extension>]
    static member inline Zip<'K,'V,'R when 'K : comparison>(series: Series<'K,'V> array, resultSelector:Func<'K,'V[],'R>) =
      CursorSeries(fun _ -> new ZipNCursor<'K,'V,'R>(resultSelector, series |> Array.map (fun s -> s.GetCursor))  :> ICursor<'K,'R>) :> Series<'K,'R>

    [<Extension>]
    static member inline Zip<'K,'V,'R when 'K : comparison>(series: ISeries<'K,'V> array, resultSelector:Func<'K,'V[],'R>) =
      CursorSeries(fun _ -> new ZipNCursor<'K,'V,'R>(resultSelector, series |> Array.map (fun s -> s.GetCursor))  :> ICursor<'K,'R>) :> Series<'K,'R>


