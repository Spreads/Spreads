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

#nowarn "0086" // operator overloads are intentional, Series are as primitive as scalars, all arithmetic operations are defined on them as maps
namespace Spreads

open System
open System.Linq
open System.Linq.Expressions
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Numerics

open Spreads
open Spreads.Collections


// TODO (perf) see benchmark for ReadOnly. Reads are very slow while iterations are not 
// affected (GetCursor() returns original cursor) in release mode. Optimize 
// reads of this wrapper either here by type-checking the source of the cursor and using direct methods on the source
// or make cursor thread-static and initialize it only once (now it is called on each method)


// TODO this fails badly, need a nice view. Should hide almost everything from lazy series
//[<AllowNullLiteral>]
//type  SeriesDebuggerProxy<'K,'V>(series:ISeries<'K,'V>) =
//    member this.Note = "Debugger should not move cursors TODO write debugger view"


/// Could return a series mapped with the provided function
/// This is one of the most important optimizations because all arithmetic operations are 
/// mappings and they are often chained.
[<Interface>]
[<AllowNullLiteral>]
type internal ICanMapSeriesValues<'K,'V> =
  abstract member Map: mapFunc:('V->'V2) * fBatch:(ArraySegment<'V>->ArraySegment<'V2>) opt -> Series<'K,'V2>

and
  [<AllowNullLiteral>]
  Series internal() =
#if NET451
    // NB this is ugly, but rewriting the whole project structure is uglier // TODO "proper" methods DI
//    static do
//      let moduleInfo = 
//        typeof<Series>.GetAssembly().GetType("Spreads.Initializer")
//      //let ty = typeof<BaseSeries>
//      let mi = moduleInfo.GetMethod("init", (Reflection.BindingFlags.Static ||| Reflection.BindingFlags.NonPublic) )
//      mi.Invoke(null, [||]) |> ignore
#else
//    static do
//      typeof<Series>.GetAssembly().InvokeMethod("Spreads.Initializer", "init") 
#endif
  do ()

