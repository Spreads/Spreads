namespace Spreads.Tests.Experimental

open System
open NUnit.Framework
open FsUnit
open Spreads
open Spreads.Collections
open Spreads.Collections.Experimental

module Buckets =

  [<TestCase(100000)>]
  let ``Could store N sorted maps with 1000 elements to MMDic``(count:int64) =
    let storage = MMBucketStorage.Instance :> IBucketStorage<int64,int64>
    let count = count
    perf count "Could write N sorted maps with 1000 elements to MMDic" (fun _ ->
      for n in 1L..count do
        let smap = SortedMap()
        for i in 0L..999L do
          smap.Add(i, i)
        storage.Save(n.ToString(), smap)
    )
    perf count "Could read N sorted maps with 1000 elements to MMDic" (fun _ ->
      for n in 1L..count do
        let loaded = storage.Load(n.ToString())
        loaded.IsPresent |> should equal true
        for i in 0L..999L do
          loaded.Present.[i] |> should equal i
    )
   
    Console.WriteLine("----------------")