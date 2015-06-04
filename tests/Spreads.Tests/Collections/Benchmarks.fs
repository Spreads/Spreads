namespace Spreads.Tests.Collections.Benchmarks

open FsUnit
open NUnit.Framework

open System
open System.Collections.Generic
open System.Diagnostics
open Spreads
open Spreads.Collections
open System.Threading
open System.Threading.Tasks

open Deedle

/// Insert last (most common usage), first (worst case usage) and read (forward and backward)
/// for each collection
module CollectionsBenchmarks =
  


  let IntMap64(count:int64) =
    let intmap = ref IntMap64Tree<int64>.Nil
    perf count "IntMap64<int64> insert" (fun _ ->
      for i in 0L..count do
        intmap := IntMap64Tree.insert ((i)) i !intmap
    )
    perf count "IntMap64<int64> read" (fun _ ->
      for i in 0L..count do
        let res = IntMap64Tree.find i !intmap
        if res <> i then failwith "IntMap64 failed"
        ()
    )
    Console.WriteLine("----------------")
  [<Test>]
  let IntMap64_run() = IntMap64(1000000L)


//  let DeedleSeries(count:int64) =
//    let deedleSeries = ref (Series.ofObservations([]))
//    perf count "DeedleSeries insert" (fun _ ->
//      for i in 0L..count do
//        deedleSeries := Series.merge !deedleSeries (Series.ofObservations([i => i]))
//    )
//    perf count "DeedleSeries read" (fun _ ->
//      for i in 0L..count do
//        let res = Series.lookup i Deedle.Lookup.Exact !deedleSeries 
//        if res <> i then failwith "DeedleSeries failed"
//        ()
//    )
//    Console.WriteLine("----------------")
//  [<Test>]
//  let DeedleSeries_run() = DeedleSeries(10000L)


  let FSXVector(count:int64) =
    let vec = ref FSharpx.Collections.PersistentVector.empty
    perf count "FSXVector insert" (fun _ ->
      for i in 0L..count do
        vec := vec.Value.Update(int32(i), i)
    )
    perf count "FSXVector read" (fun _ ->
      for i in 0L..count do
        let res = vec.Value.Item(int32(i))
        if res <> i then failwith "FSXVector failed"
        ()
    )
    Console.WriteLine("----------------")
  [<Test>]
  let FSXVector_run() = FSXVector(1000000L)



  let FSXDeque(count:int64) =
    let vec = ref FSharpx.Collections.Deque.empty
    perf count "FSXDeque Conj" (fun _ ->
      for i in 0L..count do
        vec := vec.Value.Conj i
    )
    perf count "FSXDeque Uncons" (fun _ ->
      for i in 0L..count do
        let res, tail = vec.Value.Uncons
        vec := tail
        if res <> i then failwith "FSXVector failed"
        ()
    )
    vec :=FSharpx.Collections.Deque.empty
    perf count "FSXDeque Cons" (fun _ ->
      for i in 0L..count do
        vec := vec.Value.Cons i
    )
    perf count "FSXDeque Unconj" (fun _ ->
      for i in 0L..count do
        let init, res = vec.Value.Unconj
        vec := init
        if res <> i then failwith "FSXVector failed"
        ()
    )
    vec := FSharpx.Collections.Deque.empty
    Console.WriteLine("----------------")
  [<Test>]
  let FSXDeque_run() = FSXDeque(1000000L)

  let FSXHashMap(count:int64) =
    let vec = ref FSharpx.Collections.PersistentHashMap.empty
    perf count "FSXHashMap<int64,int64> insert" (fun _ ->
      for i in 0L..count do
        vec := vec.Value.Add(i, i)
    )
    perf count "FSXHashMap<int64,int64> read" (fun _ ->
      for i in 0L..count do
        let res = vec.Value.Item(i)
        if res <> i then failwith "FSXVector failed"
        ()
    )
    Console.WriteLine("----------------")
  [<Test>]
  let FSXHashMap_run() = FSXHashMap(1000000L)

  let SCGList(count:int64) =
    let list = ref (System.Collections.Generic.List<int64>())
    perf count "SCGList insert" (fun _ ->
      for i in 0L..count do
        list.Value.Insert(int i, i)
      list.Value.TrimExcess()
    )
    perf count "SCGList read" (fun _ ->
      for i in 0L..count do
        let res = list.Value.Item(int32(i))
        if res <> i then failwith "SCGList failed"
        ()
    )
    list := (System.Collections.Generic.List<int64>())
    perf 10000L "SCGList insert first" (fun _ ->
      for i in 0L..10000L do
        list.Value.Insert(0, i)
      list.Value.TrimExcess()
    )
    list := (System.Collections.Generic.List<int64>())
    perf 100000L "SCGList insert first" (fun _ ->
      for i in 0L..100000L do
        list.Value.Insert(0, i)
      list.Value.TrimExcess()
    )
    Console.WriteLine("The bigger the list, the more data is copied")
    list := null
    Console.WriteLine("----------------")
  
  [<Test>]
  let SCGList_run() = SCGList(1000000L)


  let DeedleDeque(count:int64) =
    let deque = ref (Deque())
    perf count "DeedleDeque Add" (fun _ ->
      for i in 0L..count do
        deque.Value.Add i
    )
    perf count "DeedleDeque RemoveFirst" (fun _ ->
      for i in 0L..count do
        let res = deque.Value.RemoveFirst()
        if res <> i then failwith "DeedleDeque failed"
        ()
    )
    deque := Deque()
    Console.WriteLine("----------------")
  [<Test>]
  let DeedleDeque_run() = DeedleDeque(1000000L)


  let MapTree(count:int64) =
    let map = ref MapTree.empty
    perf count "MapTree Add" (fun _ ->
      for i in 0L..count do
        map := MapTree.add MapTree.fgc (i) i !map
    )
    perf count "MapTree Read" (fun _ ->
      for i in 0L..count do
        let res = MapTree.find MapTree.fgc (i) !map
        if res <> i then failwith "MapTree failed"
        ()
    )
    map := MapTree.empty
    perf count "MapTree Add Reverse" (fun _ ->
      for i in 0L..count do
        map := MapTree.add MapTree.fgc (count - i) i !map
    )
    perf count "MapTree Read Reverse" (fun _ ->
      for i in 0L..count do
        let res = MapTree.find MapTree.fgc (i) !map
        if res <> count - i then failwith "MapTree failed"
        ()
    )
    Console.WriteLine("----------------")
  [<Test>]
  let MapTree_run() = MapTree(1000000L)


  let SCGSortedList(count:int64) =
    let sl = ref (SortedList())
    perf count "SCGSortedList Add" (fun _ ->
      for i in 0L..count do
        sl.Value.Add(i, i)
    )
    perf count "SCGSortedList Read" (fun _ ->
      for i in 0L..count do
        let res = sl.Value.Item(i)
        if res <> i then failwith "SCGSortedList failed"
        ()
    )
    perf count "SCGSortedList Iterate" (fun _ ->
      for i in sl.Value do
        let res = i.Value
        ()
    )
    sl := SortedList()
    let count = count / 10L
    perf count "SCGSortedList Add Reverse" (fun _ ->
      for i in 0L..count do
        sl.Value.Add(count - i, i)
    )
    perf count "SCGSortedList Read Reverse" (fun _ ->
      for i in 0L..count do
        let res = sl.Value.Item(count - i)
        if res <> i then failwith "SCGSortedList failed"
        ()
    )
    Console.WriteLine("----------------")
  [<Test>]
  let SCGSortedList_run() = SCGSortedList(1000000L)

  let SCGDictionary(count:int64) =
    let sl = ref (Dictionary())
    perf count "SCGDictionary Add" (fun _ ->
      for i in 0L..count do
        sl.Value.Add(i, i)
    )
    perf count "SCGDictionary Read" (fun _ ->
      for i in 0L..count do
        let res = sl.Value.Item(i)
        if res <> i then failwith "SCGDictionary failed"
        ()
    )
    perf count "SCGDictionary Iterate" (fun _ ->
      for i in sl.Value do
        let res = i.Value
        ()
    )
    sl := Dictionary()
    perf count "SCGDictionary Add Reverse" (fun _ ->
      for i in 0L..count do
        sl.Value.Add(count - i, i)
    )
    perf count "SCGDictionary Read Reverse" (fun _ ->
      for i in 0L..count do
        let res = sl.Value.Item(count - i)
        if res <> i then failwith "SCGDictionary failed"
        ()
    )
    Console.WriteLine("----------------")
  [<Test>]
  let SCGDictionary_run() = SCGDictionary(1000000L)

  let SortedListTests(count:int64) =
    let sl = ref (Spreads.Collections.Extra.SortedList())
    perf count "SortedList Add" (fun _ ->
      for i in 0L..count do
        sl.Value.Add(i, i)
    )
    perf count "SortedList Read" (fun _ ->
      for i in 0L..count do
        let res = sl.Value.Item(i)
        if res <> i then failwith "SortedList failed"
        ()
    )
    perf count "SortedList Iterate" (fun _ ->
      for i in sl.Value do
        let res = i.Value
        if res <> i.Value then failwith "SortedList failed"
        ()
    )
