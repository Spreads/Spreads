namespace FSharpx.Collections.Experimental
open System.Collections.Specialized
open System.Collections.Generic


module internal BitUtilities =
    //Counts the number of 1-bits in bitmap
    let inline bitcount (bitmap:int): int32 = Spreads.Algorithms.Intrinsics.PopCount(uint32 bitmap)
        //let count2 = bitmap - ((bitmap >>> 1) &&& 0x55555555)
        //let count4 = (count2 &&& 0x33333333) + ((count2 >>> 2) &&& 0x33333333)
        //let count8 = (count4 + (count4 >>> 4)) &&& 0x0f0f0f0f
        //(count8 * 0x01010101) >>> 24

    //Finds the number of 1-bits below the bit at "pos"
    //Since the array in the ChampHashMap is compressed (does not have room for unfilled values)
    //This function is used to find where in the array of entries or nodes the item should be inserted
    let inline index (bitmap: BitVector32) pos = 
        bitcount (bitmap.Data &&& (pos - 1)) 
    
    //Returns the value used to index into the BitVector.  
    //The BitVector must be accessed by powers of 2, so to get the nth bit
    //the mask must be 2^n
    let inline mask index =
        1 <<< index

    //The largest value that a section of the hashcode can represent. This means
    //for each level in the tree a 5-bit slice of the hashcode will be used
    let [<Literal>] PartitionMaxValue = 31s

//An entry in the hashmap
[<Struct>]
type KeyValuePair<'TKey,'TValue> = {Key: 'TKey; Value: 'TValue}

//A Node in the Trie. Can either be Empty (meaning the hashmap has 0 entries), a Bitmap node, which contains
//entries and subnode references, or it can be a Collision node, which keeps track of all entries
//that have different keys but have hashed to the same value
type internal Node<[<EqualityConditionalOn>]'TKey, [<EqualityConditionalOn>]'TValue when 'TKey : equality> = 
    | BitmapNode of entryMap: BitVector32 * nodeMap: BitVector32 * items: array<KeyValuePair<'TKey, 'TValue>> * nodes: array<Node<'TKey, 'TValue>>
    | CollisionNode of items: array<KeyValuePair<'TKey, 'TValue>> * hash: BitVector32
    | EmptyNode

