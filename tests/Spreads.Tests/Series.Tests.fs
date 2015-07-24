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

        let ok, value = rc.TryGetValue(2)
        ok |> should equal true
        (repeated :> IReadOnlyOrderedMap<_,_>).TryGetValue(2) |> snd |> should equal 1
        value |> should equal 1

        let ok, value = rc.TryGetValue(4)
        ok |> should equal true
        (repeated :> IReadOnlyOrderedMap<_,_>).TryGetValue(4) |> snd |> should equal 3
        value |> should equal 3

        let ok, value = rc.TryGetValue(6)
        ok |> should equal true
        (repeated :> IReadOnlyOrderedMap<_,_>).TryGetValue(6) |> snd |> should equal 5
        value |> should equal 5

        let ok, value = rc.TryGetValue(123)
        ok |> should equal true
        (repeated :> IReadOnlyOrderedMap<_,_>).TryGetValue(123) |> snd |> should equal 7
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

        let add1 = map.Add(1)

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

        let repeated = map.Repeat().Add(1)
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

    [<Test>]
    member this.``Could Zip equal series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)
        
        let zipped = map.Zip(map, fun v v2 -> v + v2)
        zipped.Count() |> should equal 4

        for kvp in zipped do
          Console.WriteLine(kvp.Value.ToString())
          kvp.Value |> should equal (kvp.Key * 2)
        ()

    [<Test>]
    member this.``Could Zip unequal series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let map2 = SortedMap<int, int>()
        map2.Add(1,1)
        map2.Add(3,3)
        map2.Add(7,7)

        let zipped = map.Zip(map2, fun v v2 -> v + v2)
        zipped.Count() |> should equal 3
        for kvp in zipped do
          Console.WriteLine(kvp.Value.ToString())
          kvp.Value |> should equal (kvp.Key * 2)
        ()

    [<Test>]
    member this.``Could Zip unequal repeated series``() =
        let map = SortedMap<int, int>()
        map.Add(1,1)
        map.Add(3,3)
        map.Add(5,5)
        map.Add(7,7)

        let map2 = SortedMap<int, int>()
        map2.Add(1,1)
        map2.Add(3,3)
        map2.Add(7,7)

        let zipped = map.Zip(map2.Repeat(), fun v v2 -> v + v2)
        zipped.Count() |> should equal 4
        for kvp in zipped do
          Console.WriteLine(kvp.Value.ToString())
          kvp.Value |> should equal (if kvp.Key = 5 then 5 + 3 else  (kvp.Key * 2))
        ()


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
        let scan2 = scan.Evaluate()

        scan2.[1] |> should equal expected.[1]
        scan2.[3] |> should equal expected.[3]
        scan2.[5] |> should equal expected.[5]
        scan2.[7] |> should equal expected.[7]

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
        let lag = lagged.Evaluate()

        lag.[5] |> should equal expected.[5]
        lag.[7] |> should equal expected.[7]

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



//        
//    [<Test>]
//    member this.``Could get moving window from seq``() =
//        let lim = 100000L
//        let MovingLength n s =
//            Seq.windowed n s
//            |> Seq.map Array.length
//
//        let input =  [1L..lim]
//        let mutable res = 0
//
//        let watch1 = Stopwatch.StartNew()
//        let avgs = MovingLength 20 (Seq.map float input)
//        for avg in avgs do
//            res <- res + avg
//        watch1.Stop()
//        Console.WriteLine(">Windowed seq, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//

//    [<Test>]
//    member this.``Could Add``() =
//        let lim = 1000000L
//        let mutable s = SeriesOld<int64, int64>.Empty
//        let watch1 = Stopwatch.StartNew()
//        do
//            for i in 0L..lim do
//                s <- s.Add(i, i)
//        watch1.Stop()
//        //Console.WriteLine(res1)
//        Console.WriteLine(">Additions, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//
//
//
//    [<Test>]
//    member this.``Could Add last``() =
//        let lim = 100000L
//        let mutable s = SeriesOld<int64, int64>.Empty
//        let watch1 = Stopwatch.StartNew()
//        do
//            for i in 0L..lim do
//                s <- s.AddLast(i, i)
//        watch1.Stop()
//        //Console.WriteLine(res1)
//        Console.WriteLine(">Additions to last, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//
//    
//    [<Test>]
//    member this.``Could get moving window with MoveAt``() =
//        let lim = 100000L
//        let mutable s = SeriesOld<int64, int64>.Empty
//        do
//            for i in 0L..lim do
//                s <- s.AddLast(i, i)
//
//        let mutable res1 = 0L
//
//        /// Functional pointer
//        let watch1 = Stopwatch.StartNew()
//        let w = (new Window<int64, int64, _>(s)).WithSkip(0L).WithTake(-20L)
//        for i in 20L..lim do
//            w.MoveAt(i, LookupDirection.EQ) |> ignore
//            res1 <- res1 + int64(w.CurrentKey)
//        watch1.Stop()
//        //Console.WriteLine(res1)
//        Console.WriteLine(">Windows with MoveAt, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())


