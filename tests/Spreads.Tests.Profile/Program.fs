// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.
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


[<EntryPoint>]
let main argv = 
  Spreads.Tests.Collections.Benchmarks.CollectionsBenchmarks.SHM_regular_run() // .SortedMapRegularTest(10000000L)
//  Spreads.Tests.Experimental.Buckets
//    .``Could store N sorted maps with 1000 elements to MMDic``(10000L)
  //Console.ReadKey()
  //Spreads.Tests.Collections.Benchmarks.CollectionsBenchmarks.SortedDeque_run()
//  let count = 1000000L
//  let ss = SortedSet()
//  let heap = ref (Heap.empty<TestStruct>(false))
//  let sd = Extra.SortedDeque()
//  let sm = SortedMap()
//  let width = 10
//  for i in 0..(width-1) do
////    ss.Add(TestStruct(int64 (rng.Next(0, width-1)), int64 i)) |> ignore
////    sd.Add(TestStruct(int64 (rng.Next(0, width-1)), int64 i))
//    sm.Add(TestStruct(int64 (rng.Next(0, width-1)), int64 i), false)
////    heap := Heap.insert (TestStruct(int64 (rng.Next(0, width-1)), int64 i)) !heap
//  perf count "SortedSet rotation" (fun _ ->
//    for i in (int64 width)..(count+(int64 width)) do
////      let min = ss.Min
////      ss.Remove(min) |> ignore
////      ss.Add(TestStruct(int64 (rng.Next(0, width-1)), int64 i))|> ignore
//      let ok,min = sm.RemoveFirst()
//      sm.Add(TestStruct(int64 (rng.Next(0, width-1)), int64 i), false)
//      let min = sd.RemoveFirst()
//      sd.Add(TestStruct(int64 (rng.Next(0, width-1)), int64 i))
//      ()
//      let min, tail = Heap.uncons !heap
//      heap := Heap.insert (TestStruct(int64 (rng.Next(0, width-1)), int64 i)) tail
//      ()
    //Console.WriteLine(Heap.length !heap)
    //Console.WriteLine(ss.Count.ToString())
    //Console.WriteLine(sd.Count.ToString())
    //Console.WriteLine(sm.Count.ToString())
//  )
//  Console.ReadKey() |> ignore
//  GC.GetTotalMemory(false).ToString() |> Console.WriteLine
//  let wrArr = [| WeakReference(Array.init 10000000 id) |]
//  GC.GetTotalMemory(false).ToString() |> Console.WriteLine
//  GC.Collect(2, GCCollectionMode.Forced, true)
//  if wrArr.[0].IsAlive then Console.WriteLine "Alive" else Console.WriteLine "Dead" 
//
//  Console.ReadKey() |> ignore
//  if wrArr.[0].IsAlive then Console.WriteLine "Alive" else Console.WriteLine "Dead"
//  Console.ReadKey() |> ignore
//


//
//  let count = 1000000L
//  for n in 0..9 do
//
//    let shm = ref (SortedHashMap(Int64SortableHasher(1024)))
//    for i in 0L..count do
//      shm.Value.Add(i, i)
//
//    for i in 0L..count do
//      let res = shm.Value.Item(i)
//      if res <> i then failwith "SHM failed"
//      ()
//
//    for i in shm.Value do
//      let res = i.Value
//      if res <> i.Value then failwith "SHM failed"
//      ()

//
//    let shmt = ref (SortedHashMap(TimePeriodSortableHasher()))
//    let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
//    for i in 0..(int count) do
//      shmt.Value.AddLast(TimePeriod(UnitPeriod.Second, 1, 
//                          initDTO.AddSeconds(float i)), i)
//    for i in 0..(int count) do
//      let res = shmt.Value.Item(TimePeriod(UnitPeriod.Second, 1, 
//                    initDTO.AddSeconds(float i)))
//      ()
//    Console.WriteLine(n.ToString())

//  let shmt = ref (SortedHashMap(TimePeriodSortableHasher()))
//  let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
//  for i in 0..(int count) do
//    shmt.Value.Add(TimePeriod(UnitPeriod.Second, 1, 
//                      initDTO.AddSeconds(float i)), i)
//  for i in 0..9 do
//    for ii in shmt.Value do
//      let res = ii.Value
//      ()
//    Console.WriteLine(i.ToString())
  Console.ReadLine();
  printfn "%A" argv
  0 // return an integer exit code
