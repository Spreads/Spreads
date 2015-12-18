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

#nowarn "0086" // operators overloads are intential, Series are as primitive as scalars, all arithmetic operations are defined on them as maps
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

open Spreads
open Spreads.Collections


// TODO (perf) see benchmark for ReadOnly. Reads are very slow while iterations are not affected (GetCursor() returns original cursor) in release mode. Optimize 
// reads of this wrapper either here by type-checking the source of the cursor and using direct methods on the source
// or make cursor thread-static and initialize it only once (now it is called on each method)

// TODO duplicate IReadOnlyOrderedMap methods as an instance virtual methods to avoid casting in F#. That will require ovverrides in all children or conflict
// check how it is used from C# (do tests in C# in general)

// TODO check thread safety of the default series implementation. Should we use ThreadLocal for caching cursors that are called via the interfaces?


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
  abstract member Map: mapFunc:Func<'V,'V2> -> Series<'K,'V2>

and
  [<AllowNullLiteral>]
  [<Serializable>]
  Series internal() =
    // NB this is ugly, but rewriting the whole project structure is uglier // TODO "proper" methods DI
    static do
      let moduleInfo = 
        Reflection.Assembly.GetExecutingAssembly().GetTypes()
        |> Seq.find (fun t -> t.Name = "Initializer")
      //let ty = typeof<BaseSeries>
      let mi = moduleInfo.GetMethod("init", (Reflection.BindingFlags.Static ||| Reflection.BindingFlags.NonPublic) )
      mi.Invoke(null, [||]) |> ignore

