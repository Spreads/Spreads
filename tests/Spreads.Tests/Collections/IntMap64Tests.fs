namespace Spreads.Tests.Collections

open System
open System.Diagnostics
open NUnit.Framework
open Spreads.Collections
open Spreads.Collections.IntMap64Tree


[<TestFixture>]
type IntMap64Module() =


    let m = ofSeq [
                            (9L,"9")
                            (12L,"12")
                            (14L,"14")
                            (17L,"17")
                            (19L,"19")
                            (23L,"23")
                            (50L,"50")
                            (54L,"54")
                            (67L,"67")
                            (72L,"72")
                            (76L,"76")
                            ]

    [<Test>]
    member this.``Pointer could move next``() =
        let ptr = mkPointer m

        Assert.IsTrue(move 9L ptr)
//        let hasNext = moveNext ptr
//        let next = current ptr
        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(12L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(14L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(17L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(19L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(23L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(50L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(54L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(67L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(72L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(76L, fst (current ptr))

        Assert.IsFalse(moveNext ptr)

        ()

    [<Test>]
    member this.``Pointer could move prev``() =
        let ptr = mkPointer  m

        Assert.IsTrue(move 76L ptr)

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(72L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(67L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(54L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(50L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(23L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(19L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(17L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(14L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(12L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(9L, fst (current ptr))

        Assert.IsFalse(movePrevious ptr)


        ()

    [<Test>]
    member this.``Pointer could move next and prev ``() =
        let ptr = mkPointer m

        Assert.IsTrue(ptr |> move 9L)

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(12L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(14L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(17L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(19L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(23L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(50L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(54L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(67L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(72L, fst (current ptr))

        Assert.IsTrue(moveNext ptr)
        Assert.AreEqual(76L, fst (current ptr))

        Assert.IsFalse(moveNext ptr)

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(72L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(67L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(54L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(50L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(23L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(19L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(17L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(14L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(12L, fst (current ptr))

        Assert.IsTrue(movePrevious ptr)
        Assert.AreEqual(9L, fst (current ptr))

        Assert.IsFalse(movePrevious ptr)

        ()





    [<Test>]
    member this.``Compare iterators``() =
        // Interesting that in DEBUG mode imperative enumerator is 2-3x faster than pointer, but 3-4x slower in release mode.
        // Probably because in DEBUG garbage collector ignores list heads released after
        // enumeration steps. Pointer takes practically zero additional memory, while enumerator 
        // copies the whole trie to a list - that is the reason of slower performance and almost doubling memory requirement
        
        let lim = 1000000L
        let mutable intmap = IntMap64Tree<int64>.Nil

        for i in 0L..lim do
            intmap <- IntMap64Tree.insert ((i)) i intmap

        let mutable res = 0L
        let mutable res1 = 0L

        /// Functional pointer
        let watch1 = Stopwatch.StartNew();
        let ptr = mkPointer intmap
        Assert.IsTrue(move 0L ptr)
        for i in 1L..lim do
            moveNext ptr |> ignore
            res1 <- res1 + snd (current ptr)
        watch1.Stop()
        //Console.WriteLine(res1)
        Console.WriteLine(">Functional pointer, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())

        
        /// Imperative enumarator
        let watch = Stopwatch.StartNew();
        let intmaplist = intmap.ToList() // this is how enumerator is contructed in IntMap64Tree
        for enumerated in intmaplist do
            res <- res + (snd enumerated)
        watch.Stop()
        //Console.WriteLine(res)
        Console.WriteLine(">Imperative enumerator, per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

        Assert.AreEqual(res, res1)


    [<Test>]
    member this.``Iterate with imperative enumerator``() =
        let lim = 1000000L
        let mutable intmap = IntMap64Tree<int64>.Nil

        for i in 0L..lim do
            intmap <- IntMap64Tree.insert ((i)) i intmap

        let mutable res = 0L

        /// Imperative enumarator
        let watch = Stopwatch.StartNew();
        let intmaplist = intmap.ToList() // this is how enumerator is contructed in IntMap64Tree
        for enumerated in intmaplist do
            res <- res + (snd enumerated)
        watch.Stop()
        //Console.WriteLine(res)
        Console.WriteLine(">Imperative enumerator, per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

    
    [<Test>]
    member this.``Iterate with functional pointer``() =
        let lim = 1000000L
        let mutable intmap = IntMap64Tree<int64>.Nil

        for i in 0L..lim do
            intmap <- IntMap64Tree.insert ((i)) i intmap

        let mutable res1 = 0L

        /// Functional pointer
        let watch1 = Stopwatch.StartNew();
        let ptr = mkPointer intmap
        Assert.IsTrue(move 0L ptr)
        for i in 1L..lim do
            moveNext ptr |> ignore
            res1 <- res1 + snd (current ptr)
        watch1.Stop()
        //Console.WriteLine(res1)
        Console.WriteLine(">Functional pointer, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())

       
    
    [<Test>]
    member this.``Lookup EQ vs LT``() =
        // On Win7/MacAir2012 with 1mn lim (note that NUnit runs this test in one processor core only)
        // >EQ lookups, per sec: 4237288
        // >LT lookups, per sec: 3571428
        // 15% difference is OK for convenience of LT/LE/GT/GE lookups
        // All lookups are pretty fast, hard to imagine when this will be a bottleneck
        // compared to arrays or mutable collections (e.g. .NET's SCG ones).
        // Use LT/LE/GT/GE lookups when it is appropriate by meaning, without micro-optimizing brainfuck!
        // (funny: System.Random.Next() is about ~700k per sec on the same machine and limits performance of random search to this rate)


        let lim = 1000000L
        let start = Int64.MaxValue - lim
        let mutable intmap = IntMap64Tree<int64>.Nil

        for i in start..start+lim do
            intmap <- IntMap64Tree.insert ((i)) i intmap

        let mutable res = 0L
        let watch = Stopwatch.StartNew();
        for i in start..start+lim do
            res <- res + (find (start+((i-start)/3L)) intmap)
        watch.Stop()
        Console.WriteLine(">EQ lookups, per sec: " + (1000L * int64(lim)/watch.ElapsedMilliseconds).ToString())

        let mutable res1 = 0L
        let watch1 = Stopwatch.StartNew();
        for i in start..start+lim do
            res1 <- res1 + (snd ((tryFindLT ((start+((i-start)/3L))+1L) intmap).Value))
        watch1.Stop()
        Console.WriteLine(">LT lookups, per sec: " + (1000L * int64(lim)/watch1.ElapsedMilliseconds).ToString())
        Assert.AreEqual(res, res1)
