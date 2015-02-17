namespace Spreads.Tests.Experimental

//open System
//open NUnit.Framework
//open FsUnit
//open Spreads
//open Spreads.Collections
//open Spreads.Collections.Experimental
//
//module Buckets =
//
//  let mutable rng = new Random(0)
//  
//  [<Test>]
//  let ``Could serialize and deserialize maps``() =
//    let mutable bytes : byte[] = null
//    let mutable zippedBytes : byte[] = null
//    perf 1L "Could serialize a map" (fun _ ->
//      let smap = SortedMap()
//      for i in 0L..2999L do
//        smap.Add(i, float (i) / 10.0)
//      bytes <-  Serialization.serializeSortedMap None smap
//      
//    )
//    perf 1L "Could deserialize a map " (fun _ ->
//      let dm = Serialization.deserializeSortedMap<int64,float> None bytes
//      for i in 0L..999L do
//        dm.[i] |> should equal (float (i) / 10.0)
//    )
//   
//    Console.WriteLine("----------------")
//
//  [<Test>]
//  let ``Could serialize and deserialize maps with diff``() =
//    let mutable bytes : byte[] = null
//    let mutable zippedBytes : byte[] = null
//    perf 1L "Could serialize a map" (fun _ ->
//      let smap = SortedMap()
//      for i in 0L..999L do
//        smap.Add(i, float (i) / 10.0)
//      bytes <-  Serialization.serializeSortedMap None smap     
//    )
//    perf 1L "Could deserialize a map " (fun _ ->
//      let dm = Serialization.deserializeSortedMap<int64,float> None bytes
//      for i in 0L..999L do
//        dm.[i] |> should equal (float (i) / 10.0)
//    )
//   
//    Console.WriteLine("----------------")
//
//
//  [<TestCase(10000)>]
//  let ``Could write N sorted maps with 1000 elements to LMDB``(count:int64) =
//    let storage = new LMDBBucketStorage("test", None) :> IBucketStorage
//    let count = count
//    let size = 999L
//    perf count "Could write N sorted maps with 1000 elements to LMDB" (fun _ ->
//      for n in 1L..count do
//        let mutable previous = 10.0
//        let smap = SortedMap()
//        for i in 0L..size do
//          previous <- Math.Round(previous * (1.0 + rng.NextDouble()*0.01 - 0.004), 3)
//          smap.Add(i, previous)
//          
//        storage.Save(n.ToString(), smap) |> Async.RunSynchronously
//    )
//    rng <- new Random(0)
//    perf count "Could read N sorted maps with 1000 elements from LMDB" (fun _ ->
//      for n in 1L..count do
//        let loaded = storage.Load<int64,float>(n.ToString()) |> Async.RunSynchronously
//        loaded.IsSome |> should equal true
////        for i in 0L..999L do
////          loaded.Value.[i] |> should equal (Math.Round((float (i) + rng.NextDouble()), 4))
//    )
//    let usedSize = (storage :?> LMDBBucketStorage).UsedSize
//    let originalSize = count * size * 16L
//    Console.WriteLine("Used size: " + usedSize.ToString())
//    Console.WriteLine("Original size: " + originalSize.ToString())
//    Console.WriteLine("Saving: " + ((float usedSize)/(float originalSize) - 1.0).ToString())