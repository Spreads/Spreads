namespace Spreads.Collections.Experimental

open System
open System.Collections
open System.Collections.Generic
open System.Collections.ObjectModel
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections

// Not used yet

// TODO in HashSortedMap, MapDeque should be the outer structure in a streaming case
// e.g. for a Spread with min capacity there will be no outer arrays resizing at all,
// while we could reuse (pool) inner arrays.
// TODO2 - or use single deque instead of internal arrays

// However, the use case is not obvious and only puristic: for daily ms data there are only 540 buckets

// TODO read about LOH. 60k ms buckets will be in LOH, what are the implications? Is it better to have smaller buckets less than 85k in size + bigger outer map?
// Note that for seconds max bucket size is 36k and outer map could have 221 days of secondly data before moving to LOH
// so LOH issue is relevant only for milliseconds
// For doubles the limit is 1000 elements!

//[<DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")>]
[<SerializableAttribute>]
[<AbstractClassAttribute>]
type DoubleDeque<'K,'V when 'K : comparison>
    internal(dic:IEnumerable<KeyValuePair<'K,'V>>, capacity:int, comparer:IComparer<'K>) as this=

    //#region Main private constructor

    // View Index - external index of SortedList, internally used as "index"
    // Buffer Index - index in buffer arrays, internally used as "idx"

    let mutable comparer : IComparer<'K> = 
        if comparer = null then LanguagePrimitives.FastGenericComparer // Comparer<'K>.Default :> IComparer<'K> 
        else comparer
        
    [<DefaultValue>] val mutable internal keys : 'K array 
    [<DefaultValue>] val mutable internal values : 'V array 

    [<DefaultValue>] val mutable internal offset : int
    [<DefaultValue>] val mutable internal size : int
    [<DefaultValue>] val mutable internal version : int
    [<DefaultValue>] val mutable internal isReadOnly : bool
    [<DefaultValue>] val mutable internal capacity : int
    let maxSize = 2146435071

    let syncObject = new Object()

    do
        this.Capacity <- 8
    //#endregion

    //#region Private & Internal members

    member private this.PostIncrement(value) =
        let ret = this.offset
        this.offset <- (this.offset + value) % capacity
        ret

    member private this.PreDecrement(value) =
        this.offset <- this.offset - value
        if this.offset < 0 then this.offset <- this.offset + capacity
        this.offset

    /// Calculates buffer index from view index
    member private this.IndexToBufferIndex(index) = 
        (index + this.offset) % capacity

    member internal this.GetByViewIndex(index) = 
        let idx = this.IndexToBufferIndex(index)
        KeyValuePair(this.keys.[idx], this.values.[idx])

    member internal this.SetByViewIndex(index, k, v) =
        use lock = makeLock this.SyncRoot
        let idx = this.IndexToBufferIndex(index)
        this.keys.[idx] <- k
        this.values.[idx] <- v
        this.version <- this.version + 1
        ()
    
    member internal this.AddToBack(k, v) =
        Trace.Assert(not this.IsFull)
        //use lock = makeLock this.SyncRoot
        let idx = this.IndexToBufferIndex(this.size)
        this.keys.[idx] <- k
        this.values.[idx] <- v
        this.size <- this.size + 1
        this.version <- this.version + 1
        ()

    member internal this.AddToFront(k, v) =
        Trace.Assert(not this.IsFull)
        //use lock = makeLock this.SyncRoot
        let idx = this.PreDecrement(1)
        this.keys.[idx] <- k
        this.values.[idx] <- v
        this.size <- this.size + 1
        this.version <- this.version + 1
        ()

    member internal this.InsertAtIndex(index, k, v) =
        this.EnsureCapacity()
        Trace.Assert(1 + this.size <= capacity)
        if this.IsEmpty || index = this.size then
            this.AddToBack(k, v)
        elif index = 0 then
            this.AddToFront(k, v)
        else
            //use lock = makeLock this.SyncRoot
            if index < this.size / 2 then
                let copyCount = index
                let writeIndex = capacity - 1
                for j in 0..copyCount-1 do
                    this.keys.[this.IndexToBufferIndex(writeIndex + j)] <- this.keys.[this.IndexToBufferIndex(j)]
                    this.values.[this.IndexToBufferIndex(writeIndex + j)] <- this.values.[this.IndexToBufferIndex(j)]
                this.PreDecrement(1) |> ignore
            else
                let copyCount = this.size - index
                let writeIndex = index + 1
                for j in copyCount-1..0 do
                    this.keys.[this.IndexToBufferIndex(writeIndex + j)] <- this.keys.[this.IndexToBufferIndex(index + j)]
                    this.values.[this.IndexToBufferIndex(writeIndex + j)] <- this.values.[this.IndexToBufferIndex(index + j)]
            let idx = this.IndexToBufferIndex(index)
            this.keys.[idx] <- k
            this.values.[idx] <- v
            this.version <- this.version + 1
            this.size <- this.size + 1

    member internal this.RemoveFromBack() : KeyValuePair<'K,'V> =
        Trace.Assert(not this.IsEmpty)
        //use lock = makeLock this.SyncRoot
        let idx = this.IndexToBufferIndex(this.size - 1)
        this.size <- this.size - 1
        this.version <- this.version + 1
        KeyValuePair(this.keys.[idx], this.values.[idx])

    member internal this.RemoveFromFront() : KeyValuePair<'K,'V> =
        Trace.Assert(not this.IsEmpty)
        //use lock = makeLock this.SyncRoot
        this.size <- this.size - 1
        let idx = this.PostIncrement(1)
        this.version <- this.version + 1
        KeyValuePair(this.keys.[idx], this.values.[idx])

    member internal this.RemoveAtIndex(index)=
        //use lock = makeLock this.SyncRoot
        if index = 0 then
            this.PostIncrement(1) |> ignore
        elif index = this.size - 1 then
            ()
        else
            if index < this.size / 2 then
                let copyCount = index
                for j in copyCount-1..0 do
                    this.keys.[this.IndexToBufferIndex(j + 1)] <- this.keys.[this.IndexToBufferIndex(j)]
                    this.values.[this.IndexToBufferIndex(j + 1)] <- this.values.[this.IndexToBufferIndex(j)]
                this.PostIncrement(1) |> ignore
            else
                let copyCount = this.size - index - 1
                let readIndex = index + 1
                for j in 0..copyCount-1 do
                    this.keys.[this.IndexToBufferIndex(j + index)] <- this.keys.[this.IndexToBufferIndex(readIndex + j)]
                    this.values.[this.IndexToBufferIndex(j + index)] <- this.values.[this.IndexToBufferIndex(readIndex + j)]
        this.version <- this.version + 1
        this.size <- this.size - 1

    member internal this.EnsureCapacity(?min) =
        let mutable num = this.keys.Length * 2
        if num > maxSize then num <- maxSize
        if min.IsSome && num < min.Value then num <- min.Value
        if this.size = this.keys.Length then this.Capacity <- num
//        if min.IsSome then
//            this.Capacity <- Math.Max(num, Math.Min(this.keys.Length * 2, maxSize)) // checks and lock are in Capacity setter
    
    member internal this.GetKeyByIndex(index) = 
        if index < 0 || index >= this.size then 
            raise (ArgumentOutOfRangeException("index"))
        this.keys.[this.IndexToBufferIndex(index)]
    
    member internal this.GetValueByIndex(index) = 
        if index < 0 || index >= this.size then 
            raise (ArgumentOutOfRangeException("index"))
        this.values.[this.IndexToBufferIndex(index)]

    member internal this.IsFull with get() = this.size = capacity

    member internal this.IsSplit with get() = this.offset > (capacity - this.size)

    member internal this.IsReadOnly with get() = this.isReadOnly

    member internal this.KeysArray with get() = this.keys

    member internal this.Offset with get() = this.offset

    member internal this.SyncRoot with get() = syncObject

    member internal this.Version with get() = this.version

    /// Removes first element to free space for new element if the map is full. 
    member internal this.AddAndRemoveFisrtIfFull(key, value) =
        //use lock = makeLock this.SyncRoot
        if this.IsFull then this.RemoveFromFront() |> ignore
        this.Add(key, value)
        ()

    //#endregion

    //#region Public members

    ///Gets or sets the capacity. This value must always be greater than zero, and this property cannot be set to a value less than this.size/>.
    member this.Capacity 
        with get() = capacity
        and set(value) =
            //use lock = makeLock this.SyncRoot
            match value with
            | c when c = this.keys.Length -> ()
            | c when c < this.size -> raise (ArgumentOutOfRangeException("Small capacity"))
            | c when c > 0 -> 
                let kArr : 'K array = Array.zeroCreate c
                let vArr : 'V array = Array.zeroCreate c
                if this.IsSplit then
                    let len = capacity - this.offset
                    // Keys
                    Array.Copy(this.keys, this.offset, kArr, 0, len);
                    Array.Copy(this.keys, 0, kArr, len, this.size - len);
                    // Values
                    Array.Copy(this.values, this.offset, vArr, 0, len);
                    Array.Copy(this.values, 0, vArr, len, this.size - len);
                else
                    Array.Copy(this.keys, this.offset, kArr, 0, this.size)
                    Array.Copy(this.values, this.offset, vArr, 0, this.size)
                this.keys <- kArr
                this.values <- vArr
                this.offset <- 0
                //this.version <- this.version + 1
                this.capacity <- value
            | _ -> ()

    member this.Comparer with get() = comparer

    member this.Clear()=
        this.version <- this.version + 1
        Array.Clear(this.keys, 0, this.size)
        Array.Clear(this.values, 0, this.size)
        this.offset <- 0
        this.size <- 0

    member this.Count with get() = this.size

    member this.IsEmpty with get() = this.size = 0


    member this.Keys 
        with get() : IList<'K> =
            {new IList<'K> with
                member x.Count with get() = this.size
                member x.IsReadOnly with get() = true
                member x.Item 
                    with get index : 'K = this.GetKeyByIndex(index)
                    and set index value = raise (NotSupportedException("Keys collection is read-only"))
                member x.Add(k) = raise (NotSupportedException("Keys collection is read-only"))
                member x.Clear() = raise (NotSupportedException("Keys collection is read-only"))
                member x.Contains(key) = this.ContainsKey(key)
                member x.CopyTo(array, arrayIndex) = 
                    Array.Copy(this.keys, 0, array, arrayIndex, this.size)
                member x.IndexOf(key:'K) = this.IndexOfKey(key)
                member x.Insert(index, value) = raise (NotSupportedException("Keys collection is read-only"))
                member x.Remove(key:'K) = raise (NotSupportedException("Keys collection is read-only"))
                member x.RemoveAt(index:int) = raise (NotSupportedException("Keys collection is read-only"))
                member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
                member x.GetEnumerator() : IEnumerator<'K> = 
                    let index = ref 0
                    let eVersion = ref this.version
                    let currentKey : 'K ref = ref Unchecked.defaultof<'K>
                    { new IEnumerator<'K> with
                        member e.Current with get() = currentKey.Value
                        member e.Current with get() = box e.Current
                        member e.MoveNext() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            if index.Value < this.size then
                                currentKey := this.keys.[index.Value]
                                index := index.Value + 1
                                true
                            else
                                index := this.size + 1
                                currentKey := Unchecked.defaultof<'K>
                                false
                        member e.Reset() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            index := 0
                            currentKey := Unchecked.defaultof<'K>
                        member e.Dispose() = 
                            index := 0
                            currentKey := Unchecked.defaultof<'K>
                    }
            }

    member this.Values 
        with get() : IList<'V> =
            { new IList<'V> with
                member x.Count with get() = this.size
                member x.IsReadOnly with get() = true
                member x.Item 
                    with get index : 'V = this.GetValueByIndex(index)
                    and set index value = raise (NotSupportedException("Values colelction is read-only"))
                member x.Add(k) = raise (NotSupportedException("Values colelction is read-only"))
                member x.Clear() = raise (NotSupportedException("Values colelction is read-only"))
                member x.Contains(value) = this.ContainsValue(value)
                member x.CopyTo(array, arrayIndex) = 
                    Array.Copy(this.values, 0, array, arrayIndex, this.size)
                member x.IndexOf(value:'V) = this.IndexOfValue(value)
                member x.Insert(index, value) = raise (NotSupportedException("Values colelction is read-only"))
                member x.Remove(value:'V) = raise (NotSupportedException("Values colelction is read-only"))
                member x.RemoveAt(index:int) = raise (NotSupportedException("Values colelction is read-only"))
                member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
                member x.GetEnumerator() : IEnumerator<'V> = 
                    let index = ref 0
                    let eVersion = ref this.version
                    let currentValue : 'V ref = ref Unchecked.defaultof<'V>
                    { new IEnumerator<'V> with
                        member e.Current with get() = currentValue.Value
                        member e.Current with get() = box e.Current
                        member e.MoveNext() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            if index.Value < this.size then
                                currentValue := this.values.[index.Value]
                                index := index.Value + 1
                                true
                            else
                                index := this.size + 1
                                currentValue := Unchecked.defaultof<'V>
                                false
                        member e.Reset() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            index := 0
                            currentValue := Unchecked.defaultof<'V>
                        member e.Dispose() = 
                            index := 0
                            currentValue := Unchecked.defaultof<'V>
                    }
            }

    member this.First 
        with get()=
            //use lock = makeLock this.SyncRoot
            if this.IsEmpty then OptionalValue.Missing
            else OptionalValue(KeyValuePair(this.GetByViewIndex(0).Key, OptionalValue(this.GetByViewIndex(0).Value))) 
            // TODO OMG this opt(k, opt v) nested opts are going deeper, will be too late to redesign!

    member this.Last 
        with get() =
            //use lock = makeLock this.SyncRoot
            if this.IsEmpty then OptionalValue.Missing
            else OptionalValue(KeyValuePair(this.GetByViewIndex(this.Count - 1).Key, OptionalValue(this.GetByViewIndex(this.Count - 1).Value))) 

    member this.ContainsKey(key) = this.IndexOfKey(key) >= 0

    member this.ContainsValue(value) = this.IndexOfValue(value) >= 0

    member this.IndexOfValue(value:'V) : int =
        //use lock = makeLock this.SyncRoot
        let mutable res = 0
        let mutable found = false
        let valueComparer = Comparer<'V>.Default;
        while not found do
            if valueComparer.Compare(value,this.values.[res]) = 0 then
                found <- true
            else res <- res + 1
        if found then res else -1
   
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> =
        let index = ref -1
        let pVersion = ref this.Version
        let currentKey : 'K ref = ref Unchecked.defaultof<'K>
        let currentValue : 'V ref = ref Unchecked.defaultof<'V>
        { new IEnumerator<KeyValuePair<'K,'V>> with
            member p.Current with get() = KeyValuePair(currentKey.Value, currentValue.Value)

            member p.Current with get() = box p.Current

            member p.MoveNext() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during MoveNext"))
                use lock = makeLock this.SyncRoot
                if index.Value + 1 < this.Count then
                    index := index.Value + 1
                    currentKey := this.Keys.[index.Value]
                    currentValue := this.Values.[index.Value]
                    true
                else
                    index := this.Count + 1
                    currentKey := Unchecked.defaultof<'K>
                    currentValue := Unchecked.defaultof<'V>
                    false

            member p.Reset() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during Reset"))
                index := 0
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>

            member p.Dispose() = 
                index := 0
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>

        }

    //#endregion


    //#region Virtual members

    abstract Add : k:'K * v:'V -> unit
    default this.Add(key, value) =
        if (box key) = null then raise (ArgumentNullException("key"))
        //use lock = makeLock this.SyncRoot
        this.EnsureCapacity()
        this.AddToBack(key, value)
        ()

    abstract AddLast : k:'K * v:'V -> unit
    default this.AddLast(key, value) = this.Add(key, value)

    abstract AddFirst : k:'K * v:'V -> unit
    default this.AddFirst(key, value) =
        if (box key) = null then raise (ArgumentNullException("key"))
        //use lock = makeLock this.SyncRoot
        this.EnsureCapacity()
        this.AddToFront(key, value)
        ()

    abstract IndexOfKey: 'K -> int
    default this.IndexOfKey(key:'K) : int =
        //use lock = makeLock this.SyncRoot
        let mutable res = 0
        let mutable found = false
        while not found && res < this.Count do
            if comparer.Compare(key, this.Keys.[res]) = 0 then
                found <- true
            else res <- res + 1
        if found then res else -1
    
    abstract Item : 'K -> 'V with get, set
    default this.Item
        with get key  : 'V =
            //use lock = makeLock this.SyncRoot
            let index = this.IndexOfKey(key)
            if index >= 0 then
                this.GetValueByIndex(index)
            else
                raise (KeyNotFoundException())
        and set k v =
            if (box k) = null then raise (ArgumentNullException("key"))
            //use lock = makeLock this.SyncRoot
            let index = this.IndexOfKey(k)
            if index > 0 then
                this.SetByViewIndex(index, k, v)
            else
                this.Add(k, v)

    abstract Remove: 'K -> bool
    default this.Remove(key) =
        //use lock = makeLock this.SyncRoot
        let index = this.IndexOfKey(key)
        if index >= 0 then this.RemoveAtIndex(index)
        index >= 0

    abstract RemoveLast : unit -> KeyValuePair<'K,'V>
    default this.RemoveLast() = 
        //use lock = makeLock this.SyncRoot
        if this.IsEmpty then raise (InvalidOperationException("Deque is empty"))
        this.RemoveFromBack()

    abstract RemoveFirst : unit -> KeyValuePair<'K,'V>
    default this.RemoveFirst() =
        //use lock = makeLock this.SyncRoot
        if this.IsEmpty then raise (InvalidOperationException("Deque is empty"))
        this.RemoveFromFront()


    //#endregion

    //#region Interfaces

    interface IEnumerable with
        member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator

    interface IEnumerable<KeyValuePair<'K,'V>> with
        member this.GetEnumerator() = this.GetEnumerator()

    interface ICollection  with
        member this.SyncRoot = this.SyncRoot
        member this.CopyTo(array, arrayIndex) =
            if array = null then raise (ArgumentNullException("array"))
            if arrayIndex < 0 || arrayIndex > array.Length then raise (ArgumentOutOfRangeException("arrayIndex"))
            if array.Length - arrayIndex < this.Count then raise (ArgumentException("ArrayPlusOffTooSmall"))
            for index in 0..this.size do
                let kvp = KeyValuePair(this.keys.[index], this.values.[index])
                array.SetValue(kvp, arrayIndex + index)
        member this.Count = this.Count
        member this.IsSynchronized with get() = false

    //#endregion

    //#region Constructors

    new() = DoubleDeque(Dictionary(), 8, Comparer<'K>.Default)
    new(comparer:IComparer<'K>) = DoubleDeque(Dictionary(), 8, comparer)
    new(dic:IDictionary<'K,'V>) = DoubleDeque(dic, dic.Count, Comparer<'K>.Default)
    new(capacity:int) = DoubleDeque(Dictionary(), capacity, Comparer<'K>.Default)
    new(dic:IDictionary<'K,'V>,comparer:IComparer<'K>) = DoubleDeque(dic, dic.Count, comparer)
    new(capacity:int,comparer:IComparer<'K>) = DoubleDeque(Dictionary(), capacity, comparer)

    //#endregion


//[<DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")>]
[<SerializableAttribute>]
type MapDeque<'K,'V when 'K : comparison>
    internal(dic:IDictionary<'K,'V>, capacity:int, comparer:IComparer<'K>) as this=

    //#region Main private constructor

    // View Index - external index of SortedList, internally used as "index"
    // Buffer Index - index in buffer arrays, internally used as "idx"

    [<DefaultValue>] val mutable public comparer : IComparer<'K> 
    do
        this.comparer <- if comparer = null then LanguagePrimitives.FastGenericComparer else comparer
        

    [<DefaultValue>] val mutable public keys : 'K array 
    [<DefaultValue>] val mutable public values : 'V array  

    [<DefaultValue>] val mutable public offset : int
    [<DefaultValue>] val mutable public size : int
    [<DefaultValue>] val mutable public version : int
    [<DefaultValue>] val mutable public isReadOnly : bool
    [<DefaultValue>] val mutable public capacity : int
    [<DefaultValue>] val mutable public maxSize : int
    [<DefaultValue>] val mutable public syncObject : obj

    do
       this.keys <- [||]
       this.values <- [||]
       this.maxSize <- 2146435071
       this.Capacity <- capacity
       this.syncObject <- new Object()
    
        
    //#endregion

    //#region Private & Internal members

    member private this.PostIncrement(value) =
        let ret = this.offset
        this.offset <- (this.offset + value) % capacity
        ret

    member private this.PreDecrement(value) =
        this.offset <- this.offset - value
        if this.offset < 0 then this.offset <- this.offset + capacity
        this.offset

    /// Calculates buffer index from view index
    member private this.IndexToBufferIndex(index) = 
        (index + this.offset) % capacity

    member internal this.GetByViewIndex(index) = 
        let idx = this.IndexToBufferIndex(index)
        KeyValuePair(this.keys.[idx], this.values.[idx])

    member internal this.SetByViewIndex(index, k, v) =
        use lock = makeLock this.SyncRoot
        let idx = this.IndexToBufferIndex(index)
        this.keys.[idx] <- k
        this.values.[idx] <- v
        this.version <- this.version + 1
        ()
    
    member internal this.AddToBack(k, v) =
        Trace.Assert(not this.IsFull)
        //use lock = makeLock this.SyncRoot
        let idx = this.IndexToBufferIndex(this.size)
        this.keys.[idx] <- k
        this.values.[idx] <- v
        this.size <- this.size + 1
        this.version <- this.version + 1
        ()

    member internal this.AddToFront(k, v) =
        Trace.Assert(not this.IsFull)
        //use lock = makeLock this.SyncRoot
        let idx = this.PreDecrement(1)
        this.keys.[idx] <- k
        this.values.[idx] <- v
        this.size <- this.size + 1
        this.version <- this.version + 1
        ()

    member internal this.InsertAtIndex(index, k, v) =
        this.EnsureCapacity()
        Trace.Assert(1 + this.size <= capacity)
        if this.IsEmpty || index = this.size then
            this.AddToBack(k, v)
        elif index = 0 then
            this.AddToFront(k, v)
        else
            //use lock = makeLock this.SyncRoot
            if index < this.size / 2 then
                let copyCount = index
                let writeIndex = capacity - 1
                for j in 0..copyCount-1 do
                    this.keys.[this.IndexToBufferIndex(writeIndex + j)] <- this.keys.[this.IndexToBufferIndex(j)]
                    this.values.[this.IndexToBufferIndex(writeIndex + j)] <- this.values.[this.IndexToBufferIndex(j)]
                this.PreDecrement(1) |> ignore
            else
                let copyCount = this.size - index
                let writeIndex = index + 1
                for j in copyCount-1..0 do
                    this.keys.[this.IndexToBufferIndex(writeIndex + j)] <- this.keys.[this.IndexToBufferIndex(index + j)]
                    this.values.[this.IndexToBufferIndex(writeIndex + j)] <- this.values.[this.IndexToBufferIndex(index + j)]
            let idx = this.IndexToBufferIndex(index)
            this.keys.[idx] <- k
            this.values.[idx] <- v
            this.version <- this.version + 1
            this.size <- this.size + 1

    member internal this.RemoveFromBack() : KeyValuePair<'K,'V> =
        Trace.Assert(not this.IsEmpty)
        //use lock = makeLock this.SyncRoot
        let idx = this.IndexToBufferIndex(this.size - 1)
        this.size <- this.size - 1
        this.version <- this.version + 1
        KeyValuePair(this.keys.[idx], this.values.[idx])

    member internal this.RemoveFromFront() : KeyValuePair<'K,'V> =
        Trace.Assert(not this.IsEmpty)
        //use lock = makeLock this.SyncRoot
        this.size <- this.size - 1
        let idx = this.PostIncrement(1)
        this.version <- this.version + 1
        KeyValuePair(this.keys.[idx], this.values.[idx])

    member internal this.RemoveAtIndex(index)=
        //use lock = makeLock this.SyncRoot
        if index = 0 then
            this.PostIncrement(1) |> ignore
        elif index = this.size - 1 then
            ()
        else
            if index < this.size / 2 then
                let copyCount = index
                for j in copyCount-1..0 do
                    this.keys.[this.IndexToBufferIndex(j + 1)] <- this.keys.[this.IndexToBufferIndex(j)]
                    this.values.[this.IndexToBufferIndex(j + 1)] <- this.values.[this.IndexToBufferIndex(j)]
                this.PostIncrement(1) |> ignore
            else
                let copyCount = this.size - index - 1
                let readIndex = index + 1
                for j in 0..copyCount-1 do
                    this.keys.[this.IndexToBufferIndex(j + index)] <- this.keys.[this.IndexToBufferIndex(readIndex + j)]
                    this.values.[this.IndexToBufferIndex(j + index)] <- this.values.[this.IndexToBufferIndex(readIndex + j)]
        this.version <- this.version + 1
        this.size <- this.size - 1

    member internal this.EnsureCapacity(?min) =
        let mutable num = this.keys.Length * 2
        if num > this.maxSize then num <- this.maxSize
        if min.IsSome && num < min.Value then num <- min.Value
        if this.size = this.keys.Length then this.Capacity <- num
//        if min.IsSome then
//            this.Capacity <- Math.Max(num, Math.Min(this.keys.Length * 2, maxSize)) // checks and lock are in Capacity setter
    
    member internal this.GetKeyByIndex(index) = 
        if index < 0 || index >= this.size then 
            raise (ArgumentOutOfRangeException("index"))
        this.keys.[this.IndexToBufferIndex(index)]
    
    member internal this.GetValueByIndex(index) = 
        if index < 0 || index >= this.size then 
            raise (ArgumentOutOfRangeException("index"))
        this.values.[this.IndexToBufferIndex(index)]

    member internal this.IsFull with get() = this.size = capacity

    member internal this.IsSplit with get() = this.offset > (capacity - this.size)

    member internal this.IsReadOnly with get() = this.isReadOnly

    member internal this.KeysArray with get() = this.keys

    member internal this.Offset with get() = this.offset

    member internal this.SyncRoot with get() = this.syncObject

    member internal this.Version with get() = this.version

    /// Removes first element to free space for new element if the map is full. 
    member internal this.AddAndRemoveFisrtIfFull(key, value) =
        //use lock = makeLock this.SyncRoot
        if this.IsFull then this.RemoveFromFront() |> ignore
        this.Add(key, value)
        ()

    //#endregion

    //#region Public members

    ///Gets or sets the capacity. This value must always be greater than zero, and this property cannot be set to a value less than this.size/>.
    member this.Capacity 
        with get() = capacity
        and set(value) =
            //use lock = makeLock this.SyncRoot
            match value with
            | c when c = this.keys.Length -> ()
            | c when c < this.size -> raise (ArgumentOutOfRangeException("Small capacity"))
            | c when c > 0 -> 
                let kArr : 'K array = Array.zeroCreate c
                let vArr : 'V array = Array.zeroCreate c
                if this.IsSplit then
                    let len = capacity - this.offset
                    // Keys
                    Array.Copy(this.keys, this.offset, kArr, 0, len);
                    Array.Copy(this.keys, 0, kArr, len, this.size - len);
                    // Values
                    Array.Copy(this.values, this.offset, vArr, 0, len);
                    Array.Copy(this.values, 0, vArr, len, this.size - len);
                else
                    Array.Copy(this.keys, this.offset, kArr, 0, this.size)
                    Array.Copy(this.values, this.offset, vArr, 0, this.size)
                this.keys <- kArr
                this.values <- vArr
                this.offset <- 0
                //this.version <- this.version + 1
                this.capacity <- value
            | _ -> ()

    member this.Comparer with get() = this.comparer

    member this.Clear()=
        this.version <- this.version + 1
        Array.Clear(this.keys, 0, this.size)
        Array.Clear(this.values, 0, this.size)
        this.offset <- 0
        this.size <- 0

    member this.Count with get() = this.size

    member this.IsEmpty with get() = this.size = 0


    member this.Keys 
        with get() : IList<'K> =
            {new IList<'K> with
                member x.Count with get() = this.size
                member x.IsReadOnly with get() = true
                member x.Item 
                    with get index : 'K = this.GetKeyByIndex(index)
                    and set index value = raise (NotSupportedException("Keys collection is read-only"))
                member x.Add(k) = raise (NotSupportedException("Keys collection is read-only"))
                member x.Clear() = raise (NotSupportedException("Keys collection is read-only"))
                member x.Contains(key) = this.ContainsKey(key)
                member x.CopyTo(array, arrayIndex) = 
                    Array.Copy(this.keys, 0, array, arrayIndex, this.size)
                member x.IndexOf(key:'K) = this.IndexOfKey(key)
                member x.Insert(index, value) = raise (NotSupportedException("Keys collection is read-only"))
                member x.Remove(key:'K) = raise (NotSupportedException("Keys collection is read-only"))
                member x.RemoveAt(index:int) = raise (NotSupportedException("Keys collection is read-only"))
                member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
                member x.GetEnumerator() : IEnumerator<'K> = 
                    let index = ref 0
                    let eVersion = ref this.version
                    let currentKey : 'K ref = ref Unchecked.defaultof<'K>
                    { new IEnumerator<'K> with
                        member e.Current with get() = currentKey.Value
                        member e.Current with get() = box e.Current
                        member e.MoveNext() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            if index.Value < this.size then
                                currentKey := this.keys.[index.Value]
                                index := index.Value + 1
                                true
                            else
                                index := this.size + 1
                                currentKey := Unchecked.defaultof<'K>
                                false
                        member e.Reset() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            index := 0
                            currentKey := Unchecked.defaultof<'K>
                        member e.Dispose() = 
                            index := 0
                            currentKey := Unchecked.defaultof<'K>
                    }
            }

    member this.Values 
        with get() : IList<'V> =
            { new IList<'V> with
                member x.Count with get() = this.size
                member x.IsReadOnly with get() = true
                member x.Item 
                    with get index : 'V = this.GetValueByIndex(index)
                    and set index value = raise (NotSupportedException("Values colelction is read-only"))
                member x.Add(k) = raise (NotSupportedException("Values colelction is read-only"))
                member x.Clear() = raise (NotSupportedException("Values colelction is read-only"))
                member x.Contains(value) = this.ContainsValue(value)
                member x.CopyTo(array, arrayIndex) = 
                    Array.Copy(this.values, 0, array, arrayIndex, this.size)
                member x.IndexOf(value:'V) = this.IndexOfValue(value)
                member x.Insert(index, value) = raise (NotSupportedException("Values colelction is read-only"))
                member x.Remove(value:'V) = raise (NotSupportedException("Values colelction is read-only"))
                member x.RemoveAt(index:int) = raise (NotSupportedException("Values colelction is read-only"))
                member x.GetEnumerator() = x.GetEnumerator() :> IEnumerator
                member x.GetEnumerator() : IEnumerator<'V> = 
                    let index = ref 0
                    let eVersion = ref this.version
                    let currentValue : 'V ref = ref Unchecked.defaultof<'V>
                    { new IEnumerator<'V> with
                        member e.Current with get() = currentValue.Value
                        member e.Current with get() = box e.Current
                        member e.MoveNext() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            if index.Value < this.size then
                                currentValue := this.values.[index.Value]
                                index := index.Value + 1
                                true
                            else
                                index := this.size + 1
                                currentValue := Unchecked.defaultof<'V>
                                false
                        member e.Reset() = 
                            if eVersion.Value <> this.version then
                                raise (InvalidOperationException("Collection changed during enumeration"))
                            index := 0
                            currentValue := Unchecked.defaultof<'V>
                        member e.Dispose() = 
                            index := 0
                            currentValue := Unchecked.defaultof<'V>
                    }
            }

    member this.First 
        with get()=
            //use lock = makeLock this.SyncRoot
            if this.IsEmpty then OptionalValue.Missing
            else OptionalValue(KeyValuePair(this.GetByViewIndex(0).Key, OptionalValue(this.GetByViewIndex(0).Value))) 
            // TODO OMG this opt(k, opt v) nested opts are going deeper, will be too late to redesign!

    member this.Last 
        with get() =
            //use lock = makeLock this.SyncRoot
            if this.IsEmpty then OptionalValue.Missing
            else OptionalValue(KeyValuePair(this.GetByViewIndex(this.Count - 1).Key, OptionalValue(this.GetByViewIndex(this.Count - 1).Value))) 

    member this.ContainsKey(key) = this.IndexOfKey(key) >= 0

    member this.ContainsValue(value) = this.IndexOfValue(value) >= 0

    member this.IndexOfValue(value:'V) : int =
        //use lock = makeLock this.SyncRoot
        let mutable res = 0
        let mutable found = false
        let valueComparer = Comparer<'V>.Default;
        while not found do
            if valueComparer.Compare(value,this.values.[res]) = 0 then
                found <- true
            else res <- res + 1
        if found then res else -1
   
    member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> =
        let index = ref -1
        let pVersion = ref this.Version
        let currentKey : 'K ref = ref Unchecked.defaultof<'K>
        let currentValue : 'V ref = ref Unchecked.defaultof<'V>
        { new IEnumerator<KeyValuePair<'K,'V>> with
            member p.Current with get() = KeyValuePair(currentKey.Value, currentValue.Value)

            member p.Current with get() = box p.Current

            member p.MoveNext() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during MoveNext"))
                use lock = makeLock this.SyncRoot
                if index.Value + 1 < this.Count then
                    index := index.Value + 1
                    currentKey := this.Keys.[index.Value]
                    currentValue := this.Values.[index.Value]
                    true
                else
                    index := this.Count + 1
                    currentKey := Unchecked.defaultof<'K>
                    currentValue := Unchecked.defaultof<'V>
                    false

            member p.Reset() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during Reset"))
                index := 0
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>

            member p.Dispose() = 
                index := 0
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>

        }

    
    member this.Add(key, value) =
        if (box key) = null then raise (ArgumentNullException("key"))
        //use lock = makeLock this.SyncRoot
        let index = this.IndexOfKey(key)
        if index >= 0 then raise (ArgumentException("key already exists"))
        this.InsertAtIndex(~~~index, key, value)
        ()
    
    member this.AddLast(key, value) = 
        //use lock = makeLock this.SyncRoot 
        // check that key > last key
        if this.IsEmpty || this.Comparer.Compare(key, this.Keys.[this.Count - 1]) = 1 then
            this.Add(key, value)
        else raise (ArgumentException("Key is not bigger/later than the latest existing key"))

    member this.AddFirst(key, value) = 
        //use lock = makeLock this.SyncRoot 
        // check that key > last key
        if this.IsEmpty || this.Comparer.Compare(key, this.Keys.[0]) = -1 then
            this.Add(key, value)
        else raise (ArgumentException("Key is not smaller/earlier than the latest existing key"))

    member this.IndexOfKey(key) = 
        if (box key) = null then raise (ArgumentNullException("key"))
        let mutable index = 0
        //use lock = makeLock this.SyncRoot
        if this.IsSplit then
            let c = this.Comparer.Compare(key, this.Keys.[0])
            match c with
            | 0 -> index <- this.Capacity - this.Offset
            | -1 -> // key in the second part of the buffer
                index <- Array.BinarySearch(this.KeysArray, this.Offset, this.Capacity - this.Offset, key, this.Comparer) 
            | 1 -> // key in the first part of the buffer
                index <- Array.BinarySearch(this.KeysArray, 0, this.Offset + this.Count - this.Capacity, key, this.Comparer)
            | _ -> failwith("nonsense")
        else
            index <- Array.BinarySearch(this.KeysArray, this.Offset, this.Count, key, this.Comparer) 
        index

    member this.Item
        with get key =
            //use lock = makeLock this.SyncRoot
            let index = this.IndexOfKey(key)
            if index >= 0 then
                this.GetValueByIndex(index)
            else
                raise (KeyNotFoundException())
        and set k v =
            if (box k) = null then raise (ArgumentNullException("key"))
            //use lock = makeLock this.SyncRoot
            let index = this.IndexOfKey(k)
            if index >= 0 then // contains key            
                this.SetByViewIndex(index, k, v)
            else
                this.InsertAtIndex(~~~index, k, v)

    member this.Remove(key) =
        //use lock = makeLock this.SyncRoot
        let index = this.IndexOfKey(key)
        if index >= 0 then this.RemoveAtIndex(index)
        index >= 0

    member this.RemoveLast() = 
        //use lock = makeLock this.SyncRoot
        if this.IsEmpty then raise (InvalidOperationException("Deque is empty"))
        this.RemoveFromBack()

    member this.RemoveFirst() =
        //use lock = makeLock this.SyncRoot
        if this.IsEmpty then raise (InvalidOperationException("Deque is empty"))
        this.RemoveFromFront()

    // TODO test each direction and each if-then condition!
    member this.TryFind(key:'K,lookup:Lookup, [<Out>]result: byref<KeyValuePair<'K, 'V>>) : int =
        result <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        //use lock = makeLock this.SyncRoot
        let index = this.IndexOfKey(key)
        match lookup with
        | Lookup.EQ -> 
            if index >= 0 then
                result <- this.GetByViewIndex(index)
                index
            else
                -1
        | Lookup.LT -> 
            if index > 0 then
                result <- this.GetByViewIndex(index - 1)
                index - 1
            elif index = 0 then
                -1
            else
                let index2 = ~~~index
                if index2 = this.Count then // there are no elements larger than key
                    result <- this.GetByViewIndex(this.Count - 1)
                    this.Count - 1
                elif index2 = 0 then
                    -1
                else //  it is the index of the first element that is larger than value
                    result <- this.GetByViewIndex(index2 - 1)
                    index2 - 1
        | Lookup.LE -> 
            if index >= 0 then
                result <- this.GetByViewIndex(index) // equal
                index
            else
                let index2 = ~~~index
                if index2 = this.Count then // there are no elements larger than key
                    result <- this.GetByViewIndex(this.Count - 1)
                    this.Count - 1
                elif index2 = 0 then
                    -1
                else //  it is the index of the first element that is larger than value
                    result <- this.GetByViewIndex(index2 - 1)
                    index2 - 1
        | Lookup.GT -> 
            if index >= 0 && index < this.Count - 1 then
                result <- this.GetByViewIndex(index + 1)
                index + 1
            elif index >= this.Count - 1 then
                -1
            else
                let index2 = ~~~index
                if index2 = this.Count then // there are no elements larger than key
                    -1
                else //  it is the index of the first element that is larger than value
                    result <- this.GetByViewIndex(index2)
                    index2
        | Lookup.GE ->
            if index >= 0 && index < this.Count then
                result <- this.GetByViewIndex(index) // equal
                index
            else
                let index2 = ~~~index
                if index2 = this.Count then // there are no elements larger than key
                    -1
                else //  it is the index of the first element that is larger than value
                    result <- this.GetByViewIndex(index2)
                    index2
        | _ -> raise (ApplicationException("Wrong lookup direction"))
    
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
        let index = this.IndexOfKey(key)
        if index >= 0 then
            value <- this.GetValueByIndex(index)
            true
        else
            value <- Unchecked.defaultof<'V>
            false

    member this.GetPointer() : IPointer<'K,'V> =
        let index = ref -1
        let pVersion = ref this.Version
        let currentKey : 'K ref = ref Unchecked.defaultof<'K>
        let currentValue : 'V ref = ref Unchecked.defaultof<'V>
        { new IPointer<'K,'V> with
            member p.MoveGetNextAsync() = failwith "not implemented"
            member p.Source with get() = failwith "not implemented" //box this :?> IReadOnlySortedMap<'K,'V>
            member p.Current with get() = KeyValuePair(currentKey.Value, currentValue.Value)

            member p.Current with get() = box p.Current

            member p.MoveNext() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during MoveNext"))
                //use lock = makeLock this.SyncRoot
                if index.Value + 1 < this.Count then
                    index := index.Value + 1
                    currentKey := this.Keys.[index.Value]
                    currentValue := this.Values.[index.Value]
                    true
                else
                    index := this.Count + 1
                    currentKey := Unchecked.defaultof<'K>
                    currentValue := Unchecked.defaultof<'V>
                    false

            member p.Reset() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during Reset"))
                index := 0
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>

            member p.Dispose() = 
                index := 0
                currentKey := Unchecked.defaultof<'K>
                currentValue := Unchecked.defaultof<'V>

            member p.MoveAt(key:'K, lookup:Lookup) = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during MoveAt"))
                //use lock = makeLock this.SyncRoot
                let position, kvp = this.TryFind(key, lookup)
                if position >= 0 then
                    index := position
                    currentKey := this.Keys.[index.Value]
                    currentValue := this.Values.[index.Value]
                    true
                else
                    index := this.Count + 1
                    currentKey := Unchecked.defaultof<'K>
                    currentValue := Unchecked.defaultof<'V>
                    false

            member p.MoveFirst() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during MoveFirst"))
                //use lock = makeLock this.SyncRoot
                if not this.IsEmpty then
                    index := 0
                    currentKey := this.Keys.[index.Value]
                    currentValue := this.Values.[index.Value]
                    true
                else
                    index := this.Count + 1
                    currentKey := Unchecked.defaultof<'K>
                    currentValue := Unchecked.defaultof<'V>
                    false

            member p.MoveLast() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during MoveLast"))
                //use lock = makeLock this.SyncRoot
                if not this.IsEmpty then
                    index := this.Count - 1
                    currentKey := this.Keys.[index.Value]
                    currentValue := this.Values.[index.Value]
                    true
                else
                    index := this.Count + 1
                    currentKey := Unchecked.defaultof<'K>
                    currentValue := Unchecked.defaultof<'V>
                    false

            member p.MovePrevious() = 
                if pVersion.Value <> this.Version then
                    raise (InvalidOperationException("IEnumerable changed during MovePrevious"))
                //use lock = makeLock this.SyncRoot
                if index.Value - 1 >= 0 then
                    index := index.Value - 1
                    currentKey := this.Keys.[index.Value]
                    currentValue := this.Values.[index.Value]
                    true
                else
                    index := this.Count + 1
                    currentKey := Unchecked.defaultof<'K>
                    currentValue := Unchecked.defaultof<'V>
                    false

            member p.CurrentKey with get() = currentKey.Value

            member p.CurrentValue with get() = currentValue.Value

        }

    //#endregion


    //#region Interfaces 

    interface IEnumerable with
        member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator

    interface IDictionary<'K,'V> with
        
        member this.Count = this.Count
        
        member this.IsReadOnly with get() = this.IsReadOnly
        
        member this.Item
            with get key = this.Item(key)
            and set key value = this.[key] <- value
        
        member this.Keys with get() = this.Keys :> ICollection<'K>
        
        member this.Values with get() = this.Values :> ICollection<'V>

        member this.Clear() = this.Clear()
        
        member this.ContainsKey(key) = this.ContainsKey(key)
        
        member this.Contains(kvp:KeyValuePair<'K,'V>) = this.ContainsKey(kvp.Key)
        
        member this.CopyTo(array, arrayIndex) =
            if array = null then raise (ArgumentNullException("array"))
            if arrayIndex < 0 || arrayIndex > array.Length then raise (ArgumentOutOfRangeException("arrayIndex"))
            if array.Length - arrayIndex < this.Count then raise (ArgumentException("ArrayPlusOffTooSmall"))
            for index in 0..this.Count do
                let kvp = KeyValuePair(this.Keys.[index], this.Values.[index])
                array.[arrayIndex + index] <- kvp

        member this.Add(key, value) = this.Add(key, value)
        
        member this.Add(kvp:KeyValuePair<'K,'V>) = this.Add(kvp.Key, kvp.Value)

        member this.Remove(key) = this.Remove(key)

        member this.Remove(kvp:KeyValuePair<'K,'V>) = this.Remove(kvp.Key)

        member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
            let index = this.IndexOfKey(key)
            if index >= 0 then
                value <- this.GetValueByIndex(index)
                true
            else
                value <- Unchecked.defaultof<'V>
                false

        member this.GetEnumerator() : IEnumerator<KeyValuePair<'K,'V>> = 
            this.GetPointer() :> IEnumerator<KeyValuePair<'K,'V>> 

    //#endregion
    
    //#region Constructors

    new() = MapDeque(Dictionary(), 8, Comparer<'K>.Default)
    new(comparer:IComparer<'K>) = MapDeque(Dictionary(), 8, comparer)
    new(dic:IDictionary<'K,'V>) = MapDeque(dic, dic.Count, Comparer<'K>.Default)
    new(capacity:int) = MapDeque(Dictionary(), capacity, Comparer<'K>.Default)
    new(dic:IDictionary<'K,'V>,comparer:IComparer<'K>) = MapDeque(dic, dic.Count, comparer)
    new(capacity:int,comparer:IComparer<'K>) = MapDeque(Dictionary(), capacity, comparer)

    //#endregion


//[<DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")>]
[<SerializableAttribute>]
type IndexedMapDeque<'K,'V when 'K : comparison>
    internal(dic:IDictionary<'K,'V>, capacity:int, comparer:IComparer<'K>)=
    inherit MapDeque<'K,'V>(dic, capacity, comparer)

    let mapping = Dictionary<'K,'V>()

    //#region Abstract members inmplementation

    member this.Add(key, value) =
        if (box key) = null then raise (ArgumentNullException("key"))
        use lock = makeLock this.SyncRoot
        if mapping.ContainsKey(key) then raise (ArgumentException("Key already exists"))
        this.EnsureCapacity()
        this.AddToBack(key, value)
        mapping.[key] <- value
        ()

    member this.AddLast(key, value) = this.Add(key, value)

    member this.AddFirst(key, value) =
        if (box key) = null then raise (ArgumentNullException("key"))
        use lock = makeLock this.SyncRoot
        if mapping.ContainsKey(key) then raise (ArgumentException("Key already exists"))
        this.EnsureCapacity()
        this.AddToFront(key, value)
        mapping.[key] <- value
        ()
        
    member this.IndexOfKey(key) = 
        use lock = makeLock this.SyncRoot
        let mutable res = 0
        let mutable found = false
        while not found && res < this.Count do
            if comparer.Compare(key, this.Keys.[res]) = 0 then
                found <- true
            else res <- res + 1
        if found then res else -1

    member this.Item
        with get key  : 'V =
            use lock = makeLock this.SyncRoot
            let index = this.IndexOfKey(key)
            if index >= 0 then
                this.GetValueByIndex(index)
            else
                raise (KeyNotFoundException())
        and set k v =
            if (box k) = null then raise (ArgumentNullException("key"))
            use lock = makeLock this.SyncRoot
            let index = this.IndexOfKey(k)
            if index > 0 then
                mapping.[k] <- v
                this.SetByViewIndex(index, k, v)
            else
                this.Add(k, v)

    member this.Remove(key) =
        use lock = makeLock this.SyncRoot
        let index = this.IndexOfKey(key)
        if index >= 0 then 
            this.RemoveAtIndex(index)
            let res = mapping.Remove(key)
            Trace.Assert(res)
        index >= 0

    member this.RemoveLast() = 
        use lock = makeLock this.SyncRoot
        if this.IsEmpty then raise (InvalidOperationException("Deque is empty"))
        let res = this.RemoveFromBack()
        let res1 = mapping.Remove(res.Key)
        Trace.Assert(res1)
        res

    member this.RemoveFirst() =
        use lock = makeLock this.SyncRoot
        if this.IsEmpty then raise (InvalidOperationException("Deque is empty"))
        let res = this.RemoveFromFront()
        let res1 = mapping.Remove(res.Key)
        Trace.Assert(res1)
        res

    member this.TryFind(key:'K,lookup:Lookup, [<Out>]result: byref<KeyValuePair<'K, 'V>>) : int =
        result <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        let index = this.IndexOfKey(key)
        match lookup with
        | Lookup.EQ -> 
            if index >= 0 then
                result <- this.GetByViewIndex(index)
                index
            else
                -1
        | Lookup.LT -> 
            if index > 0 then
                result <- this.GetByViewIndex(index - 1)
                index - 1
            else
                -1
        
        | Lookup.GT -> 
            if index >= 0 && index < this.Count - 1 then
                result <- this.GetByViewIndex(index + 1)
                index + 1
            else
                -1
        | Lookup.GE | Lookup.LE -> 
            raise (InvalidOperationException("Indexed series do not support directional operations"))
        | _ -> raise (ApplicationException("Wrong lookup direction"))

    //#endregion

    
    //#region Constructors

    new() = IndexedMapDeque(Dictionary(), 8, Comparer<'K>.Default)
    new(comparer:IComparer<'K>) = IndexedMapDeque(Dictionary(), 8, comparer)
    new(dic:IDictionary<'K,'V>) = IndexedMapDeque(dic, dic.Count, Comparer<'K>.Default)
    new(capacity:int) = IndexedMapDeque(Dictionary(), capacity, Comparer<'K>.Default)
    new(dic:IDictionary<'K,'V>,comparer:IComparer<'K>) = IndexedMapDeque(dic, dic.Count, comparer)
    new(capacity:int,comparer:IComparer<'K>) = IndexedMapDeque(Dictionary(), capacity, comparer)

    //#endregion