and
  [<AllowNullLiteral>]
  [<AbstractClassAttribute>]
  //[<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
  Series<'K,'V> internal(cursorFactory:(unit -> ICursor<'K,'V>) opt) as this =
    inherit Series()
    
    let c = Lazy<_>(this.GetCursor) //new ThreadLocal<_>(Func<_>(this.GetCursor), true) 

    [<DefaultValueAttribute>] 
    val mutable locker : int
    [<DefaultValueAttribute>] 
    val mutable internal updateTcs : TaskCompletionSource<int64>

    new(iseries:ISeries<'K,'V>) = Series<_,_>(Present(iseries.GetCursor))
    internal new() = Series<_,_>(Missing)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member internal this.NotifyUpdateTcs() =
      let updateTcs = Volatile.Read(&this.updateTcs)
      if updateTcs <> null then
        if not <| updateTcs.TrySetResult(0L) then this.NotifyUpdateTcs()

    /// Main method to override
    abstract GetCursor : unit -> ICursor<'K,'V>
    override this.GetCursor() = 
      if cursorFactory.IsPresent then cursorFactory.Present()
      else raise (new NotImplementedException("Series.GetCursor is not implemented"))

    abstract IsIndexed : bool with get
    abstract IsReadOnly: bool with get
    override this.IsIndexed with get() = lock(c) (fun _ -> c.Value.Source.IsIndexed)
    override this.IsReadOnly = lock(c) (fun _ -> c.Value.Source.IsReadOnly)

    // TODO (!) IObservable needs much more love and adherence to Rx contracts, see #40
    abstract Subscribe: observer:IObserver<KVP<'K,'V>> -> IDisposable
    override this.Subscribe(observer : IObserver<KVP<'K,'V>>) : IDisposable =
      let entered = enterWriteLockIf &this.locker true
      try
        //raise (NotImplementedException("TODO Rx. Subscribe must be implemented via a cursor."))
        match box observer with
        | :? ISubscriber<KVP<'K,'V>> as subscriber ->
          raise (NotSupportedException("TODO Add reactive streams support"))
          let subscription : ISubscription = Unchecked.defaultof<_>
          subscription :> IDisposable
        | _ ->
          let cts = new CancellationTokenSource()
          let ct = cts.Token
          Task.Run(fun _ ->
            this.Do((fun k v -> observer.OnNext(KVP(k,v))), ct).ContinueWith(fun (t:Task<bool>) ->
              match t.Status with
              | TaskStatus.RanToCompletion ->
                if t.Result then
                  observer.OnCompleted()
                else
                  raise (NotSupportedException("This overload of Do() should only return true"))
              | TaskStatus.Canceled -> observer.OnError(OperationCanceledException())
              | TaskStatus.Faulted -> observer.OnError(t.Exception)
              | _ -> raise (NotSupportedException("TODO process all task statuses"))
              ()
            )
          ) |> ignore
          { 
          // NB finalizers should be only in types that are actually keeping some resource
          //  new Object() with
          //    member x.Finalize() = (x :?> IDisposable).Dispose()
            new IDisposable with
              member x.Dispose() = cts.Cancel();cts.Dispose();
          }
      finally
        exitWriteLockIf &this.locker true


    member internal this.SyncRoot with get() = c :> obj

    member internal this.Comparer with get() = lock(c) (fun _ -> c.Value.Comparer)
    member internal this.IsEmpty = lock(c) (fun _ -> not (c.Value.MoveFirst()))

    member internal this.First 
      with get() = 
        let entered = enterLockIf c true
        try
          if c.Value.MoveFirst() then c.Value.Current else invalidOp "Series is empty"
        finally
          exitLockIf c entered

    member internal this.Last 
      with get() =
        let entered = enterLockIf c true
        try
          if c.Value.MoveLast() then c.Value.Current else invalidOp "Series is empty"
        finally
          exitLockIf c entered

    member internal this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      let entered = enterLockIf c true
      try
        if c.Value.MoveAt(k, direction) then
          result <- c.Value.Current 
          true
        else false
      finally
        exitLockIf c entered

    member internal this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      let entered = enterLockIf c true
      try
        if c.Value.MoveFirst() then
          res <- c.Value.Current
          true
        else false
      finally
        exitLockIf c entered

    member internal this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) =
      let entered = enterLockIf c true
      try
        if c.Value.MoveLast() then
          res <- c.Value.Current
          true
        else false
      finally
        exitLockIf c entered

    member internal this.TryGetValue(k, [<Out>] value:byref<'V>) =
      let entered = enterLockIf c true
      try
        if c.Value.IsContinuous then
          c.Value.TryGetValue(k, &value)
        else
          let ok = c.Value.MoveAt(k, Lookup.EQ)
          if ok then value <- c.Value.CurrentValue else value <- Unchecked.defaultof<'V>
          ok
      finally
        exitLockIf c entered

    member internal this.Item 
      with get k =
        let entered = enterLockIf c true
        try
          if c.Value.MoveAt(k, Lookup.EQ) then c.Value.CurrentValue
          else raise (KeyNotFoundException())
        finally
        exitLockIf c entered

    member internal this.Keys 
      with get() =
        // TODO manual impl, seq is slow
        use c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentKey
        }

    member internal this.Values
      with get() =
        // TODO manual impl, seq is slow
        use c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentValue
        }

    override x.Finalize() = if c.IsValueCreated then c.Value.Dispose()

    interface IEnumerable<KeyValuePair<'K, 'V>> with
      member this.GetEnumerator() = this.GetCursor() :> IEnumerator<KeyValuePair<'K, 'V>>
    interface System.Collections.IEnumerable with
      member this.GetEnumerator() = (this.GetCursor() :> System.Collections.IEnumerator)     

    interface IReadOnlyOrderedMap<'K,'V> with
      member this.GetCursor() = this.GetCursor()
      member this.Subscribe(observer : IObserver<KVP<'K,'V>>) = this.Subscribe(observer)
      member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
      member this.Comparer with get() = this.Comparer
      member this.IsIndexed with get() = this.IsIndexed
      member this.IsReadOnly =  this.IsReadOnly
      member this.SyncRoot with get() = this.SyncRoot

      member this.IsEmpty = this.IsEmpty
      member this.First with get() = this.First 
      member this.Last with get() = this.Last
      member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = this.TryFind(k, direction, &result)
      member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = this.TryGetFirst(&res)
      member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = this.TryGetLast(&res)
      member this.TryGetValue(k, [<Out>] value:byref<'V>) = this.TryGetValue(k, &value)
      member this.Item with get k = this.[k]
      member this.GetAt(idx:int) = this.Skip(Math.Max(0, idx-1)).First().Value
      member this.Keys with get() = this.Keys 
      member this.Values with get() = this.Values
          

//    static member inline private batchFunction (f:Func<'V,'V2>) (vF:Vector<'T> -> Vector<'T2>) : Func<IReadOnlyOrderedMap<'K,'V>,IReadOnlyOrderedMap<'K,'V2>> =
//      let f (source:IReadOnlyOrderedMap<'K,'V>) : IReadOnlyOrderedMap<'K,'V2> =
//        match source with
//        | :? IArrayBasedMap<'K,'V> as arrayMap -> failwith ""
//        | _ -> 
//          let map = new SortedMap<'K,'V2>()
//          for kvp in batch do
//              let newValue = mapF(kvp.Value)
//              map.AddLast(kvp.Key, newValue)
//          if map.size > 0 then
//            value <- map :> IReadOnlyOrderedMap<'K,'V2>
//            true
//          else false
//          null
//      if f <> null then Func<_,_>(f) else null

    // TODO! (perf) add batching where it makes sense
    // TODO! (perf) how to use batching with selector combinations?
    /// Used to implement scalar operators which are essentially a map application
    
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    static member inline private ScalarOperatorMap<'K,'V,'V2>(source:Series<'K,'V>, mapFunc:'V->'V2, fBatch:(ArraySegment<'V>->ArraySegment<'V2>) opt) = 
      let defaultMap() =
        CursorSeries(fun _ -> new BatchMapValuesCursor<'K,'V,'V2>(Func<_>(source.GetCursor), mapFunc, Unchecked.defaultof<_>) :> ICursor<_,_>) :> Series<'K,'V2>
      if OptimizationSettings.CombineFilterMapDelegates then
        match box source with
        | :? ICanMapSeriesValues<'K,'V> as s -> s.Map(mapFunc, fBatch)
        | _ ->  defaultMap()
      else defaultMap()
    
    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    static member inline private ScalarOperatorMap<'K,'V,'V2>(source:Series<'K,'V>, mapFunc:'V->'V2) = 
      Series.ScalarOperatorMap<'K,'V,'V2>(source, mapFunc, Missing)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    static member inline private ScalarOperatorMap<'K,'V,'V2>(source:Series<'K,'V>, mapFunc:'V->'V2, fBatch:(ArraySegment<'V>->ArraySegment<'V2>)) = 
      Series.ScalarOperatorMap<'K,'V,'V2>(source, mapFunc, Present(fBatch))

    // TODO! (perf) optimize ZipN for 2, or reimplement Zip for 'V/'V2->'R, see commented out cursor below
    static member inline private BinaryOperatorMap<'K,'V,'R>(source:Series<'K,'V>,other:Series<'K,'V>, mapFunc:Func<'V,'V,'R>) = 
      let cursorFactories:(unit->ICursor<'K,'V>)[] = [|source.GetCursor; other.GetCursor|]
      CursorSeries(Func<ICursor<'K,'R>>(fun _ -> (new ZipNCursor<'K,'V,'R>((fun _ varr -> mapFunc.Invoke(varr.[0], varr.[1])), cursorFactories) :> ICursor<'K,'R>) )) :> Series<'K,'R>

    static member inline private BinaryOperatorMap2<'K,'V1,'V2,'R>(source:Series<'K,'V1>,other:Series<'K,'V2>, mapFunc:Func<'V1,'V2,'R>) = 
      let c1, c2 = source.GetCursor, other.GetCursor
      CursorSeries(Func<ICursor<'K,'R>>(fun _ -> (new Zip2Cursor<'K,'V1,'V2,'R>(Func<ICursor<_,_>>(c1), Func<ICursor<_,_>>(c2), (fun k v1 v2 -> mapFunc.Invoke(v1, v2))) :> ICursor<'K,'R>) )) :> Series<'K,'R>


    // int64
    static member (+) (series:Series<'K,int64>, addition:int64) : Series<'K,int64> = 
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (+) (addition:int64, series:Series<'K,int64>) : Series<'K,int64> = 
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (~+) (series:Series<'K,int64>) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> x)
    static member (-) (series:Series<'K,int64>, subtraction:int64) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (-) (subtraction:int64, series:Series<'K,int64>) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (~-) (series:Series<'K,int64>) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> -x)
    static member (*) (series:Series<'K,int64>, multiplicator:int64) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> x * multiplicator)
    static member (*) (multiplicator:int64,series:Series<'K,int64>) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> multiplicator * x)
    static member (/) (series:Series<'K,int64>, divisor:int64) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> x / divisor)
    static member (/) (numerator:int64,series:Series<'K,int64>) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> numerator / x)
    static member (%) (series:Series<'K,int64>, divisor:int64) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> x % divisor)
    static member (%) (numerator:int64, series:Series<'K,int64>) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> numerator % x )
    static member ( ** ) (series:Series<'K,int64>, power:int64) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member ( ** ) (power:int64, series:Series<'K,int64>) : Series<'K,int64> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,int64>, other:int64) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x = other)
    static member (=) (other:int64, series:Series<'K,int64>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other = x )
    static member (>) (series:Series<'K,int64>, other:int64) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x > other)
    static member (>) (other:int64, series:Series<'K,int64>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other > x )
    static member (>=) (series:Series<'K,int64>, other:int64) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x >= other)
    static member (>=) (other:int64, series:Series<'K,int64>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other >= x )
    static member (<) (series:Series<'K,int64>, other:int64) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x < other)
    static member (<) (other:int64, series:Series<'K,int64>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other < x)
    static member (<=) (series:Series<'K,int64>, other:int64) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <= other)
    static member (<=) (other:int64, series:Series<'K,int64>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <= x)
    static member (<>) (series:Series<'K,int64>, other:int64) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <> other)
    static member (<>) (other:int64, series:Series<'K,int64>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <> x)

    static member (+) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(series, other, fun x y -> x + y)
    static member (-) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(series, other, fun x y -> x - y)
    static member (*) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(series, other, fun x y -> x * y)
    static member (/) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(series, other, fun x y -> x / y)
    static member (%) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(series, other, fun x y -> x % y)
    static member ( ** ) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(series, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x = y)
    static member (>) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x > y)
    static member (>=) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x >= y)
    static member (<) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x < y)
    static member (<=) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <= y)
    static member (<>) (series:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <> y)


    // int32
    static member (+) (series:Series<'K,int32>, addition:int32) : Series<'K,int32> = 
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (+) (addition:int32, series:Series<'K,int32>) : Series<'K,int32> = 
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (~+) (series:Series<'K,int32>) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> x)
    static member (-) (series:Series<'K,int32>, subtraction:int32) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (-) (subtraction:int32, series:Series<'K,int32>) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (~-) (series:Series<'K,int32>) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> -x)
    static member (*) (series:Series<'K,int32>, multiplicator:int32) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> x * multiplicator)
    static member (*) (multiplicator:int32,series:Series<'K,int32>) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> multiplicator * x)
    static member (/) (series:Series<'K,int32>, divisor:int32) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> x / divisor)
    static member (/) (numerator:int32,series:Series<'K,int32>) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> numerator / x)
    static member (%) (series:Series<'K,int32>, divisor:int32) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> x % divisor)
    static member (%) (numerator:int32, series:Series<'K,int32>) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> numerator % x )
    static member ( ** ) (series:Series<'K,int32>, power:int32) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member ( ** ) (power:int32, series:Series<'K,int32>) : Series<'K,int32> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,int32>, other:int32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x = other)
    static member (=) (other:int32, series:Series<'K,int32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other = x )
    static member (>) (series:Series<'K,int32>, other:int32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x > other)
    static member (>) (other:int32, series:Series<'K,int32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other > x )
    static member (>=) (series:Series<'K,int32>, other:int32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x >= other)
    static member (>=) (other:int32, series:Series<'K,int32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other >= x )
    static member (<) (series:Series<'K,int32>, other:int32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x < other)
    static member (<) (other:int32, series:Series<'K,int32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other < x)
    static member (<=) (series:Series<'K,int32>, other:int32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <= other)
    static member (<=) (other:int32, series:Series<'K,int32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <= x)
    static member (<>) (series:Series<'K,int32>, other:int32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <> other)
    static member (<>) (other:int32, series:Series<'K,int32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <> x)

    static member (+) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(series, other, fun x y -> x + y)
    static member (-) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(series, other, fun x y -> x - y)
    static member (*) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(series, other, fun x y -> x * y)
    static member (/) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(series, other, fun x y -> x / y)
    static member (%) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(series, other, fun x y -> x % y)
    static member ( ** ) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(series, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x = y)
    static member (>) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x > y)
    static member (>=) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x >= y)
    static member (<) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x < y)
    static member (<=) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <= y)
    static member (<>) (series:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <> y)

    // float
    static member (+) (series:Series<'K,float>, addition:float) : Series<'K,float> = 
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (+) (addition:float, series:Series<'K,float>) : Series<'K,float> = 
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (~+) (series:Series<'K,float>) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> x)
    static member (-) (series:Series<'K,float>, subtraction:float) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (-) (subtraction:float, series:Series<'K,float>) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (~-) (series:Series<'K,float>) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> -x)
    static member (*) (series:Series<'K,float>, multiplicator:float) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> x * multiplicator)
    static member (*) (multiplicator:float,series:Series<'K,float>) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> multiplicator * x)
    static member (/) (series:Series<'K,float>, divisor:float) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> x / divisor)
    static member (/) (numerator:float,series:Series<'K,float>) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> numerator / x)
    static member (%) (series:Series<'K,float>, divisor:float) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> x % divisor)
    static member (%) (numerator:float, series:Series<'K,float>) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> numerator % x )
    static member ( ** ) (series:Series<'K,float>, power:float) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member ( ** ) (power:float, series:Series<'K,float>) : Series<'K,float> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,float>, other:float) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x = other)
    static member (=) (other:float, series:Series<'K,float>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other = x )
    static member (>) (series:Series<'K,float>, other:float) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x > other)
    static member (>) (other:float, series:Series<'K,float>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other > x )
    static member (>=) (series:Series<'K,float>, other:float) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x >= other)
    static member (>=) (other:float, series:Series<'K,float>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other >= x )
    static member (<) (series:Series<'K,float>, other:float) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x < other)
    static member (<) (other:float, series:Series<'K,float>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other < x)
    static member (<=) (series:Series<'K,float>, other:float) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <= other)
    static member (<=) (other:float, series:Series<'K,float>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <= x)
    static member (<>) (series:Series<'K,float>, other:float) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <> other)
    static member (<>) (other:float, series:Series<'K,float>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <> x)

    static member (+) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(series, other, fun x y -> x + y)
    static member (-) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(series, other, fun x y -> x - y)
    static member (*) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(series, other, fun x y -> x * y)
    static member (/) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(series, other, fun x y -> x / y)
    static member (%) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(series, other, fun x y -> x % y)
    static member ( ** ) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(series, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x = y)
    static member (>) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x > y)
    static member (>=) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x >= y)
    static member (<) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x < y)
    static member (<=) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <= y)
    static member (<>) (series:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <> y)


    // float32
    static member (+) (series:Series<'K,float32>, addition:float32) : Series<'K,float32> =
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (+) (addition:float32, series:Series<'K,float32>) : Series<'K,float32> =
      Series.ScalarOperatorMap(series, ScalarMap.addValue(addition), ScalarMap.addSegment(addition))
    static member (~+) (series:Series<'K,float32>) : Series<'K,float32> = series
      //Series.ScalarOperatorMap(series, fun x -> x)
    static member (-) (series:Series<'K,float32>, subtraction:float32) : Series<'K,float32> = 
      Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (-) (subtraction:float32, series:Series<'K,float32>) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (~-) (series:Series<'K,float32>) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> -x)
    static member (*) (series:Series<'K,float32>, multiplicator:float32) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> x * multiplicator)
    static member (*) (multiplicator:float32,series:Series<'K,float32>) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> multiplicator * x)
    static member (/) (series:Series<'K,float32>, divisor:float32) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> x / divisor)
    static member (/) (numerator:float32,series:Series<'K,float32>) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> numerator / x)
    static member (%) (series:Series<'K,float32>, divisor:float32) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> x % divisor)
    static member (%) (numerator:float32, series:Series<'K,float32>) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> numerator % x )
    static member ( ** ) (series:Series<'K,float32>, power:float32) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member ( ** ) (power:float32, series:Series<'K,float32>) : Series<'K,float32> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,float32>, other:float32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x = other)
    static member (=) (other:float32, series:Series<'K,float32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other = x )
    static member (>) (series:Series<'K,float32>, other:float32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x > other)
    static member (>) (other:float32, series:Series<'K,float32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other > x )
    static member (>=) (series:Series<'K,float32>, other:float32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x >= other)
    static member (>=) (other:float32, series:Series<'K,float32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other >= x )
    static member (<) (series:Series<'K,float32>, other:float32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x < other)
    static member (<) (other:float32, series:Series<'K,float32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other < x)
    static member (<=) (series:Series<'K,float32>, other:float32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <= other)
    static member (<=) (other:float32, series:Series<'K,float32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <= x)
    static member (<>) (series:Series<'K,float32>, other:float32) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <> other)
    static member (<>) (other:float32, series:Series<'K,float32>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <> x)

    static member (+) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(series, other, fun x y -> x + y)
    static member (-) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(series, other, fun x y -> x - y)
    static member (*) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(series, other, fun x y -> x * y)
    static member (/) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(series, other, fun x y -> x / y)
    static member (%) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(series, other, fun x y -> x % y)
    static member ( ** ) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(series, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x = y)
    static member (>) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x > y)
    static member (>=) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x >= y)
    static member (<) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x < y)
    static member (<=) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <= y)
    static member (<>) (series:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <> y)


    // decimal
    static member (+) (series:Series<'K,decimal>, addition:decimal) : Series<'K,decimal> = 
      Series.ScalarOperatorMap(series, fun x -> x + addition)
    static member (+) (addition:decimal, series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> x + addition)
    static member (~+) (series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> x)
    static member (-) (series:Series<'K,decimal>, subtraction:decimal) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (-) (subtraction:decimal, series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> x - subtraction)
    static member (~-) (series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> -x)
    static member (*) (series:Series<'K,decimal>, multiplicator:decimal) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> x * multiplicator)
    static member (*) (multiplicator:decimal,series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> multiplicator * x)
    static member (/) (series:Series<'K,decimal>, divisor:decimal) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> x / divisor)
    static member (/) (numerator:decimal,series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> numerator / x)
    static member (%) (series:Series<'K,decimal>, divisor:decimal) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> x % divisor)
    static member (%) (numerator:decimal, series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> numerator % x )
    static member ( ** ) (series:Series<'K,decimal>, power:decimal) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member ( ** ) (power:decimal, series:Series<'K,decimal>) : Series<'K,decimal> = 
       Series.ScalarOperatorMap(series, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,decimal>, other:decimal) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x = other)
    static member (=) (other:decimal, series:Series<'K,decimal>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other = x )
    static member (>) (series:Series<'K,decimal>, other:decimal) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x > other)
    static member (>) (other:decimal, series:Series<'K,decimal>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other > x )
    static member (>=) (series:Series<'K,decimal>, other:decimal) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x >= other)
    static member (>=) (other:decimal, series:Series<'K,decimal>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other >= x )
    static member (<) (series:Series<'K,decimal>, other:decimal) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x < other)
    static member (<) (other:decimal, series:Series<'K,decimal>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other < x)
    static member (<=) (series:Series<'K,decimal>, other:decimal) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <= other)
    static member (<=) (other:decimal, series:Series<'K,decimal>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <= x)
    static member (<>) (series:Series<'K,decimal>, other:decimal) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> x <> other)
    static member (<>) (other:decimal, series:Series<'K,decimal>) : Series<'K,bool> = 
       Series.ScalarOperatorMap(series, fun x -> other <> x)

    static member (+) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,decimal> = Series.BinaryOperatorMap(series, other, fun x y -> x + y)
    static member (-) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,decimal> = Series.BinaryOperatorMap(series, other, fun x y -> x - y)
    static member (*) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,decimal> = Series.BinaryOperatorMap(series, other, fun x y -> x * y)
    static member (/) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,decimal> = Series.BinaryOperatorMap(series, other, fun x y -> x / y)
    static member (%) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,decimal> = Series.BinaryOperatorMap(series, other, fun x y -> x % y)
    static member ( ** ) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,decimal> = Series.BinaryOperatorMap(series, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x = y)
    static member (>) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x > y)
    static member (>=) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x >= y)
    static member (<) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x < y)
    static member (<=) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <= y)
    static member (<>) (series:Series<'K,decimal>, other:Series<'K,decimal>) : Series<'K,bool> = Series.BinaryOperatorMap(series, other, fun x y -> x <> y)


    // generic
    // TODO (low) dynamic operators via Linq.Expressions, then Panels will work via series
    static member (+) (source:Series<'K,'V1>, other:Series<'K,'V2>) : Series<'K,'V3> = Series.BinaryOperatorMap2(source, other, fun x y -> Op<'V1,'V2,'V3>.Add(x,y))
    static member (+) (source:Series<'K,'V1>, other:Series<'K,'V2>) : Series<'K,'V1> = Series.BinaryOperatorMap2(source, other, fun x y -> Op<'V1,'V2,'V1>.Add(x,y))

    // TODO (high) add all math operators, e.g. Abs, Log, Exp, etc.
    // TODO other primitive numeric types
    

and
  // TODO (perf) base Series() implements IROOM inefficiently, see comments in above type Series() implementation
  /// Wraps Series over ICursor
  [<AllowNullLiteral>]
  [<SealedAttribute>]
//  [<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
  CursorSeries<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>) =
    inherit Series<'K,'V>()
    // we use cursor to implement Obsrvable, but cursor used it for MNA
    // need to remove indirection and make cursors observable as well
    let mutable observableTask = Unchecked.defaultof<_>

    override this.GetCursor() = cursorFactory.Invoke()

    interface ICanMapSeriesValues<'K,'V> with
      member this.Map<'V2>(f2, fBatch): Series<'K,'V2> = 
        let cursor = cursorFactory.Invoke()
        match cursor with
        | :? ICanMapSeriesValues<'K,'V> as mappable -> mappable.Map(f2, fBatch)
        | _ -> 
          CursorSeries(fun _ ->
                // NB #11 we had (fun _ -> cursor) as factory, but that was a closure over cursor instance
                // with the current design, we cannot reuse the cursor allocated to check its interface implementation
                // we must dispose it and provide factory here
                cursor.Dispose()
                new BatchMapValuesCursor<_,_,_>(cursorFactory, f2, Missing) :> ICursor<_,_>
              ) :> Series<_,_>


and
  // NB! Remember that cursors are single-threaded
  // TODO make it a struct
  /// Map values to new values, batch mapping if that makes sense (for simple operations additional logic overhead is usually bigger than)
  [<SealedAttribute>]
  internal BatchMapValuesCursor<'K,'V,'V2> internal(cursorFactory:Func<ICursor<'K,'V>>, f:('V->'V2), fBatch:(ArraySegment<'V>->ArraySegment<'V2>) opt) =
    let mutable cursor : ICursor<'K,'V> =  cursorFactory.Invoke()
    
    // for forward-only enumeration this could be faster with native math
    // any non-forward move makes this false and we fall back to single items
    let mutable preferBatches = fBatch.IsPresent <> Unchecked.defaultof<_>
    let mutable batchStarted = false
    let mutable batch = Unchecked.defaultof<ISeries<'K,'V2>>
    let mutable batchCursor = Unchecked.defaultof<ICursor<'K,'V2>>
    let queue = if preferBatches then Queue<Task<_>>(Environment.ProcessorCount + 1) else Unchecked.defaultof<_>

    new(cursorFactory:Func<ICursor<'K,'V>>, f:('V->'V2)) = 
      new BatchMapValuesCursor<'K,'V,'V2>(cursorFactory, f, Missing)

    new(cursorFactory:Func<ICursor<'K,'V>>, f:Func<'V,'V2>) =  new BatchMapValuesCursor<'K,'V,'V2>(cursorFactory, f.Invoke, Missing)

    member this.CurrentKey: 'K = 
      if batchStarted then batchCursor.CurrentKey
      else cursor.CurrentKey
    member this.CurrentValue: 'V2 = 
      if batchStarted then batchCursor.CurrentValue
      else f(cursor.CurrentValue)
    member this.Current: KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member private this.MapBatch(batch:ISeries<'K,'V>) : Series<'K,'V2> =
      match batch with
      | :? ICanMapSeriesValues<'K,'V> as mappable -> mappable.Map(f, fBatch)
      | _ ->
        let factory = Func<_>(batch.GetCursor)
        let c() = new BatchMapValuesCursor<'K,'V,'V2>(factory, f, fBatch) :> ICursor<'K,'V2>
        CursorSeries(Func<_>(c)) :> Series<'K,'V2>

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member private this.StartNextBatch() = 
        Task.Run(fun _ ->
            cursor.MoveNextBatch(CancellationToken.None)
              .ContinueWith( (fun (antecedant2 : Task<bool>) -> 
                  if antecedant2.Result then
                    // save reference to the batch before moving to next
                    // NB! this assumes that cursor.CurrentBatch is not mutated in place but is replaced
                    // TODO check if this assumption is valid
                    let currentBatch = cursor.CurrentBatch
                    // NB need to have slightly more than Environment.ProcessorCount, 
                    // so that finished tasks could yield a core to queued ones (here +1 due to `<=` and not `<`)
                    // but, need to limit the count if we stopped consuming, to limit memory consumption
                    if queue.Count <= Environment.ProcessorCount  then
                      let newTask = this.StartNextBatch()
                      Trace.Assert(newTask <> Unchecked.defaultof<_>)
                      lock queue (fun _ -> 
                        queue.Enqueue(newTask)
                      )
                    Some(this.MapBatch(currentBatch))
                  else
                    None
                ),
                CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default).Result
        )

    member this.CurrentBatch with get() : ISeries<'K,'V2> = batch

    member this.MoveNext(): bool = 
      if not preferBatches then cursor.MoveNext()
      else
        if batchStarted && batchCursor.MoveNext() then // the hot path with just 2 comparisons 
          true
        else
          preferBatches <- this.MoveNextBatch(CancellationToken.None).Result // TODO check if this or cursor, hangs here!
          if preferBatches then
            batchCursor <- this.CurrentBatch.GetCursor() // NB setting batch variable here via this.CurrentBatch
            batchStarted <- batchCursor.MoveNext()
            batchStarted
          else batchStarted <- false; cursor.MoveNext()

    member this.MoveNext(cancellationToken: Threading.CancellationToken) =
      // not just cursor.MoveNext(cancellationToken), because this.MoveNext() will set flags
      // when batching is over 
      if this.MoveNext() then trueTask
      else cursor.MoveNext(cancellationToken)

    member private this.ClearBatches() =
      preferBatches <- false
      batchStarted <- false
      if batchCursor <> Unchecked.defaultof<ICursor<'K,'V2>> then batchCursor.Dispose()
      batch <- Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>>
      batchCursor <- Unchecked.defaultof<ICursor<'K,'V2>>
      queue.Clear()

    member this.MoveAt(index: 'K, direction: Lookup): bool =
      if preferBatches then this.ClearBatches()
      cursor.MoveAt(index, direction)

    member this.MoveFirst() = 
      if preferBatches then this.ClearBatches()
      cursor.MoveFirst()

    member this.MoveLast() = 
      if preferBatches then this.ClearBatches()
      cursor.MoveLast()

    member this.MovePrevious() =
      if preferBatches then 
        if cursor.Comparer.Compare(cursor.CurrentKey, this.CurrentKey) <> 0 && not <| cursor.MoveAt(this.CurrentKey, Lookup.EQ) then invalidOp "Cannot move cursor "
        this.ClearBatches()
      cursor.MovePrevious()

    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> =
      if queue.Count = 0 then // there is no outstanding move
        this.StartNextBatch().ContinueWith(
            (fun (antecedant : Task<Series<'K,'V2> option>) ->
                if antecedant.Result.IsSome then 
                  batch <- antecedant.Result.Value
                  true
                else false
            ),
          cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default)
      else
        let mutable taskHolder = Unchecked.defaultof<_>
        lock queue (fun _ -> 
          taskHolder <- queue.Dequeue()
        )

        Trace.Assert(taskHolder <> Unchecked.defaultof<_>)
        taskHolder.ContinueWith(
          (fun (antecedant : Task<Series<'K,'V2> option>) ->
              if antecedant.Result.IsSome then 
                batch <- antecedant.Result.Value
                true
              else false
          ),
          cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default)


    member this.Source: ISeries<'K,'V2> = 
        let factory = Func<_>(cursor.Source.GetCursor)
        let c() = new BatchMapValuesCursor<'K,'V,'V2>(cursorFactory, f, fBatch) :> ICursor<'K,'V2>
        CursorSeries(Func<_>(c)) :> ISeries<'K,'V2>

    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =
      let mutable v = Unchecked.defaultof<_>
      let ok = cursor.TryGetValue(key, &v)
      if ok then value <- f(v)
      ok
    
    member this.Reset() = 
      preferBatches <- fBatch.IsPresent
      batchStarted <- false
      batch <- Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>>
      if batchCursor <> Unchecked.defaultof<ICursor<'K,'V2>> then 
        batchCursor.Dispose()
        batchCursor <- Unchecked.defaultof<ICursor<'K,'V2>>
      if preferBatches then queue.Clear()
      cursor.Reset()

    member this.Clone() = 
      new BatchMapValuesCursor<'K,'V,'V2>(Func<_>(cursor.Clone), f, fBatch)

    interface IEnumerator<KVP<'K,'V2>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V2> = this.Current
      member this.Current with get(): obj = this.Current :> obj 
      member this.Dispose(): unit = 
        this.Reset(); 
        cursor.Dispose()

    interface ICursor<'K,'V2> with
      member this.Comparer with get() = cursor.Comparer
      member this.CurrentBatch: ISeries<'K,'V2> = this.CurrentBatch
      member this.CurrentKey: 'K = this.CurrentKey
      member this.CurrentValue: 'V2 = this.CurrentValue
      member this.IsContinuous: bool = cursor.IsContinuous
      member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index, direction) 
      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()
      member this.MovePrevious(): bool = this.MovePrevious()
    
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNext(cancellationToken)
      member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNextBatch(cancellationToken)
    
      //member this.IsBatch with get() = this.IsBatch
      member this.Source: ISeries<'K,'V2> = this.Source
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  this.TryGetValue(key, &value)
      member this.Clone() = this.Clone() :> ICursor<'K,'V2>
    
    interface ICanMapSeriesValues<'K,'V2> with
      member this.Map<'V3>(f2, fBatch2): Series<'K,'V3> = 
        let func = f >> f2
        let batchFunc =
          if fBatch.IsPresent && fBatch2.IsPresent then
            Present(fBatch.Present >> fBatch2.Present)
          else Missing
        CursorSeries(fun _ -> new BatchMapValuesCursor<'K,'V,'V3>(cursorFactory, func, batchFunc) :> ICursor<_,_>) :> Series<'K,'V3>


// TODO! (perf) see text in the Obsolete attribute below. We must rewrite this and test with random inputs as in ZipN. This is a hot path and optimizing this is one of the priorities. However, ZipN is not that slow and we should implement other TODO!s first.
//and 
//  /// A cursor that joins to cursors. 
//  [<AbstractClassAttribute>]
////  [<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
//  [<ObsoleteAttribute("TODO (perf) This is incorrect implementation, use slower (5 mops vs 10 mops) ZipN and then optimize ZipN for 2 or rewrite this")>]
//  ZipCursor<'K,'V1,'V2,'R>(cursorFactoryL:unit->ICursor<'K,'V1>, cursorFactoryR:unit->ICursor<'K,'V2>) =
//  
//    let cursorL = cursorFactoryL()
//    let cursorR = cursorFactoryR()
//    let cmp = 
//      if cursorL.Comparer.Equals(cursorR.Comparer) then cursorL.Comparer
//      else
//        // TODO if cursors are not equal, then fallback on inner join via exact lookup
//        // E.g. (3 7 2 8).Zip(1.Repeat(), (+)) = (4 8 3 9) - makes perfect sense
//        // and/or check if source is indexed
//        invalidOp "Left and right comparers are not equal"
//
//    // if continuous, do not move but treat as if cursor is positioned at other (will move next and the next step)
//    // TryGetValue must have optimizations for forward enumeration e.g. for Repeat - keep previous and move one step ahead
//    // if not continuous, then move after k
//    let moveOrGetNextAtK (cursor:ICursor<'K,'V>) (k:'K) : 'V opt =
//      if cursor.IsContinuous then 
//        let mutable v = Unchecked.defaultof<_>
//        let ok = cursor.TryGetValue(k, &v)
//        if ok then OptionalValue(v)
//        else
//          // weird case when continuous has holes
//          let mutable found = false
//          while not found && cursor.MoveNext() do
//            let c = cmp.Compare(cursor.CurrentKey, k)
//            if c >= 0 then found <- true
//          None
//      else
//        let mutable found = false
//        let mutable ok = false
//        while not found && cursor.MoveNext() do
//          let c = cmp.Compare(cursor.CurrentKey, k)
//          if c > 0 then found <- true
//          if c = 0 then 
//            found <- true
//            ok <- true
//        if ok then
//          OptionalValue(cursor.CurrentValue)
//        else None
//
//    let moveOrGetPrevAtK (cursor:ICursor<'K,'V>) (k:'K) : 'V opt =
//      if cursor.IsContinuous then 
//        let ok, v = cursor.TryGetValue(k)
//        if ok then OptionalValue(v)
//        else
//          // weird case when continuous has holes
//          let mutable found = false
//          while not found && cursor.MovePrevious() do
//            let c = cmp.Compare(cursor.CurrentKey, k)
//            if c >= 0 then found <- true
//          None
//      else
//        let mutable found = false
//        let mutable ok = false
//        while not found && cursor.MovePrevious() do
//          let c = cmp.Compare(cursor.CurrentKey, k)
//          if c > 0 then found <- true
//          if c = 0 then 
//            found <- true
//            ok <- true
//        if ok then
//          OptionalValue(cursor.CurrentValue)
//        else None
//    
//
//    let mutable hasValidState = false
//
//    member this.Comparer with get() = cmp
//    member this.HasValidState with get() = hasValidState and set (v) = hasValidState <- v
//
//    member val IsContinuous = cursorL.IsContinuous && cursorR.IsContinuous with get, set
//
//    /// Source series
//    member this.InputCursorL with get() : ICursor<'K,'V1> = cursorL
//    member this.InputCursorR with get() : ICursor<'K,'V2> = cursorR
//
//    member val CurrentKey = Unchecked.defaultof<'K> with get, set
//    member val CurrentValue = Unchecked.defaultof<'R> with get, set
//    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)
//
//    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
//    member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>> with get, set
//
//    /// Update state with a new value. Should be optimized for incremental update of the current state in custom implementations.
//    abstract TryZip: key:'K * v1:'V1 * v2:'V2 * [<Out>] value: byref<'R> -> bool
////      raise (NotImplementedException("must implement in derived cursor"))
//
//    /// If input and this cursor support batches, then process a batch and store it in CurrentBatch
//    abstract TryZipNextBatches: nextBatchL: IReadOnlyOrderedMap<'K,'V1> * nextBatchR: IReadOnlyOrderedMap<'K,'V2> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'R>> -> bool  
//    override this.TryZipNextBatches(nextBatchL: IReadOnlyOrderedMap<'K,'V1>, nextBatchR: IReadOnlyOrderedMap<'K,'V2>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'R>>) : bool =
//      // should work only when keys are equal
//      false
//
//    member this.Reset() = 
//      hasValidState <- false
//      cursorL.Reset()
//    member this.Dispose() = 
//      hasValidState <- false
//      cursorL.Dispose()
//    
//    abstract Clone: unit -> ICursor<'K,'R>
//    override x.Clone(): ICursor<'K,'R> =
//      // run-time type of the instance, could be derived type
//      let ty = x.GetType()
//      let args = [|cursorFactoryL :> obj;cursorFactoryR :> obj|]
//      // TODO using Activator is a very bad sign, are we doing something wrong here?
//      let clone = Activator.CreateInstance(ty, args) :?> ICursor<'K,'R> // should not be called too often
//      if hasValidState then clone.MoveAt(x.CurrentKey, Lookup.EQ) |> ignore
//      //Trace.Assert(movedOk) // if current key is set then we could move to it
//      clone
//
//    // https://en.wikipedia.org/wiki/Sort-merge_join
//    member this.MoveNext(skipMove:bool): bool =
//      let mutable skipMove = skipMove
//      // TODO movefirst must try batch. if we are calling MoveNext and the input is in the batch state, then iterate over the batch
//
//      // MoveNext is called after previous MoveNext or when MoveFirst() cannot get value after moving both cursors to first positions
//      // we must make a step: if positions are equal, step both and check if they are equal
//      // if positions are not equal, then try to get continuous values or move the laggard forward and repeat the checks
//
//      // there are three possible STARTING STATES before MN
//      // 1. cl.k = cr.k - on the previous move cursors ended up at equal key, but these keys are already consumed.
//      //    must move both keys
//      //    other cases are possible if one or both cursors are continuous
//      // 2. cl.k < cr.k, cl should took cr's value at cl.k, implies that cr IsCont
//      //    must move cl since previous (before this move) cl.k is already consumed
//      // 3. reverse of 2
//      // MN MUST leave cursors in one of these states as well
//
//      let cl = this.InputCursorL
//      let cr = this.InputCursorR
//
//      let mutable ret = false // could have a situation when should stop but the state is valid, e.g. for async move next
//      let mutable shouldStop = false
//
//      let mutable c = cmp.Compare(cl.CurrentKey, cr.CurrentKey)
//      let mutable move = c // - 1 - move left, 0 - move both, 1 - move right
//      let mutable shouldMoveL = if skipMove then false else c <= 0 // left is equal or behind
//      let mutable shouldMoveR = if skipMove then false else c >= 0 // right is equal or behind
//
//      while not shouldStop do // Check that all paths set shouldStop to true
//        //if shouldMove = false, lmoved & rmoved = true without moves via short-circuit eval of ||
//        let lmoved, rmoved = 
//          if skipMove then
//            skipMove <- false
//            true, true
//          else
//            shouldMoveL && cl.MoveNext(), shouldMoveR && cr.MoveNext()
//        c <- cmp.Compare(cl.CurrentKey, cr.CurrentKey) 
//        match lmoved, rmoved with
//        | false, false ->
//          Trace.Assert(shouldMoveL || shouldMoveR)
//          shouldStop <- true // definetly cannot move forward in any way
//          //this.HasValidState <- // leave it as it was, e.g. move first was ok, but no new values. async could pick up later
//          ret <- false
//        | false, true ->
//          Trace.Assert(shouldMoveR)
//          if c = 0 then
//            let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, cr.CurrentValue)
//            if valid then
//              shouldStop <- true
//              ret <- true
//              this.CurrentKey <- cl.CurrentKey
//              this.CurrentValue <- v
//              this.HasValidState <- true
//          else
//            // the new key is defined by R
//            // regardless of the relative position, try get value from cl if it IsCont
//            if cl.IsContinuous then
//              let ok, lv = cl.TryGetValue(cr.CurrentKey)
//              if ok then
//                let valid, v = this.TryZip(cr.CurrentKey, lv, cr.CurrentValue)
//                if valid then
//                  shouldStop <- true
//                  ret <- true
//                  this.CurrentKey <- cr.CurrentKey
//                  this.CurrentValue <- v
//                  this.HasValidState <- true
//          if not shouldStop then
//            // if shouldMoveL was true but we couldn't, it is over - move only right
//            // else move it if it is <= r
//            shouldMoveL <- if shouldMoveL then false else c <= 0
//            shouldMoveR <- if shouldMoveL then true else c >= 0
//        | true, false ->
//          Trace.Assert(shouldMoveL)
//          if c = 0 then
//            let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, cr.CurrentValue)
//            if valid then
//              shouldStop <- true
//              ret <- true
//              this.CurrentKey <- cl.CurrentKey
//              this.CurrentValue <- v
//              this.HasValidState <- true
//          else
//            // the new key is defined by L
//            // regardless of the relative position, try get value from cr if it IsCont
//            if cr.IsContinuous then
//              let ok, rv = cr.TryGetValue(cl.CurrentKey)
//              if ok then
//                let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, rv)
//                if valid then
//                  shouldStop <- true
//                  ret <- true
//                  this.CurrentKey <- cl.CurrentKey
//                  this.CurrentValue <- v
//                  this.HasValidState <- true
//          if not shouldStop then
//            shouldMoveL <- if shouldMoveR then true else c <= 0
//            shouldMoveR <- if shouldMoveR then false else c >= 0
//        | true, true -> //
//          // now we have potentially valid positioning and should try to get values from it
//          // new c after moves, c will remain if at the updated positions we cannot get result. save this comparison
//          //c <- cmp.Compare(cl.CurrentKey, cr.CurrentKey)
//          match c with
//          | l_vs_r when l_vs_r = 0 -> // new keys are equal, try get result from them and iterate if cannot do so
//            let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, cr.CurrentValue)
//            if valid then
//              shouldStop <- true
//              ret <- true
//              this.CurrentKey <- cl.CurrentKey
//              this.CurrentValue <- v
//              this.HasValidState <- true
//            if not shouldStop then // keys were equal and consumed, move both next
//              shouldMoveL <- true
//              shouldMoveR <- true
//          | l_vs_r when l_vs_r > 0 ->
//            // cl is ahead, cr must check if cl IsCont and try to get cl's value at cr's key
//            // smaller key defines current position
//            if cl.IsContinuous then
//              let ok, lv = cl.TryGetValue(cr.CurrentKey) // trying to get a value before cl's current key if cl IsCont
//              if ok then
//                let valid, v = this.TryZip(cr.CurrentKey, lv, cr.CurrentValue)
//                if valid then
//                  shouldStop <- true
//                  ret <- true
//                  this.CurrentKey <- cr.CurrentKey
//                  this.CurrentValue <- v
//                  this.HasValidState <- true
//            // cr will move on next iteration, cl must stay
//            if not shouldStop then
//              shouldMoveL <- false
//              shouldMoveR <- true
//          | _ -> //l_vs_r when l_vs_r < 0
//            // flip side of the second case
//            if cr.IsContinuous then
//              let ok, rv = cr.TryGetValue(cl.CurrentKey) // trying to get a value before cl's current key if cl IsCont
//              if ok then
//                let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, rv)
//                if valid then
//                  shouldStop <- true
//                  ret <- true
//                  this.CurrentKey <- cl.CurrentKey
//                  this.CurrentValue <- v
//                  this.HasValidState <- true
//            // cl will move on next iteration, cr must stay
//            if not shouldStop then
//              shouldMoveL <- true
//              shouldMoveR <- false
//      ret
//
//   
//    interface IEnumerator<KVP<'K,'R>> with    
//      member this.Reset() = this.Reset()
//      member this.MoveNext(): bool = 
//        if this.HasValidState then this.MoveNext(false)
//        else (this :> ICursor<'K,'R>).MoveFirst()
//
//      member this.Current with get(): KVP<'K, 'R> = this.Current
//      member this.Current with get(): obj = this.Current :> obj 
//      member x.Dispose(): unit = x.Dispose()
//
//    interface ICursor<'K,'R> with
//      member this.Comparer with get() = cmp
//      member x.Current: KVP<'K,'R> = KVP(x.CurrentKey, x.CurrentValue)
//      member x.CurrentBatch: IReadOnlyOrderedMap<'K,'R> = x.CurrentBatch
//      member x.CurrentKey: 'K = x.CurrentKey
//      member x.CurrentValue: 'R = x.CurrentValue
//      member x.IsContinuous: bool = x.IsContinuous
//      member x.MoveAt(index: 'K, direction: Lookup): bool = failwith "not implemented"
//
//      member this.MoveFirst(): bool =
//        let cl = this.InputCursorL
//        let cr = this.InputCursorR
//        if cl.MoveFirst() && cr.MoveFirst() then this.MoveNext(true)
//        else false
//    
//      member this.MoveLast(): bool = 
//        let cl = this.InputCursorL
//        let cr = this.InputCursorR
//        let mutable ok = false
//        let mutable k = Unchecked.defaultof<'K>
//        let mutable vl = Unchecked.defaultof<'V1>
//        let mutable vr = Unchecked.defaultof<'V2>
//
//        if cl.MoveLast() && cr.MoveLast() then
//          // minus, reverse direction
//          let c = -cmp.Compare(cl.CurrentKey, cr.CurrentKey)
//          match c with
//          | l_vs_r when l_vs_r = 0 ->
//            k <- cl.CurrentKey
//            vl <- cl.CurrentValue
//            vr <- cr.CurrentValue
//            ok <- true
//          | l_vs_r when l_vs_r > 0 -> // left is ahead
//            // need to get right's value at left's key
//            let vrOpt = moveOrGetPrevAtK cr cl.CurrentKey
//            if vrOpt.IsPresent then
//              k <- cl.CurrentKey
//              vl <- cl.CurrentValue
//              vr <- vrOpt.Present
//              ok <- true
//          | _ -> // right is ahead
//            // need to get left's value at right's key
//            let vrOpt = moveOrGetPrevAtK cl cr.CurrentKey
//            if vrOpt.IsPresent then
//              k <- cl.CurrentKey
//              vl <- vrOpt.Present
//              vr <- cr.CurrentValue
//              ok <- true
//          if ok then
//            let valid, v = this.TryZip(k, vl, vr)
//            if valid then
//              this.CurrentKey <- k
//              this.CurrentValue <- v
//              true
//            else false
//          else false
//        else false
//
//      member x.MovePrevious(): bool = failwith "not implemented"
////        let cl = x.InputCursorL
////        let cr = x.InputCursorR
////        if hasValidState then
////          let mutable found = false
////          while not found && x.InputCursorL.MovePrevious() do
////            let ok, value = x.TryUpdatePrevious(x.InputCursorL.Current)
////            if ok then 
////              found <- true
////              x.CurrentKey <- value.Key
////              x.CurrentValue <- value.Value
////          if found then 
////            hasValidState <- true
////            true 
////          else false
////        else (x :> ICursor<'K,'R>).MoveLast()
//    
//      member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
//        failwith "Not implemented yet"
//      member x.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
//        failwith "Not implemented yet"
//    
//      //member x.IsBatch with get() = x.IsBatch
//      member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((x :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>
//      member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
//        // this method must not move cursors or position of the current cursor
//        let cl = this.InputCursorL
//        let cr = this.InputCursorR
//        let ok1, v1 = cl.TryGetValue(key)
//        let ok2, v2 = cr.TryGetValue(key)
//        if ok1 && ok2 then
//          this.TryZip(key, v1, v2, &value)
//        else false
//    
//      member this.Clone() = this.Clone()

and
  private UnionKeysCursor<'K,'V>([<ParamArray>] cursors:ICursor<'K,'V>[]) =
    let cmp = 
      let c' = cursors.[0].Comparer
      for c in cursors do
        if not <| c.Comparer.Equals(c') then invalidOp "ZipNCursor: Comparers are not equal" 
      c'
    // TODO (perf) allocates for the lifetime of cursor
    let movedKeysFlags : bool[] = Array.zeroCreate cursors.Length
    let movedKeys = SortedDeque(ZipNComparer(cmp)) // , cursors.Length

    let mutable semaphore : SemaphoreSlim = Unchecked.defaultof<_> //  new SemaphoreSlim(0) //, cursors.Length
    // Live counter shows how many cont cursor not yet returned false on MoveNextAsync
    let mutable liveCounter = cursors.Length
    let mutable subscriptions : IDisposable[] = Unchecked.defaultof<_> 
    let mutable outOfOrderKeys : SortedDeque<'K> = Unchecked.defaultof<_>
    // Same meaning as in BingCursor: we have at least one sucessful move and some state for further moves
    let mutable hasValidState = false

    member this.Comparer with get() = cmp
    member this.HasValidState with get() = hasValidState

    member val IsContinuous = false

    member val CurrentKey = Unchecked.defaultof<'K> with get, set

    // NB lazy application of resultSelector, only when current value is requested
    member this.CurrentValue with get() = Unchecked.defaultof<'V>

    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
    member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V>> with get, set

    member this.Reset() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Reset()) |> ignore

    member this.Dispose() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Dispose()) |> ignore
      if semaphore <> Unchecked.defaultof<_> then semaphore.Dispose()
      if subscriptions <> Unchecked.defaultof<_> then
        for s in subscriptions do
          s.Dispose()

    // the smallest key if any moved first, or false
    member this.MoveFirst(): bool =
      let mutable moved = false
      Array.Clear(movedKeysFlags, 0, movedKeysFlags.Length)
      movedKeys.Clear()
      let mutable i = 0
      for c in cursors do
        let moved' = c.MoveFirst()
        if moved' then 
          movedKeysFlags.[i] <- true
          movedKeys.Add(KV(c.CurrentKey, i)) |> ignore
        moved <- moved || moved'
        i <- i + 1
      if moved then
        this.CurrentKey <- movedKeys.First.Key
        hasValidState <- true
        true
      else false

    // the smallest key if any moved last, or false
    member this.MoveLast(): bool =
      let mutable moved = false
      Array.Clear(movedKeysFlags, 0, movedKeysFlags.Length)
      movedKeys.Clear()
      let mutable i = 0
      for c in cursors do
        movedKeysFlags.[i] <- true
        let moved' = c.MoveFirst()
        if moved' then 
          movedKeysFlags.[i] <- true
          movedKeys.Add(KV(c.CurrentKey, i)) |> ignore
        moved <- moved || moved'
        i <- i + 1
      if moved then
        this.CurrentKey <- movedKeys.Last.Key
        hasValidState <- true
        true
      else false

    member this.MoveAt(key, direction): bool =
      let mutable moved = false
      Array.Clear(movedKeysFlags, 0, movedKeysFlags.Length)
      movedKeys.Clear()
      let mutable i = 0
      for c in cursors do
        let moved' = c.MoveAt(key, direction)
        if moved' then 
          movedKeysFlags.[i] <- true
          movedKeys.Add(KV(c.CurrentKey, i)) |> ignore
        moved <- moved || moved'
        i <- i + 1
      if moved then
        match direction with
        | Lookup.EQ ->
          #if PRERELEASE
          Trace.Assert(cmp.Compare(movedKeys.First.Key, movedKeys.Last.Key) = 0)
          Trace.Assert(cmp.Compare(key, movedKeys.Last.Key) = 0)
          #endif
          this.CurrentKey <- movedKeys.First.Key
        | Lookup.LE | Lookup.LT ->
          this.CurrentKey <- movedKeys.Last.Key
        | Lookup.GE | Lookup.GT ->
          this.CurrentKey <- movedKeys.First.Key
        | _ -> failwith "Wrong lookup direction, should never be there"
        true
      else false

    member this.MoveNext(): bool =
      if not this.HasValidState then this.MoveFirst()
      else
        // try to recover cursors that have not moved before
        if movedKeys.Count < cursors.Length then
          let mutable i = 0
          while i < movedKeysFlags.Length do
            if not movedKeysFlags.[i] then
              let c = cursors.[i]
              let moved' = c.MoveAt(this.CurrentKey, Lookup.GT)
              if moved' then 
                movedKeysFlags.[i] <- true
                movedKeys.Add(KV(c.CurrentKey, i)) |> ignore
            i <- i + 1

        // ignore cursors that cannot move ahead of frontier during this move, but do 
        // not remove them from movedKeys so that we try to move them again on the next move
        let mutable ignoreOffset = 0
        let mutable leftmostIsAheadOfFrontier = false
        // current key is frontier, we could call MN after MP, etc.
        while ignoreOffset < movedKeys.Count && not leftmostIsAheadOfFrontier do
          //leftmostIsAheadOfFrontier <- not cmp.Compare(movedKeys.First.Key, this.CurrentKey) <= 0
          let initialPosition = movedKeys.[ignoreOffset]
          let cursor = cursors.[initialPosition.Value]

          let mutable shouldMove = cmp.Compare(cursor.CurrentKey, this.CurrentKey) <= 0
          let mutable movedAtLeastOnce = false
          let mutable passedFrontier = not shouldMove
          // try move while could move and not passed the frontier
          while shouldMove do
            let moved = cursor.MoveNext()
            movedAtLeastOnce <- movedAtLeastOnce || moved
            passedFrontier <- cmp.Compare(cursor.CurrentKey, this.CurrentKey) > 0
            shouldMove <- moved && not passedFrontier
          
          if movedAtLeastOnce || passedFrontier then
            if movedAtLeastOnce then
              let newPosition = KV(cursor.CurrentKey, initialPosition.Value)
              // update positions if the current has changed, regardless of the frontier
              movedKeys.RemoveAt(ignoreOffset) |> ignore
              movedKeys.Add(newPosition)

            // here passedFrontier if for cursor that after remove/add is not at ignoreOffset idx
            if passedFrontier && cmp.Compare(movedKeys.[ignoreOffset].Key, this.CurrentKey) > 0 then
              leftmostIsAheadOfFrontier <- true
          else
            Trace.Assert(not passedFrontier, "If cursor hasn't moved, I couldn't pass the prontier")
            ignoreOffset <- ignoreOffset + 1
            ()
        // end of outer loop
        if leftmostIsAheadOfFrontier then
            this.CurrentKey <- movedKeys.[ignoreOffset].Key
            true
        else
            false

    // NB mirror of MN, do not change separately
    member this.MovePrevious(): bool =
      if not this.HasValidState then this.MoveLast()
      else
        if movedKeys.Count < cursors.Length then
          let mutable i = 0
          while i < movedKeysFlags.Length do
            if not movedKeysFlags.[i] then
              let c = cursors.[i]
              let moved' = c.MoveAt(this.CurrentKey, Lookup.LT)
              if moved' then 
                movedKeysFlags.[i] <- true
                movedKeys.Add(KV(c.CurrentKey, i)) |> ignore
            i <- i + 1

        let mutable ignoreOffset = movedKeys.Count - 1
        let mutable rightmostIsAheadOfFrontier = false
        while ignoreOffset >= 0 && not rightmostIsAheadOfFrontier do
          let initialPosition = movedKeys.[ignoreOffset]
          let cursor = cursors.[initialPosition.Value]

          let mutable shouldMove = cmp.Compare(cursor.CurrentKey, this.CurrentKey) >= 0
          let mutable movedAtLeastOnce = false
          let mutable passedFrontier = not shouldMove
          // try move while could move and not passed the frontier
          while shouldMove do
            let moved = cursor.MovePrevious()
            movedAtLeastOnce <- movedAtLeastOnce || moved
            passedFrontier <- cmp.Compare(cursor.CurrentKey, this.CurrentKey) < 0
            shouldMove <- moved && not passedFrontier
          
          if movedAtLeastOnce || passedFrontier then
            if movedAtLeastOnce then
              let newPosition = KV(cursor.CurrentKey, initialPosition.Value)
              // update positions if the current has changed, regardless of the frontier
              movedKeys.RemoveAt(ignoreOffset) |> ignore
              movedKeys.Add(newPosition)

            // here passedFrontier if for cursor that after remove/add is not at ignoreOffset idx
            if passedFrontier && cmp.Compare(movedKeys.[ignoreOffset].Key, this.CurrentKey) < 0 then
              rightmostIsAheadOfFrontier <- true
          else
            Trace.Assert(not passedFrontier, "If cursor hasn't moved, I couldn't pass the prontier")
            ignoreOffset <- ignoreOffset - 1
            ()
        // end of outer loop
        if rightmostIsAheadOfFrontier then
            this.CurrentKey <- movedKeys.[ignoreOffset].Key
            true
        else
            false

    
    member private this.MoveFirst(ct): Task<bool> =
      task {
        let mutable valuesOk = false
        movedKeys.Clear()
        //if not movedFirst then
        let rec moveCursor i = // NB for loop inside task{} is transformed into a computation expression method, avoid it
          let x = cursors.[i]
          let movedFirst' = x.MoveFirst()
          if movedFirst' then
            lock(movedKeys) (fun _ -> movedKeys.Add(KV(x.CurrentKey, i)) |> ignore )
            semaphore.Release() |> ignore
          else
            // MF returns false only when series is empty, then MNAsync is equivalent to MFAsync
            x.MoveNext(ct).ContinueWith(fun (t:Task<bool>) ->
              match t.Status with
              | TaskStatus.RanToCompletion -> 
                if t.Result then
                  lock(movedKeys) (fun _ -> movedKeys.Add(KV(x.CurrentKey, i)) |> ignore )
                  semaphore.Release() |> ignore
                else
                  let decremented = Interlocked.Decrement(&liveCounter)
                  if decremented = 0 then semaphore.Release() |> ignore
              | _ -> failwith "TODO remove task{} and process all task results"
            ) |> ignore
          if i + 1 < cursors.Length then moveCursor (i+1) else ()
        moveCursor 0
        // waith for at least one to move
        let! signal = semaphore.WaitAsync(-1, ct)
        if not signal || Interlocked.Add(&liveCounter, 0) = 0 then
          ct.ThrowIfCancellationRequested()
          valuesOk <- false
        else
          this.CurrentKey <- movedKeys.First.Key
          valuesOk <- true
        hasValidState <- valuesOk
        return valuesOk
      }

    member this.MoveNext(ct): Task<bool> =
      let mutable tcs = Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create() //new TaskCompletionSource<_>() //
      let returnTask = tcs.Task // NB! must access this property first
      let rec loop() =
        // we make null comparison even when outOfOrderKeys is empty, and this is a hot path
        // TODO add OOO keys counter or always allocate SD - but counter could take 4 bytes only, while SD is an object with 16+ bytes overhead
        if outOfOrderKeys <> Unchecked.defaultof<_> && outOfOrderKeys.Count > 0 then
          lock (outOfOrderKeys) (fun _ -> 
            this.CurrentKey <- outOfOrderKeys.RemoveFirst()
            tcs.SetResult(true)
          )
        elif this.MoveNext() then
          tcs.SetResult(true)
        elif Interlocked.Add(&liveCounter, 0) = 0 then
          tcs.SetResult(false)
        else
          if semaphore = Unchecked.defaultof<_> then
            semaphore <- new SemaphoreSlim(0)
            subscriptions <- Array.zeroCreate cursors.Length
            let mutable i = 0
            for c in cursors do
                let ii = i
                let cc = c.Clone()
                let sourceObserver = { new IObserver<KVP<'K,'V>> with
                    member x.OnNext(kvp) = 
                      // We must compare a key to the current key and if
                      // kvp.Key is LE that the current one, we should store it
                      // in an out-of-order deque. Then we should check OOO deque and 
                      // consume it. Because OOO deque grows only when new key if LE 
                      // the current one, it is bounded by construction.
                      // Union key then could return repeated keys and OOO keys,
                      // and it is ZipNs responsibility to handle these cases
                      if cmp.Compare(kvp.Key, this.CurrentKey) <= 0 then
                        if outOfOrderKeys = Unchecked.defaultof<_> then outOfOrderKeys <- SortedDeque(cmp)
                        lock (outOfOrderKeys) (fun _ -> 
                          // TODO check this, add perf counters for frequence and max size
                          //if not <| outOfOrderKeys.TryAdd(kvp.Key) then
                          //  Console.WriteLine(kvp.Key.ToString() + " - " + kvp.Value.ToString() )
                          outOfOrderKeys.TryAdd(kvp.Key) |> ignore
                        )
                      semaphore.Release() |> ignore
                    member x.OnCompleted() =
                      let decremented = Interlocked.Decrement(&liveCounter)
                      if decremented = 0 then semaphore.Release() |> ignore
                    member x.OnError(exn) = ()
                }
                subscriptions.[i] <- c.Source.Subscribe(sourceObserver)
                i <- i + 1
          let semaphorePeek = semaphore.Wait(0)
          if semaphorePeek then
            // TODO check for live count here
            loop()
          else
            // initial count was zero, could return here only after at least one cursor moved
            let semaphoreTask = semaphore.WaitAsync(50, ct) // TODO return back -1
            let awaiter = semaphoreTask.GetAwaiter()
            awaiter.OnCompleted(fun _ -> 
              match semaphoreTask.Status with
              | TaskStatus.RanToCompletion -> 
                let signal = semaphoreTask.Result
                if Interlocked.Add(&liveCounter, 0) = 0 then
                  ct.ThrowIfCancellationRequested()
                  tcs.SetResult(false)
                else
                  loop()
              | _ -> failwith "TODO process all task results"
              ()
            )
      loop()
      returnTask

    interface IEnumerator<KVP<'K,'V>> with
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K,'V> = KVP(this.CurrentKey, this.CurrentValue)
      member this.Current with get(): obj = this.Current :> obj 
      member this.Dispose(): unit = this.Dispose()

    interface ICursor<'K,'V> with
      member this.Comparer with get() = cmp
      member this.CurrentBatch: ISeries<'K,'V> = Unchecked.defaultof<ISeries<'K,'V>>
      member this.CurrentKey: 'K = this.CurrentKey
      member this.CurrentValue: 'V = this.CurrentValue
      member this.IsContinuous: bool = this.IsContinuous
      member this.MoveAt(key: 'K, direction: Lookup) : bool = this.MoveAt(key, direction)
      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()
      member this.MovePrevious(): bool = this.MovePrevious()
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNext(cancellationToken)
      member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        falseTask
      member this.Source: ISeries<'K,'V> = CursorSeries<'K,'V>(Func<ICursor<'K,'V>>((this :> ICursor<'K,'V>).Clone)) :> ISeries<'K,'V>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'V>): bool =
        raise (NotSupportedException("UnionKeysCursor should be used only as a pivot inside continuous ZipN"))
      member this.Clone() = 
        raise (NotSupportedException("UnionKeysCursor should be used only as a pivot inside continuous ZipN"))


/// Repack original types into value tuples. Due to the lazyness this only happens for a current value of cursor. ZipN keeps vArr instance and
/// rewrites its values. For value types we will always be in L1/stack, for reference types we do not care that much about performance.
and 
  Zip2Cursor<'K,'V,'V2,'R>(cursorFactoryL:Func<ICursor<'K,'V>>,cursorFactoryR:Func<ICursor<'K,'V2>>, mapF:Func<'K,'V,'V2,'R>) =
    inherit ZipNCursor<'K,ValueTuple<'V,'V2>,'R>(
      Func<'K, ValueTuple<'V,'V2>[],'R>(fun (k:'K) (tArr:ValueTuple<'V,'V2>[]) -> mapF.Invoke(k, tArr.[0].Value1, tArr.[1].Value2)), 
      (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryL, (fun (x:'V) -> ValueTuple<'V,'V2>(x, Unchecked.defaultof<'V2>)), Missing) :> ICursor<'K,ValueTuple<'V,'V2>>), 
      (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryR, (fun (x:'V2) -> ValueTuple<'V,'V2>(Unchecked.defaultof<'V>, x)), Missing) :> ICursor<'K,ValueTuple<'V,'V2>>)
    )


and
  ZipNCursor<'K,'V,'R>(resultSelector:Func<'K,'V[],'R>, [<ParamArray>] cursorFactories:(unit->ICursor<'K,'V>)[]) as this =
    do
      if cursorFactories.Length < 2 then invalidArg "cursorFactories" "ZipN takes at least two cursor factories"
    let cursorsFactory() = cursorFactories |> Array.map (fun x -> x())
    let mutable cursors = cursorsFactory()

    // Current values of all cursors. We keep them in an array because for continuous cursors there is no current value,
    // they just return TryGetValue at a key. Also applying a resultSelector function to an array is fast.
    let currentValues = Array.zeroCreate cursors.Length // OptimizationSettings.ArrayPool.Take<'V>(cursors.Length)
    
    let cmp = 
      let c' = cursors.[0].Comparer
      for c in cursors do
        if not <| c.Comparer.Equals(c') then invalidOp "ZipNCursor: Comparers are not equal" 
      c'

    // Same meaning as in BingCursor: we have at least one sucessful move and some state for further moves
    let mutable hasValidState = false
    // all keys where discrete cursors are positioned. their intersect define where resulting keys are present.
    let discreteKeysSet = SortedDeque(ZipNComparer(cmp))

    let isContinuous = cursors |> Array.map (fun x -> x.IsContinuous) |> Array.forall id
    let unionKeys : ICursor<'K,'V> = if isContinuous then new UnionKeysCursor<'K,'V>(cursors) :> ICursor<'K,'V> else Unchecked.defaultof<_>
    
    
    /// TODO(perf) Now using TryGetValue without moving cursors. The idea is that continuous series are usually less frequent than
    /// the pivot ones, e.g. daily vs. minutely/secondly data, so the "depth" of binary search is not too big
    /// However, due to the same fact, one single MoveNext on daily data could cover many pivot points
    /// Continuous cursors should be optimized for the cases when the key in `.TryGetValue(key)` is between
    /// the current and the previous position of the continuous cursor
    let fillContinuousValuesAtKey (key:'K) =
        // we must try to get values from all continuous series, regardless if they are empty or not
        let mutable cont = true
        let mutable c = 0
        while cont && c < cursors.Length do
          if cursors.[c].IsContinuous then
            let mutable v = Unchecked.defaultof<_>
            let ok = cursors.[c].TryGetValue(key, &v)
            if ok then currentValues.[c] <- v
            else cont <- false // cannot get value
          c <- c + 1
        cont

    // return true only if all discrete cursors moved to the same key or they cannot move further
    let rec doMoveNext() =
      let mutable continueMoves = true
      // check if we reached the state where all cursors are at the same position
      while cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) < 0 && continueMoves do
        // pivotKeysSet is essentially a task queue:
        // we take every cursor that is not at the frontier and try to move it forward until it reaches the frontier
        // if we do this in parallel, the frontier could be moving while we move cursors
        let first = discreteKeysSet.RemoveFirst()
        let ac = cursors.[first.Value]
        let mutable moved = true
        let mutable c = -1 // by construction // cmp.Compare(ac.CurrentKey, pivotKeysSet.Max.Key)
        
        // move active cursor forward while it is before the current max key
        // max key of non-cont series is the frontier: we will never get a value before it,
        // and if any pivot moves ahead of the frontier, then it shifts the frontier 
        // and the old one becomes unreachable
        while c < 0 && moved do
          moved <- ac.MoveNext()
          if moved then c <- cmp.Compare(ac.CurrentKey, discreteKeysSet.Last.Key)

        if not moved then continueMoves <- false
        // must add it back regardless of moves
        // TODO (perf) should benefit here from RemoveFirstAddLast method, becuase is moved = true, 
        // we add the last value by construction
        discreteKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore

      // now all discrete cursors have moved at or ahead of frontier
      // the loop could stop only when all cursors are at the same key or we cannot move ahead
      if continueMoves then
        // this only possible if all discrete cursors are at the same key
        #if PRERELEASE
        Trace.Assert(cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0)
        #endif
        if fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
          if not isContinuous then
            // now we could access values of discrete keys and fill current values with them
            for kvp in discreteKeysSet do // TODO (perf) Check if F# compiler behaves like C# one, optimizing for structs enumerator. Or just benchmark compared with for loop
              currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
          this.CurrentKey <- discreteKeysSet.First.Key
          true
        else
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position

          let first = discreteKeysSet.RemoveFirst()
          let firstCursor = if isContinuous then unionKeys else cursors.[first.Value]
          if firstCursor.MoveNext() then
            discreteKeysSet.Add(KV(firstCursor.CurrentKey, first.Value)) |> ignore
            doMoveNext() // recursive
          else
            // add back, should not be very often TODO (perf, low) add counter to see if this happens often
            discreteKeysSet.Add(KV(firstCursor.CurrentKey, first.Value)) |> ignore
            false
      else false
    
    // a copy of doMoveNextDiscrete() with changed direction. 
    let rec doMovePrevious() =
      let mutable continueMoves = true
      while cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) < 0 && continueMoves do
        let last = discreteKeysSet.RemoveLast()
        let ac = cursors.[last.Value]
        let mutable moved = true
        let mutable c = +1
        
        // move active cursor forward while it is before the current max key
        // max key of non-cont series is the frontier: we will never get a value before it,
        // and if any pivot moves ahead of the frontier, then it shifts the frontier 
        // and the old one becomes unreachable
        while c > 0 && moved do
          moved <- ac.MovePrevious()
          if moved then c <- cmp.Compare(ac.CurrentKey, discreteKeysSet.First.Key)

        if not moved then continueMoves <- false
        // must add it back regardless of moves
        discreteKeysSet.Add(KV(ac.CurrentKey, last.Value)) |> ignore

      // now all discrete cursors have moved at or ahead of frontier
      // the loop could stop only when all cursors are at the same key or we cannot move ahead
      if continueMoves then
        // this only possible if all discrete cursors are at the same key
        #if PRERELEASE
        Trace.Assert(cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0)
        #endif
        if fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
          if not isContinuous then
            // now we could access values of discrete keys and fill current values with them
            for kvp in discreteKeysSet do // TODO (perf) Check if F# compiler behaves like C# one, optimizing for structs enumerator. Or just benchmark compared with for loop
              currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
          this.CurrentKey <- discreteKeysSet.Last.Key
          true
        else
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position

          let last = discreteKeysSet.RemoveLast()
          let lastCursor = if isContinuous then unionKeys else cursors.[last.Value]
          if lastCursor.MovePrevious() then
            discreteKeysSet.Add(KV(lastCursor.CurrentKey, last.Value)) |> ignore
            doMovePrevious() // recursive
          else
            // add back, should not be very often TODO (perf, low) add counter to see if this happens often
            discreteKeysSet.Add(KV(lastCursor.CurrentKey, last.Value)) |> ignore
            false
      else false

    // Manual state machine instead of a task computation expression, this is visibly faster
    let doMoveNextTask(ct:CancellationToken) : Task<bool> =
      #if PRERELEASE
      //Trace.Assert(this.HasValidState)
      #endif
      let mutable tcs = Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create() //new TaskCompletionSource<_>() //
      let returnTask = tcs.Task // NB! must access this property first
      let mutable firstStep = ref true
      let mutable sourceMoveTask = Unchecked.defaultof<_>
      let mutable initialPosition = Unchecked.defaultof<_>
      let mutable ac : ICursor<'K,'V> = Unchecked.defaultof<_>
      let rec loop(isOuter:bool) : unit =
        if isOuter then
          if not !firstStep 
            && 
              (
              (isContinuous && cmp.Compare(ac.CurrentKey, this.CurrentKey) > 0)
              || 
              (not isContinuous && cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0)
              )
            && fillContinuousValuesAtKey(ac.CurrentKey) then
            this.CurrentKey <- ac.CurrentKey
            if not isContinuous then
              // we set values only here, when we know that we could return
              for kvp in discreteKeysSet do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            tcs.SetResult(true) // the only true exit
          else
            if discreteKeysSet.Count = 0 then invalidOp "discreteKeysSet is empty"
            initialPosition <- discreteKeysSet.RemoveFirst()
            ac <- if isContinuous then unionKeys else cursors.[initialPosition.Value]
            loop(false)
        else
          firstStep := false
          let idx = initialPosition.Value
          let cursor = ac
          let inline onMoved() =
            discreteKeysSet.Add(KV(cursor.CurrentKey, idx)) |> ignore
            loop(true)
          let mutable reachedFrontier = false
          while not reachedFrontier && cursor.MoveNext() do
            if isContinuous then
              reachedFrontier <- 
                if hasValidState then cmp.Compare(cursor.CurrentKey, this.CurrentKey) > 0
                else true
            else
              reachedFrontier <- cmp.Compare(cursor.CurrentKey, this.CurrentKey) >= 0
          if reachedFrontier then
            onMoved()
          else
            // call itself until reached the frontier, then call outer loop
            sourceMoveTask <- cursor.MoveNext(ct)
            // there is a big chance that this task is already completed
            let inline onCompleted() =
              let moved =  sourceMoveTask.Result
              if not moved then
                tcs.SetResult(false) // the only false exit
                ()
              else
                if isContinuous then
                  let c = cmp.Compare(cursor.CurrentKey, this.CurrentKey)
                  if c > 0 then onMoved()
                  else
                    if hasValidState then
                      loop(false)
                    else
                      discreteKeysSet.Add(initialPosition)
                      loop(true)
                else
                  let c = cmp.Compare(cursor.CurrentKey, this.CurrentKey)
                  #if PRERELEASE
                  Trace.Assert(c > 0)
                  #endif
//                  if c < 0 then
//                    discreteKeysSet.Add(initialPosition)
//                    loop(false)
//                  else
                  onMoved()

            let awaiter = sourceMoveTask.GetAwaiter()
            // NB! do not block, use a callback
            awaiter.OnCompleted(fun _ ->
              // TODO! Test all cases
              if sourceMoveTask.Status = TaskStatus.RanToCompletion then
                onCompleted()
              else
                discreteKeysSet.Add(initialPosition) // TODO! Add/remove only when needed
                if sourceMoveTask.Status = TaskStatus.Canceled then
                  tcs.SetException(OperationCanceledException())
                else
                  tcs.SetException(sourceMoveTask.Exception)
            )
      loop(true)
      returnTask

    
    member this.Comparer with get() = cmp
    member this.HasValidState with get() = hasValidState

    member val IsContinuous = isContinuous

    member val CurrentKey = Unchecked.defaultof<'K> with get, set

    // NB lazy application of resultSelector, only when current value is requested
    member this.CurrentValue with get() = 
      // TODO lazy 
      resultSelector.Invoke(this.CurrentKey, currentValues)

    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
    member val CurrentBatch = Unchecked.defaultof<ISeries<'K,'R>> with get, set

    member this.Reset() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Reset()) |> ignore

    member this.Dispose() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Dispose()) |> ignore
      //OptimizationSettings.ArrayPool.Return(currentValues) |> ignore
    
    member this.Clone(): ICursor<'K,'R> =
      // run-time type of the instance, could be derived type
      let clone = new ZipNCursor<'K,'V,'R>(resultSelector, cursorFactories) :> ICursor<'K,'R>
      if hasValidState then 
        // TODO!!! There is a bug inside MoveAt()
        let movedOk = clone.MoveAt(this.CurrentKey, Lookup.EQ)
        Trace.Assert(movedOk) // if current key is set then we could move to it
      clone
         

    member this.MoveFirst(): bool =
      let mutable doContinue = true
      let mutable valuesOk = false
      let mutable movedFirst = false
      discreteKeysSet.Clear()
      while doContinue do
        if not movedFirst then
          if isContinuous then
            movedFirst <- unionKeys.MoveFirst()
            if movedFirst then discreteKeysSet.Add(KV(unionKeys.CurrentKey, 0)) |> ignore
            doContinue <- movedFirst
          else
            let mutable i = 0
            for x in cursors do
              if not cursors.[i].IsContinuous then 
                let movedFirst' = x.MoveFirst()
                if movedFirst' then
                  discreteKeysSet.Add(KV(x.CurrentKey, i)) |> ignore 
                else doContinue <- false
              i <- i + 1
            movedFirst <- doContinue
        else
          // all cursors are positioned so that it is possible to get value, but not guaranteed
          // if we are lucky and have equal keys right after MoveFirst of each cursors
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
            if not isContinuous then
              for kvp in discreteKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            this.CurrentKey <- discreteKeysSet.First.Key
            valuesOk <- true
            doContinue <- false 
          else
            // move to max key until min key matches max key so that we can use values
            valuesOk <- doMoveNext()
            doContinue <- valuesOk
      hasValidState <- valuesOk
      valuesOk

    member this.MoveLast(): bool = 
      let mutable doContinue = true
      let mutable valuesOk = false
      let mutable movedLast = false
      discreteKeysSet.Clear()
      while doContinue do
        if not movedLast then
          if isContinuous then
            movedLast <- unionKeys.MoveLast()
            if movedLast then discreteKeysSet.Add(KV(unionKeys.CurrentKey, 0)) |> ignore
            doContinue <- movedLast
          else
            let mutable i = 0
            for x in cursors do
              if not x.IsContinuous then 
                let movedFirst' = x.MoveLast()
                if movedFirst' then
                  let kv = KV(x.CurrentKey, i)
                  discreteKeysSet.Add(kv) |> ignore 
                else doContinue <- false
              i <- i + 1
            movedLast <- doContinue
        else
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
            if not isContinuous then
              for kvp in discreteKeysSet do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            this.CurrentKey <- discreteKeysSet.Last.Key
            valuesOk <- true
            doContinue <- false 
          else
            valuesOk <- doMovePrevious()
            doContinue <- valuesOk
      hasValidState <- valuesOk
      valuesOk

    member x.MoveAt(key: 'K, direction: Lookup) : bool =
      let mutable doContinue = true
      let mutable valuesOk = false
      let mutable movedAt = false
      discreteKeysSet.Clear()
      while doContinue do
        if not movedAt then
          if isContinuous then
            movedAt <- unionKeys.MoveAt(key, direction)
            if movedAt then discreteKeysSet.Add(KV(unionKeys.CurrentKey, 0)) |> ignore
            doContinue <- movedAt
          else
            let mutable i = 0
            for x in cursors do
              if not cursors.[i].IsContinuous then 
                let movedAt' = x.MoveAt(key, direction)
                if movedAt' then
                  discreteKeysSet.Add(KV(x.CurrentKey, i)) |> ignore 
                else doContinue <- false
              i <- i + 1
            movedAt <- doContinue
        else
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
            if not isContinuous then
              for kvp in discreteKeysSet do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            this.CurrentKey <- discreteKeysSet.First.Key
            valuesOk <- true
            doContinue <- false 
          else
            match direction with
            | Lookup.EQ -> 
              valuesOk <- false
              doContinue <- false
            | Lookup.LE | Lookup.LT ->
              valuesOk <- doMovePrevious()
            | Lookup.GE | Lookup.GT ->
              valuesOk <- doMoveNext()
            | _ -> failwith "Wrong lookup direction, should never be there"
      hasValidState <- valuesOk
      valuesOk

    member this.MoveNext(): bool =
      if not this.HasValidState then this.MoveFirst()
      else
        let doContinue =
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 then
            let first = discreteKeysSet.RemoveFirst()
            let ac = if isContinuous then unionKeys else cursors.[first.Value]
            if ac.MoveNext() then
              discreteKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
              true
            else
              discreteKeysSet.Add(first)
              false
          else true
        if doContinue then doMoveNext()
        else false
            
    member this.MovePrevious(): bool = 
      if not this.HasValidState then this.MoveLast()
      else
        let cont =
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 then
            let last = discreteKeysSet.RemoveLast()
            let ac = if isContinuous then unionKeys else cursors.[last.Value]
            if ac.MovePrevious() then
              discreteKeysSet.Add(KV(ac.CurrentKey, last.Value)) |> ignore
              true
            else 
              discreteKeysSet.Add(last)
              false
          else true
        if cont then doMovePrevious()
        else false


    member private this.MoveFirst(ct): Task<bool> =
      task {
        let mutable doContinue = true
        let mutable valuesOk = false
        let mutable movedFirst = false
        discreteKeysSet.Clear()
        while doContinue do
          if not movedFirst then
            if isContinuous then
              movedFirst <- unionKeys.MoveFirst()
              if not movedFirst then
                let! movedAsync = unionKeys.MoveNext(ct)
                movedFirst <- movedAsync
              if movedFirst then
                discreteKeysSet.Add(KV(unionKeys.CurrentKey, 0)) |> ignore
              doContinue <- movedFirst
            else
              let mutable i = 0
              for x in cursors do
                if not cursors.[i].IsContinuous then 
                  let mutable movedFirst' = x.MoveFirst()
                  if not movedFirst' then 
                    let! movedAsync = x.MoveNext(ct)
                    movedFirst' <- movedAsync
                  if movedFirst' then
                    discreteKeysSet.Add(KV(x.CurrentKey, i)) |> ignore 
                  else doContinue <- false
                i <- i + 1
              movedFirst <- doContinue
          else
            // all cursors are positioned so that it is possible to get value, but not guaranteed
            // if we are lucky and have equal keys right after MoveFirst of each cursors
            if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
              if not isContinuous then
                for kvp in discreteKeysSet.AsEnumerable() do
                  currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- discreteKeysSet.First.Key
              valuesOk <- true
              doContinue <- false
            else
              // move to max key until min key matches max key so that we can use values
              let! movedNext = doMoveNextTask(ct)
              valuesOk <- movedNext
              doContinue <- valuesOk
        hasValidState <- valuesOk
        return valuesOk
      }


    member this.MoveNext(ct:CancellationToken): Task<bool> =
      if this.HasValidState then doMoveNextTask(ct)
      else this.MoveFirst(ct)


    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
      let mutable cont = true
      let values = 
        cursors 
        // NB cursors are single-threaded, all inner cursors were created by this thread
        // if we pass them to a thread pool, the current thread should not touch them until
        // all TGV return. So parallel is probably safe here, but for simple cursors
        // switching costs could be higher. We could benchmark after which N parallel if better
        // for simplest cursors, and later return to the idea of storing internal complexity/depth of 
        // cursors in metadata, e.g. in ConditionalWeakTable (this definitely shouldn't be a part
        // of ICursor, but we could make an internal interface and check if a cursor implements it)
        // CWT is an interesting thing and I want to try using it for metadata of objects, R-like style.
        |> Array.map (fun x ->
          let mutable v = Unchecked.defaultof<_>
          let ok = x.TryGetValue(key, &v)
          if ok then v
          else
            cont <- false
            Unchecked.defaultof<'V>
        )
      if cont then
        value <- resultSelector.Invoke(key, values)
        true
      else false
        
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = falseTask
    
    //member this.IsBatch with get() = this.IsBatch
    member this.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((this :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>

    interface IEnumerator<KVP<'K,'R>> with
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'R> = KVP(this.CurrentKey, this.CurrentValue)
      member this.Current with get(): obj = this.Current :> obj 
      member this.Dispose(): unit = this.Dispose()

    // TODO (perf, low) move implementations directly to interface, that will save one callvirt. That trick has already improved perf in other places, e.g. SortedDeque.

    interface ICursor<'K,'R> with
      member this.Comparer with get() = cmp
      member this.CurrentBatch: ISeries<'K,'R> = this.CurrentBatch
      member this.CurrentKey: 'K = this.CurrentKey
      member this.CurrentValue: 'R = this.CurrentValue
      member this.IsContinuous: bool = this.IsContinuous
      member this.MoveAt(key: 'K, direction: Lookup) : bool = this.MoveAt(key, direction)
      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()
      member this.MovePrevious(): bool = this.MovePrevious()
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNext(cancellationToken)
      member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNextBatch(cancellationToken)
      member this.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((this :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
        this.TryGetValue(key, &value)
      member this.Clone() = this.Clone()
