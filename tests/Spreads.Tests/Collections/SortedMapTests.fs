// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Tests.Collections

open FsUnit
open NUnit.Framework

open System
open System.Linq
open System.Collections.Generic
open System.Diagnostics
open Spreads
open Spreads.Collections
open System.Threading
open System.Threading.Tasks


[<TestFixture>]
type SortedMapTests() =

    [<Test>]
    member this.``Could create SM of irregular keys and values``() =
        let keys = [|1;2;3;5;6;8;9;15|]
        let values =  [|1;2;3;5;6;8;9;15|]
        let sm = SortedMap.OfSortedKeysAndValues(keys, values)
        Assert.IsTrue(sm.Keys.SequenceEqual(keys))
        Assert.IsTrue(sm.Values.SequenceEqual(values))
        Assert.IsTrue(not sm.IsRegular)
        Assert.IsTrue(sm.keys.Length = keys.Length)
        ()

    [<Test>]
    member this.``Could create SM of regular keys and values``() =
        let keys = [|1;2;3;4;5;6;7;8|]
        let values =  [|1;2;3;4;5;6;7;8|]
        let sm = SortedMap.OfSortedKeysAndValues(keys, values)
        Assert.IsTrue(sm.Keys.SequenceEqual(keys))
        Assert.IsTrue(sm.Values.SequenceEqual(values))
        Assert.IsTrue(sm.IsRegular)
        Assert.IsTrue(sm.keys.Length = 2)
        ()

    [<Test>]
    member this.``Sorted list inserts``() =
        let watch = Stopwatch.StartNew();
        let sm = SortedList<DateTime, float>()
        let lim = 1000000L
        let td = DateTime.Today
        for i in 0L..lim do
            let dt = td.AddTicks(i)
            sm.[dt] <- float(i)
        
        watch.Stop()
        Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

        ()



    [<Test>]
    member this.``Sorted map inserts``() =
        for c in 0..9 do
            let mutable watch = Stopwatch.StartNew();
            let sm = SortedMap<int64, float>()
            let lim = 1000000L
            for i in 0L..lim do
                sm.[i] <- float(i)
        
            watch.Stop()
            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())


            watch <- Stopwatch.StartNew();

            let mutable sum = 0.0
            for i in 0L..lim do
                sum <- sm.[i]
        
            watch.Stop()
            Console.WriteLine(">Reads per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())


            watch <- Stopwatch.StartNew();
            let ptr = sm.GetCursor()
            let mutable sum = 0.0
            for i in 0L..lim do
                ptr.MoveNext() |> ignore
                sum <- ptr.CurrentValue
        
            watch.Stop()
            Console.WriteLine(">Iterations per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())


            ()


    [<Test>]
    member this.``Sorted map indexed reads``() =
        for c in 0..9 do
            let mutable watch = Stopwatch.StartNew();
            let sm = SortedMap<int64, float>()
            let lim = 1000000L
            for i in 0L..lim do
                sm.[i] <- float(i)
        
            watch.Stop()
            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())


            watch <- Stopwatch.StartNew();

            let mutable sum = 0.0
            for i in 0L..lim do
                sum <- sm.values.[int(i)]
        
            watch.Stop()
            Console.WriteLine(">Indexed reads per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())


            watch <- Stopwatch.StartNew();

            let mutable sum = 0.0
            for i in 0L..lim do
                sm.values.[int(i)] <- float(i)
        
            watch.Stop()
            Console.WriteLine(">Indexed sets per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())


            ()

    [<Test>]
    member this.``Dictionary inserts and reads``() =
        for c in 0..9 do
            let mutable watch = Stopwatch.StartNew();
            let sm = Dictionary<int64, float>()
            let lim = 1000000L
            for i in 0L..lim do
                sm.[i] <- float(i)
        
            watch.Stop()
            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())


            watch <- Stopwatch.StartNew();

            let mutable sum = 0.0
            for i in 0L..lim do
                sum <- sm.[i]
        
            watch.Stop()
            Console.WriteLine(">Reads per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

            ()

    [<Test>]
    member this.``Could remove data from SM``() =
        let mutable watch = Stopwatch.StartNew();
        let sm = SortedMap<int64, float>()
        let lim = 100000L
        for i in 0L..lim do
          if i <> 2L then sm.[i] <- float(i) // irregular
        sm.TrimExcess()
        let okf, fs = sm.RemoveFirst()

        let okl, ls = sm.RemoveLast()

        ()

//    [<Test>]
//    member this.``IImmutableOrderedMap reverse``() =
//
//        let mutable watch = Stopwatch.StartNew();
//        let hsm:IImmutableOrderedMap<int,int> = SortedMap<int, int>() :> IImmutableOrderedMap<int,int>
//        let lim = 5120
//        for i = 0 to lim  do
//            (hsm :?> IOrderedMap<int,int>).Add(i,i)
//            //sm.[i] <- sm.Add()
//        
//        watch.Stop()
//        Console.WriteLine(">Mutable inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//        watch <- Stopwatch.StartNew();
//        let mutable hsm:IImmutableOrderedMap<int,int> = SortedMap<int, int>() :> IImmutableOrderedMap<int,int>
//        let lim = 5120
//        for i = 0 to lim  do
//            hsm <- hsm.Add(i,i)
//        
//        watch.Stop()
//        Console.WriteLine(">Immutable inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//        ()

    [<Test>]
    member this.``Nested sorted list``() =
        for c in 0..9 do
            let watch = Stopwatch.StartNew();
            let nsl = SortedList<int64, SortedList<uint16, float>>()
            let lim = 1000000L
            let bucket_size = 512
            for i = int(0L) to  int(lim) do
                let hash = int64(i / bucket_size)
                let idx = i % bucket_size
                let tg = nsl.TryGetValue(hash)
                if fst tg then 
                    (snd tg).[uint16(idx)] <- float(i)
                else 
                    let newSm = SortedList()
                    newSm.[uint16(idx)] <- float(i)
                    nsl.[hash] <- newSm
        
            watch.Stop()
            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

            ()

    [<Test>]
    member this.``Nested sorted map``() =
        for c in 0..9 do
            let watch = Stopwatch.StartNew();
            let nsl = SortedMap<int64, SortedMap<uint16, float>>()
            let lim = 1000000L
            let bucket_size = 512
            for i = int(0L) to  int(lim) do
                let hash = int64(i / bucket_size)
                let idx = i % bucket_size
                let tg = nsl.TryGetValue(hash)
                if fst tg then 
                    (snd tg).[uint16(idx)] <- float(i)
                else 
                    let newSm = SortedMap()
                    newSm.[uint16(idx)] <- float(i)
                    nsl.[hash] <- newSm
        
            watch.Stop()
            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

            ()