//    sl := Spreads.Collections.Extra.SortedList()
//    let count = count / 10L
//    perf count "SortedList Add Reverse" (fun _ ->
//      for i in 0L..count do
//        sl.Value.Add(count - i, i)
//    )
//    perf count "SortedList Read Reverse" (fun _ ->
//      for i in 0L..count do
//        let res = sl.Value.Item(count - i)
//        if res <> i then failwith "SortedList failed"
//        ()
//    )
    Console.WriteLine("----------------")
  [<Test>]
  let SortedList_run() = SortedListTests(1000000L)


  let SCIOrderedMap(count:int64) =
    let shm = ref (System.Collections.Immutable.ImmutableSortedDictionary.Empty)
    perf count "SCIOrderedMap Add" (fun _ ->
      for i in 0L..count do
        shm := shm.Value.Add(i, i)
    )
    perf count "SCIOrderedMap Read" (fun _ ->
      for i in 0L..count do
        let res = shm.Value.Item(i)
        if res <> i then failwith "SCIOrderedMap failed"
        ()
    )
    shm := (System.Collections.Immutable.ImmutableSortedDictionary.Empty)
    let count = count / 10L
    perf count "SCIOrderedMap Add Reverse" (fun _ ->
      for i in 0L..count do
        shm := shm.Value.Add(count - i, i)
    )
    perf count "SCIOrderedMap Read Reverse" (fun _ ->
      for i in 0L..count do
        let res = shm.Value.Item(count - i)
        if res <> i then failwith "SCIOrderedMap failed"
        ()
    )
    Console.WriteLine("----------------")
  [<Test>]
  let SCIOrderedMap_run() = SCIOrderedMap(1000000L)




