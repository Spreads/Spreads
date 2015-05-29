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
            let ptr = sm.GetPointer()
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
    member this.``IImmutableOrderedMap reverse``() =

        let mutable watch = Stopwatch.StartNew();
        let hsm:IImmutableOrderedMap<int,int> = SortedMap<int, int>() :> IImmutableOrderedMap<int,int>
        let lim = 5120
        for i = 0 to lim  do
            (hsm :?> IOrderedMap<int,int>).Add(i,i)
            //sm.[i] <- sm.Add()
        
        watch.Stop()
        Console.WriteLine(">Mutable inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

        watch <- Stopwatch.StartNew();
        let mutable hsm:IImmutableOrderedMap<int,int> = SortedMap<int, int>() :> IImmutableOrderedMap<int,int>
        let lim = 5120
        for i = 0 to lim  do
            hsm <- hsm.Add(i,i)
        
        watch.Stop()
        Console.WriteLine(">Immutable inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

        ()

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
    
//    [<Test>]
//    member this.``Nested sorted list - optimized``() =
//        for c in 0..19 do
//            let watch = Stopwatch.StartNew();
//            let nsl = SortedList<int64, SortedList<uint16, float>>()
//            let lim = 1000000L
//            let bucket_size = 256
//            let hc = Int64SortableHasher(512us)
//            let mutable prevHash = 0L
//            let mutable prevBucket = null
//            for i =  int(0L) to int(lim)  do
//                let hash = hc.Hash(int64(i)) // int64(i / bucket_size)
//                let idx = hc.BucketIndex(int64(i)) // i % bucket_size
//                let bucket : SortedList<uint16, float> =
//                    if hash = prevHash && prevBucket <> null then
//                        prevBucket
//                    else
//                        let tg = nsl.TryGetValue(hash) 
//                        if fst tg then 
//                            (snd tg)
//                        else
//                            let newSm = SortedList()
//                            nsl.[hash] <- newSm
//                            newSm
//                bucket.[uint16(idx)] <- float(i)
//                prevHash <- hash
//                prevBucket <- bucket
//        
//            watch.Stop()
//            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//            ()


//    [<Test>]
//    member this.``Nested sorted map - optimized``() =
//        for c in 0..19 do
//            let watch = Stopwatch.StartNew();
//            let nsl = SortedMap<int64, SortedMap<uint16, float>>()
//            let lim = 1000000L
//            let bucket_size = 256
//            let hc = Int64SortableHasher(512us)
//            let mutable prevHash = 0L
//            let mutable prevBucket = null
//            for i =  int(0L) to int(lim)  do
//                let hash = hc.Hash(int64(i)) // int64(i / bucket_size)
//                let idx = hc.BucketIndex(int64(i)) // i % bucket_size
//                let bucket : SortedMap<uint16, float> =
//                    if hash = prevHash && prevBucket <> null then
//                        prevBucket
//                    else
//                        let tg = nsl.TryGetValue(hash) 
//                        if fst tg then 
//                            (snd tg)
//                        else
//                            let newSm = SortedMap()
//                            nsl.[hash] <- newSm
//                            newSm
//                bucket.[uint16(idx)] <- float(i)
//                prevHash <- hash
//                prevBucket <- bucket
//        
//            watch.Stop()
//            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//            ()

//    [<Test>]
//    member this.``Nested intmap + sorted map - optimized``() =
//        for c in 0..19 do
//            let watch = Stopwatch.StartNew();
//            let mutable nsl = ImmutableIntMap64<SortedMap<uint16, float>>.Empty
//            let lim = 1000000L
//            let bucket_size = 256
//            let hc = Int64SortableHasher(512us)
//            let mutable prevHash = 0L
//            let mutable prevBucket = null
//            for i =  int(lim) downto int(0L)   do
//                let hash = hc.Hash(int64(i)) // int64(i / bucket_size)
//                let idx = hc.BucketIndex(int64(i)) // i % bucket_size
//                let bucket : SortedMap<uint16, float> =
//                    if hash = prevHash && prevBucket <> null then
//                        prevBucket
//                    else
//                        let tg = nsl.TryFind(hash, Lookup.EQ) 
//                        if fst tg then 
//                            (snd tg).Value
//                        else
//                            let newSm = SortedMap()
//                            nsl <- nsl.Add(hash, newSm)
//                            newSm
//                bucket.[uint16(idx)] <- float(i)
//                prevHash <- hash
//                prevBucket <- bucket
//        
//            watch.Stop()
//            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//            Thread.Sleep(1000)
//            ()


//
//
//    [<Test>]
//    member this.``SHM - Could insert and read``() =
//        let rounds = 5
//        for round in 0..(rounds-1) do
//            let mutable watch = Stopwatch.StartNew();
//
//            let nsl = SortedHashMap<int64, float>(Int64SortableHasher(512us)) //TimePeriodHashComparer())
//
//            let lim = 1000000
//            for i =  0 to lim do
//            //for i =  int(lim) downto int(0) do
//                //let start = DateTimeOffset(DateTime.Today.AddMilliseconds(float(i)))
//                let key = int64(i) //TimePeriod(Frequency.Millisecond, 1us, start)
//                nsl.[key] <- float(i)
//            watch.Stop()
//            Console.WriteLine(">Inserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//            watch <- Stopwatch.StartNew()
//            for i =  0 to lim do
//            //for i =  int(lim) downto int(0) do
//                //let start = DateTimeOffset(DateTime.Today.AddMilliseconds(float(i)))
//                let key = int64(i) //TimePeriod(Frequency.Millisecond, 1us, start)
//                let value = nsl.[key]
//                if value <> float(i) then failwith("failed " + i.ToString() + ", exp: " + float(i).ToString()+ ", act: " + value.ToString() )
//                ()
//            watch.Stop()
//            Console.WriteLine(">Reads per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//            let isEmpty = nsl.IsEmpty
//
//            isEmpty |> should equal false
//
//            watch <- Stopwatch.StartNew()
//            let ptr = nsl.GetPointer()
//            for i =  0 to lim do
//            //for i =  int(lim) downto int(0) do
//                //let start = DateTimeOffset(DateTime.Today.AddMilliseconds(float(i)))
//                let move = ptr.MoveNext()
//                let value = ptr.CurrentValue
//                if value <> float(i) then failwith("failed " + i.ToString() + ", exp: " + float(i).ToString()+ ", act: " + value.ToString() )
//                if (not move) && i < lim then failwith("failed " + i.ToString())
//                ()
//            watch.Stop()
//            Console.WriteLine(">Iterations per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//            watch <- Stopwatch.StartNew()
//            let ptr = nsl.GetPointer()
//            for i =  0 to lim do
//            //for i =  int(lim) downto int(0) do
//                //let start = DateTimeOffset(DateTime.Today.AddMilliseconds(float(i)))
//                let move = ptr.MovePrevious()
//                let value = ptr.CurrentValue
//                if value <> float(lim - i) then failwith("failed " + i.ToString() + ", exp: " + float(i).ToString()+ ", act: " + value.ToString() )
//                if (not move) && i < lim then failwith("failed " + i.ToString())
//                ()
//            watch.Stop()
//            Console.WriteLine(">Backward per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//            watch <- Stopwatch.StartNew()
//            let ptr = nsl.GetPointer()
//            for i =  0 to lim do
//            //for i =  int(lim) downto int(0) do
//                //let start = DateTimeOffset(DateTime.Today.AddMilliseconds(float(i)))
//                let move = ptr.MoveAt(int64(i), Lookup.EQ)
//                let value = ptr.CurrentValue
//                if value <> float(i) then failwith("failed " + i.ToString() + ", exp: " + float(i).ToString()+ ", act: " + value.ToString() )
//                if (not move) && i < lim then failwith("failed " + i.ToString())
//                ()
//            watch.Stop()
//            Console.WriteLine(">MoveAt per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//
//
////            watch <- Stopwatch.StartNew()
////            for i =  0 to lim do
////            //for i =  int(lim) downto int(0) do
////                let value = nsl.[int64(i)]
////                value |> should equal (float(i))
////            watch.Stop()
//            //Console.WriteLine(">Asserts per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())
//
//
//            ()
//
//
//    [<Test>]
//    member this.``SHM - Could insert concurrently``() =
//        let rounds = 5
//        for round in 0..(rounds-1) do
//            let lim = 1000000L
//            let s = ref (new SortedHashMap<int64, int64>(Int64SortableHasher(512us)))
//
//            let task1 = new Task( fun () ->
//                    let watch1 = Stopwatch.StartNew()
//                    for i in 0L..lim-1L do
//                        s.Value.[i] <- i
//                    watch1.Stop()
//                    //Console.WriteLine(">Task 1, inserts per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//                )
//
//            let task2 = new Task( fun () ->
//                    let watch1 = Stopwatch.StartNew()
//                    for i in 0L..lim-1L do
//                        s.Value.[i+lim] <- i+lim
//                    watch1.Stop()
//                    //Console.WriteLine(">Task 2, inserts per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//                )
//
//            let task3 = new Task( fun () ->
//                    let watch1 = Stopwatch.StartNew()
//                    let mutable value = 0L
//                    let mutable found = 0
//                    for i in 0L..lim-1L do
//                        try
//                            value <- s.Value.[i]
//                            found <- found + 1
//                        with
//                            | _ -> ()
//                    watch1.Stop()
//                    //Console.WriteLine(">Task 3, reads per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//                    //Console.WriteLine(">Found: " + found.ToString())
//                )
//
//            let task4 = new Task( fun () ->
//                    let watch1 = Stopwatch.StartNew()
//                    let mutable value = 0L
//                    let mutable found = 0
//                    for i in 0L..lim-1L do
//                        try
//                            value <- s.Value.[i+lim]
//                            found <- found + 1
//                        with
//                            | _ -> ()
//                    watch1.Stop()
//                    //Console.WriteLine(">Task 4, reads per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
//                    //Console.WriteLine(">Found: " + found.ToString())
//                )
//
//
//            let mutable watch1 = Stopwatch.StartNew()
//            task1.Start()
//            task2.Start()
//            Task.WaitAll([|task1; task2|])
//
//            watch1.Stop()
//            Console.WriteLine(">C.Inserts to SHM, per sec: " + (1000L * int64(lim*2L)/watch1.ElapsedMilliseconds).ToString())
//
//            watch1 <- Stopwatch.StartNew()
//            task3.Start()
//            task4.Start()
//            Task.WaitAll([|task3; task4|])
//
//            watch1.Stop()
//            //Console.WriteLine(res1)
//            Console.WriteLine(">C.Reads to SHM, per sec: " + (1000L * int64(lim*2L)/watch1.ElapsedMilliseconds).ToString())
//
//            s.Value.Count |> should equal (lim * 2L)