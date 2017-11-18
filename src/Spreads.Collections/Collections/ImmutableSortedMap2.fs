// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


// MapTree is modified from https://github.com/fsharp/fsharp/blob/master/src/fsharp/FSharp.Core/map.fs

//----------------------------------------------------------------------------
// Copyright (c) 2002-2012 Microsoft Corporation. 
//
// This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
// copy of the license can be found in the License.html file at the root of this distribution. 
// By using this source code in any fashion, you are agreeing to be bound 
// by the terms of the Apache License, Version 2.0.
//
// You must not remove this notice, or any other, from this software.
//----------------------------------------------------------------------------

namespace Spreads.Collections.Experimental


//#region Original Implementation 

    open System
    open System.Collections.Generic
    open System.Diagnostics
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
    open Microsoft.FSharp.Core.Operators
    open Microsoft.FSharp.Collections
    open Microsoft.FSharp.Primitives.Basics

    open Spreads.DataTypes;

    type NodeType =
      | Empty = 0u
      | MapOne = 1u
      | MapNode = 2u

    type Node<'K,'V when 'K : comparison > =
      struct 
        // val nodeReference: int64
        val refCount: int32

        val nodeType: NodeType
        val depth: uint16

        
        val reserveds: int64

        val leftReference: int64
        val rightReference: int64
        
        // ----- 

        val hash: Bytes32;

        
        val key: 'K;
        val value: 'V;
        
      end

    type Nodes<'K,'V when 'K : comparison> = Node<'K,'V>[]
    

    [<NoEquality; NoComparison>]
    type internal MapTree1<'K,'V when 'K : comparison > = 
        | MapEmpty of Node<'K,'V>
        | MapOne of Node<'K,'V>
        | MapNode of Node<'K,'V>

    [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
    [<NoEquality; NoComparison>]
    type internal MapTree<'K,'V when 'K : comparison > = 
        | MapEmpty 
        | MapOne of 'K * 'V
        | MapNode of 'K * 'V * MapTree<'K,'V> *  MapTree<'K,'V> * int
            // REVIEW: performance rumour has it that the data held in MapNode and MapOne should be
            // exactly one cache line. It is currently ~7 and 4 words respectively. 

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module internal MapTree = 
        //let testx() = 
        //   let node : Node  = Unchecked.defaultof<_>
        //   let d = ref node.nodeReference
        //   System.Threading.Interlocked.Increment(d) |> ignore
        //   d := 123L
        //   ()
        let size x = 
            let rec sizeAux acc m = 
                match m with  
                | MapEmpty -> acc
                | MapOne _ -> acc + 1
                | MapNode(_,_,l,r,_) -> sizeAux (sizeAux (acc+1) l) r 
            sizeAux 0 x

        let empty = MapEmpty 

        let height  = function
          | MapEmpty -> 0
          | MapOne _ -> 1
          | MapNode(_,_,_,_,h) -> h

        let isEmpty m = 
            match m with 
            | MapEmpty -> true
            | _ -> false

        let mk l k v r = 
            match l,r with 
            | MapEmpty,MapEmpty -> MapOne(k,v)
            | _ -> 
                let hl = height l 
                let hr = height r 
                let m = max hl hr // if hl < hr then hr else hl 
                MapNode(k,v,l,r,m+1) // new map is higher by 1 than the highest

        let rebalance t1 k v t2 =
            let t1h = height t1 
            let t2h = height t2 
            if  t2h > t1h + 2 then (* right is heavier than left *)
                match t2 with 
                | MapNode(t2k,t2v,t2l,t2r,_) -> 
                   (* one of the nodes must have height > height t1 + 1 *)
                   if height t2l > t1h + 1 then  (* balance left: combination *)
                     match t2l with 
                     | MapNode(t2lk,t2lv,t2ll,t2lr,_) ->
                        mk (mk t1 k v t2ll) t2lk t2lv (mk t2lr t2k t2v t2r) 
                     | _ -> failwith "rebalance"
                   else (* rotate left *)
                     mk (mk t1 k v t2l) t2k t2v t2r
                | _ -> failwith "rebalance"
            else
                if  t1h > t2h + 2 then (* left is heavier than right *)
                  match t1 with 
                  | MapNode(t1k,t1v,t1l,t1r,_) -> 
                    (* one of the nodes must have height > height t2 + 1 *)
                      if height t1r > t2h + 1 then 
                      (* balance right: combination *)
                        match t1r with 
                        | MapNode(t1rk,t1rv,t1rl,t1rr,_) ->
                            mk (mk t1l t1k t1v t1rl) t1rk t1rv (mk t1rr k v t2)
                        | _ -> failwith "rebalance"
                      else
                        mk t1l t1k t1v (mk t1r k v t2)
                  | _ -> failwith "rebalance"
                else mk t1 k v t2

        let rec add (comparer: IComparer<'K>) k v m = // was 'V instead of 'K, slightly measleading
            match m with 
            | MapEmpty -> MapOne(k,v)
            | MapOne(k2,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0   then MapNode (k,v,MapEmpty,m,2)
                elif c = 0 then MapOne(k,v)
                else            MapNode (k,v,m,MapEmpty,2)
            | MapNode(k2,v2,l,r,h) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then rebalance (add comparer k v l) k2 v2 r
                elif c = 0 then MapNode(k,v,l,r,h)
                else rebalance l k2 v2 (add comparer k v r) 

        let rec find (comparer: IComparer<'K>) k m = 
            match m with 
            | MapEmpty -> raise (System.Collections.Generic.KeyNotFoundException())
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c = 0 then v2
                else raise (System.Collections.Generic.KeyNotFoundException())
            | MapNode(k2,v2,l,r,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then find comparer k l
                elif c = 0 then v2
                else find comparer k r

        let rec tryFind (comparer: IComparer<'K>) k m = 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c = 0 then Some v2
                else None
            | MapNode(k2,v2,l,r,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then tryFind comparer k l
                elif c = 0 then Some v2
                else tryFind comparer k r
        
        let partition (comparer: IComparer<'K>) f s = 
            let rec partitionAux (comparer: IComparer<'K>) f s acc = 
                let partition1 (comparer: IComparer<'K>) f k v (acc1,acc2) = 
                    if f k v then (add comparer k v acc1,acc2) else (acc1,add comparer k v acc2) 
                match s with 
                | MapEmpty -> acc
                | MapOne(k,v) -> partition1 comparer f k v acc
                | MapNode(k,v,l,r,_) -> 
                    let acc = partitionAux comparer f r acc 
                    let acc = partition1 comparer f k v acc
                    partitionAux comparer f l acc
            partitionAux comparer f s (empty,empty)

        let filter (comparer: IComparer<'K>) f s = 
            let rec filterAux (comparer: IComparer<'V>) f s acc = 
                let filter1 (comparer: IComparer<'V>) f k v acc = 
                    if f k v then add comparer k v acc else acc 
                match s with 
                | MapEmpty -> acc
                | MapOne(k,v) -> filter1 comparer f k v acc
                | MapNode(k,v,l,r,_) ->
                    let acc = filterAux comparer f l acc
                    let acc = filter1 comparer f k v acc
                    filterAux comparer f r acc
            filterAux comparer f s empty

        

        let rec remove (comparer: IComparer<'K>) k m = 
            let rec spliceOutSuccessor m = 
                match m with 
                | MapEmpty -> failwith "internal error: Map.spliceOutSuccessor"
                | MapOne(k2,v2) -> k2,v2,MapEmpty
                | MapNode(k2,v2,l,r,_) ->
                    match l with 
                    | MapEmpty -> k2,v2,r
                    | _ -> let k3,v3,l' = spliceOutSuccessor l in k3,v3,mk l' k2 v2 r
            match m with 
            | MapEmpty -> empty
            | MapOne(k2,_) -> 
                let c = comparer.Compare(k,k2) 
                if c = 0 then MapEmpty else m
            | MapNode(k2,v2,l,r,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then rebalance (remove comparer k l) k2 v2 r 
                elif c = 0 then 
                  match l,r with 
                  | MapEmpty,_ -> r
                  | _,MapEmpty -> l
                  | _ -> 
                      let sk,sv,r' = spliceOutSuccessor r 
                      mk l sk sv r'
                else rebalance l k2 v2 (remove comparer k r) 

        let rec mem (comparer: IComparer<'K>) k m = 
            match m with 
            | MapEmpty -> false
            | MapOne(k2,_) -> (comparer.Compare(k,k2) = 0)
            | MapNode(k2,_,l,r,_) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then mem comparer k l
                else (c = 0 || mem comparer k r)

        let rec iter f m = 
            match m with 
            | MapEmpty -> ()
            | MapOne(k2,v2) -> f k2 v2
            | MapNode(k2,v2,l,r,_) -> iter f l; f k2 v2; iter f r

        let rec tryPick f m = 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> f k2 v2 
            | MapNode(k2,v2,l,r,_) -> 
                match tryPick f l with 
                | Some _ as res -> res 
                | None -> 
                    match f k2 v2 with 
                    | Some _ as res -> res 
                    | None -> 
                    tryPick f r

        let rec exists f m = 
            match m with 
            | MapEmpty -> false
            | MapOne(k2,v2) -> f k2 v2
            | MapNode(k2,v2,l,r,_) -> exists f l || f k2 v2 || exists f r

        let rec forall f m = 
            match m with 
            | MapEmpty -> true
            | MapOne(k2,v2) -> f k2 v2
            | MapNode(k2,v2,l,r,_) -> forall f l && f k2 v2 && forall f r

        let rec map f m = 
            match m with 
            | MapEmpty -> empty
            | MapOne(k,v) -> MapOne(k,f v)
            | MapNode(k,v,l,r,h) -> 
                let l2 = map f l 
                let v2 = f v 
                let r2 = map f r 
                MapNode(k,v2,l2, r2,h)

        let rec mapi f m = 
            match m with
            | MapEmpty -> empty
            | MapOne(k,v) -> MapOne(k,f k v)
            | MapNode(k,v,l,r,h) -> 
                let l2 = mapi f l 
                let v2 = f k v 
                let r2 = mapi f r 
                MapNode(k,v2, l2, r2,h)

        let rec foldBack f m x = 
            let f' = OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt(f)
            match m with 
            | MapEmpty -> x
            | MapOne(k,v) -> f'.Invoke(k,v,x)
            | MapNode(k,v,l,r,_) -> 
                let x = foldBack f r x
                let x = f'.Invoke(k,v,x)
                foldBack f l x

        let rec fold f x m  = 
            let f' = OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt(f)
            match m with 
            | MapEmpty -> x
            | MapOne(k,v) -> f'.Invoke(x,k,v)
            | MapNode(k,v,l,r,_) -> 
                let x = fold f x l
                let x = f'.Invoke(x,k,v)
                fold f x r

        let foldSection (comparer: IComparer<'V>) lo hi f m x =
            let rec foldFromTo f m x = 
                match m with 
                | MapEmpty -> x
                | MapOne(k,v) ->
                    let cLoKey = comparer.Compare(lo,k)
                    let cKeyHi = comparer.Compare(k,hi)
                    let x = if cLoKey <= 0 && cKeyHi <= 0 then f k v x else x
                    x
                | MapNode(k,v,l,r,_) ->
                    let cLoKey = comparer.Compare(lo,k)
                    let cKeyHi = comparer.Compare(k,hi)
                    let x = if cLoKey < 0                then foldFromTo f l x else x
                    let x = if cLoKey <= 0 && cKeyHi <= 0 then f k v x                     else x
                    let x = if cKeyHi < 0                then foldFromTo f r x else x
                    x
           
            if comparer.Compare(lo,hi) = 1 then x else foldFromTo f m x


        let toList m = 
            let rec loop m acc = 
                match m with 
                | MapEmpty -> acc
                | MapOne(k,v) -> (k,v)::acc
                | MapNode(k,v,l,r,_) -> loop l ((k,v)::loop r acc)
            loop m []
        let toArray m = m |> toList |> Array.ofList
        let ofList comparer l = List.fold (fun acc (k,v) -> add comparer k v acc) empty l
        let ofArray comparer (arr : array<_>) =
            let mutable res = empty
            for (x,y) in arr do
                res <- add comparer x y res 
            res

        
        let ofSeq comparer (c : seq<'K * 'T>) =
            let rec mkFromEnumerator comparer acc (e : IEnumerator<_>) = 
                if e.MoveNext() then 
                    let (x,y) = e.Current 
                    mkFromEnumerator comparer (add comparer x y acc) e
                else acc
            match c with 
            | :? array<'K * 'T> as xs -> ofArray comparer xs
            | :? list<'K * 'T> as xs -> ofList comparer xs
            | _ -> 
                use ie = c.GetEnumerator()
                mkFromEnumerator comparer empty ie 

          
        let copyToArray s (arr: _[]) i =
            let j = ref i 
            s |> iter (fun x y -> arr.[!j] <- KeyValuePair(x,y); j := !j + 1)


        /// Imperative left-to-right iterators.
        [<NoEquality; NoComparison>]
        type MapIterator<'K,'V when 'K : comparison > = 
             { /// invariant: always collapseLHS result 
               mutable stack: MapTree<'K,'V> list;  
               /// true when MoveNext has been called   
               mutable started : bool }

        // collapseLHS:
        // a) Always returns either [] or a list starting with MapOne.
        // b) The "fringe" of the set stack is unchanged. 
        let rec collapseLHS stack =
            match stack with
            | []                           -> []
            | MapEmpty             :: rest -> collapseLHS rest
            | MapOne _         :: _ -> stack
            | (MapNode(k,v,l,r,_)) :: rest -> collapseLHS (l :: MapOne (k,v) :: r :: rest)
          
        let mkIterator s : MapIterator<_,_> = { stack = collapseLHS [s]; started = false }

        let current i =
            let notStarted() = raise (new System.InvalidOperationException("SR.GetString(SR.enumerationNotStarted)"))
            let alreadyFinished() = raise (new System.InvalidOperationException("SR.GetString(SR.enumerationAlreadyFinished)"))

            if i.started then
                match i.stack with
                  | MapOne (k,v) :: _ -> new KeyValuePair<_,_>(k,v)
                  | []            -> alreadyFinished()
                  | _             -> failwith "Please report error: Map iterator, unexpected stack for current"
            else
                notStarted()

        let moveNext i = // was rec but never calls itself?
          if i.started then
            match i.stack with
              | MapOne _ :: rest -> i.stack <- collapseLHS rest;
                                    not i.stack.IsEmpty
              | [] -> false
              | _ -> failwith "Please report error: Map iterator, unexpected stack for moveNext"
          else
              i.started <- true;  (* The first call to MoveNext "starts" the enumeration. *)
              not i.stack.IsEmpty

        let mkIEnumerator s = 
          let i = ref (mkIterator s) 
          { new IEnumerator<_> with 
                member self.Current = current !i
            interface System.Collections.IEnumerator with
                member self.Current = box (current !i)
                member self.MoveNext() = moveNext !i
                member self.Reset() = i :=  mkIterator s
            interface System.IDisposable with 
                member self.Dispose() = ()}

//#endregion


//#region Additional methods
////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////
///
///               Additional methods
///
////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////
        
        /// FastGenericComparer
        let fgc<'K when 'K : comparison> = 
            LanguagePrimitives.FastGenericComparer<'K> 

        let singleton k v = add fgc k v empty

        let rec tryFindMin m = 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> Some(k2,v2)              
            | MapNode(k2,v2,l,r,_) ->
                if isEmpty l then Some(k2,v2) 
                else tryFindMin l

        /// Read the leftmost element from a non-empty tree. Raises an error if the tree 
        /// is empty. If the tree is sorted this will return the least element.
        let rec findMin m = 
            match tryFindMin m with 
            | Some(r) -> r
            | None -> raise (System.Collections.Generic.KeyNotFoundException())


        let rec tryFindMax  m = 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> Some(k2,v2)                 
            | MapNode(k2,v2,l,r,_) ->
                if isEmpty r then Some(k2,v2)
                else  tryFindMax r

        /// Read the rightmost element from a non-empty tree. Raises an error if the tree 
        /// is empty. If the tree is sorted this will return the greatest element.
        let rec findMax  m = 
            match tryFindMax m with 
            | Some(r) -> r
            | None -> raise (System.Collections.Generic.KeyNotFoundException())


        let rec splitLookupAux (comparer:IComparer<'K>) k (l1:MapTree<'K,'V>) k1 (v1:'V) (r1:MapTree<'K,'V>) : MapTree<'K,'V> * 'V option * MapTree<'K,'V>=
            let c = comparer.Compare(k,k1) 
            if c = 0 then (l1, Some(v1), r1)
            elif c < 0 then
                match l1 with
                | MapEmpty -> (empty, None, rebalance empty k1 v1 r1)
                | MapOne(k2,v2) ->
                    let c = comparer.Compare(k,k2) 
                    if c = 0 then (empty, Some(v2), rebalance empty k1 v1 r1)
                    elif c < 0 then (empty, None, rebalance l1 k1 v1 r1)
                    else (MapOne(k2,v2), None, rebalance empty k1 v1 r1)
                | MapNode(k2,v2,l2,r2,_) ->
                    let c2 = comparer.Compare(k, fst (findMax l1))
                    if c2 > 0 then
                        (l1, None, rebalance empty k1 v1 r1)
                    else 
                        splitLookupAux comparer k l2 k2 v2 (mk r2 k1 v1 r1)
            else
                match r1 with
                | MapEmpty -> (rebalance l1 k1 v1 empty, None, empty)
                | MapOne(k2,v2) ->
                    let c = comparer.Compare(k,k2) 
                    if c = 0 then (rebalance l1 k1 v1 empty, Some(v2), empty)
                    elif c < 0 then (rebalance l1 k1 v1 empty, None, MapOne(k2,v2))
                    else (rebalance l1 k1 v1 r1, None, empty)
                | MapNode(k2,v2,l2,r2,_) ->
                    let c2 = comparer.Compare(k, fst (findMin r1))
                    if c2 < 0 then
                        (rebalance l1 k1 v1 empty, None, r1)
                    else 
                        splitLookupAux comparer k (mk l1 k1 v1 l2) k2 v2 r2
    
        // Performs a split but also returns whether the pivot key was found in the original map.
        let splitLookup (comparer: IComparer<'K>) k m =
            match m with 
            | MapEmpty -> raise (System.Collections.Generic.KeyNotFoundException())
            | MapOne(k1,v1) -> 
                splitLookupAux comparer k empty k1 v1 empty
    //            let c = comparer.Compare(k,k1) 
    //            if c = 0 then (empty, Some(v1), empty)
    //            else raise (System.Collections.Generic.KeyNotFoundException())
            | MapNode(k1,v1,l,r,_) -> 
                splitLookupAux comparer k l k1 v1  r

        
        // Same as for IntMal
        // The expression (split k map) is a pair (map1,map2) where 
        // all keys in map1 are lower than k and all keys in map2 larger 
        // than k. Any key equal to k is found in neither map1 nor map2. http://hackage.haskell.org/package/containers-0.5.0.0/docs/Data-IntMap-Strict.html#v:split
        let split (comparer: IComparer<'K>) k m =
            let l, _, r = splitLookup (comparer: IComparer<'K>) k m
            (l, r)


        /// Find largest key smaller than the given one and return the corresponding (key, value) pair.
        let rec tryFindLT (comparer: IComparer<'K>) (k:'K) (m:MapTree<'K, 'V>) : ('K*'V) option= 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c > 0 then Some(k2,v2)
                else None
            | MapNode(k2,v2,l,r,_) ->
                let c = comparer.Compare(k,k2) 
                if c > 0 then 
                    match tryFindLT comparer k r with
                        | Some(p)  -> Some(p)
                        | None -> Some(k2, v2)
                    //lookupLT comparer k r
                else tryFindLT comparer k l
        
        /// Find largest key smaller or equal to the given one and return the corresponding (key, value) pair.
        let rec tryFindLE (comparer: IComparer<'K>) (k:'K) (m:MapTree<'K, 'V>) : ('K*'V) option= 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c >= 0 then Some(k2,v2)
                else None
            | MapNode(k2,v2,l,r,_) ->
                let c = comparer.Compare(k,k2) 
                if c = 0 then Some(k2,v2)
                elif c > 0 then 
                    match tryFindLE comparer k r with  // check with 15
                    | Some(p)  -> Some(p) // when comparer.Compare(fst p, k2) > 0 
                    | None -> Some(k2, v2)
                else tryFindLE comparer k l


        /// Find largest key smaller than the given one and return the corresponding (key, value) pair.
        let rec tryFindGT (comparer: IComparer<'K>) (k:'K) (m:MapTree<'K, 'V>) : ('K*'V) option= 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c < 0 then Some(k2,v2)
                else None
            | MapNode(k2,v2,l,r,_) ->
                let c = comparer.Compare(k,k2) 
                if c < 0 then 
                    match tryFindGT comparer k l with
                        | Some(p)  -> Some(p)
                        | None -> Some(k2, v2)
                    //lookupLT comparer k r
                else tryFindGT comparer k r
        
        /// Find largest key smaller or equal to the given one and return the corresponding (key, value) pair.
        let rec tryFindGE (comparer: IComparer<'K>) (k:'K) (m:MapTree<'K, 'V>) : ('K*'V) option= 
            match m with 
            | MapEmpty -> None
            | MapOne(k2,v2) -> 
                let c = comparer.Compare(k,k2) 
                if c <= 0 then Some(k2,v2)
                else None
            | MapNode(k2,v2,l,r,_) ->
                let c = comparer.Compare(k,k2) 
                if c = 0 then Some(k2,v2)
                elif c < 0 then 
                    match tryFindGE comparer k l with  // check with 15
                    | Some(p)  -> Some(p) // when comparer.Compare(fst p, k2) > 0 
                    | None -> Some(k2, v2)
                else tryFindGE comparer k r

//#endregion



namespace Spreads.Collections
    open System
    open System.Collections
    open System.Collections.Generic
    open System.Linq
    open System.Runtime.InteropServices

    open Spreads
    open Spreads.Cursors
    open Spreads.Collections

    [<Sealed>]
    [<CompiledName("ImmutableSortedMap`2")>]
    type ImmutableSortedMap<[<EqualityConditionalOn>]'K,[<EqualityConditionalOn;ComparisonConditionalOn>]'V when 'K : comparison >
      internal(comparer: KeyComparer<'K>, tree: MapTree<'K,'V>) =
      inherit ContainerSeries<'K,'V, Cursor<'K,'V>>()

      let syncRoot = new Object()

      // We use .NET generics per-instantiation static fields to avoid allocating a new object for each empty
      // set (it is just a lookup into a .NET table of type-instantiation-indexed static fields).
      static let empty = 
        let comparer = KeyComparer<'K>.Default
        new ImmutableSortedMap<'K,'V>(comparer,MapTree<_,_>.MapEmpty)

      static member Empty : ImmutableSortedMap<'K,'V> = empty

      static member Create(elements : IEnumerable<KeyValuePair<'K, 'V>>) : ImmutableSortedMap<'K,'V> = 
        let comparer = KeyComparer<'K>.Default
        new ImmutableSortedMap<_,_>(comparer,MapTree.ofSeq comparer (elements |> Seq.map (fun x -> x.Key,x.Value) ))
    
      static member Create() : ImmutableSortedMap<'K,'V> = empty

      new(elements : IEnumerable<KeyValuePair<'K, 'V>>) = 
        let comparer = KeyComparer<'K>.Default
        new ImmutableSortedMap<_,_>(comparer,MapTree.ofSeq comparer (elements |> Seq.map (fun x -> x.Key,x.Value) ))
    
      override this.Comparer = comparer
      //[<DebuggerBrowsable(DebuggerBrowsableState.Never)>]
      member internal m.Tree = tree

      override this.IsEmpty with get() = MapTree.isEmpty tree
      override this.IsIndexed with get() = false
      override this.IsReadOnly with get() = true
      override this.Updated = Utils.TaskUtil.FalseTask

      override this.First
        with get() = 
          let res = MapTree.tryFindMin tree
          if res.IsNone then raise (InvalidOperationException("Could not get the first element of an empty map"))
          KeyValuePair(fst res.Value, snd res.Value)

      override this.Last
        with get() = 
          let res = MapTree.tryFindMax tree
          if res.IsNone then raise (InvalidOperationException("Could not get the last element of an empty map"))
          KeyValuePair(fst res.Value, snd res.Value)
      
      //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
      override this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
      
      //[<ObsoleteAttribute("Naive impl, optimize if used often")>]
      override this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)

      override this.TryFind(key,direction:Lookup, [<Out>]result: byref<KeyValuePair<'K, 'V>>) : bool =
        result <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        match direction with
        | Lookup.EQ -> 
          let tr = MapTree.tryFind comparer key tree
          if tr.IsNone then 
            false
          else
            result <- KeyValuePair<'K, 'V>(key, tr.Value)
            true
        | Lookup.LT -> 
          let tr = MapTree.tryFindLT comparer key tree
          if tr.IsNone then 
            false
          else
            result <- KeyValuePair<'K, 'V>(fst tr.Value, snd tr.Value)
            true
        | Lookup.LE -> 
          let tr = MapTree.tryFindLE comparer key tree
          if tr.IsNone then 
            false
          else
            result <- KeyValuePair<'K, 'V>(fst tr.Value, snd tr.Value)
            true
        | Lookup.GT -> 
          let tr = MapTree.tryFindGT comparer key tree
          if tr.IsNone then 
            false
          else
            result <- KeyValuePair<'K, 'V>(fst tr.Value, snd tr.Value)
            true
        | Lookup.GE ->
          let tr = MapTree.tryFindGE comparer key tree
          if tr.IsNone then 
            false
          else
            result <- KeyValuePair<'K, 'V>(fst tr.Value, snd tr.Value)
            true
        | _ -> raise (ApplicationException("Wrong lookup direction"))

      override this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
        try
          res <- this.First
          true
        with
        | _ -> 
          res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
          false
            
      override this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
        try
          res <- this.Last
          true
        with
        | _ -> 
          res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
          false

      override this.GetCursor() = new MapCursor<'K, 'V>(this) :> ICursor<'K, 'V>
      
      override this.GetContainerCursor() = this.GetWrapper()

      member this.Size with get() = int64(MapTree.size tree)

      member this.Add(k, v):ImmutableSortedMap<'K,'V> = 
        ImmutableSortedMap(comparer, MapTree.add comparer k v tree)

      member this.AddFirst(k, v):ImmutableSortedMap<'K,'V> = 
        if not this.IsEmpty && k >= this.First.Key then 
            raise (ArgumentOutOfRangeException("New key is larger or equal to the smallest existing key"))
        this.Add(k, v)

      member this.AddLast(k, v):ImmutableSortedMap<'K,'V> = 
        if not this.IsEmpty && k <= this.Last.Key then 
            raise (ArgumentOutOfRangeException("New key is smaller or equal to the largest existing key"))
        this.Add(k, v)
    
      member this.Remove(k):ImmutableSortedMap<'K,'V> = 
        ImmutableSortedMap<'K,'V>(comparer, MapTree.remove comparer k tree)

      member this.RemoveLast([<Out>]value: byref<KeyValuePair<'K,'V>>):ImmutableSortedMap<'K,'V>=
        if this.IsEmpty then raise (InvalidOperationException("Could not get the last element of an empty map"))
        let m,_ = MapTree.split comparer (this.Last.Key) tree
        value <- KeyValuePair(this.Last.Key, this.Last.Value)
        ImmutableSortedMap<'K,'V>(comparer, m)
        
      member this.RemoveFirst([<Out>]value: byref<KeyValuePair<'K,'V>>):ImmutableSortedMap<'K,'V>=
        if this.IsEmpty then raise (InvalidOperationException("Could not get the first element of an empty map"))
        let _,m = MapTree.split comparer (this.First.Key) tree
        value <- KeyValuePair(this.First.Key, this.First.Value)
        ImmutableSortedMap<'K,'V>(comparer, m)

      member this.RemoveMany(k, direction:Lookup):ImmutableSortedMap<'K,'V>=
        let newTree = 
          match direction with
          | Lookup.LT ->
            let lt, eq, gt = (MapTree.splitLookup comparer k tree)
            MapTree.add comparer k eq.Value gt
          | Lookup.LE ->
            let lt, eq, gt = (MapTree.splitLookup comparer k tree)
            gt
          | Lookup.GT ->
            let lt, eq, gt = (MapTree.splitLookup comparer k tree)
            MapTree.add comparer k eq.Value lt
          | Lookup.GE ->
            let lt, eq, gt = (MapTree.splitLookup comparer k tree)
            lt
          | Lookup.EQ ->
            MapTree.remove comparer k tree
          | _ -> failwith "unexpected wrong direction"
        ImmutableSortedMap<'K,'V>(comparer, newTree)

      interface IImmutableSeries<'K, 'V> with
        member this.Updated = falseTask
        member this.Size with get() = this.Size
        member this.Add(key, value):IImmutableSeries<'K, 'V> =
          this.Add(key, value) :> IImmutableSeries<'K, 'V>

        member this.AddFirst(key, value):IImmutableSeries<'K, 'V> =
          this.AddFirst(key, value) :> IImmutableSeries<'K, 'V>

        member this.AddLast(key, value):IImmutableSeries<'K, 'V> =
          this.AddLast(key, value) :> IImmutableSeries<'K, 'V>

        member this.Remove(key):IImmutableSeries<'K, 'V> =
          this.Remove(key) :> IImmutableSeries<'K, 'V>

        member this.RemoveLast([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableSeries<'K, 'V> =
          let m,v = this.RemoveLast()
          value <- v
          m :> IImmutableSeries<'K, 'V>

        member this.RemoveFirst([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableSeries<'K, 'V> =
          let m,v = this.RemoveFirst()
          value <- v
          m :> IImmutableSeries<'K, 'V>

        member this.RemoveMany(key,direction:Lookup):IImmutableSeries<'K, 'V> =
          this.RemoveMany(key, direction) :> IImmutableSeries<'K, 'V>
