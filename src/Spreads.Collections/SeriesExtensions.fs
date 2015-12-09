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


// TODO extensions on ISeries but return series, to keep operators working
// TODO check performance impact of Func instead of FSharpFunc


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
      new RepeatCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor)) :> Series<'K,'V>


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
          new BatchMapValuesCursor<'K,'V,'V2>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> Series<'K,'V2>
#if PRERELEASE
      else new BatchMapValuesCursor<'K,'V,'V2>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> Series<'K,'V2>
#endif
//    [<Extension>]
//    static member inline Map(source: Series<'K,'V>, mapFunc:Func<'K,'K>) : Series<'K,'V> =
//      CursorSeries(fun _ -> new MapKeysCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Filter(source: ISeries<'K,'V>, filterFunc:Func<'V,bool>) : Series<'K,'V> = 
      new FilterValuesCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), filterFunc) :> Series<'K,'V>
      //inline


    /// Fill missing values with the given value
    [<Extension>]
    static member Fill(source: ISeries<'K,'V>, fillValue:'V) : Series<'K,'V> = 
      new FillCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), fillValue) :> Series<'K,'V>

    [<Extension>]
    static member inline Lag(source: ISeries<'K,'V>, lag:uint32) : Series<'K,'V> = 
      CursorSeries(fun _ -> new LagCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), lag) :> ICursor<'K,'V>) :> Series<'K,'V>

    /// Apply zipCurrentPrev function to current and lagged values (in this order, current is the first) and return the result
    [<Extension>]
    static member inline ZipLag(source: ISeries<'K,'V>, lag:uint32, zipCurrentPrev:Func<'V,'V,'R>) : Series<'K,'R> = 
      CursorSeries(fun _ -> new ZipLagCursor<'K,'V,'R>(Func<ICursor<'K,'V>>(source.GetCursor), lag, zipCurrentPrev) :> ICursor<'K,'R>) :> Series<'K,'R>

//    [<Extension>]
//    static member inline Diff(source: ISeries<'K,'V>) : Series<'K,'V> = 
//      CursorSeries(fun _ -> new LagMapCursor<'K,'V,'V>(Func<ICursor<'K,'V>>(source.GetCursor), 1u, fun c p -> c - p) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Window(source: ISeries<'K,'V>, width:uint32, step:uint32) : Series<'K,Series<'K,'V>> = 
      CursorSeries(fun _ -> new WindowCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), width, step, false) :> ICursor<'K,Series<'K,'V>>) :> Series<'K,Series<'K,'V>>

    [<Extension>]
    static member inline Window(source: ISeries<'K,'V>, width:uint32, step:uint32, returnIncomplete:bool) : Series<'K,Series<'K,'V>> = 
      CursorSeries(fun _ -> new WindowCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), width, step, returnIncomplete) :> ICursor<'K,Series<'K,'V>>) :> Series<'K,Series<'K,'V>>


    /// Enumerates the source into SortedMap<'K,'V> as Series<'K,'V>. Similar to LINQ ToArray/ToList methods.
    [<Extension>]
    static member inline ToSortedMap(source: ISeries<'K,'V>) : SortedMap<'K,'V> =
      let sm = SortedMap()
      for kvp in source do
        sm.AddLast(kvp.Key, kvp.Value)
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
    static member inline Scan(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'K,'V,'R>) : Series<'K,'R> = 
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


// TODO generators