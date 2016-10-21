// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Tests.Collections

open System
open NUnit.Framework
open Spreads.Collections
open Spreads.Collections.MapTree

(*
[Test Strategy]
Make sure each method works on:
* Maps with reference keys
* Maps with value keys
* Empty Maps (0 elements)
* One-element maps
* Multi-element maps (2 – 7 elements)
*)

[<TestFixture>]
type MapTreeModule() =
    // Visula help here http://upload.wikimedia.org/wikipedia/commons/0/06/AVLtreef.svg   
    // (but here the initial tree is rebalanced differently with heights 2 - 4 and top 17)

    let c = fgc<int>
    let mt = ofSeq c [
                            (9,"9")
                            (12,"12")
                            (14,"14")
                            (17,"17")
                            (19,"19")
                            (23,"23")
                            (50,"50")
                            (54,"54")
                            (67,"67")
                            (72,"72")
                            (76,"76")
                            ]

    [<Test>]
    member this.Empty() =
        let emptyMap = empty        
        Assert.IsTrue(isEmpty emptyMap)
        let a : Map<int,int>    = Map.empty<int,int>
        let b : Map<string,string> = Map.empty<string,string>
        let c : Map<int,string> = Map.empty<int,string>  
              
        ()


    [<Test>]
    member this.``Could split MapTree``() =   
        match splitLookup c 17 mt with
        | l, Some "17", r ->
            Assert.AreEqual(size l, 3)
            Assert.AreEqual(size r, 7) 
        | _ -> failwith "wrong result"

        match splitLookup c 16 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 3)
            Assert.AreEqual(size r, 8) // 17 goes to right 
        | _ -> failwith "wrong result"

        match splitLookup c 18 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 4) // 17 goes to left 
            Assert.AreEqual(size r, 7) 
        | _ -> failwith "wrong result"

        // Left side
        match splitLookup c 9 mt with
        | l, Some "9", r ->
            Assert.AreEqual(size l, 0)
            Assert.AreEqual(size r, 10) 
        | _ -> failwith "wrong result"

        match splitLookup c 8 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 0)
            Assert.AreEqual(size r, 11) 
        | _ -> failwith "wrong result"

        match splitLookup c 10 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 1)
            Assert.AreEqual(size r, 10) 
        | _ -> failwith "wrong result"

        // right side
        match splitLookup c 76 mt with
        | l, Some "76", r ->
            Assert.AreEqual(size l, 10)
            Assert.AreEqual(size r, 0) 
        | _ -> failwith "wrong result"

        match splitLookup c 77 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 11)
            Assert.AreEqual(size r, 0) 
        | _ -> failwith "wrong result"

        match splitLookup c 75 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 10)
            Assert.AreEqual(size r, 1) 
        | _ -> failwith "wrong result"



        // middle left from top
        match splitLookup c 12 mt with
        | l, Some "12", r ->
            Assert.AreEqual(size l, 1)
            Assert.AreEqual(size r, 9) 
        | _ -> failwith "wrong result"

        match splitLookup c 11 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 1)
            Assert.AreEqual(size r, 10) 
        | _ -> failwith "wrong result"

        match splitLookup c 13 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 2)
            Assert.AreEqual(size r, 9) 
        | _ -> failwith "wrong result"


        // middle right from top
        match splitLookup c 23 mt with
        | l, Some "23", r ->
            Assert.AreEqual(size l, 5)
            Assert.AreEqual(size r, 5) 
        | _ -> failwith "wrong result"

        match splitLookup c 22 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 5)
            Assert.AreEqual(size r, 6) 
        | _ -> failwith "wrong result"

        match splitLookup c 24 mt with
        | l, None, r ->
            Assert.AreEqual(size l, 6)
            Assert.AreEqual(size r, 5) 
        | _ -> failwith "wrong result"


    
    [<Test>]
    member this.``Could lookupGT and lookupGE``() =   
        
        match tryFindGT c 17 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 19)
        | _ -> failwith "wrong result"

        match tryFindGE c 17 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 17)
        | _ -> failwith "wrong result"

        match tryFindGT c 15 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 17)
        | _ -> failwith "wrong result"

        match tryFindGE c 15 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 17)
        | _ -> failwith "wrong result"

        match tryFindGT c 76 mt with
        | None -> Assert.IsTrue(true)
        | _ -> failwith "wrong result"

        match tryFindGE c 76 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 76)
        | _ -> failwith "wrong result"

        match tryFindGT c 77 mt with
        | None -> Assert.IsTrue(true)
        | _ -> failwith "wrong result"

        match tryFindGE c 77 mt with
        | None -> Assert.IsTrue(true)
        | _ -> failwith "wrong result"

        match tryFindGT c 71 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 72)
        | _ -> failwith "wrong result"

        match tryFindGE c 71 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 72)
        | _ -> failwith "wrong result"

        match tryFindGT c 9 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 12)
        | _ -> failwith "wrong result"

        match tryFindGE c 9 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 9)
        | _ -> failwith "wrong result"

        match tryFindGT c 8 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 9)
        | _ -> failwith "wrong result"

        match tryFindGE c 8 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 9)
        | _ -> failwith "wrong result"


        
    [<Test>]
    member this.``Could lookupLT and lookupLE``() =   
        
        match tryFindLT c 17 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 14)
        | _ -> failwith "wrong result"

        match tryFindLE c 17 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 17)
        | _ -> failwith "wrong result"

        match tryFindLT c 15 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 14)
        | _ -> failwith "wrong result"

        match tryFindLE c 15 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 14)
        | _ -> failwith "wrong result"

        match tryFindLT c 9 mt with
        | None -> Assert.IsTrue(true)
        | _ -> failwith "wrong result"

        match tryFindLE c 9 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 9)
        | _ -> failwith "wrong result"

        match tryFindLT c 8 mt with
        | None -> Assert.IsTrue(true)
        | _ -> failwith "wrong result"

        match tryFindLE c 8 mt with
        | None -> Assert.IsTrue(true)
        | _ -> failwith "wrong result"

        match tryFindLT c 71 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 67)
        | _ -> failwith "wrong result"

        match tryFindLE c 71 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 67)
        | _ -> failwith "wrong result"

        match tryFindLT c 76 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 72)
        | _ -> failwith "wrong result"

        match tryFindLE c 76 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 76)
        | _ -> failwith "wrong result"

        match tryFindLT c 90 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 76)
        | _ -> failwith "wrong result"

        match tryFindLE c 90 mt with
        | Some(kvp) -> Assert.AreEqual(fst kvp, 76)
        | _ -> failwith "wrong result"

        
    [<Test>]
    member this.``Could findMin/Max``() =   
        let mutable map = MapTree.empty
        for i in 1..1000 do
            map <- MapTree.add c i i map
            Assert.AreEqual(fst (MapTree.findMin map), 1)
            Assert.AreEqual(fst (MapTree.findMax map), i)

        for i in 1..999 do
            map <- MapTree.remove c i map
            Assert.AreEqual(fst (MapTree.findMin map), i+1)
            Assert.AreEqual(fst (MapTree.findMax map), 1000)