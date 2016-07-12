(*  
    Copyright (c) 2014-2016 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
        
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

// Ported from https://github.com/fsharp/fsharpx/blob/master/src/FSharpx.Collections.Experimental/IntMap.fs
// which in turn ported from http://hackage.haskell.org/packages/archive/containers/latest/doc/html/src/Data-IntMap-Base.html

namespace Spreads.Collections

//#region Original implementation 

open System.Collections
open System.Collections.Generic
open Spreads

#nowarn "25"


[<NoEquality; NoComparison>]
type internal IntMap64UTree<'T> =
    | Nil
    | Tip of uint64 * 'T
    | Bin of uint64 * uint64 * IntMap64UTree<'T> * IntMap64UTree<'T> // Prefix * Mask * Map * Map

    member x.FoldBackWithKey f z =
        let rec go z =
            function
            | Nil -> z
            | Tip(kx, x) -> f kx x z
            | Bin(_, _, l, r) -> go (go z r) l
        match x with
        | Bin(_, m, l, r) when m < 0UL -> go (go z l) r  // put negative numbers before.
        | Bin(_, m, l, r) -> go (go z r) l
        | _ -> go z x
    
    member x.ToList() = x.FoldBackWithKey (fun k x xs -> (k, x) :: xs) []

    interface IEnumerable<uint64 * 'T> with
        member x.GetEnumerator() =
            (x.ToList() :> (_ * _) seq).GetEnumerator()
        
        member x.GetEnumerator() =
            (x :> _ seq).GetEnumerator() :> IEnumerator




[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal IntMap64UTree =

    let inline private maskW i m = uint64 (i &&& (~~~ (m - 1UL) ^^^ m)) // Nat -> Nat -> Prefix
    let inline private mask i m = maskW (uint64 i) (uint64 m) // Key -> Mask -> Prefix
    let inline private match' i p m = mask i m = p // Key -> Prefix -> Mask -> Bool
    let inline private nomatch i p m = mask i m <> p // Key -> Prefix -> Mask -> Bool
    let inline private zero i m = (uint64 i) &&& (uint64 m) = 0UL 
    let inline private shorter m1 m2 = (uint64 m1) > (uint64 m2) // Mask -> Mask -> Bool // with Big-endian a larger mask will be closer to the roor

    let inline private highestBitMask x0 =
        let x1 = x0 ||| (x0 >>> 1)
        let x2 = x1 ||| (x1 >>> 2)
        let x3 = x2 ||| (x2 >>> 4)
        let x4 = x3 ||| (x3 >>> 8)
        let x5 = x4 ||| (x4 >>> 16)
        let x6 = x5 ||| (x5 >>> 32)   // for 64 bit platforms
        x6 ^^^ (x6 >>> 1)

    let inline private branchMask p1 p2 = uint64 (highestBitMask (uint64 p1 ^^^ uint64 p2))

    // called link in Haskell // Prefix -> IntMap a -> Prefix -> IntMap a -> IntMap a
    let inline private join p1 t1 p2 t2 =
        let m = branchMask p1 p2
        let p = mask p1 m
        if zero p1 m then Bin(p, m, t1, t2)
        else Bin(p, m, t2, t1)

    let inline private bin p m l r =
        match l, r with
        | (l, Nil) -> l
        | (Nil, r) -> r
        | (l, r) -> Bin(p, m, l, r)

    ///O(1). Map is empty.  Credit: Haskell.org
    let isEmpty =
        function
        | Nil -> true
        | _ -> false

    ///O(n). Number of elements in the map. Credit: Haskell.org
    let rec size =
        function
        | Bin(_, _, l, r) -> size l + size r
        | Tip _ -> 1UL
        | Nil -> 0UL

    ///O(min(n,W)). Lookup the value at a key in the map. Returns 'T option. Credit: Haskell.org
    let rec tryFind k =
        function
        | Bin(p, m, l, r) when nomatch k p m -> None
        | Bin(p, m, l, r) when zero k m -> tryFind k l
        | Bin(p, m, l, r) -> tryFind k r
        | Tip(kx, x) when k = kx -> Some x
        | Tip(kx, x) -> None
        | Nil -> None

    ///O(min(n,W)). Is the key a member of the map? Credit: Haskell.org
    let rec exists k =
        function
        | Bin(p, m, l, r) when nomatch k p m -> false
        | Bin(p, m, l, r) when zero k m -> exists k l
        | Bin(p, m, l, r) -> exists k r
        | Tip(kx, _) -> k = kx
        | Nil -> false

    ///O(log n). Is the key not a member of the map? Credit: Haskell.org
    let notExists k m = not <| exists k m

    ///O(min(n,W)). Lookup the value at a key in the map. Credit: Haskell.org
    let rec find k m =
        let notFound() = raise (System.Collections.Generic.KeyNotFoundException(sprintf "IntMap64UTree.find: key %d is not an element of the map" k))
        match m with
        | Bin(p, m, l, r) when nomatch k p m -> notFound()
        | Bin(p, m, l, r) when zero k m -> find k l
        | Bin(p, m, l, r) -> find k r
        | Tip(kx, x) when k = kx -> x
        | Tip(kx, x) -> notFound()
        | Nil -> notFound()

    ///O(min(n,W)). The expression (findWithDefault def k map) returns the value at key k or returns def when the key is not an element of the map.  Credit: Haskell.org
    let rec findWithDefault def k =
        function
        | Bin(p, m, l, r) when nomatch k p m -> def
        | Bin(p, m, l, r) when zero k m -> findWithDefault def k l
        | Bin(p, m, l, r) -> findWithDefault def k r
        | Tip(kx, x) when k = kx -> x
        | Tip(kx, x) -> def
        | Nil -> def

    let rec private unsafeFindMax =
        function
        | Nil -> None
        | Tip(ky, y) -> Some(ky, y)
        | Bin(_, _, _, r) -> unsafeFindMax r

    ///O(log n). Find largest key smaller than the given one and return the corresponding (key, value) pair.  Credit: Haskell.org
    let tryFindLT k t =
        let rec go def =
            function
            | Bin(p, m, l, r) when nomatch k p m -> if k < p then unsafeFindMax def else unsafeFindMax r
            | Bin(p, m, l, r) when zero k m -> go def l
            | Bin(p, m, l, r) -> go l r
            | Tip(ky, y) when k <= ky -> unsafeFindMax def
            | Tip(ky, y) -> Some(ky, y)
            | Nil -> unsafeFindMax def
        match t with
        | Bin(_, m, l, r) when m < 0UL -> if k >= 0UL then go r l else go Nil r
        | _ -> go Nil t

    let rec private unsafeFindMin =
        function
        | Nil -> None
        | Tip(ky, y) -> Some(ky, y)
        | Bin(_, _, l, _) -> unsafeFindMin l

    ///O(log n). Find smallest key greater than the given one and return the corresponding (key, value) pair. Credit: Haskell.org
    let tryFindGT k t =
        let rec go def =
            function
            | Bin(p, m, l, r) when nomatch k p m -> if k < p then unsafeFindMin l else unsafeFindMin def
            | Bin(p, m, l, r) when zero k m -> go r l
            | Bin(p, m, l, r) -> go def r
            | Tip(ky, y) when k >= ky -> unsafeFindMin def
            | Tip(ky, y) -> Some(ky, y)
            | Nil -> unsafeFindMin def
        match t with
        | Bin(_, m, l, r) when m < 0UL -> if k >= 0UL then go Nil l else go l r
        | _ -> go Nil t

    ///O(log n). Find largest key smaller or equal to the given one and return the corresponding (key, value) pair. Credit: Haskell.org
    let tryFindLE k t =
        let rec go def =
            function
            | Bin(p, m, l, r) when nomatch k p m -> if k < p then unsafeFindMax def else unsafeFindMax r
            | Bin(p, m, l, r) when zero k m -> go def l
            | Bin(p, m, l, r) -> go l r
            | Tip(ky, y) when k < ky -> unsafeFindMax def
            | Tip(ky, y) -> Some(ky, y)
            | Nil -> unsafeFindMax def
        match t with
        | Bin(_, m, l, r) when m < 0UL -> if k >= 0UL then go r l else go Nil r
        | _ -> go Nil t

    ///O(log n). Find smallest key greater or equal to the given one and return the corresponding (key, value) pair Credit: Haskell.org
    let tryFindGE k t =
        let rec go def =
            function
            | Bin(p, m, l, r) when nomatch k p m -> if k < p then unsafeFindMin l else unsafeFindMin def
            | Bin(p, m, l, r) when zero k m -> go r l
            | Bin(p, m, l, r) -> go def r
            | Tip(ky, y) when k > ky -> unsafeFindMin def
            | Tip(ky, y) -> Some(ky, y)
            | Nil -> unsafeFindMin def
        match t with
        | Bin(_, m, l, r) when m < 0UL -> if k >= 0UL then go Nil l else go l r
        | _ -> go Nil t

    ///O(1). The empty map. Credit: Haskell.org
    let empty = Nil

    ///O(1). A map of one element. Credit: Haskell.org
    let inline singleton k x = Tip(k, x)

    ///O(min(n,W)). Insert a new key/value pair in the map. If the key is already present in the map, the associated value is replaced with the supplied value, i.e. insert is equivalent to insertWith const. Credit: Haskell.org
    let rec insert k x t =
        match t with
        | Bin(p, m, l, r) when nomatch k p m -> join k (Tip(k, x)) p t
        | Bin(p, m, l, r) when zero k m -> Bin(p, m, insert k x l, r)
        | Bin(p, m, l, r) -> Bin(p, m, l, insert k x r)
        | Tip(ky, _) when k = ky -> Tip(k, x)
        | Tip(ky, _) -> join k (Tip(k, x)) ky t
        | Nil -> Tip(k, x)

    ///O(min(n,W)). Insert with a combining function. insertWithKey f key value mp will insert the pair (key, value) into mp if key does not exist in the map. If the key does exist, the function will insert f key new_value old_value. Credit: Haskell.org
    let rec insertWithKey f k x t =
        match t with
        | Bin(p, m, l, r) when nomatch k p m -> join k (Tip(k, x)) p t
        | Bin(p, m, l, r) when zero k m -> Bin(p, m, insertWithKey f k x l, r)
        | Bin(p, m, l, r) -> Bin(p, m, l, insertWithKey f k x r)
        | Tip(ky, y) when k = ky -> Tip(k, f k x y)
        | Tip(ky, _) -> join k (Tip(k, x)) ky t
        | Nil -> Tip(k, x)

    ///O(min(n,W)). Insert with a combining function. insertWith f key value mp will insert the pair (key, value) into mp if key does not exist in the map. If the key does exist, the function will insert f new_value old_value. Credit: Haskell.org
    let insertWith f k x t = insertWithKey (fun _ x' y' -> f x' y') k x t

    ///O(min(n,W)). The expression (insertLookupWithKey f k x map) is a pair where the first element is equal to (lookup k map) and the second element equal to (insertWithKey f k x map). Credit: Haskell.org
    let rec insertTryFindWithKey f k x t =
        match t with
        | Bin(p, m, l, r) when nomatch k p m -> (None, join k (Tip(k, x)) p t)
        | Bin(p, m, l, r) when zero k m ->
            let found, l = insertTryFindWithKey f k x l
            (found, Bin(p, m, l, r))
        | Bin(p, m, l, r) ->
            let found, r = insertTryFindWithKey f k x r
            (found, Bin(p, m, l, r))
        | Tip(ky, y) when k = ky -> (Some y, Tip(k, f k x y))
        | Tip(ky, _) -> (None, join k (Tip(k, x)) ky t)
        | Nil -> (None, Tip(k, x))

    ///O(min(n,W)). Delete a key and its value from the map. When the key is not a member of the map, the original map is returned. Credit: Haskell.org
    let rec delete k t =
        match t with
        | Bin(p, m, l, r) when nomatch k p m -> t
        | Bin(p, m, l, r) when zero k m -> bin p m (delete k l) r
        | Bin(p, m, l, r) -> bin p m l (delete k r)
        | Tip(ky, _) when k = ky -> Nil
        | Tip _ -> t
        | Nil -> Nil

    ///O(min(n,W)). The expression (update f k map) updates the value x at k (if it is in the map). If (f k x) is Nothing, the element is deleted. If it is (Just y), the key k is bound to the new value y. Credit: Haskell.org
    let rec updateWithKey f k t =
        match t with
        | Bin(p, m, l, r) when nomatch k p m -> t
        | Bin(p, m, l, r) when zero k m -> bin p m (updateWithKey f k l) r
        | Bin(p, m, l, r) -> bin p m l (updateWithKey f k r)
        | Tip(ky, y) when k = ky ->
            match f k y with
            | Some y -> Tip(ky, y)
            | None -> Nil
        | Tip _ -> t
        | Nil -> Nil

    ///O(min(n,W)). The expression (update f k map) updates the value x at k (if it is in the map). If (f x) is Nothing, the element is deleted. If it is (Just y), the key k is bound to the new value y. Credit: Haskell.org
    let update f k m = updateWithKey (fun _ x -> f x) k m

    ///O(min(n,W)). Adjust a value at a specific key. When the key is not a member of the map, the original map is returned. Credit: Haskell.org
    let adjustWithKey f k m = updateWithKey (fun k' x -> Some (f k' x)) k m

    ///O(min(n,W)). Adjust a value at a specific key. When the key is not a member of the map, the original map is returned. Credit: Haskell.org
    let adjust f k m = adjustWithKey (fun _ x -> f x) k m

    ///O(min(n,W)). Lookup and update. Credit: Haskell.org
    let rec updateTryFindWithKey f k t =
        match t with
        | Bin(p, m, l, r) when nomatch k p m -> (None, t)
        | Bin(p, m, l, r) when zero k m ->
            let (found, l) = updateTryFindWithKey f k l
            (found, bin p m l r)
        | Bin(p, m, l, r) ->
            let (found, r) = updateTryFindWithKey f k r
            (found, bin p m l r)
        | Tip(ky, y) when k = ky ->
            match f k y with
            | Some y' -> (Some y, Tip(ky, y'))
            | None -> (Some y, Nil)
        | Tip(ky, _) -> (None, t)
        | Nil -> (None, Nil)

    ///O(log n). The expression (alter f k map) alters the value x at k, or absence thereof. alter can be used to insert, delete, or update a value in an IntMap64UTree. Credit: Haskell.org
    let rec alter f k t =
        match t with
        | Bin(p, m, l, r) when nomatch k p m ->
            match f None with
            | None -> t
            | Some x -> join k (Tip(k, x)) p t
        | Bin(p, m, l, r) when zero k m -> bin p m (alter f k l) r
        | Bin(p, m, l, r) -> bin p m l (alter f k r)
        | Tip(ky, y) when k = ky ->
            match f (Some y) with
            | Some x -> Tip(ky, x)
            | None -> Nil
        | Tip(ky, y) ->
            match f None with
            | Some x -> join k (Tip(k, x)) ky t
            | None -> Tip(ky, y)
        | Nil ->
            match f None with
            | Some x -> Tip(k, x)
            | None -> Nil

    let inline private mergeWithKey' bin' f g1 g2 =

        let inline maybe_join p1 t1 p2 t2  =
            match t1, t2 with
            | Nil, t2 -> t2
            | t1, Nil -> t1
            | _ ->  join p1 t1 p2 t2
     
        let rec merge1 p1 m1 t1 l1 r1 p2 m2 t2 =
            if nomatch p2 p1 m1 then maybe_join p1 (g1 t1) p2 (g2 t2)
            elif zero p2 m1 then bin' p1 m1 (go l1 t2) (g1 r1)
            else bin' p1 m1 (g1 l1) (go r1 t2)

        and merge2 p1 m1 t1 p2 m2 t2 l2 r2 =
            if nomatch p1 p2 m2 then maybe_join p1 (g1 t1) p2 (g2 t2)
            elif zero p1 m2 then bin' p2 m2 (go t1 l2) (g2 r2)
            else bin' p2 m2 (g2 l2) (go t1 r2)

        and go t1 t2 =
            match t1, t2 with
            | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when shorter m1 m2 -> merge1 p1 m1 t1 l1 r1 p2 m2 t2
            | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when shorter m2 m1 -> merge2 p1 m1 t1 p2 m2 t2 l2 r2
            | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when p1 = p2 -> bin' p1 m1 (go l1 l2) (go r1 r2)
            | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) -> maybe_join p1 (g1 t1) p2 (g2 t2)
            | Bin(_, _, _, _), Tip( k2', _) ->
                let rec merge t2 k2 t1 =
                    match t1 with
                    | Bin(p1, m1, l1, r1) when nomatch k2 p1 m1 -> maybe_join p1 (g1 t1) k2 (g2 t2)
                    | Bin(p1, m1, l1, r1) when zero k2 m1 -> bin' p1 m1 (merge t2 k2 l1) (g1 r1)
                    | Bin(p1, m1, l1, r1) -> bin' p1 m1 (g1 l1) (merge t2 k2 r1)
                    | Tip(k1, _) when k1 = k2 -> f t1 t2
                    | Tip(k1, _) -> maybe_join k1 (g1 t1) k2 (g2 t2)
                    | Nil -> g2 t2
                merge t2 k2' t1
            | Bin(_, _, _, _), Nil -> g1 t1
            | Tip(k1', _), t2' -> 
                let rec merge t1 k1 t2 =
                    match t2 with
                    | Bin(p2, m2, l2, r2) when nomatch k1 p2 m2 -> maybe_join k1 (g1 t1) p2 (g2 t2)
                    | Bin(p2, m2, l2, r2) when zero k1 m2 -> bin' p2 m2 (merge t1 k1 l2) (g2 r2)
                    | Bin(p2, m2, l2, r2) -> bin' p2 m2 (g2 l2) (merge t1 k1 r2)
                    | Tip(k2, _) when k1 = k2 -> f t1 t2
                    | Tip(k2, _) -> maybe_join k1 (g1 t1) k2 (g2 t2)
                    | Nil -> g1 t1
                merge t1 k1' t2'
            | Nil, t2 -> g2 t2
        go

    ///Refer to Haskell documentation. Unexpected code growth or corruption of the data structure can occure from wrong use. Credit: Haskell.org
    let mergeWithKey f g1 g2 =
        let combine =
            fun (Tip(k1, x1)) (Tip(_, x2)) ->
                match f k1 x1 x2 with
                | None -> Nil
                | Some x -> Tip(k1, x)
        mergeWithKey' bin combine g1 g2

    let append m1 m2 = mergeWithKey' (fun x y m1' m2' -> Bin(x, y, m1', m2')) konst id id m1 m2

    let appendWithKey f m1 m2 =
        mergeWithKey' (fun x y m1' m2' -> Bin(x, y, m1', m2')) (fun (Tip(k1, x1)) (Tip(_, x2)) -> Tip(k1, f k1 x1 x2)) id id m1 m2

    let appendWith f m1 m2 = appendWithKey (fun _ x y -> f x y) m1 m2

    let concat xs = List.fold append empty xs

    let concatWith f xs = List.fold (appendWith f) empty xs

    ///O(n+m). Difference between two maps (based on keys). Credit: Haskell.org
    let difference m1 m2 = mergeWithKey (fun _ _ _ -> None) id (konst Nil) m1 m2

    ///O(n+m). Difference with a combining function. When two equal keys are encountered, the combining function is applied to the key and both values. If it returns Nothing, the element is discarded (proper set difference). If it returns (Just y), the element is updated with a new value y. Credit: Haskell.org
    let differenceWithKey f m1 m2 = mergeWithKey f id (konst Nil) m1 m2

    ///O(n+m). Difference with a combining function. Credit: Haskell.org
    let differenceWith f m1 m2 = differenceWithKey (fun _ x y -> f x y) m1 m2

    ///O(n+m). The (left-biased) intersection of two maps (based on keys). Credit: Haskell.org
    let intersection m1 m2 = mergeWithKey' bin konst (konst Nil) (konst Nil) m1 m2

    ///O(n+m). The intersection with a combining function. Credit: Haskell.org
    let intersectionWithKey f m1 m2 =
        mergeWithKey' bin (fun (Tip(k1, x1)) (Tip(_, x2)) -> Tip(k1, f k1 x1 x2)) (konst Nil) (konst Nil) m1 m2

    ///O(n+m). The intersection with a combining function. Credit: Haskell.org
    let intersectionWith f m1 m2 = intersectionWithKey (fun _ x y -> f x y) m1 m2

    ///O(n+m). The union with a combining function.
    let unionWithKey f m1 m2 = mergeWithKey (fun k x1 x2 -> Some (f k x1 x2)) id id m1 m2

    ///O(n+m). The union with a combining function.
    let unionWith f m1 m2 = unionWithKey (fun _ x1 x2 -> f x1 x2) m1 m2

    ///O(n+m). The (left-biased) union of two maps. It prefers the first map when duplicate keys are encountered
    let union m1 m2 = unionWithKey (fun _ x1 _ -> x1) m1 m2

    ///O(log n). Update the value at the minimal key. Credit: Haskell.org
    let updateMinWithKey f t =
        let rec go f =
            function
            | Bin(p, m, l, r) -> bin p m (go f l) r
            | Tip(k, y) ->
                match f k y with
                | Some y -> Tip(k, y)
                | None -> Nil
            | Nil -> failwith "updateMinWithKey Nil"
        match t with
        | Bin(p, m, l, r) when m < 0UL -> bin p m l (go f r)
        | _ -> go f t

    ///O(log n). Update the value at the maximal key. Credit: Haskell.org
    let updateMaxWithKey f t =
        let rec go f =
            function
            | Bin(p, m, l, r) -> bin p m l (go f r)
            | Tip(k, y) ->
                match f k y with
                | Some y -> Tip(k, y)
                | None -> Nil
            | Nil -> failwith "updateMaxWithKey Nil"
        match t with
        | Bin(p, m, l, r) when m < 0UL -> bin p m (go f l) r
        | _ -> go f t

    ///O(log n). Retrieves the maximal (key,value) couple of the map, and the map stripped from that element. fails (in the monad) when passed an empty map. Credit: Haskell.org
    let maxViewWithKey t =
        let rec go =
            function
            | Bin(p, m, l, r) -> let (result, r) = go r in (result, bin p m l r)
            | Tip(k, y) -> ((k, y), Nil)
            | Nil -> failwith "maxViewWithKey Nil"
        match t with
        | Nil -> None
        | Bin(p, m, l, r) when m < 0UL -> let (result, l) = go l in Some(result, bin p m l r)
        | _ -> Some(go t)

    ///O(log n). Retrieves the minimal (key,value) couple of the map, and the map stripped from that element. fails (in the monad) when passed an empty map. Credit: Haskell.org
    let minViewWithKey t =
        let rec go =
            function
            | Bin(p, m, l, r) -> let (result, l) = go l in (result, bin p m l r)
            | Tip(k, y) -> ((k,y), Nil)
            | Nil -> failwith "minViewWithKey Nil"
        match t with
        | Nil -> None
        | Bin(p, m, l, r) when m < 0UL -> let (result, r) = go r in Some(result, bin p m l r)
        | _ -> Some(go t)

    ///O(log n). Update the value at the maximal key. Credit: Haskell.org
    let updateMax f = updateMaxWithKey (konst f)

    ///O(log n). Update the value at the minimal key. Credit: Haskell.org
    let updateMin f = updateMinWithKey (konst f)

    let private first f (x, y) = (f x, y)

    ///O(min(n,W)). Retrieves the maximal key of the map, and the map stripped of that element, or Nothing if passed an empty map. Credit: Haskell.org
    let maxView t = Option.map (first snd) (maxViewWithKey t)

    ///O(min(n,W)). Retrieves the minimal key of the map, and the map stripped of that element, or Nothing if passed an empty map. Credit: Haskell.org
    let minView t = Option.map (first snd) (minViewWithKey t)

    ///O(log n). Retrieves the maximal key of the map, and the map stripped from that element. Credit: Haskell.org
    let deleteFindMax t = 
      match maxViewWithKey <| t with
        | Some x -> x
        | _ -> failwith "deleteFindMax: empty map has no maximal element"

    ///O(log n). Retrieves the minimal key of the map, and the map stripped from that element. Credit: Haskell.org
    let deleteFindMin t = 
      match minViewWithKey <| t with
      | Some x -> x
      | _ -> failwith "deleteFindMin: empty map has no minimal element"

    ///O(log n). The minimal key of the map. Credit: Haskell.org
    let findMin t =
        let rec go =
            function
            | Tip(k, v) -> (k, v)
            | Bin(_, _, l, _) -> go l
            | Nil -> failwith "findMin Nil"
        match t with
        | Nil -> raise (System.Collections.Generic.KeyNotFoundException("findMin: empty map has no minimal element"))
        | Tip(k, v) -> (k, v)
        | Bin(_, m, l, r) when m < 0UL -> go r
        | Bin(_, m, l, r) -> go l

    ///O(log n). The minimal key of the map. Credit: Haskell.org
    let tryFindMin t =
        let rec go =
            function
            | Tip(k, v) -> Some(k, v)
            | Bin(_, _, l, _) -> go l
            | Nil -> failwith "findMax Nil"
        match t with
        | Nil -> None
        | Tip(k, v) -> Some(k, v)
        | Bin(_, m, l, r) when m < 0UL -> go r
        | Bin(_, m, l, r) -> go l

    ///O(log n). The maximal key of the map. Credit: Haskell.org
    let findMax t =
        let rec go =
            function
            | Tip(k, v) -> (k, v)
            | Bin(_, _, _, r) -> go r
            | Nil -> failwith "findMax Nil"
        match t with
        | Nil -> raise (System.Collections.Generic.KeyNotFoundException("findMax: empty map has no maximal element"))
        | Tip(k, v) -> (k, v)
        | Bin(_, m, l, r) when m < 0UL -> go l
        | Bin(_, m, l, r) -> go r

    ///O(log n). The maximal key of the map. Credit: Haskell.org
    let tryFindMax t =
        let rec go =
            function
            | Tip(k, v) -> Some(k, v)
            | Bin(_, _, _, r) -> go r
            | Nil -> failwith "findMax Nil"
        match t with
        | Nil -> None
        | Tip(k, v) -> Some(k, v)
        | Bin(_, m, l, r) when m < 0UL -> go l
        | Bin(_, m, l, r) -> go r

    ///O(log n). Delete the minimal key. Credit: Haskell.org
    let deleteMin t = 
      match minView <| t with
      | Some x -> snd x
      | _ -> Nil

    ///O(log n). Delete the maximal key. Credit: Haskell.org
    let deleteMax t = 
      match maxView <| t with
        | Some x -> snd x
        | _ -> Nil

    ///O(n). Map a function over all values in the map. Credit: Haskell.org
    let rec mapWithKey f =
        function
        | Bin(p, m, l, r) -> Bin(p, m, mapWithKey f l, mapWithKey f r)
        | Tip(k, x) -> Tip(k, f k x)
        | Nil -> Nil

    ///O(n). Map a function over all values in the map. Credit: Haskell.org
    let rec map f =
        function
        | Bin(p, m, l, r) -> Bin(p, m, map f l, map f r)
        | Tip(k, x) -> Tip(k, f x)
        | Nil -> Nil

    let rec private mapAccumL f a =
        function
        | Bin(p, m, l, r) ->
            let (a1,l) = mapAccumL f a l
            let (a2,r) = mapAccumL f a1 r
            (a2, Bin(p, m, l, r))
        | Tip(k, x) -> let (a,x) = f a k x in (a,Tip(k, x))
        | Nil -> (a, Nil)

    ///O(n). The function mapAccum threads an accumulating argument through the map in ascending order of keys. Credit: Haskell.org
    let mapAccumWithKey f a t = mapAccumL f a t

    ///O(n). The function mapAccumWithKey threads an accumulating argument through the map in ascending order of keys. Credit: Haskell.org
    let mapAccum f = mapAccumWithKey (fun a' _ x -> f a' x)

    ///O(n). Filter all keys/values that satisfy some predicate. Credit: Haskell.org
    let rec filterWithKey predicate =
        function
        | Bin(p, m, l, r) -> bin p m (filterWithKey predicate l) (filterWithKey predicate r)
        | Tip(k, x) when predicate k x -> Tip(k, x)
        | Tip _ -> Nil
        | Nil -> Nil

    ///O(n). Filter all values that satisfy some predicate. Credit: Haskell.org
    let filter p m = filterWithKey (fun _ x -> p x) m

    ///O(n). partition the map according to some predicate. The first map contains all elements that satisfy the predicate, the second all elements that fail the predicate. See also split. Credit: Haskell.org
    let rec partitionWithKey predicate t =
        match t with
        | Bin(p, m, l, r)  ->
            let (l1, l2) = partitionWithKey predicate l
            let (r1, r2) = partitionWithKey predicate r
            (bin p m l1 r1, bin p m l2 r2)
        | Tip(k, x) when predicate k x -> (t, Nil)
        | Tip _-> (Nil, t)
        | Nil -> (Nil, Nil)

    ///O(n). partition the map according to some predicate. The first map contains all elements that satisfy the predicate, the second all elements that fail the predicate. See also split. Credit: Haskell.org
    let partition p m = partitionWithKey (fun _ x -> p x) m

    ///O(n). Map keys/values and collect the Just results. Credit: Haskell.org
    let rec mapOptionWithKey f =
        function
        | Bin(p, m, l, r) -> bin p m (mapOptionWithKey f l) (mapOptionWithKey f r)
        | Tip(k, x) ->
            match f k x with
            | Some y -> Tip(k, y)
            | None -> Nil
        | Nil -> Nil

    ///O(n). Map values and collect the Just results. Credit: Haskell.org
    let mapOption f = mapOptionWithKey (fun _ x -> f x)

    ///O(n). Map keys/values and separate the Left and Right results. Credit: Haskell.org
    let rec mapChoiceWithKey f =
        function
        | Bin(p, m, l, r) ->
            let (l1, l2) = mapChoiceWithKey f l
            let (r1, r2) = mapChoiceWithKey f r
            (bin p m l1 r1, bin p m l2 r2)
        | Tip(k, x) ->
            match f k x with
            | Choice1Of2 y  -> (Tip(k, y), Nil)
            | Choice2Of2 z -> (Nil, Tip(k, z))
        | Nil -> (Nil, Nil)

    ///O(n). Map values and separate the Left and Right results. Credit: Haskell.org
    let mapChoice f = mapChoiceWithKey (fun _ x -> f x)

    ///O(log n). The expression (split k map) is a pair (map1,map2) where all keys in map1 are lower than k and all keys in map2 larger than k. Any key equal to k is found in neither map1 nor map2. Credit: Haskell.org
    let split k t =
        let rec go k t =
            match t with
            | Bin(p, m, l, r) when nomatch k p m -> if k > p then (t, Nil) else (Nil, t)
            | Bin(p, m, l, r) when zero k m ->
                let (lt, gt) = go k l
                (lt, append gt r)
            | Bin(p, m, l, r) ->
                let (lt, gt) = go k r
                (append l lt, gt)
            | Tip(ky, _) when k > ky -> (t, Nil)
            | Tip(ky, _) when k < ky -> (Nil, t)
            | Tip(ky, _) -> (Nil, Nil)
            | Nil -> (Nil, Nil)
        match t with
        | Bin(_, m, l, r) when  m < 0UL ->
            if k >= 0UL // handle negative numbers.
                then let (lt, gt) = go k l in let lt = append r lt in (lt, gt)
            else let (lt, gt) = go k r in let gt = append gt l in (lt, gt)
        | _ -> go k t

    ///O(log n). Performs a split but also returns whether the pivot key was found in the original map. Credit: Haskell.org
    let splitTryFind k t =
        let rec go k t =
            match t with
            | Bin(p, m, l, r) when nomatch k p m -> if k > p then (t, None, Nil) else (Nil, None, t)
            | Bin(p, m, l, r) when zero k m ->
                let (lt, fnd, gt) = go k l
                let gt = append gt r
                (lt, fnd, gt)
            | Bin(p, m, l, r) ->
                let (lt, fnd, gt) = go k r
                let lt = append l lt
                (lt, fnd, gt)
            | Tip(ky, y) when k > ky -> (t, None, Nil)
            | Tip(ky, y) when k < ky -> (Nil, None, t)
            | Tip(ky, y) -> (Nil, Some y, Nil)
            | Nil -> (Nil, None, Nil)
        match t with
        | Bin(_, m, l, r) when  m < 0UL ->
            if k >= 0UL // handle negative numbers.
                then let (lt, fnd, gt) = go k l in let lt = append r lt in (lt, fnd, gt)
            else let (lt, fnd, gt) = go k r in let gt = append gt l in (lt, fnd, gt)
        | _ -> go k t

    ///O(n). FoldBack the values in the map, such that fold f z == Prelude.foldr f z . elems. Credit: Haskell.org
    let foldBack f z =
        let rec go z =
            function
            | Nil -> z
            | Tip(_, x) -> f x z
            | Bin(_, _, l, r) -> go (go z r) l
        fun t ->
            match t with
            | Bin(_, m, l, r) when m < 0UL -> go (go z l) r  // put negative numbers before.
            | Bin(_, m, l, r) -> go (go z r) l
            | _ -> go z t

    ///O(n). Fold the values in the map, such that fold f z == Prelude.foldr f z . elems. Credit: Haskell.org
    let fold f z =
        let rec go z =
            function
            | Nil -> z
            | Tip(_, x) -> f z x
            | Bin(_, _, l, r) -> go (go z l) r
        fun t ->
            match t with
            | Bin(_, m, l, r) when m < 0UL -> go (go z r) l  // put negative numbers before.
            | Bin(_, m, l, r) -> go (go z l) r
            | _ -> go z t

    ///O(n). FoldBack the keys and values in the map, such that foldWithKey f z == Prelude.foldr (uncurry f) z . toAscList. Credit: Haskell.org
    let inline foldBackWithKey f z = fun (t: _ IntMap64UTree) -> t.FoldBackWithKey f z

    ///O(n). Fold the keys and values in the map, such that foldWithKey f z == Prelude.foldr (uncurry f) z . toAscList. Credit: Haskell.org
    let foldWithKey f z =
        let rec go z =
            function
            | Nil -> z
            | Tip(kx, x) -> f z kx x
            | Bin(_, _, l, r) -> go (go z l) r
        fun t ->
            match t with
            | Bin(_, m, l, r) when m < 0UL -> go (go z r) l  // put negative numbers before.
            | Bin(_, m, l, r) -> go (go z l) r
            | _ -> go z t
    
    ///O(n). Return all elements of the map in the ascending order of their keys. Credit: Haskell.org
    let values m = foldBack cons [] m

    ///O(n). Return all keys of the map in ascending order. Credit: Haskell.org
    let keys m = foldBackWithKey (fun k _ ks -> k :: ks) [] m

    ///O(n). Convert the map to a list of key/value pairs. Credit: Haskell.org
    let inline toList (m: _ IntMap64UTree) = m.ToList()

    ///O(n). Convert the map to a seq of key/value pairs. Credit: Haskell.org
    let toSeq m = m |> toList |> List.toSeq

    ///O(n). Convert the map to an array of key/value pairs. Credit: Haskell.org
    let toArray m = m |> toList |> List.toArray

    ///O(n*min(n,W)). Create a map from a list of key/value pairs. Credit: Haskell.org
    let ofList xs =
        let ins t (k, x) = insert k x t
        List.fold ins empty xs

    ///O(n*min(n,W)). Build a map from a list of key/value pairs with a combining function. See also fromAscListWithKey'. Credit: Haskell.org
    let ofListWithKey f xs =
        let ins t (k, x) = insertWithKey f k x t
        List.fold ins empty xs

    ///O(n*min(n,W)). Create a map from a list of key/value pairs with a combining function. See also fromAscListWith. Credit: Haskell.org
    let ofListWith f xs = ofListWithKey (fun _ x y -> f x y) xs

    ///O(n*min(n,W)). Create a map from a seq of key/value pairs. Credit: Haskell.org
    let ofSeq xs = xs |> List.ofSeq |> ofList

    ///O(n*min(n,W)). Build a map from a seq of key/value pairs with a combining function. See also fromAscListWithKey'. Credit: Haskell.org
    let ofSeqWithKey f xs = xs |> List.ofSeq |> ofListWithKey f

    ///O(n*min(n,W)). Create a map from a seq of key/value pairs with a combining function. See also fromAscListWith. Credit: Haskell.org
    let ofSeqWith f xs = xs |> List.ofSeq |> ofListWith f

    ///O(n*min(n,W)). Create a map from an array of key/value pairs. Credit: Haskell.org
    let ofArray xs = xs |> List.ofArray |> ofList

    ///O(n*min(n,W)). Build a map from an array of key/value pairs with a combining function. See also fromAscListWithKey'. Credit: Haskell.org
    let ofArrayWithKey f xs = xs |> List.ofArray |> ofListWithKey f

    ///O(n*min(n,W)). Create a map from an array of key/value pairs with a combining function. See also fromAscListWith. Credit: Haskell.org
    let ofArrayWith f xs = xs |> List.ofArray |> ofListWith f

    ///O(n*min(n,W)). mapKeys f s is the map obtained by applying f to each key of s. The size of the result may be smaller if f maps two or more distinct keys to the same new key. In this case the value at the greatest of the original keys is retained. Credit: Haskell.org
    let mapKeys f = ofList << foldBackWithKey (fun k x xs -> (f k, x) :: xs) []

    ///O(n*log n). mapKeysWith c f s is the map obtained by applying f to each key of s. The size of the result may be smaller if f maps two or more distinct keys to the same new key. In this case the associated values will be combined using c. Credit: Haskell.org
    let mapKeysWith c f = ofListWith c << foldBackWithKey (fun k x xs -> (f k, x) :: xs) []

    ///O(n+m). The expression (isSubmapOfBy f m1 m2) returns True if all keys in m1 are in m2, and when f returns True when applied to their respective values. Credit: Haskell.org
    let rec isSubmapOfBy predicate t1 t2 =
      match t1, t2 with
      | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when shorter m1 m2 -> false
      | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when shorter m2 m1 ->
          match' p1 p2 m2 &&
              (if zero p1 m2 then isSubmapOfBy predicate t1 l2
                  else isSubmapOfBy predicate t1 r2)
      | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) ->
          (p1 = p2) && isSubmapOfBy predicate l1 l2 && isSubmapOfBy predicate r1 r2
      | Bin _, _ -> false
      | Tip(k, x), t ->
          match tryFind k t with
          | Some y  -> predicate x y
          | None -> false
      | Nil, _ -> true

    ///O(n+m). Is this a submap? Defined as (isSubmapOf = isSubmapOfBy (==)). Credit: Haskell.org
    let isSubmapOf m1 m2 = isSubmapOfBy (=) m1 m2

    type private Ordering =
        | GT
        | LT
        | EQ

    let rec private submapCmp predicate t1 t2 =

        let submapCmpLt p1 r1 t1 p2 m2 l2 r2  =
            if nomatch p1 p2 m2 then GT
            elif zero p1 m2 then submapCmp predicate t1 l2
            else submapCmp predicate t1 r2

        let submapCmpEq l1 r1 l2 r2 =
            match (submapCmp predicate l1 l2, submapCmp predicate r1 r2) with
            | (GT,_ ) -> GT
            | (_ ,GT) -> GT
            | (EQ,EQ) -> EQ
            | _ -> LT
        match t1, t2 with
        | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when shorter m1 m2 -> GT
        | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when shorter m2 m1 -> submapCmpLt p1 r1 t1 p2 m2 l2 r2
        | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) when p1 = p2 -> submapCmpEq l1 r1 l2 r2
        | Bin(p1, m1, l1, r1), Bin(p2, m2, l2, r2) -> GT  // disjoint
        | Bin _, _ -> GT
        | Tip(kx, x), Tip(ky, y) when (kx = ky) && predicate x y -> EQ
        | Tip(kx, x), Tip(ky, y) -> GT  // disjoint
        | Tip(k, x), t ->
            match tryFind k t with
            | Some y when predicate x y -> LT
            | _ -> GT // disjoint
        | Nil, Nil -> EQ
        | Nil, _ -> LT

    ///O(n+m). Is this a proper submap? (ie. a submap but not equal). The expression (isProperSubmapOfBy f m1 m2) returns True when m1 and m2 are not equal, all keys in m1 are in m2, and when f returns True when applied to their respective values.  Credit: Haskell.org
    let isProperSubmapOfBy predicate t1 t2 =
        match submapCmp predicate t1 t2 with
        | LT -> true
        | _ -> false

    ///O(n+m). Is this a proper submap? (ie. a submap but not equal). Defined as (isProperSubmapOf = isProperSubmapOfBy (==)). Credit: Haskell.org
    let isProperSubmapOf m1 m2 = isProperSubmapOfBy (=) m1 m2


