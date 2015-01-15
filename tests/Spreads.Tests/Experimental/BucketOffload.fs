namespace Spreads.Tests.Experimental

open System
open NUnit.Framework
open FsUnit
open Spreads
open Spreads.Collections
open Spreads.Collections.Experimental

module Buckets =

  let mutable rng = new Random(0)
  
  [<Test>]
  let ``Could serialize and deserialize maps``() =
    let mutable bytes : byte[] = null
    let mutable zippedBytes : byte[] = null
    perf 1L "Could serialize a map" (fun _ ->
      let smap = SortedMap()
      for i in 0L..999L do
        smap.Add(i, float (i) / 10.0)
      bytes <-  Serialization.serializeSortedMap None smap
      
    )
    perf 1L "Could deserialize a map " (fun _ ->
      let dm = Serialization.deserializeSortedMap<int64,float> None bytes
      for i in 0L..999L do
        dm.[i] |> should equal (float (i) / 10.0)
    )
   
    Console.WriteLine("----------------")

  [<Test>]
  let ``Could serialize and deserialize maps with diff``() =
    let mutable bytes : byte[] = null
    let mutable zippedBytes : byte[] = null
    perf 1L "Could serialize a map" (fun _ ->
      let smap = SortedMap()
      for i in 0L..999L do
        smap.Add(i, float (i) / 10.0)
      bytes <-  Serialization.serializeSortedMap None smap
      //zippedBytes <- ZipBytes <| bytes
      
    )
//    perf 1L "Could deserialize a map " (fun _ ->
//      let unzippedBytes = (zippedBytes |> UnzipBytes)
//      let dm = Serialization.deserializeSortedMap<int64,float> None unzippedBytes
//      for i in 0L..999L do
//        dm.[i] |> should equal (float (i) / 10.0)
//    )
   
    Console.WriteLine("----------------")


  [<TestCase(1000)>]
  let ``Could write N sorted maps with 1000 elements to LMDB``(count:int64) =
    let storage = new LMDBBucketStorage("test", None) :> IBucketStorage
    let count = count
    perf count "Could write N sorted maps with 1000 elements to LMDB" (fun _ ->
      for n in 1L..count do
        let smap = SortedMap()
        for i in 0L..99L do
          smap.Add(i, 
            Math.Round((float (i) + rng.NextDouble()), 4)
            )
            
        storage.Save(n.ToString(), smap) |> Async.RunSynchronously
    )
//    perf count "Could read N sorted maps with 1000 elements from LMDB" (fun _ ->
//      for n in 1L..count do
//        let loaded = storage.Load<int64,float>(n.ToString()) |> Async.RunSynchronously
//        loaded.IsSome |> should equal true
//        for i in 0L..999L do
//          loaded.Value.[i] |> should equal (Math.Round((float (i) / (float (i + 100L))), 4))
//    )
    let usedSize = (storage :?> LMDBBucketStorage).UsedSize
    Console.WriteLine("Used size: " + usedSize.ToString())