//  let MapDequeTest(count:int64) =
//    let mdq = ref (MapDeque())
//    perf count "MapDeque Add" (fun _ ->
//      for i in 0L..count do
//        mdq.Value.Add(i, i)
//    )
//    perf count "MapDeque Read" (fun _ ->
//      for i in 0L..count do
//        let res = mdq.Value.Item(i)
//        if res <> i then failwith "MapDeque failed"
//        ()
//    )
//    mdq := MapDeque()
//    let count = count / 10L
//    perf count "MapDeque Add Reverse" (fun _ ->
//      for i in 0L..count do
//        mdq.Value.Add(count - i, i)
//    )
//    perf count "MapDeque Read Reverse" (fun _ ->
//      for i in 0L..count do
//        let res = mdq.Value.Item(count - i)
//        if res <> i then failwith "MapDeque failed"
//        ()
//    )
//    Console.WriteLine("----------------")
//  [<Test>]
//  let MapDeque_run() = MapDequeTest(1000000L)



  let SortedMapTest(count:int64) =
    let smap = ref (SortedMap())
    for i in 0..4 do
      smap := SortedMap()
      perf count "SortedMap Add" (fun _ ->
        for i in 0L..count do
          smap.Value.Add(i, i)
      )
    for i in 0..4 do
      perf count "SortedMap Read" (fun _ ->
        for i in 0L..count do
          let res = smap.Value.Item(i)
          if res <> i then failwith "SortedMap failed"
          ()
      )
    for i in 0..9 do
      perf count "SortedMap Iterate" (fun _ ->
        for i in smap.Value do
          let res = i.Value
          ()
      )
