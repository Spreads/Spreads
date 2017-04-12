// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


open System
open System.Collections.Generic
open System.Diagnostics
open Spreads
open Spreads.Collections
open Spreads.Collections.Experimental
open Spreads.Collections.Obsolete
open Spreads.Tests.Collections.Benchmarks

open System
open System.Reflection
open System.Xml.Serialization
open Spreads.Tests.Profile
open System.Threading.Tasks
open NUnit.Framework

//module Prelude =
//    open System.Linq
//    [<AutoOpenAttribute>]
//    module Seq =
//        let ofType<'T> (sequence : _ seq) = sequence.OfType<'T>()


//open Prelude
//let getXmlIncludes (xtype : Type) =
//    xtype.GetCustomAttributes() |> Seq.ofType<XmlIncludeAttribute>

//  /// run f and measure ops per second
//let perf (count:int64) (message:string) (f:unit -> unit) : unit = // int * int =
//  GC.Collect(3, GCCollectionMode.Forced, true)
//  let startMem = GC.GetTotalMemory(false)
//  let sw = Stopwatch.StartNew()
//  f()
//  sw.Stop()
//  let endtMem = GC.GetTotalMemory(true)
//  let p = (1000L * count/sw.ElapsedMilliseconds)
//  //int p, int((endtMem - startMem)/1024L)
//  Console.WriteLine(message + ", #{0}, ops: {1}, mem/item: {2}", 
//    count.ToString(), p.ToString(), ((endtMem - startMem)/count).ToString())
//  //Console.WriteLine("Elapsed ms: " + sw.ElapsedMilliseconds.ToString())
//let rng = Random()

//[<CustomComparison;CustomEquality>]
//type TestStruct =
//  struct 
//    val value : int64
//    val n : int64
//    internal new(value:int64, n:int64) = {value = value; n = n}
//  end
//  override x.Equals(yobj) =
//    match yobj with
//    | :? TestStruct as y -> (x.value = y.value && x.n = y.n)
//    | _ -> false
//  override x.GetHashCode() = x.value.GetHashCode()
//  override x.ToString() = x.value.ToString()
//  interface System.IComparable<TestStruct> with
//    member x.CompareTo y = 
//      let first = x.value.CompareTo(y.value)
//      if  first = 0 then
//        x.n.CompareTo(y.n)
//      else first
//  interface System.IComparable with
//    member x.CompareTo other = 
//      match other with
//      | :? TestStruct as y -> x.value.CompareTo(y.value)
//      | _ -> invalidArg "other" "Cannot compare values of different types"

//type IInc =
//  abstract Inc : unit -> int
//  abstract Value: int with get

//type MyInc =
//  struct 
//    val mutable value : int
//    new(v:int) = {value = v}
//  end
//  member this.Inc() = this.value <- (this.value + 1);this.value
//  member this.Value with get() = this.value
//  interface IInc with
//    member this.Inc() = this.value <- this.value + 1;this.value
//    member this.Value with get() = this.value

//[<StructAttribute>]
//type MyInc2 =
//  val mutable v : int
//  new(v:int) = {v = v}

//  member this.Inc() = this.v <- this.v + 1;this.v
//  member this.Value with get() = this.v

//  interface IInc with
//    member this.Inc() = this.v <- this.v + 1;this.v
//    member this.Value with get() = this.v


let writeLockTest() =
  let count = 10000000
  let sw = new Stopwatch();
  sw.Restart();

  let lockTest = new LockTestSeries();

  let t1 = Task.Run(fun _ ->
              for i in 0..count-1 do
                  lockTest.Increment();
              )


  let t2 = Task.Run(fun _ ->
    for i in 0..count-1 do
        lockTest.Increment();
    )

  t2.Wait();
  t1.Wait();
  sw.Stop();
  Console.WriteLine("Elapsed " + sw.ElapsedMilliseconds.ToString())
  Assert.AreEqual(2 * count, lockTest.Counter);

[<EntryPoint>]
let main argv = 
 
  for i in 0..100 do
    writeLockTest()

  Console.ReadLine();
  printfn "%A" argv
  0
