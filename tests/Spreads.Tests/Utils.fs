namespace Spreads
open System
open System.Linq
open System.Collections.Generic
open System.Diagnostics

//[<AutoOpenAttribute>]
//module TestUtils =
//  /// run f and measure ops per second
//  let perf (count:int64) (message:string) (f:unit -> unit) : unit = // int * int =
//    GC.Collect(3, GCCollectionMode.Forced, true)
//    let startMem = GC.GetTotalMemory(false)
//    f() // warm up
//    let sw = Stopwatch.StartNew()
//    f()
//    sw.Stop()
//    let endtMem = GC.GetTotalMemory(true)
//    let p = (1000L * count/sw.ElapsedMilliseconds)
//    //int p, int((endtMem - startMem)/1024L)
//    Console.WriteLine(message + ", #{0}, ops: {1}, mem/item: {2}", 
//      count.ToString(), p.ToString(), ((endtMem - startMem)/count).ToString())