//    smap := SortedMap()
//    let count = count / 10L
//    perf count "SortedMap Add Reverse" (fun _ ->
//      for i in 0L..count do
//        smap.Value.Add(count - i, i)
//    )
//    perf count "SortedMap Read Reverse" (fun _ ->
//      for i in 0L..count do
//        let res = smap.Value.Item(count - i)
//        if res <> i then failwith "SortedMap failed"
//        ()
//    )
    Console.WriteLine("----------------")
  [<Test>]
  let SortedMap_run() = SortedMapTest(1000000L)

  let SortedMapPeriodTest(count:int64) =
    let smap = ref (SortedMap())
    let sp = Period(UnitPeriod.Tick, 1, DateTimeOffset.UtcNow)
    for i in 0..4 do
      smap := SortedMap()
      perf count "SortedMap Period Add" (fun _ ->
        for i in 0L..count do
          smap.Value.Add(sp.Add(i), i)
      )
    for i in 0..4 do
      perf count "SortedMap Period Read" (fun _ ->
        for i in 0L..count do
          let res = smap.Value.Item(sp.Add(i))
          if res <> i then failwith "SortedMap failed"
          ()
      )
    for i in 0..9 do
      perf count "SortedMap Period Iterate" (fun _ ->
        for i in smap.Value do
          let res = i.Value
          ()
      )
    Console.WriteLine("----------------")
  [<Test>]
  let SortedMapPeriod_run() = SortedMapPeriodTest(1000000L)


  let SortedMapDTTest(count:int64) =
    let smap = ref (SortedMap())
    let sp = DateTime.UtcNow
    for i in 0..4 do
      smap := SortedMap()
      perf count "SortedMap DT Add" (fun _ ->
        for i in 0L..count do
          smap.Value.Add(sp.AddTicks(i), i)
      )
    for i in 0..4 do
      perf count "SortedMap DT Read" (fun _ ->
        for i in 0L..count do
          let res = smap.Value.Item(sp.AddTicks(i))
          if res <> i then failwith "SortedMap failed"
          ()
      )
    for i in 0..9 do
      perf count "SortedMap DT Iterate" (fun _ ->
        for i in smap.Value do
          let res = i.Value
          ()
      )
    Console.WriteLine("----------------")
  [<Test>]
  let SortedMapDT_run() = SortedMapDTTest(1000000L)

  let SortedMapRegularTest(count:int64) =
    let dc : IKeyComparer<int64> = SpreadsComparerInt64() :> IKeyComparer<int64> 

    let smap = ref (Spreads.Collections.SortedMap(comparer = (dc :> IComparer<int64>)))
    for i in 0..4 do
      smap := Spreads.Collections.SortedMap(comparer = (dc :> IComparer<int64>))
      perf count "SortedMapRegular Add" (fun _ ->
        for i in 0L..count do
          smap.Value.Add(i, i)
      )
//    for i in 0..4 do
//      perf count "SortedMapRegular Read" (fun _ ->
//        for i in 0L..count do
//          let res = smap.Value.Item(i)
//          if res <> i then failwith "SortedMap failed"
//          ()
//      )
    for i in 0..9 do
      perf count "SortedMapRegular Iterate" (fun _ ->
        for i in smap.Value do
          let res = i.Value
          ()
      )
//    smap := Spreads.Collections.SortedMap(comparer = (dc :?> IComparer<int64>))
//    let count = count / 10L
//    perf count "SortedMapRegular Add Reverse" (fun _ ->
//      for i in 0L..count do
//        smap.Value.Add(count - i, i)
//    )
//    perf count "SortedMapRegular Read Reverse" (fun _ ->
//      for i in 0L..count do
//        let res = smap.Value.Item(count - i)
//        if res <> i then failwith "SortedMap failed"
//        ()
//    )
    Console.WriteLine("----------------")
  [<Test>]
  let SortedMapRegular_run() = SortedMapRegularTest(1000000L)


  [<TestCase(10000000)>]
  let SHM(count:int64) =
    let shm = ref (SortedHashMap(SpreadsComparerInt64()))
    perf count "SHM<1024> Add" (fun _ ->
      for i in 0L..count do
        shm.Value.Add(i, i)
    )
    perf count "SHM Read" (fun _ ->
      for i in 0L..count do
        let res = shm.Value.Item(i)
        if res <> i then failwith "SHM failed"
        ()
    )
    perf count "SHM Iterate" (fun _ ->
      for i in shm.Value do
        let res = i.Value
        if res <> i.Value then failwith "SHM failed"
        ()
    )
    shm := (SortedHashMap(SpreadsComparerInt64()))
    let count = count / 10L
    perf count "SHM<1024> Add Reverse" (fun _ ->
      for i in 0L..count do
        shm.Value.Add(count - i, i)
    )
    perf count "SHM Read Reverse" (fun _ ->
      for i in 0L..count do
        let res = shm.Value.Item(count - i)
        if res <> i then failwith "SHM failed"
        ()
    )