//    [<Test>]
//    member this.``Could get moving window with MoveNext``() =
//        let lim = 100000L
//        let mutable s = SeriesOld<int64, int64>.Empty
//        do
//            for i in 0L..lim do
//                s <- s.AddLast(i, i)
//
//        let mutable res1 = 0L
//        /// Functional pointer
//        let watch1 = Stopwatch.StartNew()
//        let len = 20L
//        let w = (new Window<int64, int64, _>(s)).WithSkip(0L).WithTake(-len)
//        w.MoveAt(len, LookupDirection.EQ) |> ignore
//        for i in len..lim do
//            res1 <- res1 + int64(w.CurrentKey)
//            w.MoveNext() |> ignore
//        watch1.Stop()
//        //Console.WriteLine(res1)
//        Console.WriteLine(">Windows with MoveNext, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())


//    [<Test>]
//    member this.``Could calculate moving sum of moving sum with window``() =
//        let lim = 100000L
//        let a = id
//        //let mutable s = SeriesOld<int64, int64>(Func<int64,int64>id, Func<int64,int64>id)
//        let mutable s = Series<int64, int64>()
//
//        do
//            for i in 0L..lim do
//                //s <- s.AddLast(i, i)
//                (s :> IOrderedMap<int64, int64>).Add(i, i)
//
//        let mutable res1 = 0L
//        /// Functional pointer
//        let watch1 = Stopwatch.StartNew()
//        let len = 20L
//        let ms = msum s len //((msum s len)) len //cached
//        //let ptr = (ms :?> Window<int64, int64, int64>) :> IPointer<int64, int64>
//        for i in (2L*len)..lim do
//            //ptr.MoveAt(i, LookupDirection.EQ) |> ignore
//            res1 <- res1 + ms.[i].Present
//        watch1.Stop()
//        Console.WriteLine(">Moving sum, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//        
//        let watch2 = Stopwatch.StartNew()
//        for i in (2L*len)..lim do
//            //ptr.MoveAt(i, LookupDirection.EQ) |> ignore
//            res1 <- res1 + ms.[i].Present
//        watch2.Stop()
//        Console.WriteLine(">Cached moving sum, per sec: " + (1000L * int64(lim)/watch2.ElapsedMilliseconds).ToString())
//        
//
//
//    [<Test>]
//    member this.``Could repeat``() =
//        let lim = 100000L
//        let a = id
//        let mutable s = SeriesOld<int64, int64>(Func<int64,int64>id, Func<int64,int64>id)
//        
//        s <- s.Add(10L, 10L)
//
//        let rps = rp(s)
//
//        let res = rps.[10L]
//
//        res.Present |> should equal 10L
//
//        ()
//
//    [<Test>]
//    member this.``Could repeat back``() =
//        let lim = 100000L
//        let a = id
//        let mutable s = SeriesOld<int64, int64>(Func<int64,int64>id, Func<int64,int64>id)
//        
//        s <- s.Add(10L, 10L)
//
//        let rpbs = rpb(s)
//
//        let res = rpbs.[5L]
//
//        res.Present |> should equal 10L
//
//        ()
//
//    [<Test>]
//    member this.``Could repeat-add``() =
//        let lim = 100000L
//        let a = id
//        let mutable s = SeriesOld<int64, int64>(Func<int64,int64>id, Func<int64,int64>id)
//        
//        s <- s.Add(10L, 10L)
//
//        let rps = addscalar (downcast rp(s)) 5L
//
//        let res = rps.[14L]
//
//        res.Present |> should equal 15L // repeated 10 add added 5
//
//        ()
//
//
//    
//
//    [<Test>]
//    member this.``Could zip consequtive series``() =
//        let lim = 1000000L
//        let a = id
//        let mutable l = SeriesOld<int64, int64>(Func<int64,int64>id, Func<int64,int64>id)
//        let mutable r = SeriesOld<int64, int64>(Func<int64,int64>id, Func<int64,int64>id)
//
//        for i in 1L..lim do
//            l <- l.AddLast(i, i)
//            r <- r.AddLast(i, i)
//
//        let watch = Stopwatch.StartNew()
//        let res = SeriesOld.zip (+) (l) (r)
//        watch.Stop()
//        Console.WriteLine(">Zip IntMap consequtive, per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//        
//        let rpl = rp l
//        let rpr = rp r
//
//        let watch1 = Stopwatch.StartNew()
//        let res1 = SeriesOld.zip (+) (rpl) (rpr)
//        watch1.Stop()
//        Console.WriteLine(">Zip repeated consequtive, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//        
//        
//        let watch2 = Stopwatch.StartNew()
//        let res1 = SeriesOld.zip (+) (rpl) (rpr)
//        watch1.Stop()
//        Console.WriteLine(">Zip repeated cached consequtive, per sec: " + (1000L * int64(lim)/watch2.ElapsedMilliseconds).ToString())
//        
//        
//
//    [<Test>]
//    member this.``Could zip irregular series``() =
//        let mutable l = SeriesOld<int, int>()
//                            .Add(1, 1).Add(6,6).Add(8,8).Add(9,9).Add(12,12)
//        let mutable r = SeriesOld<int, int>()
//                            .Add(3, 3).Add(9,9).Add(14,14).Add(18,18)
//
//         
//         // l + r
//        let res = (SeriesOld.zip (+) (l) (r)) :?> SeriesOld<int, int>
//        res.Size |> should equal 1L
//        res.[9].Present |> should equal 18L
//
//        // rp(l) + rp(r)
//        let res1 = (SeriesOld.zip (+) (rp l) (rp r)) :?> SeriesOld<int, int>
//        res1.Size |> should equal 7L
//        res1.First.Present.Value.Present |> should equal 4L
//        res1.Last.Present.Value.Present |> should equal 30L
//        res1.[1].IsPresent |> should be False
//        res1.[3].Present |> should equal 4L
//        res1.[6].Present |> should equal 9L
//        res1.[8].Present |> should equal 11L
//        res1.[9].Present |> should equal 18L
//        res1.[12].Present |> should equal 21L
//        res1.[14].Present |> should equal 26L
//        res1.[18].Present |> should equal 30L
//
//        // rp(l) + r
//        let res2 = (SeriesOld.zip (+) (rp l) (r)) :?> SeriesOld<int, int>
//        res2.Size |> should equal 4L
//        res2.First.Present.Value.Present |> should equal 4L
//        res2.Last.Present.Value.Present |> should equal 30L
//        res2.[1].IsPresent |> should be False
//        res2.[3].Present |> should equal 4L
//        res2.[9].Present |> should equal 18L
//        res2.[14].Present |> should equal 26L
//        res2.[18].Present |> should equal 30L
//
//        // l + rp(r)
//        let res3 = (SeriesOld.zip (+) (l) (rp r)) :?> SeriesOld<int, int>
//        res3.Size |> should equal 4L
//        res3.First.Present.Value.Present |> should equal 9L
//        res3.Last.Present.Value.Present |> should equal 21L
//        res3.[1].IsPresent |> should be False
//        res3.[6].Present |> should equal 9L
//        res3.[8].Present |> should equal 11L
//        res3.[9].Present |> should equal 18L
//        res3.[12].Present |> should equal 21L
//
//        Console.WriteLine(">Zipped irregular series")
//
//
//
//    [<Test>]
//    member this.``Could zip consequtive series using Combination``() =
//
//        let lim = 100000L
//        let mutable l = SeriesOld<int64, int64>()
//        let mutable r = SeriesOld<int64, int64>()
//        //    let mutable l = SeriesOld<int64, int64>.Empty :> BaseSeries<int64, int64>
//        //    let mutable r = SeriesOld<int64, int64>.Empty :> BaseSeries<int64, int64>
//        do
//            for i in 1L..lim do
//                l <- l.AddLast(i, i)
//                r <- r.AddLast(i, i)
//
//        //        l <- cached(ls)
//        //        r <- cached(rs)
//
//            ()
//
//
//        let inline sumCombine series = 
//            let c = Combination(Seq.sum, series) 
//            c
//        
//
//        let watch = Stopwatch.StartNew()
//        let res = sumCombine([l; r;])// l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r])
//        watch.Stop()
////        for i in 1L..lim do
////            res.Result.[i].Present |> should equal (i*2L)//384L)
//        Console.WriteLine(">Zip IntMap consequtive, per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//        
//
//        // won't work now, should work for spreads where reference to series in changed
//        // with every addition
////        l <- l.Add(lim + 1L, lim + 1L)
////        r <- r.Add(lim + 1L, lim + 1L)
////        res.Calculate(lim + 1L)
////        res.Result.[lim + 1L].Value |> should equal ((lim + 1L)*4L)
//
//
//        let rpl = rp l
//        let rpr = rp r
//
//        let watch1 = Stopwatch.StartNew()
//        let res1 = sumCombine([(rpl); (rpr);])// (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);])
//        watch1.Stop()
////        for i in 1L..lim do
////            res1.Result.[i].Value |> should equal (i*4L)
//        Console.WriteLine(">Zip repeated consequtive, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//        
//
//
//        let watch2 = Stopwatch.StartNew()
//        let res2 = sumCombine( [(rpl); (rpr);])// (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);])
//        watch2.Stop()
////        for i in 1L..lim do
////            res1.Result.[i].Value |> should equal (i*4L)
//        Console.WriteLine(">Zip repeated consequtive cached, per sec: " + (1000L * int64(lim)/watch2.ElapsedMilliseconds).ToString())
////        
//
//
//
//
//    [<Test>]
//    member this.``Could join SCGs``() =
//
//
//        let lim = 100000L
//        let mutable l = System.Collections.Generic.Dictionary<int64, int64>()
//        let mutable r = System.Collections.Generic.SortedList<int64, int64>()
//
//        for i in 1L..lim do
//            l.Add(i, i)
//            r.Add(i, i)
//
//        
//
//        let watch = Stopwatch.StartNew()
//        l <- l.Join(r, (fun o -> o.Key), (fun i -> i.Key), (fun l r -> l.Value + r.Value )).ToDictionary((fun x -> x), (fun x -> x)) // sumCombine([l; r; l; r; ]) // l; r; l; r; l; r; l; r])
////        l <- l.Join(r, (fun o -> o.Key), (fun i -> i.Key), (fun l r -> l.Value + r.Value )).ToDictionary((fun x -> x), (fun x -> x)) // sumCombine([l; r; l; r; ]) // l; r; l; r; l; r; l; r])
////        l <- l.Join(r, (fun o -> o.Key), (fun i -> i.Key), (fun l r -> l.Value + r.Value )).ToDictionary((fun x -> x), (fun x -> x)) // sumCombine([l; r; l; r; ]) // l; r; l; r; l; r; l; r])
//        r <- System.Collections.Generic.SortedList<int64, int64>(l)
//        watch.Stop()
////        for i in 1L..lim do
////            res.Result.[i].Value |> should equal (i*4L)
//        Console.WriteLine(">LINQ Join, per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//        
//
//        
//        
//    [<Test>]
//    member this.``Could zip consequtive spreads using Combination``() =
//
//        let lim = 100000L
//        let mutable l = new Spread<int64, int64>()
//        let mutable r = new Spread<int64, int64>()
//        //    let mutable l = SeriesOld<int64, int64>.Empty :> BaseSeries<int64, int64>
//        //    let mutable r = SeriesOld<int64, int64>.Empty :> BaseSeries<int64, int64>
//        do
//            for i in 0L..lim do
//                l.AddLast(i, i)
//
//                r.AddLast(i, i)
//
//        //        l <- cached(ls)
//        //        r <- cached(rs)
//
//            ()
//
//
//        let inline sumCombine series = 
//            let c = Combination(Seq.sum, series) 
//            c
//        
//
//        let watch = Stopwatch.StartNew()
//        let res = sumCombine([l; r;])// l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r;l; r; l; r; l; r; l; r; l; r; l; r])
//        watch.Stop()
//        //for i in 1L..lim do
//        //    res.[i].Present |> should equal (i*2L)//384L)
//        Console.WriteLine(">Zip Spreads consequtive, per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//        
//









//        // won't work now, should work for spreads where reference to series in changed
//        // with every addition
////        l <- l.Add(lim + 1L, lim + 1L)
////        r <- r.Add(lim + 1L, lim + 1L)
////        res.Calculate(lim + 1L)
////        res.Result.[lim + 1L].Value |> should equal ((lim + 1L)*4L)
//
//
//        let rpl = rp l
//        let rpr = rp r
//
//        let watch1 = Stopwatch.StartNew()
//        let res1 = sumCombine([(rpl); (rpr);])// (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);(rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rpl); (rpr); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r); (rp l); (rp r);])
//        watch1.Stop()
////        for i in 1L..lim do
////            res1.Result.[i].Value |> should equal (i*4L)
//        Console.WriteLine(">Zip repeated consequtive, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//        
//
//
//        let watch2 = Stopwatch.StartNew()
//        let res2 = sumCombine( [(rpl); (rpr);])// (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);(rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr); (rpl); (rpr);])
//        watch2.Stop()
////        for i in 1L..lim do
////            res1.Result.[i].Value |> should equal (i*4L)
//        Console.WriteLine(">Zip repeated consequtive cached, per sec: " + (1000L * int64(lim)/watch2.ElapsedMilliseconds).ToString())
//        


