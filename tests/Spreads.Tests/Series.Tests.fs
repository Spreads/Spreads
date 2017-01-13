// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Tests.Series


open FsUnit
open NUnit.Framework

open System
open System.Linq
open System.Collections.Generic
open System.Diagnostics
open Spreads
open Spreads.Collections
open System.Threading

type SeriesTestsModule() =
    
    // TODO (low) add edge cases to the contract suite if there are some new here

    [<Test>]
    member this.``SM Could TryFind and MoveAt with single value``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)

                // at existing inclusive
        
        let ok, value = map.TryFind(1, Lookup.GE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.GE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        let ok, value = map.TryFind(1, Lookup.LE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.LE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1


        // at existing exclusive
        let ok, value = map.TryFind(1, Lookup.GT);
        ok |> should equal false
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.GT)
        moved |> should equal false

        let ok, value = map.TryFind(1, Lookup.LT);
        ok |> should equal false
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.LT)
        moved |> should equal false


        // before existing to its direction
        let ok, value = map.TryFind(0, Lookup.GT);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(0, Lookup.GT)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        let ok, value = map.TryFind(0, Lookup.GE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(0, Lookup.GE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        // after existing to its direction
        let ok, value = map.TryFind(2, Lookup.LE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(2, Lookup.LE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        let ok, value = map.TryFind(2, Lookup.LT);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(2, Lookup.LT)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

    [<Test>]
    member this.``SCM Could TryFind and MoveAt with single value``() =
        let map = SortedChunkedMap<int, int>()
        map.Add(1,1)

        // at existing inclusive
        
        let ok, value = map.TryFind(1, Lookup.GE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.GE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        let ok, value = map.TryFind(1, Lookup.LE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.LE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1


        // at existing exclusive
        let ok, value = map.TryFind(1, Lookup.GT);
        ok |> should equal false
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.GT)
        moved |> should equal false

        let ok, value = map.TryFind(1, Lookup.LT);
        ok |> should equal false
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(1, Lookup.LT)
        moved |> should equal false


        // before existing to its direction
        let ok, value = map.TryFind(0, Lookup.GT);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(0, Lookup.GT)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        let ok, value = map.TryFind(0, Lookup.GE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(0, Lookup.GE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        // after existing to its direction
        let ok, value = map.TryFind(2, Lookup.LE);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(2, Lookup.LE)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

        let ok, value = map.TryFind(2, Lookup.LT);
        ok |> should equal true
        value.Value |> should equal 1
        let cursor = map.GetCursor()
        let moved = cursor.MoveAt(2, Lookup.LT)
        moved |> should equal true
        cursor.CurrentValue |> should equal 1

            
    [<Test>]
    member this.``Could repeat series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let repeated = map.Repeat()
        let rc = repeated.GetCursor()
        rc.MoveAt(5, Lookup.EQ) |> ignore

        let ok, value = rc.TryGetValue(2)
        ok |> should equal true
        (repeated :> IReadOnlySeries<_,_>).TryGetValue(2) |> snd |> should equal 1
        value |> should equal 1

        let ok, value = rc.TryGetValue(4)
        ok |> should equal true
        (repeated :> IReadOnlySeries<_,_>).TryGetValue(4) |> snd |> should equal 3
        value |> should equal 3

        let ok, value = rc.TryGetValue(6)
        ok |> should equal true
        (repeated :> IReadOnlySeries<_,_>).TryGetValue(6) |> snd |> should equal 5
        value |> should equal 5

        let ok, value = rc.TryGetValue(123)
        ok |> should equal true
        (repeated :> IReadOnlySeries<_,_>).TryGetValue(123) |> snd |> should equal 7
        value |> should equal 7

        let ok, value = rc.TryGetValue(0)
        ok |> should equal false

    [<Test>]
    member this.``Could Add1 to series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let add1 = map + 1

        let rc = add1.GetCursor()

        let sl = SortedList()
        sl.Add(1,1)
        let ok, value = sl.TryGetValue(1)

        let ok, value = rc.TryGetValue(1)
        ok |> should equal true
        value |> should equal 2

        let ok, value = rc.TryGetValue(3)
        ok |> should equal true
        value |> should equal 4

        let ok, value = rc.TryGetValue(5)
        ok |> should equal true
        value |> should equal 6

        let ok, value = rc.TryGetValue(7)
        ok |> should equal true
        value |> should equal 8

        let ok, value = rc.TryGetValue(10)
        ok |> should equal false


    [<Test>]
    member this.``Could Add1 to repeated series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)
        
        let repeated = map.Repeat() + 1
        let rc = repeated.GetCursor()

        let ok, value = rc.TryGetValue(2)
        ok |> should equal true
        value |> should equal 2

        let ok, value = rc.TryGetValue(4)
        ok |> should equal true
        value |> should equal 4

        let ok, value = rc.TryGetValue(6)
        ok |> should equal true
        value |> should equal 6

        let ok, value = rc.TryGetValue(123)
        ok |> should equal true
        value |> should equal 8

        let ok, value = rc.TryGetValue(0)
        ok |> should equal false

//    [<Test>]
//    member this.``Could Zip equal series``() =
//        let map = SortedMap<int, int>()
//        map.Add(1,1)
//        map.Add(3,3)
//        map.Add(5,5)
//        map.Add(7,7)
//        
//        let zipped = map.Zip(map, fun v v2 -> v + v2)
//        zipped.Count() |> should equal 4
//
//        for kvp in zipped do
//          Console.WriteLine(kvp.Value.ToString())
//          kvp.Value |> should equal (kvp.Key * 2)
//        ()
//
//    [<Test>]
//    member this.``Could Zip unequal series``() =
//        let map = SortedMap<int, int>()
//        map.Add(1,1)
//        map.Add(3,3)
//        map.Add(5,5)
//        map.Add(7,7)
//
//        let map2 = SortedMap<int, int>()
//        map2.Add(1,1)
//        map2.Add(3,3)
//        map2.Add(7,7)
//
//        let zipped = map.Zip(map2, fun v v2 -> v + v2)
//        zipped.Count() |> should equal 3
//        for kvp in zipped do
//          Console.WriteLine(kvp.Value.ToString())
//          kvp.Value |> should equal (kvp.Key * 2)
//        ()
//
//    [<Test>]
//    member this.``Could Zip unequal repeated series``() =
//        let map = SortedMap<int, int>()
//        map.Add(1,1)
//        map.Add(3,3)
//        map.Add(5,5)
//        map.Add(7,7)
//
//        let map2 = SortedMap<int, int>()
//        map2.Add(1,1)
//        map2.Add(3,3)
//        map2.Add(7,7)
//
//        let zipped = map.Zip(map2.Repeat(), fun v v2 -> v + v2)
//        zipped.Count() |> should equal 4
//        for kvp in zipped do
//          Console.WriteLine(kvp.Value.ToString())
//          kvp.Value |> should equal (if kvp.Key = 5 then 5 + 3 else  (kvp.Key * 2))
//        ()


    [<Test>]
    member this.``Could fold series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)
        let folder : Func<int,int,int,int>  = Func<int,int,int,int> (fun st k v -> st + v)
        let sum = map.Fold(0, folder)
        sum |> should equal (map.Values.Sum())


    [<Test>]
    member this.``Could scan series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let expected = SortedMap<int, int>()
        expected.Add(1,1)
        expected.Add(3,4)
        expected.Add(5,9)
        expected.Add(7,16)

        let folder : Func<int,int,int,int>  = Func<int,int,int,int> (fun st k v -> st + v)
        let scan = map.Scan(0, folder)
        //let scan2 = scan.Evaluate()

        scan.[7] |> should equal expected.[7]
        scan.[1] |> should equal expected.[1]
        scan.[3] |> should equal expected.[3]
        scan.[5] |> should equal expected.[5]
        

        scan.Last.Value |> should equal expected.[7]
        let sc = scan.GetCursor()
        sc.MoveAt(5, Lookup.EQ) |> ignore
        sc.CurrentValue |> should equal expected.[5]



    [<Test>]
    member this.``Could get series lag``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let expected = SortedMap<int, int>()
        expected.Add(5,1)
        expected.Add(7,3)
        let lagged = map.Lag(2u)
        let lag = lagged.ToSortedMap()

        lag.[5] |> should equal expected.[5]
        lag.[7] |> should equal expected.[7]


    [<Test>]
    member this.``Could get series ZipLag``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let expected = SortedMap<int, int>()
        expected.Add(3,4)
        expected.Add(5,8)
        expected.Add(7,12)
        let lagged = map.ZipLag(1u, fun c p -> c + p)
        let lag = lagged.ToSortedMap()

        lag.[3] |> should equal expected.[3]
        lag.[5] |> should equal expected.[5]
        lag.[7] |> should equal expected.[7]

    [<Test>]
    member this.``Could apply ZipLag``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let lagged = map.Map(fun x -> x).ZipLag(1u, fun c p -> (c - p)*c )
        let lag = lagged.ToSortedMap()

        lag.[3] |> should equal (2*3)
        lag.[5] |> should equal (2*5)
        lag.[7] |> should equal (2*7)

    [<Test>]
    member this.``Could apply ZipLag without evaluation``() =
      let sm = new SortedMap<DateTime, float>()

      sm.Add(DateTime(2013, 11, 01),1.0)
      sm.Add(DateTime(2013, 11, 03),1.0)
      sm.Add(DateTime(2013, 11, 04),-1.0)
      sm.Add(DateTime(2013, 11, 06),1.0)
      sm.Add(DateTime(2013, 11, 07),-1.0)
      sm.Add(DateTime(2013, 11, 08),1.0)
      sm.Add(DateTime(2013, 11, 10),1.0)
      let lazyMapSeries = sm.Map(fun x->x)
      
      let lazyDiff = lazyMapSeries.ZipLag(1u, fun c p -> c-p)
      let eagerDiff=sm.ZipLag(1u, fun c p -> c-p)

      //eagerDiff.Count
      for kvp in eagerDiff do
        printfn "%f" kvp.Value

      printfn ""

      for kvp in lazyDiff do
        printfn "%f" kvp.Value

    [<Test>]
    member this.``Could calculate lagged moving maximum``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)
        map.Add(9,9)
        map.Add(11,11)

        let mMaxLag = 
          map
            .Window(uint32 2, 1u, false)
            .Map(fun (inner:Series<int,int>) -> inner.Values.Max()) // .Fold(0, (fun st x -> if x >= st then x else st)))
            .Lag(uint32 1u)
            
        let mMaxLagEval = mMaxLag.ToSortedMap()

        mMaxLag.[5] |> should equal (3)
        mMaxLag.[7] |> should equal (5)
        mMaxLag.[9] |> should equal (7)
        mMaxLag.[11] |> should equal (9)

    [<Test>]
    member this.``Could get series range``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let range = map.Range(3, 5)
        let range2 = range.ToSortedMap()
        range2.Count |> should equal 2
        range2.[3] |> should equal map.[3]
        range2.[5] |> should equal map.[5]
        
        let range = map.Range(0, 2)
        let range2 = range.ToSortedMap()
        range2.Count |> should equal 1
        range2.[1] |> should equal map.[1]

        let range = map.Range(0, 3)
        let range2 = range.ToSortedMap()
        range2.Count |> should equal 2
        range2.[1] |> should equal map.[1]
        range2.[3] |> should equal map.[3]

        let range = map.Range(0, 6)
        let range2 = range.ToSortedMap()
        range2.Count |> should equal 3
        range2.[1] |> should equal map.[1]
        range2.[3] |> should equal map.[3]
        range2.[5] |> should equal map.[5]

        let range = map.Range(6, 10)
        let range2 = range.ToSortedMap()
        range2.Count |> should equal 1
        range2.[7] |> should equal map.[7]

        let range = map.Range(5, 10)
        let range2 = range.ToSortedMap()
        range2.Count |> should equal 2
        range2.[5] |> should equal map.[5]
        range2.[7] |> should equal map.[7]

        let range = map.Range(0, 10)
        let range2 = range.ToSortedMap()
        range2.Count |> should equal 4
        range2.[1] |> should equal map.[1]
        range2.[3] |> should equal map.[3]
        range2.[5] |> should equal map.[5]
        range2.[7] |> should equal map.[7]

    [<Test>]
    member this.``Could get series windows``() =
        let map = SortedMap<int, int>()
        for i in 0..20 do
          map.Add(i,i)

        let expected = SortedMap<int, int>()
        expected.Add(5,1)
        expected.Add(7,3)

        let windows = map.Window(4u, 2u)
        let windows2 = windows.ToSortedMap()

        windows.First.Value.Count() |> should equal 4
        windows.First.Key |> should equal 3


    [<Test>]
    member this.``Could get series incomplete windows``() =
        let map = SortedMap<int, int>()
        for i in 0..9 do
          map.Add(i,1)

        let expected = SortedMap<int, int>()
        expected.Add(0,1)
        expected.Add(1,2)
        expected.Add(2,3)
        expected.Add(3,4)
        expected.Add(4,4)
        expected.Add(5,4)
        expected.Add(6,4)
        expected.Add(7,4)
        expected.Add(8,4)
        expected.Add(9,4)

        let windows = map.Window(4u, 1u, true)
        let windows2 = windows.Map(fun inner -> inner.Values.Sum()).ToSortedMap() :> _ seq
        
        Assert.IsTrue(expected.SequenceEqual(windows2))




    [<Test>]
    member this.``Mutable local capture``() =
        let data = new SortedMap<DateTime, double>()
        let count = 5000

        for i in 0..count-1 do
            data.Add(DateTime.UtcNow.Date.AddSeconds(float i), float i)
        
        let mutable sign = 1.0
        let mutable rList = new List<double>()
        let testList = ref rList
        let getSign s () = s
        // there was a strage behavior that looked like a local mutable was captured twice
        // cannot reproduce it here
        let mutable runnningSum = data.Zip(data, (fun d1 d2 -> d1 + d2)).Scan(0.0, fun st k v -> 
            let getSign' = getSign sign
            if st > 100.0 && getSign'() > 0.0 then
                sign <- -1.0
            elif st < -100.0 && getSign'() < 0.0 then
                sign <- 1.0
                rList.Add(sign)
                rList.Sort()
            if sign > 0.0 then st + v
            else st - v
        )

        Assert.AreEqual(runnningSum.Count(), count)
