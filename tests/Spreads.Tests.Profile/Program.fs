open System
open System.Collections.Generic
open System.Diagnostics
open Spreads
open Spreads.Collections
open Spreads.Collections.Experimental
open Spreads.Collections.Obsolete
  /// run f and measure ops per second
let perf (count:int64) (message:string) (f:unit -> unit) : unit = // int * int =
  GC.Collect(3, GCCollectionMode.Forced, true)
  let startMem = GC.GetTotalMemory(false)
  let sw = Stopwatch.StartNew()
  f()
  sw.Stop()
  let endtMem = GC.GetTotalMemory(true)
  let p = (1000L * count/sw.ElapsedMilliseconds)
  //int p, int((endtMem - startMem)/1024L)
  Console.WriteLine(message + ", #{0}, ops: {1}, mem/item: {2}", 
    count.ToString(), p.ToString(), ((endtMem - startMem)/count).ToString())
  //Console.WriteLine("Elapsed ms: " + sw.ElapsedMilliseconds.ToString())
let rng = Random()

[<CustomComparison;CustomEquality>]
type TestStruct =
  struct 
    val value : int64
    val n : int64
    internal new(value:int64, n:int64) = {value = value; n = n}
  end
  override x.Equals(yobj) =
    match yobj with
    | :? TestStruct as y -> (x.value = y.value && x.n = y.n)
    | _ -> false
  override x.GetHashCode() = x.value.GetHashCode()
  override x.ToString() = x.value.ToString()
  interface System.IComparable<TestStruct> with
    member x.CompareTo y = 
      let first = x.value.CompareTo(y.value)
      if  first = 0 then
        x.n.CompareTo(y.n)
      else first
  interface System.IComparable with
    member x.CompareTo other = 
      match other with
      | :? TestStruct as y -> x.value.CompareTo(y.value)
      | _ -> invalidArg "other" "Cannot compare values of different types"

type IInc =
  abstract Inc : unit -> int
  abstract Value: int with get

type MyInc =
  struct 
    val mutable value : int
    new(v:int) = {value = v}
  end
  member this.Inc() = this.value <- (this.value + 1);this.value
  member this.Value with get() = this.value
  interface IInc with
    member this.Inc() = this.value <- this.value + 1;this.value
    member this.Value with get() = this.value

[<StructAttribute>]
type MyInc2 =
  val mutable v : int
  new(v:int) = {v = v}

  member this.Inc() = this.v <- this.v + 1;this.v
  member this.Value with get() = this.v

  interface IInc with
    member this.Inc() = this.v <- this.v + 1;this.v
    member this.Value with get() = this.v


[<EntryPoint>]
let main argv = 
 
  let state = SortedMap()
  state.Add(DateTime.UtcNow.AddDays(-1.0), 0.0)
  let circular = TheSimplestTradingStrategy(state)

  circular.Execute()

  Console.ReadLine();
  printfn "%A" argv
  0