open BitUtilities
// open FSharpx.Collections
[<RequireQualifiedAccess>]
module internal Node = 
    
    //Sets the value at index "index" to "value". If inplace is true it sets the 
    //value without copying the array. Otherwise it returns a new copy
    let set items index value inplace = 
        if (inplace) then
            Array.set items index value |> ignore
            items
        else
            let copy = Array.copy items
            Array.set copy index value |> ignore
            copy

    //Throws a KeyNotFound exception with "key" as the error message
    let keyNotFound key =
        raise (KeyNotFoundException())

    //Inserts a new element in an array at index "index"
    let insert (array: array<'T>) index value =
        let newItems = Array.zeroCreate (array.Length + 1)
        Array.blit array 0 newItems 0 index
        Array.blit array index newItems (index + 1) (array.Length - index)
        Array.set newItems index value
        newItems

    //Returns a new array with the item at index removed
    let removeAt (array: array<'T>) index = 
        let newItems = Array.zeroCreate (array.Length - 1)
        if (index > 0) then
            Array.blit array 0 newItems 0 index
        if (index + 1 < array.Length) then
            Array.blit array (index + 1) newItems index (array.Length - index - 1)
        newItems

    //Returns the value associated with "key" in this subtree, and throws a 
    //KeyNotFound exception if the key is not present in the subtree
    let rec getValue node key (hash: BitVector32) (section: BitVector32.Section) = 
        match node with
        | BitmapNode(entryMap, nodeMap, items, nodes) -> 
            let hashIndex = hash.[section]
            let mask = mask hashIndex
            if (entryMap.[mask]) then
                let entryIndex = index entryMap mask
                if ((Array.get items entryIndex).Key = key) then
                    (Array.get items entryIndex).Value
                else keyNotFound key
            elif (nodeMap.[mask]) then
                let subNode = Array.get nodes <| index nodeMap mask
                getValue subNode key hash (BitVector32.CreateSection (PartitionMaxValue, section))
            else keyNotFound key
        | EmptyNode -> keyNotFound key
        | CollisionNode(items, _) -> (Array.find (fun k -> k.Key = key) items).Value

    //Returns Some value when the key exists in the node or any of the subnodes of this node
    // and None otherwise
    let rec tryGetValue node key (hash: BitVector32) (section: BitVector32.Section) = 
        match node with
        | BitmapNode(entryMap, nodeMap, items, nodes) -> 
            let hashIndex = hash.[section]
            let mask = mask hashIndex
            if (entryMap.[mask]) then
                let entryIndex = index entryMap mask
                if ((Array.get items entryIndex).Key = key) then
                    Some (Array.get items entryIndex).Value
                else None
            elif (nodeMap.[mask]) then
                let subNode = Array.get nodes <| index nodeMap mask
                tryGetValue subNode key hash (BitVector32.CreateSection (PartitionMaxValue, section))
            else None
        | EmptyNode -> None
        | CollisionNode(items, _) -> 
            match (Array.tryFind (fun k -> k.Key = key) items) with
            | Some(pair) -> Some(pair.Value)
            | None -> None

    //Combines two key/value pairs into a new subnode
    let rec merge pair1 pair2 (pair1Hash: BitVector32) (pair2Hash: BitVector32) (section: BitVector32.Section) = 
        if (section.Offset >= 25s) then
            CollisionNode([|pair1;pair2|], pair1Hash)
        else
            let nextLevel = BitVector32.CreateSection (PartitionMaxValue, section)
            let pair1Index = pair1Hash.Item nextLevel
            let pair2Index = pair2Hash.Item nextLevel
            if (pair1Index <> pair2Index) then
                let mutable dataMap = BitVector32 (mask pair1Index)
                dataMap.[(mask pair2Index)] <- true
                if (pair1Index < pair2Index) then
                    BitmapNode(dataMap, BitVector32 0, [|pair1;pair2|], Array.empty)
                else 
                    BitmapNode(dataMap, BitVector32 0, [|pair2;pair1|], Array.empty)
            else 
                let node = merge pair1 pair2 pair1Hash pair2Hash (nextLevel)
                let nodeMap = BitVector32 (mask pair1Index)
                BitmapNode(BitVector32 0, nodeMap, Array.empty, [|node|])
    
    //Adds a key/value pair to the node or its subnodes, or replaces the existing value at "key" if the 
    // key is already present in this subtree
    let rec update node inplace change (hash: BitVector32) (section: BitVector32.Section) =
        match node with
        | EmptyNode -> 
            let dataMap = hash.[section] |> mask |> BitVector32
            let items = [|change|]
            let nodes = Array.empty
            let nodeMap = BitVector32(0)
            BitmapNode(dataMap, nodeMap, items, nodes)
        | BitmapNode(entryMap, nodeMap, items, nodes) ->
            let hashIndex = hash.[section]
            let mask = mask hashIndex
            if (entryMap.[mask]) then
                let entryIndex = index entryMap mask
                if ((Array.get items entryIndex).Key = change.Key) then
                    let newItems = set items entryIndex change inplace
                    BitmapNode(nodeMap, entryMap, newItems, nodes)
                else 
                    let currentEntry = Array.get items entryIndex
                    let currentHash = BitVector32(int32(currentEntry.Key.GetHashCode()))
                    let node = merge change currentEntry hash currentHash section
                    let newItems = Array.filter (fun elem -> elem.Key <> currentEntry.Key) items
                    let mutable newEntryMap = entryMap
                    newEntryMap.Item mask <- false
                    let mutable newNodeMap = nodeMap
                    newNodeMap.Item mask <- true
                    let nodeIndex = index nodeMap mask
                    let newNodes = insert nodes nodeIndex node
                    BitmapNode(newEntryMap, newNodeMap, newItems, newNodes)
            elif (nodeMap.[mask]) then
                let nodeIndex = index nodeMap mask
                let nodeToUpdate = Array.get nodes nodeIndex 
                let newNode = update nodeToUpdate inplace change hash (BitVector32.CreateSection (PartitionMaxValue, section))
                let newNodes = set nodes nodeIndex newNode inplace
                BitmapNode(entryMap, nodeMap, items, newNodes)
            else 
                let entryIndex = index entryMap mask
                let mutable entries = entryMap
                entries.Item mask <- true
                let newItems = insert items entryIndex change
                BitmapNode(entries, nodeMap, newItems, nodes)
        | CollisionNode(items, hash) -> 
            match Array.tryFindIndex (fun i -> i.Key = change.Key) items with
            | Some(index) -> 
                let newArr = set items index change false
                CollisionNode(newArr, hash)
            | None ->
                let newArr = Array.append items [|change|]
                CollisionNode(newArr, hash)
            
    //Returns a new node with the entry mapped to "key" removed
    let rec remove node key (hash: BitVector32) (section: BitVector32.Section) = 
        match node with
        | EmptyNode -> keyNotFound key
        | CollisionNode(items, _) -> 
            match Array.length items with
            | 0 -> failwith "remove was called on CollisionNode but CollisionNode contained 0 elements"
            | 1 -> EmptyNode
            | 2 -> 
                let item = Array.find (fun i -> i.Key = key) items 
                update EmptyNode false item hash (BitVector32.CreateSection PartitionMaxValue)
            | _ -> CollisionNode(Array.filter (fun i -> i.Key <> key) items, hash)
        | BitmapNode(entryMap, nodeMap, entries, nodes) -> 
            let hashIndex = hash.Item section
            let mask = mask hashIndex
            //If key belongs to an entry
            if (entryMap.[mask]) then
                let ind = index entryMap mask
                if (entries.[ind].Key = key) then
                    let mutable newMap = entryMap
                    newMap.Item mask <- false
                    let newItems = removeAt entries ind
                    BitmapNode(newMap, nodeMap, newItems, nodes)
                else keyNotFound key
            //If key lies in a subnode
            elif (nodeMap.[mask]) then
                let ind = index nodeMap mask
                let subNode = remove nodes.[ind] key hash (BitVector32.CreateSection(PartitionMaxValue, section))
                match subNode with
                | EmptyNode -> failwith "Subnode must have at least one element"
                | BitmapNode(subItemMap, subNodeMap, subItems, subNodes) ->
                    if (Array.length subItems = 1 && Array.length subNodes = 0) then
                        // If the node only has one subnode, make that subnode the new node
                        if (Array.length entries = 0 && Array.length nodes = 1) then
                            BitmapNode(subItemMap, subNodeMap, subItems, subNodes)
                        else
                            let indexToInsert = index entryMap mask 
                            let mutable newNodeMap = nodeMap
                            let mutable newEntryMap = entryMap
                            newNodeMap.[mask] <- false
                            newEntryMap.[mask] <- true
                            let newEntries = insert entries indexToInsert subItems.[0]
                            let newNodes = removeAt nodes ind
                            BitmapNode(newEntryMap, newNodeMap, newEntries, newNodes)
                    else
                        let nodeCopy = Array.copy nodes
                        nodeCopy.[ind] <- subNode
                        BitmapNode(entryMap, nodeMap, entries, nodeCopy)
                | CollisionNode(_, _) -> 
                    let nodeCopy = Array.copy nodes
                    nodeCopy.[ind] <- subNode
                    BitmapNode(entryMap, nodeMap, entries, nodeCopy)
            else 
                node

    //Converts the node to a sequence of all entries in the node and all of its sub-nodes
    let rec toSeq = function 
        | EmptyNode -> Seq.empty
        | BitmapNode(_ , _, items, nodes) -> 
            seq {
                    yield! items
                    yield! Seq.collect toSeq nodes
                }
        | CollisionNode(items, _) -> Array.toSeq items

    //Returns the number of entries in the node and all of its sub-nodes
    let rec count = function
        | EmptyNode -> 0
        | BitmapNode(_, _, items, nodes) -> 
            let itemLength = Array.length items
            Array.fold (fun acc subNode -> acc + (count subNode)) itemLength nodes
        | CollisionNode(items, _) -> Array.length items
                
    
open System
type ChampHashMap<[<EqualityConditionalOn>]'TKey, [<EqualityConditionalOn>]'TValue when 'TKey : equality> private (root: Node<'TKey,'TValue>) = 
    member private this.Root = root

    //O(N), although subtrees that are shared between other and this will not be checked.
    override this.Equals(other) =
        match other with
        | :? ChampHashMap<'TKey, 'TValue> as map -> Unchecked.equals this.Root map.Root
        | _ -> false
    
    //O(1)
    override this.GetHashCode() = Unchecked.hash this.Root

    new() = ChampHashMap(EmptyNode)

    //O(N) Returns this size of the collection
    member public this.Count = Node.count this.Root

    //Retrieves the value associated with "key," and retrieves it using valuefunc
    member private this.retrieveValue key valuefunc = 
        let hashVector = BitVector32(int32(key.GetHashCode()))
        let section = BitVector32.CreateSection(PartitionMaxValue)
        valuefunc this.Root key hashVector section
    
    //O(log32(N)) Returns the value associated with "key." Throws a KeyNotFoundException
    // if the key is not present in the hashmap
    member public this.GetValue key =
        this.retrieveValue key Node.getValue
    
    //O(log32(N)) Returns an option of the value associated with "key."
    // returns None if the key is not present in the hashmap
    member public this.TryGetValue key = 
        this.retrieveValue key Node.tryGetValue

    //Adds a key/value pair, where the boolean "inplace" refers to whether or not the 
    // addition mutates the collection
    member private this.AddInPlace key value inplace = 
        let hashVector = BitVector32(int32(key.GetHashCode()))
        let section = BitVector32.CreateSection(PartitionMaxValue)
        let newRoot = Node.update this.Root inplace {Key=key; Value=value} hashVector section 
        ChampHashMap(newRoot)

    //O(log32(N)) Adds a key/value pair to the hashmap. 
    // updates the current value associated with "key" if the key is already present in the hashmap
    member public this.Add key value =
        this.AddInPlace key value false

    //O(log32(N)) Returns a hashmap with the key/value pair with the given key removed.
    // Throws a KeyNotFound Exception if the key is not present in the hashmap
    member public this.Remove key = 
        let hashVector = BitVector32(int32(key.GetHashCode()))
        let section = BitVector32.CreateSection PartitionMaxValue
        let newRoot = Node.remove this.Root key hashVector section
        ChampHashMap(newRoot)
    
    //Converts the hashmap to a sequence
    member this.ToSeq = Node.toSeq this.Root

    //Bulk loads a hashmap from a sequence, where key selector is a function
    //that returns the key from an element of sequence and valueselector returns the value 
    static member ofSeq keySelector valueSelector sequence = 
        let startingMap = ChampHashMap()
        Seq.fold (fun (acc: ChampHashMap<'TKey, 'TValue>) item -> acc.AddInPlace (keySelector item) (valueSelector item) true) startingMap sequence 
    
    //O(N), although subtrees that are shared between this and other only need to be checked at the root
    interface IEquatable<ChampHashMap<'TKey, 'TValue>> with
        member this.Equals(other) = Unchecked.equals root other.Root

[<RequireQualifiedAccess>]
module ChampHashMap =

    //O(N) Returns this size of the collection
    let inline count (map: ChampHashMap<_,_>) = map.Count

    //O(log32(N)) Returns the value associated with "key." Throws a KeyNotFoundException
    // if the key is not present in the hashmap
    let inline getValue (map: ChampHashMap<_,_>) = map.GetValue

    //O(log32(N)) Returns an option of the value associated with "key."
    // returns None if the key is not present in the hashmap
    let inline tryGetValue (map: ChampHashMap<_,_>) = map.TryGetValue

    //O(log32(N)) Adds a key/value pair to the hashmap. 
    // updates the current value associated with "key" if the key is already present in the hashmap
    let inline add (map: ChampHashMap<_,_>) = map.Add

    //O(log32(N)) Returns a hashmap with the key/value pair with the given key removed.
    // Throws a KeyNotFound Exception if the key is not present in the hashmap
    let inline remove (map: ChampHashMap<_,_>) = map.Remove

    //Bulk loads a hashmap from a sequence, where key selector is a function
    //that returns the key from an element of sequence and valueselector returns the value
    let inline toSeq (map: ChampHashMap<_,_>) = map.ToSeq