//    for i in 0..9 do
//      let shmt = ref (SortedHashMap(TimePeriodComparer()))
//      let count = count * 10L
//      let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
//      perf count "SHM Time Period Add" (fun _ ->
//        for i in 0..(int count) do
//          shmt.Value.AddLast(TimePeriod(UnitPeriod.Second, 1, 
//                            initDTO.AddSeconds(float i)), int64 i)
//      )
//      perf count "SHM Time Period Read" (fun _ ->
//        for i in 0..(int count) do
//          let res = shmt.Value.Item(TimePeriod(UnitPeriod.Second, 1, 
//                        initDTO.AddSeconds(float i)))
//          ()
//      )
//      perf count "SHM Time Period Iterate" (fun _ ->
//        for i in shmt.Value do
//          let res = i.Value
//          ()
//      )
//    Console.WriteLine("----------------")
  [<Test>]
  let SHM_run() = SHM(1000000L)

  
  [<TestCase(10000000)>]
  let SCM(count:int64) =
    let shm = ref (SortedChunkedMap(SpreadsComparerInt64(8192us)))
    perf count "SCM<1024> Add" (fun _ ->
      for i in 0L..count do
        shm.Value.Add(i, i)
    )
    perf count "SCM Read" (fun _ ->
      for i in 0L..count do
        let res = shm.Value.Item(i)
        if res <> i then failwith "SCM failed"
        ()
    )
    perf count "SCM Iterate" (fun _ ->
      for i in shm.Value do
        let res = i.Value
        if res <> i.Value then failwith "SCM failed"
        ()
    )
    shm := (SortedChunkedMap(SpreadsComparerInt64(8192us)))
    let count = count / 10L
    perf count "SCM<1024> Add Reverse" (fun _ ->
      for i in 0L..count do
        shm.Value.Add(count - i, i)
    )
    perf count "SCM Read Reverse" (fun _ ->
      for i in 0L..count do
        let res = shm.Value.Item(count - i)
        if res <> i then failwith "SCM failed"
        ()
    )
//    for i in 0..9 do
//      let shmt = ref (SortedHashMap(TimePeriodComparer()))
//      let count = count * 10L
//      let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
//      perf count "SHM Time Period Add" (fun _ ->
//        for i in 0..(int count) do
//          shmt.Value.AddLast(TimePeriod(UnitPeriod.Second, 1, 
//                            initDTO.AddSeconds(float i)), int64 i)
//      )
//      perf count "SHM Time Period Read" (fun _ ->
//        for i in 0..(int count) do
//          let res = shmt.Value.Item(TimePeriod(UnitPeriod.Second, 1, 
//                        initDTO.AddSeconds(float i)))
//          ()
//      )
//      perf count "SHM Time Period Iterate" (fun _ ->
//        for i in shmt.Value do
//          let res = i.Value
//          ()
//      )
//    Console.WriteLine("----------------")
  [<Test>]
  let SCM_run() = SCM(1000000L)

//  [<TestCase(10000000)>]
//  let SHM_regular(count:int64) =
//    //let smap = ref (Spreads.Collections.SortedMap(comparer = (dc :?> IComparer<int64>)))
//
//    let shm = ref (SortedHashMap(SpreadsComparerInt64()))
//    for i in 0..9 do
//      shm := SortedHashMap(SpreadsComparerInt64())
//      perf count "SHM<1024> Add" (fun _ ->
//        for i in 0L..count do
//          shm.Value.Add(i, i)
//      )
//
//    for i in 0..9 do
//      perf count "SHM Read" (fun _ ->
//        for i in 0L..count do
//          let res = shm.Value.Item(i)
//          if res <> i then failwith "SHM failed"
//          ()
//      )
//    for i in 0..9 do
//      perf count "SHM Iterate" (fun _ ->
//        for i in shm.Value do
//          let res = i.Value
//          if res <> i.Value then failwith "SHM failed"
//          ()
//      )
//    
//    let count = count / 10L
//    for i in 0..9 do
//      shm := (SortedHashMap(SpreadsComparerInt64()))
//      perf count "SHM<1024> Add Reverse" (fun _ ->
//        for i in 0L..count do
//          shm.Value.Add(count - i, i)
//      )
//    for i in 0..9 do
//      perf count "SHM Read Reverse" (fun _ ->
//        for i in 0L..count do
//          let res = shm.Value.Item(count - i)
//          if res <> i then failwith "SHM failed"
//          ()
//      )
//    
//    let shmt = ref (SortedHashMap(TimePeriodComparer()))
//    let count = count * 10L
//    let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
//    for i in 0..9 do
//      shmt := (SortedHashMap(TimePeriodComparer()))
//      perf count "SHM Time Period Add" (fun _ ->
//        for i in 0..(int count) do
//          shmt.Value.AddLast(TimePeriod(UnitPeriod.Second, 1, 
//                            initDTO.AddSeconds(float i)), int64 i)
//      )
//    for i in 0..9 do
//      perf count "SHM Time Period Read" (fun _ ->
//        for i in 0..(int count) do
//          let res = shmt.Value.Item(TimePeriod(UnitPeriod.Second, 1, 
//                        initDTO.AddSeconds(float i)))
//          ()
//      )
//    for i in 0..9 do
//      perf count "SHM Time Period Iterate" (fun _ ->
//        for i in shmt.Value do
//          let res = i.Value
//          ()
//      )
//    Console.WriteLine("----------------")