and
  [<AllowNullLiteral>]
  [<Serializable>]
  [<AbstractClassAttribute>]
  //[<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
  Series<'K,'V>() =
    inherit Series()
    let mutable syncRoot = Unchecked.defaultof<_> // avoid allocation on each series creation, many of them are lighweight and never need a sync root
    
    /// Main method to override
    abstract GetCursor : unit -> ICursor<'K,'V>

    // TODO! check
    member this.IsIndexed with get() = this.GetCursor().Source.IsIndexed
    member this.IsMutable =  this.GetCursor().Source.IsMutable

    /// Locks any mutations for mutable implementations
    member this.SyncRoot 
      with get() = 
        if syncRoot = Unchecked.defaultof<_> then syncRoot <- Object()
        syncRoot

    member this.Comparer with get() = this.GetCursor().Comparer
    member this.IsEmpty = not (this.GetCursor().MoveFirst())
    //member this.Count with get() = map.Count
    member this.First 
      with get() = 
        let c = this.GetCursor()
        if c.MoveFirst() then c.Current else invalidOp "Series is empty"

    member this.Last 
      with get() =
        let c = this.GetCursor()
        if c.MoveLast() then c.Current else invalidOp "Series is empty"

    member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      let c = this.GetCursor()
      if c.MoveAt(k, direction) then 
        result <- c.Current 
        true
      else false

    member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      let c = this.GetCursor()
      if c.MoveFirst() then
        res <- c.Current
        true
      else false

    member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      let c = this.GetCursor()
      if c.MoveLast() then
        res <- c.Current
        true
      else false

    member this.TryGetValue(k, [<Out>] value:byref<'V>) = 
      let c = this.GetCursor()
      if c.IsContinuous then
        c.TryGetValue(k, &value)
      else
        let v = ref Unchecked.defaultof<KVP<'K,'V>>
        let ok = c.MoveAt(k, Lookup.EQ)
        if ok then value <- c.CurrentValue else value <- Unchecked.defaultof<'V>
        ok

    member this.Item 
      with get k = 
        let c = this.GetCursor()
        if c.MoveAt(k, Lookup.EQ) then c.CurrentValue
        else raise (KeyNotFoundException())

    member this.Keys 
      with get() =
        let c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentKey
        }

    member this.Values
      with get() =
        let c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentValue
        }

    interface IEnumerable<KeyValuePair<'K, 'V>> with
      member this.GetEnumerator() = this.GetCursor() :> IEnumerator<KeyValuePair<'K, 'V>>
    interface System.Collections.IEnumerable with
      member this.GetEnumerator() = (this.GetCursor() :> System.Collections.IEnumerator)     

    interface IReadOnlyOrderedMap<'K,'V> with
      member this.GetCursor() = this.GetCursor()
      member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
      member this.Comparer with get() = this.Comparer
      member this.IsIndexed with get() = this.IsIndexed
      member this.IsMutable =  this.IsMutable
      member this.SyncRoot with get() = this.SyncRoot

      member this.IsEmpty = this.IsEmpty
      //member this.Count with get() = map.Count
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
          

    // TODO! (perf) add batching where it makes sense
    // TODO! (perf) how to use batching with selector combinations?
    /// Used to implement scalar operators which are essentially a map application
    static member inline private ScalarOperatorMap<'K,'V,'V2>(source:Series<'K,'V>, mapFunc:Func<'V,'V2>, ?fBatch:Func<IReadOnlyOrderedMap<'K,'V>,IReadOnlyOrderedMap<'K,'V2>>) = 
      let defaultMap() =
        let mapF = mapFunc
        let fBatch =
          if fBatch.IsSome then OptionalValue(fBatch.Value) 
          else 
            if OptimizationSettings.AlwaysBatch then
              let fBatch' b =
                let ok, v = VectorMathProvider.Default.MapBatch(mapFunc.Invoke, b)
                v
              OptionalValue(Func<_,_>(fBatch'))
            else OptionalValue.Missing
        new BatchMapValuesCursor<'K,'V,'V2>(Func<_>(source.GetCursor), mapF, fBatch) :> Series<'K,'V2>
#if PRERELEASE // we could switch off this optimization in prerelease builds
      if OptimizationSettings.CombineFilterMapDelegates then
        match box source with
        | :? ICanMapSeriesValues<'K,'V> as s -> s.Map(mapFunc)
        | _ ->  defaultMap()
      else defaultMap()
#else
      defaultMap()
#endif


    // TODO! (perf) optimize ZipN for 2, or reimplement Zip for 'V/'V2->'R, see commented out cursor below
    static member inline private BinaryOperatorMap<'K,'V,'R>(source:Series<'K,'V>,other:Series<'K,'V>, mapFunc:Func<'V,'V,'R>) = 
      let cursorFactories:(unit->ICursor<'K,'V>)[] = [|source.GetCursor; other.GetCursor|]
      CursorSeries(Func<ICursor<'K,'R>>(fun _ -> (new ZipNCursor<'K,'V,'R>((fun _ varr -> mapFunc.Invoke(varr.[0], varr.[1])), cursorFactories) :> ICursor<'K,'R>) )) :> Series<'K,'R>

    

    // int64
    static member (+) (source:Series<'K,int64>, addition:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x + addition)
    static member (~+) (source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x)
    static member (-) (source:Series<'K,int64>, subtraction:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x - subtraction)
    static member (~-) (source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> -x)
    static member (*) (source:Series<'K,int64>, multiplicator:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x * multiplicator)
    static member (*) (multiplicator:int64,source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> multiplicator * x)
    static member (/) (source:Series<'K,int64>, divisor:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x / divisor)
    static member (/) (numerator:int64,source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> numerator / x)
    static member (%) (source:Series<'K,int64>, modulo:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x % modulo)
    static member ( ** ) (source:Series<'K,int64>, power:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,int64>, other:int64) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x = other)
    static member (>) (source:Series<'K,int64>, other:int64) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x > other)
    static member (>=) (source:Series<'K,int64>, other:int64) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x >= other)
    static member (<) (other:int64, source:Series<'K,int64>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other < x)
    static member (<=) (other:int64, source:Series<'K,int64>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <= x)
    static member (<>) (other:int64, source:Series<'K,int64>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <> x)

    static member (+) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(source, other, fun x y -> x + y)
    static member (-) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(source, other, fun x y -> x - y)
    static member (*) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(source, other, fun x y -> x * y)
    static member (/) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(source, other, fun x y -> x / y)
    static member (%) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(source, other, fun x y -> x % y)
    static member ( ** ) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,int64> = Series.BinaryOperatorMap(source, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x = y)
    static member (>) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x > y)
    static member (>=) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x >= y)
    static member (<) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x < y)
    static member (<=) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <= y)
    static member (<>) (source:Series<'K,int64>, other:Series<'K,int64>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <> y)


    // int32
    static member (+) (source:Series<'K,int>, addition:int) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> x + addition)
    static member (~+) (source:Series<'K,int>) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> x)
    static member (-) (source:Series<'K,int>, subtraction:int) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> x - subtraction)
    static member (~-) (source:Series<'K,int>) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> -x)
    static member (*) (source:Series<'K,int>, multiplicator:int) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> x * multiplicator)
    static member (*) (multiplicator:int,source:Series<'K,int>) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> multiplicator * x)
    static member (/) (source:Series<'K,int>, divisor:int) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> x / divisor)
    static member (/) (numerator:int,source:Series<'K,int>) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> numerator / x)
    static member (%) (source:Series<'K,int>, modulo:int) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> x % modulo)
    static member ( ** ) (source:Series<'K,int>, power:int) : Series<'K,int> = Series.ScalarOperatorMap(source, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,int>, other:int) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x = other)
    static member (>) (source:Series<'K,int>, other:int) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x > other)
    static member (>=) (source:Series<'K,int>, other:int) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x >= other)
    static member (<) (other:int, source:Series<'K,int>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other < x)
    static member (<=) (other:int, source:Series<'K,int>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <= x)
    static member (<>) (other:int, source:Series<'K,int>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <> x)

    static member (+) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(source, other, fun x y -> x + y)
    static member (-) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(source, other, fun x y -> x - y)
    static member (*) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(source, other, fun x y -> x * y)
    static member (/) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(source, other, fun x y -> x / y)
    static member (%) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(source, other, fun x y -> x % y)
    static member ( ** ) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,int> = Series.BinaryOperatorMap(source, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x = y)
    static member (>) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x > y)
    static member (>=) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x >= y)
    static member (<) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x < y)
    static member (<=) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <= y)
    static member (<>) (source:Series<'K,int>, other:Series<'K,int>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <> y)

    // float
    static member (+) (source:Series<'K,float>, addition:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x + addition)
    static member (~+) (source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x)
    static member (-) (source:Series<'K,float>, subtraction:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x - subtraction)
    static member (~-) (source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> -x)
    static member (*) (source:Series<'K,float>, multiplicator:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x * multiplicator)
    static member (*) (multiplicator:float,source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> multiplicator * x)
    static member (/) (source:Series<'K,float>, divisor:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x / divisor)
    static member (/) (numerator:float,source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> numerator / x)
    static member (%) (source:Series<'K,float>, modulo:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x % modulo)
    static member ( ** ) (source:Series<'K,float>, power:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,float>, other:float) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x = other)
    static member (>) (source:Series<'K,float>, other:float) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x > other)
    static member (>=) (source:Series<'K,float>, other:float) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x >= other)
    static member (<) (other:float, source:Series<'K,float>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other < x)
    static member (<=) (other:float, source:Series<'K,float>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <= x)
    static member (<>) (other:float, source:Series<'K,float>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <> x)

    static member (+) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(source, other, fun x y -> x + y)
    static member (-) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(source, other, fun x y -> x - y)
    static member (*) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(source, other, fun x y -> x * y)
    static member (/) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(source, other, fun x y -> x / y)
    static member (%) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(source, other, fun x y -> x % y)
    static member ( ** ) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,float> = Series.BinaryOperatorMap(source, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x = y)
    static member (>) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x > y)
    static member (>=) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x >= y)
    static member (<) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x < y)
    static member (<=) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <= y)
    static member (<>) (source:Series<'K,float>, other:Series<'K,float>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <> y)


    // float32
    static member (+) (source:Series<'K,float32>, addition:float32) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> x + addition)
    static member (~+) (source:Series<'K,float32>) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> x)
    static member (-) (source:Series<'K,float32>, subtraction:float32) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> x - subtraction)
    static member (~-) (source:Series<'K,float32>) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> -x)
    static member (*) (source:Series<'K,float32>, multiplicator:float32) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> x * multiplicator)
    static member (*) (multiplicator:float32,source:Series<'K,float32>) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> multiplicator * x)
    static member (/) (source:Series<'K,float32>, divisor:float32) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> x / divisor)
    static member (/) (numerator:float32,source:Series<'K,float32>) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> numerator / x)
    static member (%) (source:Series<'K,float32>, modulo:float32) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> x % modulo)
    static member ( ** ) (source:Series<'K,float32>, power:float32) : Series<'K,float32> = Series.ScalarOperatorMap(source, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,float32>, other:float32) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x = other)
    static member (>) (source:Series<'K,float32>, other:float32) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x > other)
    static member (>=) (source:Series<'K,float32>, other:float32) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x >= other)
    static member (<) (other:float32, source:Series<'K,float32>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other < x)
    static member (<=) (other:float32, source:Series<'K,float32>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <= x)
    static member (<>) (other:float32, source:Series<'K,float32>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other <> x)

    static member (+) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(source, other, fun x y -> x + y)
    static member (-) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(source, other, fun x y -> x - y)
    static member (*) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(source, other, fun x y -> x * y)
    static member (/) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(source, other, fun x y -> x / y)
    static member (%) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(source, other, fun x y -> x % y)
    static member ( ** ) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,float32> = Series.BinaryOperatorMap(source, other, fun x y -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x = y)
    static member (>) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x > y)
    static member (>=) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x >= y)
    static member (<) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x < y)
    static member (<=) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <= y)
    static member (<>) (source:Series<'K,float32>, other:Series<'K,float32>) : Series<'K,bool> = Series.BinaryOperatorMap(source, other, fun x y -> x <> y)

    // TODO (high) add all math operators, e.g. Abs, Log, Exp, etc.
    // TODO other primitive numeric types
    // TODO (low) dynamic operators via Linq.Expressions, then Panels will work via series

and
  // TODO (perf) base Series() implements IROOM inefficiently, see comments in above type Series() implementation
  /// Wraps Series over ICursor
  [<AllowNullLiteral>]
  [<Serializable>]
//  [<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
  CursorSeries<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>) =
    inherit Series<'K,'V>()
    override this.GetCursor() = cursorFactory.Invoke()
    
    interface ICanMapSeriesValues<'K,'V> with
      member this.Map<'V2>(f2:Func<'V,'V2>): Series<'K,'V2> = 
        let cursor = cursorFactory.Invoke()
        match cursor with
        | :? ICanMapSeriesValues<'K,'V> as mappable -> mappable.Map(f2)
        | _ -> new BatchMapValuesCursor<_,_,_>((fun _ -> cursor), f2) :> Series<_,_>



and
  // NB! Remember that a cursors are single-threaded
  /// Map values to new values, batch mapping if that makes sense (for simple operations additional logic overhead is usually bigger than)
  [<SealedAttribute>]
  internal BatchMapValuesCursor<'K,'V,'V2> internal(cursorFactory:Func<ICursor<'K,'V>>, f:Func<'V,'V2>, fBatch:Func<IReadOnlyOrderedMap<'K,'V>,IReadOnlyOrderedMap<'K,'V2>> opt)=
    inherit Series<'K,'V2>()
    let mutable cursor : ICursor<'K,'V> =  cursorFactory.Invoke()
    
    let f : Func<'V,'V2> = f
    let fBatch = fBatch
    // for forward-only enumeration this could be faster with native math
    // any non-forward move makes this false and we fall back to single items
    let mutable preferBatches = fBatch.IsPresent
    let mutable batchStarted = false
    let mutable batch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>>
    let mutable batchCursor = Unchecked.defaultof<ICursor<'K,'V2>>
    let queue = if preferBatches then Queue<Task<_>>() else Unchecked.defaultof<_>

    let threadId = Environment.CurrentManagedThreadId
    [<DefaultValueAttribute>]
    val mutable started : bool
    override this.GetCursor() =
      let cursor = 
        if not this.started && threadId = Environment.CurrentManagedThreadId then 
          this.started <- true
          this 
        else this.Clone()
      cursor.started <- true
      cursor :> ICursor<'K,'V2>

    new(cursorFactory:Func<ICursor<'K,'V>>, f:Func<'V,'V2>) = new BatchMapValuesCursor<'K,'V,'V2>(cursorFactory, f, OptionalValue.Missing)
    new(cursorFactory:Func<ICursor<'K,'V>>, f:Func<'V,'V2>,fBatch:Func<IReadOnlyOrderedMap<'K,'V>,IReadOnlyOrderedMap<'K,'V2>>) = new BatchMapValuesCursor<'K,'V,'V2>(cursorFactory, f, OptionalValue(fBatch))



    member this.CurrentKey: 'K = 
      if batchStarted then batchCursor.CurrentKey
      else cursor.CurrentKey
    member this.CurrentValue: 'V2 = 
      if batchStarted then batchCursor.CurrentValue
      else f.Invoke(cursor.CurrentValue)
    member this.Current: KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)

    member private this.MapBatch(batch:IReadOnlyOrderedMap<'K,'V>) =
      if preferBatches then
        fBatch.Present.Invoke(batch)
      else
        let factory = Func<_>(batch.GetCursor)
        let c() = new BatchMapValuesCursor<'K,'V,'V2>(factory, f, fBatch) :> ICursor<'K,'V2>
        CursorSeries(Func<_>(c)) :> IReadOnlyOrderedMap<'K,'V2>

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

    member this.CurrentBatch
      with get() : IReadOnlyOrderedMap<'K,'V2> =
        batch

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
      if this.MoveNext() then Task.FromResult(true)
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
            (fun (antecedant : Task<IReadOnlyOrderedMap<'K,'V2> option>) ->
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
          (fun (antecedant : Task<IReadOnlyOrderedMap<'K,'V2> option>) ->
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
      let ok, v = cursor.TryGetValue(key)
      if ok then value <- f.Invoke(v)
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
      this.started <- false
    member this.Clone() = new BatchMapValuesCursor<'K,'V,'V2>(Func<_>(cursor.Clone), f, fBatch)

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
      member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
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
      member this.Map<'V3>(f2:Func<'V2,'V3>): Series<'K,'V3> = 
        // NB CoreUtils.CombineSelectors is visivly faster for operators
        let func = CoreUtils.CombineMaps(f, f2) //  Func<'V,'V3>(fun x -> f2.Invoke(f.Invoke(x))) // NB (WTF?) this is much slower in the benchmark, but slightly faster with microbench with doubles : Func<'V,'V3>(f.Invoke >> f2.Invoke)  //
        //NB! Expression is slower by 30%
        //let invoker =
        //  let arg = Expression.Parameter(typeof<'V>, "arg")
        //  let invokeExp = Expression.Invoke(Expression.Constant(func), arg)
        //  let lambda = Expression.Lambda<Func<'V,'V3>>(invokeExp, arg)
        //  lambda.Compile()
        let combinedF : Func<'V,'V3> = func
        new BatchMapValuesCursor<'K,'V,'V3>(cursorFactory, combinedF) :> Series<'K,'V3>


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
//        let ok, v = cursor.TryGetValue(k)
//        if ok then OptionalValue(v)
//        else
//          // weird case when continuous has holes
//          let mutable found = false
//          while not found && cursor.MoveNext() do
//            let c = cmp.Compare(cursor.CurrentKey, k)
//            if c >= 0 then found <- true
//          OptionalValue.Missing
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
//        else OptionalValue.Missing
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
//          OptionalValue.Missing
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
//        else OptionalValue.Missing
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
  ZipNCursor<'K,'V,'R>(resultSelector:Func<'K,'V[],'R>, [<ParamArray>] cursorFactories:(unit->ICursor<'K,'V>)[]) as this =
    do
      if cursorFactories.Length < 2 then invalidArg "cursorFactories" "ZipN takes at least two cursor factories"
    let cursorsFactory() = cursorFactories |> Array.map (fun x -> x())
    let mutable cursors = cursorsFactory()
    // positions of cursor, including virtual positions of continuous cursors
    // NB this is probably not needed, one of attempts to deal with continuous cursors
    let positions = Array.zeroCreate<'K> cursors.Length

    // current values of all cursor. we keep them in an array because for continuous cursors there is no current value,
    // they just return TryGetValue at a key. Also, applying a resultSelector function to an array is fast
    let currentValues = Array.zeroCreate<'V> cursors.Length
    
    let cmp = 
      let c' = cursors.[0].Comparer
      for c in cursors do
        if not <| c.Comparer.Equals(c') then invalidOp "ZipNCursor: Comparers are not equal" 
      c'

    let continuous = cursors |> Array.map (fun x -> x.IsContinuous) // NB only two bool arrays per cursor, could use BitArray but "later"(TM)
    let isContinuous = continuous |> Array.forall id

    // indicates that previous move was OK and that a next move should not pre-build a state
    let mutable hasValidState = false
    // for ZipN, valid states are:
    // - all cursors are at the same key (virtually for continuous, they are at the next existing key)
    // - cursors are not at the same position but one of them returned false on move next/previous after 
    //   we tried to move from a valid state. In this state we could call MoveNextAsync and try to call Move Next repeatedly.
    //   current key/values are undefined here because a move returned false

    // all keys where non-continuous cursors are positioned. they define where resulting keys are present
    let pivotKeysSet = SortedDeque(KVComparer(cmp, Comparer<int>.Default))
    // active continuous cursors
    let contKeysSet = SortedDeque(KVComparer(cmp, Comparer<int>.Default))
    

    /// TODO(perf) Now using TryGetValue without moving cursors. The idea is that continuous series are usually less frequent than
    /// the pivot ones, e.g. daily vs. minutely/secondly data, so the "depth" of binary search is not too big
    /// However, due to the same fact, one single MoveNext on daily data could cover many pivot points
    /// Continuous cursors should be optimized for the cases when the key in `.TryGetValue(key)` is between
    /// the current and the previous position of the continuous cursor
    let fillContinuousValuesAtKey (key:'K) =
      if contKeysSet.Count = 0 then true
      else
        let mutable cont = true
        let mutable c = 0
        while cont && c < cursors.Length do
          if continuous.[c] then
            let ok, value = cursors.[c].TryGetValue(key)
            if ok then currentValues.[c] <- value
            else cont <- false // cannot get value
          c <- c + 1
        cont

    // do... functions do move at least one cursor, so they should only be called
    // when state is valid or when it is proven invalid and we must find the first valid position
    // MoveFirst/Last/At must try to check the initial position before calling the do... functions

    // non-cont
    // this is a single-threaded algotithm that uses a SortedDeque data structure to determine moves priority
    let rec doMoveNextNonContinuous() =
      let mutable cont = true
      // check if we reached the state where all cursors are at the same position
      while cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) < 0 && cont do
        // pivotKeysSet is essentially a task queue:
        // we take every cursor that is not at fthe frontier and try to move it forward until it reaches the frontier
        // if we do this in parallel, the frontier could be moving while we move cursors
        let first = pivotKeysSet.RemoveFirst()
        let ac = cursors.[first.Value]
        let mutable moved = true
        let mutable c = -1 // by construction // cmp.Compare(ac.CurrentKey, pivotKeysSet.Max.Key)
        
        // move active cursor forward while it is before the current max key
        // max key of non-cont series is the frontier: we will never get a value before it,
        // and if any pivot moves ahead of the frontier, then it shifts the frontier 
        // and the old one becomes unreachable

        while c < 0 && moved do
          moved <- ac.MoveNext()
          c <- cmp.Compare(ac.CurrentKey, pivotKeysSet.Last.Key)

        if moved then
          currentValues.[first.Value] <- ac.CurrentValue
          pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
        else
          cont <- false // cannot move, stop sync move next, leave cursors where they are
      if cont then
        if fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
          this.CurrentKey <- pivotKeysSet.First.Key
          true
        else
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position
          let first =  pivotKeysSet.RemoveFirst()
          let ac = cursors.[first.Value]
          if ac.MoveNext() then
            currentValues.[first.Value] <- ac.CurrentValue
            pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
            doMoveNextNonContinuous() // recursive
          else false
      else false
    
    // TODO ensure this is syncronized with movenext and tested 
    let rec doMovePrevNonCont() =
      let mutable cont = true
      //let mutable activeCursorIdx = 0
      // check if we reached the state where all cursors are at the same position
      while cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) < 0 && cont do //
        // pivotKeysSet is essentially a task queue:
        // we take every cursor that is not at fthe frontier and try to move it forward until it reaches the frontier
        // if we do this in parallel, the frontier could be moving while we are 
        let last = pivotKeysSet.RemoveLast()
        let ac = cursors.[last.Value]
        let mutable moved = true
        let mutable c = +1 // by construction 

        // move active cursor backward while it is before the current min key
        // ... see move next

        while c > 0 && moved do
          moved <- ac.MovePrevious()
          c <- cmp.Compare(ac.CurrentKey, pivotKeysSet.First.Key)

        if moved then
          currentValues.[last.Value] <- ac.CurrentValue
          pivotKeysSet.Add(KV(ac.CurrentKey, last.Value)) |> ignore // TODO(low) SortedDeque AddFirst optimization similar to last.
        else
          cont <- false // cannot move, stop sync move next, leave cursors where they are
      if cont then
        if fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
          this.CurrentKey <- pivotKeysSet.First.Key
          true
        else
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position
          let first =  pivotKeysSet.RemoveFirst()
          let ac = cursors.[first.Value]
          if ac.MoveNext() then
            currentValues.[first.Value] <- ac.CurrentValue
            pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
            doMoveNextNonContinuous() // recursive
          else false
      else false

    // manual state machine instead of a task computation expression, this is visibly faster
    let doMoveNextNonContinuousTask(ct:CancellationToken) : Task<bool> =
      let mutable tcs = new TaskCompletionSource<_>() //(Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create())
      let returnTask = tcs.Task // NB! must access this property first
      let mutable firstStep = ref true
      let mutable sourceMoveTask = Unchecked.defaultof<_>
      let mutable lingering = false
      let mutable initialPosition = Unchecked.defaultof<_>
      let mutable ac = Unchecked.defaultof<_>
      let rec loop(isOuter:bool) : unit =
        if isOuter then
          if not !firstStep && cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 && fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
            this.CurrentKey <- pivotKeysSet.First.Key
            tcs.SetResult(true) // the only true exit
            () // return
          else
            // pivotKeysSet is essentially a task queue:
            // we take every cursor that is not at fthe frontier and try to move it forward until it reaches the frontier
            // if we do this in parallel, the frontier could be moving while we move cursors
            if lingering then invalidOp "previous position is not added back"
            if pivotKeysSet.Count = 0 then invalidOp "pivotKeysSet is empty"
            
            initialPosition <- pivotKeysSet.RemoveFirst()
            lingering <- true
            ac <- cursors.[initialPosition.Value]
            loop(false)
            // stop loop //Console.WriteLine("Should not be here")
            //activeCursorLoop()
        else
          firstStep := false
          let idx = initialPosition.Value
          let cursor = ac
          let onMoved() =
            currentValues.[idx] <- cursor.CurrentValue
            pivotKeysSet.Add(KV(cursor.CurrentKey, idx)) |> ignore
            lingering <- false
            loop(true)
          let mutable c = -1
          while c < 0 && cursor.MoveNext() do
            c <- cmp.Compare(cursor.CurrentKey, pivotKeysSet.Last.Key)
          if c >= 0 then
            onMoved()
          else
            // call itself until reached the frontier, then call outer loop
            sourceMoveTask <- cursor.MoveNext(ct)
            //task.Start()
            
            // there is a big chance that this task is already completed
            let onCompleted() =
              let moved =  sourceMoveTask.Result
              if not moved then
                tcs.SetResult(false) // the only false exit
                //Console.WriteLine("Finished")
                ()
              else
                let c = cmp.Compare(cursor.CurrentKey, pivotKeysSet.Last.Key)
                if c < 0 then
                  pivotKeysSet.Add(initialPosition) // TODO! Add/remove only when needed
                  loop(false)
                else
                  onMoved()
                  
            if sourceMoveTask.Status = TaskStatus.RanToCompletion then
              onCompleted()
            else
//              Thread.SpinWait(50)
//              if sourceMoveTask.IsCompleted then
//                onCompleted()
//              else
                let awaiter = sourceMoveTask.GetAwaiter()
                // NB! do not block, use callback
                awaiter.OnCompleted(fun _ ->
                  // TODO! Test all cases
                  if sourceMoveTask.Status = TaskStatus.RanToCompletion then
                    onCompleted()
                  else
                    pivotKeysSet.Add(initialPosition) // TODO! Add/remove only when needed
                    if sourceMoveTask.Status = TaskStatus.Canceled then
                      tcs.SetCanceled()
                    else
                      tcs.SetException(sourceMoveTask.Exception)
                )
            ()
      loop(true)
      returnTask

    
    // Continuous
    let doMoveNextContinuous(frontier:'K) =
      let mutable frontier = frontier

      // frontier is the current key. on each zip move we must move at least one cursor ahead 
      // of the current key, and the position of this cursor is the new key
      //    [---x----x-----x-----x-------x---]
      //    [-----x--|--x----x-----x-------x-]
      //    [-x----x-|---x-----x-------x-----]

      // found all values
      let mutable valuesOk = false
      let mutable found = false
      while not found do
        /// ignore this number of leftmost cursors because they could not move
        let mutable ignoreOffset = 0 // NB is reset on each move next, because the sources could be updated between moves
        let mutable leftmostIsAheadOfFrontier = false
        while (ignoreOffset < contKeysSet.Count) && not leftmostIsAheadOfFrontier do // wrong condition
          // if we will have to update contKeysSet, we must freeze the initial position
          let initialPosition = contKeysSet.[ignoreOffset]
          // the leftmost cursor
          let cursor = cursors.[initialPosition.Value]
          Debug.Assert(cmp.Compare(initialPosition.Key, cursor.CurrentKey) = 0)

          let shouldMove = cmp.Compare(cursor.CurrentKey, frontier) <= 0
          let mutable doTryMove = shouldMove
          let mutable movedAtLeastOnce = false
          let mutable passedFrontier = not shouldMove
          
          while doTryMove do
            // if we break due to the second condition below, that is possible only after a move
            passedFrontier <- cursor.MoveNext()
            movedAtLeastOnce <- movedAtLeastOnce || passedFrontier
            doTryMove <- passedFrontier && cmp.Compare(cursor.CurrentKey, frontier) <= 0

          if movedAtLeastOnce || passedFrontier then
            if movedAtLeastOnce then
              let newPosition = KV(cursor.CurrentKey, initialPosition.Value)
              // update positions if the current has changed, regardless of the frontier
              contKeysSet.RemoveAt(ignoreOffset) |> ignore
              contKeysSet.Add(newPosition)

            if passedFrontier then
              // here we have the most lefmost position updated (it could be the same cursor, but is doesn't matter)
              // we should check if the updated leftmost position is ahead of the frontier
              // if it is, then we should try get values at the key - EXIT inner loop
              //      if values are OK there, then return
              //      else move frontier there and continues
              // iterate the loop

              // compare the new leftmost cursor with the frontier
              if cmp.Compare(contKeysSet.[ignoreOffset].Key, frontier) > 0 then
                // could just set ignoreOffset above the deque length
                leftmostIsAheadOfFrontier <- true // NB inner loop exit
              else
                // should iterate the inner loop
                ()
            else
              // should iterate the inner loop
              // if the cursor is still the leftmost and cannot move, on the next iteration movedAtLeastOnce will be false
              // and ignoreOffset will be incremented below
              ()
          else
            Debug.Assert(not passedFrontier, "If cursor hasn't moved, I couldn't pass the prontier")
            // we should ignore this cursor during this ZipN.MoveNext()
            // it is already the most leftmost, and couldn't move
            ignoreOffset <- ignoreOffset + 1
            ()

        // if the leftmost cursor passed the frontier
        if leftmostIsAheadOfFrontier then
          found <- fillContinuousValuesAtKey(contKeysSet.[ignoreOffset].Key) 
          if found then 
            valuesOk <- true
            this.CurrentKey <- contKeysSet.[ignoreOffset].Key
          else
            frontier <- contKeysSet.[ignoreOffset].Key
        else
          // we cannot move further if we have exited the inner loop but couldn't pass the frontier
          found <- true
          valuesOk <- false
      valuesOk
      
      
    // TODO!!! this is wrong
    let doMovePrevContinuous(frontier:'K) =
      failwith "ZipN MovePrevious is very likely wrong"
      // found all values
      let mutable valuesOk = false
      let cksEnumerator = contKeysSet.Reverse().GetEnumerator()
      let mutable found = false
      while not found && cksEnumerator.MoveNext() do // need to update contKeysSet!!!!!!!!!!!!!!!!!!!
        let position = cksEnumerator.Current
        let cursor = cursors.[position.Value]
        let mutable moved = true
        while cmp.Compare(cursor.CurrentKey, frontier) >= 0 && moved && not found do
          moved <- cursor.MovePrevious()
          if moved then // cursor moved
            contKeysSet.Remove(position)
            contKeysSet.Add(KV(cursor.CurrentKey, position.Value))
            
            if cmp.Compare(cursor.CurrentKey, frontier) < 0  // ahead of the previous key
              && fillContinuousValuesAtKey(cursor.CurrentKey) then // and we could get all values at the new position
              found <- true
              valuesOk <- true
              this.CurrentKey <- cursor.CurrentKey
      valuesOk

    // TODO (perf) this passes tests, but no attemp was made to optimize the async version
    let doMoveNextContinuousTask(frontier:'K, ct:CancellationToken) : Task<bool> =
      task {
      let mutable frontier = frontier

      // frontier is the current key. on each zip move we must move at least one cursor ahead 
      // of the current key, and the position of this cursor is the new key
      //    [---x----x-----x-----x-------x---]
      //    [-----x--|--x----x-----x-------x-]
      //    [-x----x-|---x-----x-------x-----]

      // found all values
      let mutable valuesOk = false
      let mutable found = false
      while not found do
        /// ignore this number of leftmost cursors because they could not move
        let mutable ignoreOffset = 0 // NB is reset on each move next, because the sources could be updated between moves
        let mutable leftmostIsAheadOfFrontier = false
        lock contKeysSet (fun _ ->
          while (ignoreOffset < contKeysSet.Count) && not leftmostIsAheadOfFrontier do // wrong condition
            // if we will have to update contKeysSet, we must freeze the initial position
            let initialPosition = contKeysSet.[ignoreOffset]
            // the leftmost cursor
            let cursor = cursors.[initialPosition.Value]
            Debug.Assert(cmp.Compare(initialPosition.Key, cursor.CurrentKey) = 0)

            let shouldMove = cmp.Compare(cursor.CurrentKey, frontier) <= 0
            let mutable doTryMove = shouldMove
            let mutable movedAtLeastOnce = false
            let mutable passedFrontier = not shouldMove
          
            while doTryMove do
              // if we break due to the second condition below, that is possible only after a move
              passedFrontier <- cursor.MoveNext()
              movedAtLeastOnce <- movedAtLeastOnce || passedFrontier
              doTryMove <- passedFrontier && cmp.Compare(cursor.CurrentKey, frontier) <= 0

            if movedAtLeastOnce || passedFrontier then
              if movedAtLeastOnce then
                let newPosition = KV(cursor.CurrentKey, initialPosition.Value)
                // update positions if the current has changed, regardless of the frontier
                contKeysSet.Remove(initialPosition)
                contKeysSet.Add(newPosition)
              

              if passedFrontier then
                // here we have the most lefmost position updated (it could be the same cursor, but is doesn't matter)
                // we should check if the updated leftmost position is ahead of the frontier
                // if it is, then we should try get values at the key - EXIT inner loop
                //      if values are OK there, then return
                //      else move frontier there and continues
                // iterate the loop

                // compare the new leftmost cursor with the frontier
                if cmp.Compare(contKeysSet.[ignoreOffset].Key, frontier) > 0 then
                  // could just set ignoreOffset above the deque length
                  leftmostIsAheadOfFrontier <- true // NB inner loop exit
                else
                  // should iterate the inner loop
                  ()
              else
                // should iterate the inner loop
                // if the cursor is still the leftmost and cannot move, on the next iteration movedAtLeastOnce will be false
                // and ignoreOffset will be incremented below
                ()
            else
              Debug.Assert(not passedFrontier, "If cursor hasn't moved, I couldn't pass the prontier")
              // we should ignore this cursor during this ZipN.MoveNext()
              // it is already the most leftmost, and couldn't move
              ignoreOffset <- ignoreOffset + 1
              ()
        ) // lock

        // if the leftmost cursor passed the frontier
        if leftmostIsAheadOfFrontier then
          found <- fillContinuousValuesAtKey(contKeysSet.[ignoreOffset].Key) 
          if found then 
            valuesOk <- true
            this.CurrentKey <- contKeysSet.[ignoreOffset].Key
          else
            frontier <- contKeysSet.[ignoreOffset].Key
        else
          let continueMoveNext (position:KV<'K,int>) =
            let cur = cursors.[position.Value]
            cur.MoveNext(ct).ContinueWith(fun (t:Task<bool>) -> 
                if t.Result then
                  if cmp.Compare(cur.CurrentKey, frontier) <= 0 then
                    invalidOp "Out of order data in ZipN continuous"
                  lock contKeysSet (fun _ ->
                    contKeysSet.Remove(position) |> ignore
                    contKeysSet.Add(KV(cur.CurrentKey, position.Value))
                    ()
                  )
                  true
                else
                  lock contKeysSet (fun _ ->
                    // it will never move again, remove forever
                    contKeysSet.Remove(position) |> ignore
                    ()
                  )
                  false
              )

          let tasks = 
            contKeysSet 
            |> Seq.filter (fun kv -> cmp.Compare(kv.Key, frontier) <= 0)
            |> Seq.map (fun kv -> continueMoveNext kv)
            |> Seq.toArray
          let! oneMoved = Task.WhenAny(tasks)
          if contKeysSet.Count = 0 then
            // we cannot move further if we have exited the inner loop but couldn't pass the frontier
            found <- true
            valuesOk <- false
          else
            // continue the outer (not found) loop until there is at least one source
            ()

      return valuesOk }

    member this.Comparer with get() = cmp
    member this.HasValidState with get() = hasValidState and set (v) = hasValidState <- v

    member val IsContinuous = isContinuous

    member val CurrentKey = Unchecked.defaultof<'K> with get, set

    // NB lazy application of resultSelector, only when current value is requested
    member this.CurrentValue with get() = resultSelector.Invoke(this.CurrentKey, currentValues)

    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
    member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>> with get, set

    member this.Reset() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Reset()) |> ignore

    member this.Dispose() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Dispose()) |> ignore
    
    member this.Clone(): ICursor<'K,'R> =
      // run-time type of the instance, could be derived type
      let clone = new ZipNCursor<'K,'V,'R>(resultSelector, cursorFactories) :> ICursor<'K,'R>
      if hasValidState then 
        let movedOk = clone.MoveAt(this.CurrentKey, Lookup.EQ)
        Trace.Assert(movedOk) // if current key is set then we could move to it
      clone
         

    member this.MoveNext(): bool =
      if not this.HasValidState then this.MoveFirst()
      else
        if isContinuous then
          doMoveNextContinuous(this.CurrentKey)
        else
          let doContinue =
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 then
              let first = pivotKeysSet.RemoveFirst()
              let ac = cursors.[first.Value]
              if ac.MoveNext() then
                currentValues.[first.Value] <- ac.CurrentValue
                pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
                true
              else
                pivotKeysSet.Add(first) // TODO! only replace when needed, do not do this round trip!
                false
            else true
          if doContinue then doMoveNextNonContinuous()
          else false

    // manual
    member this.MoveNext(ct:CancellationToken): Task<bool> =
      if not this.HasValidState then Task.Run(fun _-> this.MoveFirst()) // TODO this is potentially blocking, make it async
      else
        if isContinuous then
          doMoveNextContinuousTask(this.CurrentKey, ct) // failwith "TODO noncont" //doMoveNextContinuousTask(this.CurrentKey, ct)
        else
          doMoveNextNonContinuousTask(ct)

    member x.MovePrevious(): bool = 
      if not this.HasValidState then this.MoveLast()
      else
        if isContinuous then
          doMovePrevContinuous(this.CurrentKey)
        else
          let cont =
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 then
              let last = pivotKeysSet.RemoveLast()
              let ac = cursors.[last.Value]
              if ac.MovePrevious() then
                currentValues.[last.Value] <- ac.CurrentValue
                pivotKeysSet.Add(KV(ac.CurrentKey, last.Value)) |> ignore
                true
              else false
            else true
          if cont then doMovePrevNonCont()
          else false

    member this.MoveFirst(): bool =
      let mutable doContinue = true
      let mutable valuesOk = false
      let mutable allMovedFirst = false
      pivotKeysSet.Clear()
      contKeysSet.Clear()
      while doContinue do
        if not allMovedFirst then
          cursors 
          |> Array.iteri (fun i x -> 
            let movedFirst = x.MoveFirst()
            if movedFirst then
              if continuous.[i] then 
                contKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              else
                pivotKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              currentValues.[i] <- x.CurrentValue
            else
              doContinue <- false // series has no values, stop here
          )
          allMovedFirst <- doContinue
        else
          if isContinuous then
            if fillContinuousValuesAtKey(contKeysSet.First.Key) then
              this.CurrentKey <- contKeysSet.First.Key
              valuesOk <- true
              doContinue <- false
            else
              valuesOk <- doMoveNextContinuous(contKeysSet.First.Key)
          else
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
              for kvp in pivotKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- pivotKeysSet.First.Key
              valuesOk <- true
              doContinue <- false 
            else
              // move to max key until min key matches max key so that we can use values
              valuesOk <- doMoveNextNonContinuous()
              doContinue <- not valuesOk
      if valuesOk then 
        this.HasValidState <- true
        true
      else false

    member this.MoveLast(): bool = 
      let mutable cont = true
      let mutable valuesOk = false
      let mutable allMovedLast = false
      pivotKeysSet.Clear()
      contKeysSet.Clear()
      while cont do
        if not allMovedLast then
          cursors 
          |> Array.iteri (fun i x -> 
            let movedLast = x.MoveLast()
            if movedLast then
              if continuous.[i] then 
                contKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              else
                pivotKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              currentValues.[i] <- x.CurrentValue
            else
              cont <- false // series has no values, stop here
          )
          allMovedLast <- cont
        else
          if isContinuous then
            if fillContinuousValuesAtKey(contKeysSet.Last.Key) then
              this.CurrentKey <- contKeysSet.Last.Key
              valuesOk <- true
              cont <- false
            else
              valuesOk <- doMovePrevContinuous(contKeysSet.Last.Key)
          else
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
              for kvp in pivotKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- pivotKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              // move to max key until min key matches max key so that we can use values
              valuesOk <- doMovePrevNonCont() //failwith "TODO" //this.DoMoveNext()
              cont <- not valuesOk
      if valuesOk then 
        this.HasValidState <- true
        true
      else false

    member x.MoveAt(key: 'K, direction: Lookup) : bool =
      let mutable cont = true
      let mutable valuesOk = false
      let mutable allMovedAt = false
      pivotKeysSet.Clear()
      contKeysSet.Clear()
      while cont do
        if not allMovedAt then
          cursors 
          |> Array.iteri (fun i x -> 
            let movedAt = x.MoveAt(key, direction)
            if movedAt then
              if continuous.[i] then 
                contKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              else
                pivotKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              currentValues.[i] <- x.CurrentValue
            else
              cont <- false // series has no values, stop here
          )
          allMovedAt <- cont
        else
          if isContinuous then
            // this condition is applied to all directions
            if cmp.Compare(contKeysSet.First.Key, contKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(contKeysSet.First.Key) then
              this.CurrentKey <- contKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              match direction with
              | Lookup.EQ -> 
                valuesOk <- false
                cont <- false
              | Lookup.LE | Lookup.LT ->
                if fillContinuousValuesAtKey(contKeysSet.Last.Key) then
                  this.CurrentKey <- contKeysSet.Last.Key
                  valuesOk <- true
                  cont <- false
                else
                  valuesOk <- doMovePrevContinuous(contKeysSet.Last.Key)
                  cont <- not valuesOk
              | Lookup.GE | Lookup.GT ->
                if fillContinuousValuesAtKey(contKeysSet.First.Key) then
                  this.CurrentKey <- contKeysSet.First.Key
                  valuesOk <- true
                  cont <- false 
                else
                  valuesOk <- doMoveNextContinuous(contKeysSet.First.Key)
                  cont <- not valuesOk
              | _ -> failwith "Wrong lookup direction, should never be there"
          else
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
              for kvp in pivotKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- pivotKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              match direction with
              | Lookup.EQ -> 
                valuesOk <- false
                cont <- false
              | Lookup.LE | Lookup.LT ->
                valuesOk <- doMovePrevNonCont()
              | Lookup.GE | Lookup.GT ->
                valuesOk <- doMoveNextNonContinuous()
              | _ -> failwith "Wrong lookup direction, should never be there"
      if valuesOk then 
        this.HasValidState <- true
        true
      else false

    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
      let mutable cont = true
      let values = 
        cursors 
        |> Array.map (fun x ->  // TODO instead of Array.Parallel, use PLINQ, it is smart and tested, I do not have the same confidence in F#.Core
          let ok, value = x.TryGetValue(key)
          if ok then value 
          else 
            cont <- false
            Unchecked.defaultof<'V>
        )
      if cont then
        value <- resultSelector.Invoke(key, values)
        true
      else false
        
    member x.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = Task.FromResult(false)
    
    //member x.IsBatch with get() = x.IsBatch
    member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((x :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>

    interface IEnumerator<KVP<'K,'R>> with
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'R> = KVP(this.CurrentKey, this.CurrentValue)
      member this.Current with get(): obj = this.Current :> obj 
      member x.Dispose(): unit = x.Dispose()

    // TODO (perf, low) move implementations directly to interface, that will save one callvirt. That trick has already improved perf in other places, e.g. SortedDeque.

    interface ICursor<'K,'R> with
      member this.Comparer with get() = cmp
      member x.CurrentBatch: IReadOnlyOrderedMap<'K,'R> = x.CurrentBatch
      member x.CurrentKey: 'K = x.CurrentKey
      member x.CurrentValue: 'R = x.CurrentValue
      member x.IsContinuous: bool = x.IsContinuous
      member x.MoveAt(key: 'K, direction: Lookup) : bool = x.MoveAt(key, direction)

      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()

      member this.MovePrevious(): bool = this.MovePrevious()
    
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNext(cancellationToken)
 
       member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNextBatch(cancellationToken)
     
      //member x.IsBatch with get() = x.IsBatch
      member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((x :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>
      
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
        // TODO should keep a lazy array of cursors that is initiated on first call to this function
        // and then is reused on evey call
        this.TryGetValue(key, &value)
    
      member this.Clone() = this.Clone()