//#endregion

////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////
///
///               Additional methods
///
////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////
        

    // Pointer is initialized with move(k) and supports next() and previous()
    [<NoEquality; NoComparison>]
    type MapPointer<'V> = 
            {
            map:IntMap64UTree<'V>;
            /// path from root to the current position
            mutable stack: IntMap64UTree<'V> list;  
            /// true when Move was called
            mutable positioned : bool }

    let private mkPointerStack (k:uint64) (m:IntMap64UTree<'V>) : IntMap64UTree<'V> list = 
        let rec mkPointerStackAux k (m:IntMap64UTree<'V>) (stack:IntMap64UTree<'V> list) : IntMap64UTree<'V> list =
            //let notFound() = raise (System.Collections.Generic.KeyNotFoundException(sprintf "IntMap64UTree.find: key %d is not an element of the map" k))
            match m with
            | Bin(p, m, l, r) when nomatch k p m -> [] //notFound()
            | Bin(p, m, l, r) as node when zero k m  -> mkPointerStackAux k l (node::stack)
            | Bin(p, m, l, r) as node -> mkPointerStackAux k r (node::stack)
            | Tip(kx, x) as node when k = kx -> node::stack // return the stack
            | Tip(kx, x) -> [] //notFound()
            | Nil -> [] //notFound()
        mkPointerStackAux k m []

    let private mkPointerStackMin (m:IntMap64UTree<'V>) : IntMap64UTree<'V> list = 
        let rec mkPointerStackAuxMin (m:IntMap64UTree<'V>) (stack:IntMap64UTree<'V> list) : IntMap64UTree<'V> list=
            //let notFound() = raise (System.Collections.Generic.KeyNotFoundException())
            match m with
            | Bin(_, _, l, _) as node -> mkPointerStackAuxMin l (node::stack)
            | Tip(kx, x) as node -> node::stack
            | Nil -> [] //notFound()
        mkPointerStackAuxMin m []

    let private mkPointerStackMax (m:IntMap64UTree<'V>) : IntMap64UTree<'V> list = 
        let rec mkPointerStackAuxMax (m:IntMap64UTree<'V>) (stack:IntMap64UTree<'V> list) : IntMap64UTree<'V> list=
            //let notFound() = raise (System.Collections.Generic.KeyNotFoundException())
            match m with
            | Bin(_, _, _, r) as node -> mkPointerStackAuxMax r (node::stack)
            | Tip(kx, x) as node -> node::stack
            | Nil -> [] //notFound()
        mkPointerStackAuxMax m []

    // creates new Pointer that is set to position k in the map m
    let mkPointer (m:IntMap64UTree<'V>) : MapPointer<_> = 
        { map = m; stack = []; positioned = false }

    let private notPositioned() = raise (new System.InvalidOperationException("Pointer is not positioned."))

    let current (pointer:MapPointer<'V>) : uint64*'V =
        //let notPositioned() = raise (new System.InvalidOperationException("Pointer is not positioned."))
        if pointer.positioned then
            match pointer.stack with
                | Tip (k,v) :: _ -> k,v
                | []            -> notPositioned()
                | _             -> failwith "Please report error: MapPointer, unexpected stack for current"
        else
            notPositioned()

    let move k (pointer:MapPointer<'V>):bool =
        let newStack = mkPointerStack k pointer.map
        if not (List.isEmpty newStack) then 
            pointer.stack <- newStack
            pointer.positioned <- true
            true
        else
            false // keep stack and flag

    let moveMin (pointer:MapPointer<'V>):bool =
        let newStack = mkPointerStackMin pointer.map
        if not (List.isEmpty newStack) then 
            pointer.stack <- newStack
            pointer.positioned <- true
            true
        else
            false // keep stack and flag


    let moveMax (pointer:MapPointer<'V>):bool =
        let newStack = mkPointerStackMax pointer.map
        if not (List.isEmpty newStack) then 
            pointer.stack <- newStack
            pointer.positioned <- true
            true
        else
            false // keep stack and flag

    // TODO moveLT/LE/GT/GE with stacks constructed similar to findLT/LE/GT/GE
    // This is naive implementation that is 2x slower because it searches position twice (however moveNext/Prev do not depend on initial positioning, the main benefit of a pointer is in efficient next/prev movements!)   
    let moveLT k (pointer:MapPointer<'V>):bool =
        let existingKey = tryFindLT k pointer.map
        match existingKey with
        | Some(k1, v) -> 
            let newStack = mkPointerStack k1 pointer.map
            if not (List.isEmpty newStack) then 
                pointer.stack <- newStack
                pointer.positioned <- true
                true
            else
                failwith "unexpected state: tryFindLT should have returned existing key" 
        | None -> false

    let moveLE k (pointer:MapPointer<'V>):bool =
        let existingKey = tryFindLE k pointer.map
        match existingKey with
        | Some(k1, v) -> 
            let newStack = mkPointerStack k1 pointer.map
            if not (List.isEmpty newStack) then 
                pointer.stack <- newStack
                pointer.positioned <- true
                true
            else
                failwith "unexpected state: tryFindLE should have returned existing key" 
        | None -> false

    let moveGT k (pointer:MapPointer<'V>):bool =
        let existingKey = tryFindGT k pointer.map
        match existingKey with
        | Some(k1, v) -> 
            let newStack = mkPointerStack k1 pointer.map
            if not (List.isEmpty newStack) then 
                pointer.stack <- newStack
                pointer.positioned <- true
                true
            else
                failwith "unexpected state: tryFindGT should have returned existing key" 
        | None -> false

    let moveGE k (pointer:MapPointer<'V>):bool =
        let existingKey = tryFindGE k pointer.map
        match existingKey with
        | Some(k1, v) -> 
            let newStack = mkPointerStack k1 pointer.map
            if not (List.isEmpty newStack) then 
                pointer.stack <- newStack
                pointer.positioned <- true
                true
            else
                failwith "unexpected state: tryFindGE should have returned existing key" 
        | None -> false


    let moveNext (pointer:MapPointer<'V>) : bool =
        // first climb up the stack until it has different right node 
        // and then rebuild the stack with left nodes down
             
        // ascend by trail until could move to a different right node
        let rec ascendR = function //(stack:IntMap64UTree<'V> list) : (IntMap64UTree<'V> list) =
            //match stack with
            | Bin(p, m, _, _) :: (Bin(_, _, pl, pr) as par) :: tail -> // current :: parent:: tail
                match pr with // parent right
                | Bin(prp, prm, _, _) when prp = p && prm = m -> ascendR (par :: tail) // pr is current, ascending from right, need to go upper
                | _ -> pr :: par :: tail
            | Tip (k,v) :: (Bin(_, _, _, r) as bin) :: tail -> 
                match r with
                | Tip (k1,v1) when k1 = k -> ascendR (bin :: tail) // ascending from right, need to go upper
                | _ -> r :: (bin :: tail) // ascending from left, go directly to the right
            | Bin(_, _, _, _) :: [] -> []
            | Tip (k,v) :: [] -> []
            | [] -> [] // recursively could go there if found nothing
            | _ -> failwith "Please report error: Map iterator, unexpected stack for moveNext"

        // descend to the minimum value
        let rec descendL = function
            | (Bin(_, _, l, _) as bin) :: tl ->
                match l with
                | Nil -> failwith "unexpected!"
                | Tip(_,_) as tip -> tip :: bin :: tl
                | Bin(_, _, _, _) as node -> descendL (node :: bin :: tl)
            | _ as x -> x

        if pointer.positioned then
            let nextStack = descendL (ascendR pointer.stack)
            if not (List.isEmpty nextStack) then 
                pointer.stack <- nextStack
                true
            else
                false
        else
            notPositioned()


    let movePrevious (pointer:MapPointer<'V>) : bool =
        let rec ascendL = function 
            | [] -> [] 
            | Tip (k,v) :: [] -> []
            | Tip (k,v) :: (Bin(_, _, l, _) as bin) :: tail -> 
                match l with
                | Tip (k1,v1) when k1 = k -> ascendL (bin :: tail) 
                | _ -> l :: (bin :: tail) 
            | Bin(_, _, _, _) :: [] -> []
            | Bin(p, m, _, _) :: (Bin(_, _, pl, pr) as par) :: tail -> 
                match pl with // parent right
                | Bin(prp, prm, _, _) when prp = p && prm = m -> ascendL (par :: tail) 
                | _ -> pl :: par :: tail
            | _ -> failwith "Please report error: Map iterator, unexpected stack for moveNext"

        // descend to the minimum value
        let rec descendR = function
            | (Bin(_, _, _, r) as bin) :: tl ->
                match r with
                | Nil -> failwith "unexpected!"
                | Tip(_,_) as tip -> tip :: bin ::tl
                | Bin(_, _, _, _) as node -> descendR (node:: bin ::tl)
            | _ as x -> x

        if pointer.positioned then
            let nextStack = descendR (ascendL pointer.stack)
            if not (List.isEmpty nextStack) then 
                pointer.stack <- nextStack
                true
            else
                false
        else
            notPositioned()



// type IntMap64UTree is the underlying data structure, internal
// module IntMap64UTree contains algos to modify IntMap64UTree
// public type ImmutableIntMap64U in the only public API, design follows F#.Core.Map

namespace Spreads.Collections
    open System
    open System.Collections
    open System.Collections.Generic
    open System.Linq
    open System.Runtime.InteropServices

    open Spreads
    open Spreads.Collections

    [<Sealed>]
    [<CompiledName("ImmutableIntMap64U`1")>]
    type ImmutableIntMap64U<[<EqualityConditionalOn;ComparisonConditionalOn>]'T>
        internal(tree: IntMap64UTree<'T>)=

        let syncRoot = new Object()

        static let empty = ImmutableIntMap64U<'T>(IntMap64UTree.empty)

        static member Empty : ImmutableIntMap64U<'T> = empty

        static member Create(elements : IEnumerable<KeyValuePair<uint64, 'T>>) : ImmutableIntMap64U<'T> = 
           new ImmutableIntMap64U<'T>(IntMap64UTree.ofSeq (elements |> Seq.map (fun x -> x.Key,x.Value) ))
    
        static member Create() : ImmutableIntMap64U<'T> = empty

        new(elements : IEnumerable<KeyValuePair<uint64, 'T>>) = new ImmutableIntMap64U<'T>(IntMap64UTree.ofSeq (elements |> Seq.map (fun x -> x.Key,x.Value) ))

        member internal this.Tree = tree




        member this.IsEmpty
                with get() = IntMap64UTree.isEmpty tree

        member this.First
            with get() = 
                let res = IntMap64UTree.tryFindMin tree
                if res.IsNone then raise (InvalidOperationException("Could not get the first element of an empty map"))
                KeyValuePair(fst res.Value, snd res.Value)

        member this.Last
            with get() = 
                let res = IntMap64UTree.tryFindMax tree
                if res.IsNone then raise (InvalidOperationException("Could not get the last element of an empty map"))
                KeyValuePair(fst res.Value, snd res.Value)

        member this.Item
            with get key =
                let tr = IntMap64UTree.tryFind key tree
                if tr.IsNone then raise (KeyNotFoundException())
                tr.Value

        member this.TryFind(key,direction:Lookup, [<Out>]result: byref<KeyValuePair<uint64, 'T>>) : bool =
            result <- Unchecked.defaultof<KeyValuePair<uint64, 'T>>
            match direction with
            | Lookup.EQ -> 
                let tr = IntMap64UTree.tryFind key tree
                if tr.IsNone then 
                    false
                else
                    result <- KeyValuePair<uint64, 'T>(key, tr.Value)
                    true
            | Lookup.LT -> 
                let tr = IntMap64UTree.tryFindLT key tree
                if tr.IsNone then 
                    false
                else
                    result <- KeyValuePair<uint64, 'T>(fst tr.Value, snd tr.Value)
                    true
            | Lookup.LE -> 
                let tr = IntMap64UTree.tryFindLE key tree
                if tr.IsNone then 
                    false
                else
                    result <- KeyValuePair<uint64, 'T>(fst tr.Value, snd tr.Value)
                    true
            | Lookup.GT -> 
                let tr = IntMap64UTree.tryFindGT key tree
                if tr.IsNone then 
                    false
                else
                    result <- KeyValuePair<uint64, 'T>(fst tr.Value, snd tr.Value)
                    true
            | Lookup.GE ->
                let tr = IntMap64UTree.tryFindGE key tree
                if tr.IsNone then 
                    false
                else
                    result <- KeyValuePair<uint64, 'T>(fst tr.Value, snd tr.Value)
                    true
            | _ -> raise (ApplicationException("Wrong lookup direction"))


        member this.GetCursor() = new MapCursor<uint64,'T>(this) :> ICursor<uint64,'T>
            
        member this.Size with get() = IntMap64UTree.size tree

        member this.SyncRoot with get() = syncRoot


        member this.Add(k, v):ImmutableIntMap64U<'T> = ImmutableIntMap64U(IntMap64UTree.insert k v tree)

        member this.AddFirst(k, v):ImmutableIntMap64U<'T> = 
            if not this.IsEmpty && k >= this.First.Key then 
                raise (ArgumentOutOfRangeException("New key is larger or equal to the smallest existing key"))
            this.Add(k, v)

        member this.AddLast(k, v):ImmutableIntMap64U<'T> = 
            if not this.IsEmpty && k <= this.Last.Key then 
                raise (ArgumentOutOfRangeException("New key is smaller or equal to the largest existing key"))
            this.Add(k, v)
    
        member this.Remove(k):ImmutableIntMap64U<'T> = ImmutableIntMap64U(IntMap64UTree.delete k tree)

        member this.RemoveLast([<Out>]value: byref<KeyValuePair<uint64, 'T>>):ImmutableIntMap64U<'T>=
            if this.IsEmpty then raise (InvalidOperationException("Could not get the last element of an empty map"))
            let v, newTree = IntMap64UTree.deleteFindMax tree
            value <- KeyValuePair(fst v, snd v)
            ImmutableIntMap64U(newTree)

        member this.RemoveFirst([<Out>]value: byref<KeyValuePair<uint64, 'T>>):ImmutableIntMap64U<'T>=
            if this.IsEmpty then raise (InvalidOperationException("Could not get the first element of an empty map"))
            let v, newTree = IntMap64UTree.deleteFindMin tree
            value <- KeyValuePair(fst v, snd v)
            ImmutableIntMap64U(newTree)

        member this.RemoveMany(k:uint64, direction:Lookup):ImmutableIntMap64U<'T>=
            let newTree = 
                match direction with
                | Lookup.LT ->
                    snd (IntMap64UTree.split (k - 1UL) tree)
                | Lookup.LE ->
                    snd (IntMap64UTree.split (k) tree) 
                | Lookup.GT ->
                    fst (IntMap64UTree.split (k + 1UL) tree)
                | Lookup.GE ->
                    fst (IntMap64UTree.split (k) tree)
                | Lookup.EQ ->
                    IntMap64UTree.delete k tree
                | _ -> failwith "unexpected wrong direction"
            ImmutableIntMap64U(newTree)

        interface IEnumerable<KeyValuePair<uint64, 'T>> with
          member m.GetEnumerator() = 
            (tree.ToList().ToArray() 
            |> Array.map (fun (k,v) -> KeyValuePair(k,v)) :> (KeyValuePair<uint64, 'T>) seq).GetEnumerator()
          member m.GetEnumerator() =
            (m :> _ seq).GetEnumerator() :> IEnumerator


        interface IImmutableOrderedMap<uint64, 'T> with
          member this.Subscribe(observer) = raise (NotImplementedException())
          member this.Comparer with get() = KeyComparer.GetDefault<uint64>()
          member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<uint64, 'T>>
          member this.GetCursor() = this.GetCursor()
          member this.IsEmpty = this.IsEmpty
          member this.IsIndexed with get() = false
          member this.IsReadOnly = true
          member this.First with get() = this.First
          member this.Last with get() = this.Last
          member this.Item with get (k) : 'T = this.Item(k)
          member this.GetAt(idx:int) : 'T = this.Skip(Math.Max(0, idx-1)).First().Value
          member this.Keys with get() = IntMap64UTree.keys tree :> IEnumerable<uint64>
          member this.Values with get() = IntMap64UTree.values tree :> IEnumerable<'T>

          member this.TryFind(k, direction:Lookup, [<Out>] res: byref<KeyValuePair<uint64, 'T>>) = 
            res <- Unchecked.defaultof<KeyValuePair<uint64, 'T>>
            let tr = this.TryFind(k, direction)
            if (fst tr) then
              res <- snd tr
              true
            else
              false

          member this.TryGetFirst([<Out>] res: byref<KeyValuePair<uint64, 'T>>) = 
            try
              res <- this.First
              true
            with
            | _ -> 
              res <- Unchecked.defaultof<KeyValuePair<uint64, 'T>>
              false
            
          member this.TryGetLast([<Out>] res: byref<KeyValuePair<uint64, 'T>>) = 
            try
              res <- this.Last
              true
            with
            | _ -> 
              res <- Unchecked.defaultof<KeyValuePair<uint64, 'T>>
              false
        
          member this.TryGetValue(k, [<Out>] value:byref<'T>) = 
            let success, pair = this.TryFind(k, Lookup.EQ)
            if success then 
              value <- pair.Value
              true
            else false

//            member this.Count with get() = int(this.Size)
          member this.Size with get() = int64(this.Size)

          member this.SyncRoot with get() = this.SyncRoot

          member this.Add(key, value):IImmutableOrderedMap<uint64,'T> =
            this.Add(key, value) :> IImmutableOrderedMap<uint64,'T>

          member this.AddFirst(key, value):IImmutableOrderedMap<uint64,'T> =
            this.AddFirst(key, value) :> IImmutableOrderedMap<uint64,'T>

          member this.AddLast(key, value):IImmutableOrderedMap<uint64,'T> =
            this.AddLast(key, value) :> IImmutableOrderedMap<uint64,'T>

          member this.Remove(key):IImmutableOrderedMap<uint64,'T> =
            this.Remove(key) :> IImmutableOrderedMap<uint64,'T>

          member this.RemoveLast([<Out>] value: byref<KeyValuePair<uint64, 'T>>):IImmutableOrderedMap<uint64,'T> =
            let m,v = this.RemoveLast()
            value <- v
            m :> IImmutableOrderedMap<uint64,'T>

          member this.RemoveFirst([<Out>] value: byref<KeyValuePair<uint64, 'T>>):IImmutableOrderedMap<uint64,'T> =
            let m,v = this.RemoveFirst()
            value <- v
            m :> IImmutableOrderedMap<uint64,'T>

          member this.RemoveMany(key,direction:Lookup):IImmutableOrderedMap<uint64,'T>=
            this.RemoveMany(key, direction) :> IImmutableOrderedMap<uint64,'T>
                