//
//  [<Test>]
//  let SHM_regular_run() = SHM_regular(1000000L)

  let SortedDequeTest(count:int64) =
    let vec = ref (Extra.SortedDeque<int64>())
    perf count "SortedDeque Add" (fun _ ->
      for i in 0L..count do
        vec.Value.AddLast(i)
    )
    perf count "SortedDeque Read" (fun _ ->
      for i in 0L..count do
        let res = vec.Value.Item(i)
        if res <> i then failwith "SortedDeque failed"
        ()
    )
    vec := (Extra.SortedDeque<int64>())
    perf count "SortedDeque Reverse" (fun _ ->
      for i in 0L..count do
        vec.Value.Add(count - i)
    )
    perf count "SortedDeque Read reverse" (fun _ ->
      for i in 0L..count do
        let res = vec.Value.Item(count - i)
        if res <> i then failwith "SortedDeque failed"
        ()
    )

    Console.WriteLine("----------------")
  [<Test>]
  let SortedDeque_run() = SortedDequeTest(100000L)


  let PersistentSortedMapTest(count:int64) =
    let smap = ref (PersistentSortedMap(SpreadsDB.DefaultDbEnvironment, "testDb", 
      "testMap".CalculateMD5Bytes(), SpreadsComparerInt64()))
    for i in 0..4 do
      smap := PersistentSortedMap(SpreadsDB.DefaultDbEnvironment, "testDb", 
        "testMap".CalculateMD5Bytes(), SpreadsComparerInt64())
      perf count "PersistentSortedMap Add" (fun _ ->
        for i in 0L..count do
          smap.Value.Add(i, i)
      )
    for i in 0..4 do
      perf count "PersistentSortedMap Read" (fun _ ->
        for i in 0L..count do
          let res = smap.Value.Item(i)
          if res <> i then failwith "SortedMap failed"
          ()
      )
    for i in 0..9 do
      perf count "PersistentSortedMap Iterate" (fun _ ->
        for i in smap.Value do
          let res = i.Value
          ()
      )
//    smap := SortedMap()
//    let count = count / 10L
//    perf count "SortedMap Add Reverse" (fun _ ->
//      for i in 0L..count do
//        smap.Value.Add(count - i, i)
//    )
//    perf count "SortedMap Read Reverse" (fun _ ->
//      for i in 0L..count do
//        let res = smap.Value.Item(count - i)
//        if res <> i then failwith "SortedMap failed"
//        ()
//    )
    Console.WriteLine("----------------")
  [<Test>]
  let PersistenSortedMap_run() = PersistentSortedMapTest(10000L)


  [<Test>]
  let ``Run all``() =
//    Console.WriteLine("VECTORS")
//    FSXVector_run()
//    SCGList_run()
//
//    Console.WriteLine("DEQUE")
//    FSXDeque_run()
//    DeedleDeque_run()

    Console.WriteLine("MAPS")
//    DeedleSeries_run()
//    FSXHashMap_run()
//    IntMap64_run()
//    MapTree_run()
//    SCGSortedList_run()
//    SCIOrderedMap_run()
    //SortedDeque_run()
//    SortedList_run()
    SortedMap_run()
    SortedMapPeriod_run()
//    SortedMapDT_run()
    //SortedMapRegular_run()
    //MapDeque_run() // bugs!
    SHM_run()
    SCM_run()
//    SHM_regular_run()
    //PersistenSortedMap_run()