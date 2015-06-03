module Spreads.Period.PeriodTests
open System
open System.Diagnostics

open Spreads
open NUnit.Framework
open FsUnit

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




[<Test>]
let CreateManyPeriodFromDateTime() = 
    let count = 1000000L
    perf count "IntMap64<int64> insert" (fun _ ->
      for i in 0L..count do
        let tp = Spreads.Period(UnitPeriod.Millisecond, 1, 
                  DateTime.Today.AddMilliseconds(float i), TimeZoneInfo.Local)
        ()
    )

[<Test>]
let CreateManyPeriodFromDateTimeOffset() = 
    let count = 10000000L
    let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
    perf count "Period from DTO" (fun _ ->
      let arr = Array.init (int count) (fun i -> Spreads.Period(UnitPeriod.Millisecond, 1, 
                      initDTO.AddMilliseconds(float i)))
//      for i in 0L..count do
//        let tp = Spreads.Period(UnitPeriod.Millisecond, 1, 
//                  DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero).AddMilliseconds(float i))
//        ()
      arr.Length |> ignore
    )

[<Test>]
let CreateManyPeriodFromParts() = 
    let count = 1000000L
    perf count "IntMap64<int64> insert" (fun _ ->
      let arr = Array.init (int count) (fun i -> Spreads.Period(UnitPeriod.Millisecond, 1, 
                      2014, 11, 23, i / 3600000, i / 60000, i/1000, i))
//      for i in 0L..count do
//        let tp = Spreads.Period(UnitPeriod.Millisecond, 1, 
//                  DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero).AddMilliseconds(float i))
//        ()
      arr.Length |> ignore
    )



[<Test>]
let CouldCalculateDiff() = 
  let fisrt = Spreads.Period(UnitPeriod.Second, 1, 
                  DateTime.Today, TimeZoneInfo.Local)
  let second = Spreads.Period(UnitPeriod.Second, 1, 
                  DateTime.Today.AddSeconds(float 1), TimeZoneInfo.Local)
  let diff =  second.Diff(fisrt)
  diff |> should equal 1

  let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
  let initTp = Period(UnitPeriod.Second, 1,initDTO)
  let newTp = Period(UnitPeriod.Second, 1, 
                        initDTO.AddSeconds(float 1))
  let diff2 = initTp.Diff(newTp)
  diff2 |> should equal -1
  ()
    
[<Test>]
let CouldCalculateHash() = 
  let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
  let initTp = Period(UnitPeriod.Second, 1,initDTO)
  let newTp = Period(UnitPeriod.Second, 1, 
                        initDTO.AddSeconds(float 3600))
  let hash = Period.Hash(initTp)
  let hash2 = Period.Hash(newTp)
  let diff = hash2.Diff(hash)
  diff |> should equal 3600