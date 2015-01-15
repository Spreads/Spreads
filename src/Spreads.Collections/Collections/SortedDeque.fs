namespace Spreads.Collections.Extra

open System
open System.Collections
open System.Collections.Generic
open System.Collections.ObjectModel
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections


/// A shortcut for KeyValuePair
//[<CustomComparison;CustomEquality>]
//type KV<'K,'V when 'K : comparison and 'K : equality> =
//  struct
//    val Key : 'K
//    val Value : 'V
//    new(key, value) = {Key = key; Value = value}
//  end
//  override x.Equals(yobj) =
//    match yobj with
//    | :? KV<'K,_> as y -> (x.Key = y.Key)
//    | _ -> false
//  override x.GetHashCode() = x.Key.GetHashCode()
//  interface System.IComparable<KVP<'K,'V>> with
//    member x.CompareTo y = 
//      let c = Comparer<'K>.Default
//      c.Compare(x.Key, y.Key)
//  interface System.IComparable with
//    member x.CompareTo other = 
//      match other with
//      | :? KV<'K,_> as y -> 
//        let c = Comparer<'K>.Default
//        c.Compare(x.Key, y.Key)
//      | _ -> invalidArg "other" "Cannot compare values of different types"



[<SerializableAttribute>]
type SortedDeque<'T when 'T : comparison and 'T : equality>
  internal(input:IEnumerable<'T>, capacity1:int, comparer:IComparer<'T>) as this=
  let mutable cpct : int = capacity1
  [<DefaultValue>] val mutable comparer : IComparer<'T> 
  [<DefaultValue>] val mutable buffer : 'T array 
  [<DefaultValue>] val mutable firstOffset : int
  [<DefaultValue>] val mutable size : int
  [<DefaultValue>] val mutable version : int
  [<DefaultValue>] val mutable isReadOnly : bool
  [<DefaultValue>] val mutable maxSize : int
  //[<DefaultValue>] val mutable syncObject : obj
  [<DefaultValue>] val mutable isSynchronized : bool
  [<NonSerializedAttribute>]
  let syncRoot = new Object()
  do
    this.comparer <- if comparer = null then LanguagePrimitives.FastGenericComparer else comparer
    this.buffer <- Array.zeroCreate capacity1
    this.maxSize <- 2146435071
    this.Capacity <- capacity1
    
  //#region Private & Internal members

  /// Calculates offset from index
  member inline private this.IndexToOffset(index) = (index + this.firstOffset) % cpct

  member inline private this.GetByIndex(index) : 'T =
    if index <= this.size then
      this.buffer.[this.IndexToOffset(index)]
    else raise (ArgumentOutOfRangeException("index"))

  member inline private this.SetByIndex(index, element) =
    if index <= this.size then
      let offset = this.IndexToOffset(index)
      this.buffer.[offset] <- element
      this.version <- this.version + 1
    else raise (ArgumentOutOfRangeException("index"))

  member inline private this.AddToRight(element) =
    this.EnsureCapacity()
    let offset = this.IndexToOffset(this.size)
    this.buffer.[offset] <- element
    this.size <- this.size + 1
    this.version <- this.version + 1

  member inline private this.AddToLeft(element) =
    this.EnsureCapacity()
    let offset = (this.firstOffset - 1 + cpct) % cpct
    this.firstOffset <- offset
    this.buffer.[offset] <- element
    this.size <- this.size + 1
    this.version <- this.version + 1

  // inline
  member private this.InsertAtIndex(index, element) =
    if this.size = 0 || index = this.size then
        this.AddToRight(element)
    elif index = 0 then
        this.AddToLeft(element)
    else
      this.EnsureCapacity()
      // if (this.fisrtOffset + index) % cpct 
      // TODO check for split and do array.copy when possible
      if index < this.size / 2 then
          let copyCount = index
          let writeIndex = cpct - 1
          for j in 0..copyCount-1 do
              this.buffer.[this.IndexToOffset(writeIndex + j)] 
                <- this.buffer.[this.IndexToOffset(j)]
          let offset = (this.firstOffset - 1 + cpct) % cpct
          this.firstOffset <- offset
      else
          let copyCount = this.size - index
          let writeIndex = index + 1
          for j in copyCount-1..0 do
              this.buffer.[this.IndexToOffset(writeIndex + j)] <- this.buffer.[this.IndexToOffset(index + j)]
      let offset = this.IndexToOffset(index)
      this.buffer.[offset] <- element
      this.version <- this.version + 1
      this.size <- this.size + 1

  member inline private this.RemoveFromRight() : 'T =
    if this.size > 0 then
      let offset = this.IndexToOffset(this.size - 1)
      this.size <- this.size - 1
      this.version <- this.version + 1
      this.buffer.[offset]
    else raise (InvalidOperationException("Deque is empty"))

  member inline private this.RemoveFromLeft() : 'T =
    if this.size > 0 then
      this.size <- this.size - 1
      let offset = this.firstOffset
      this.firstOffset <- (this.firstOffset + 1) % cpct
      this.version <- this.version + 1
      this.buffer.[offset]
    else raise (InvalidOperationException("Deque is empty"))

  member inline private this.RemoveAtIndex(index) : 'T =
    let ret = this.GetByIndex(index)
    if index = 0 then
        this.firstOffset <- (this.firstOffset + 1) % cpct
    elif index = this.size - 1 then
        // decrement size at the end of the method
        ()
    else
        if index < this.size / 2 then
            let copyCount = index
            for j in copyCount-1..0 do
                this.buffer.[this.IndexToOffset(j + 1)] <- this.buffer.[this.IndexToOffset(j)]
            this.firstOffset <- (this.firstOffset + 1) % cpct
        else
            let copyCount = this.size - index - 1
            let readIndex = index + 1
            for j in 0..copyCount-1 do
                this.buffer.[this.IndexToOffset(j + index)] <- this.buffer.[this.IndexToOffset(readIndex + j)]
    this.version <- this.version + 1
    this.size <- this.size - 1
    ret

  member internal this.EnsureCapacity(?min) =
    Trace.Assert(this.size <= this.buffer.Length)
    if this.size = this.buffer.Length then 
      let mutable num = ((this.buffer.Length + 1) * 3) / 2
      if num > this.maxSize then num <- this.maxSize
      if min.IsSome && num < min.Value then num <- min.Value
      this.Capacity <- num
    
  member this.IsSynchronized with get() = this.isSynchronized and set v = this.isSynchronized <- v

  /// Removes first element to free space for new element if the map is full. 
  member internal this.AddAndRemoveFisrtIfFull(element) =
      //use lock = makeLock this.SyncRoot
      if this.size = cpct then this.RemoveFromLeft() |> ignore
      this.Add(element)
      ()

  //#endregion

  //#region Public members

  ///Gets or sets the capacity. This value must always be greater than zero, and this property cannot be set to a value less than this.size/>.
  member this.Capacity 
      with get() = cpct
      and set(value) =
        let entered = enterLockIf syncRoot  this.isSynchronized
        try
          match value with
          | c when c = this.buffer.Length -> ()
          | c when c < this.size -> raise (ArgumentOutOfRangeException("Small capacity"))
          | c when c > 0 -> 
              let elArr : 'T array = Array.zeroCreate c
              if this.firstOffset + this.size > cpct then // is split
                  let len = cpct - this.firstOffset
                  // Elements
                  Array.Copy(this.buffer, this.firstOffset, elArr, 0, len);
                  Array.Copy(this.buffer, 0, elArr, len, this.size - len);
              else
                  Array.Copy(this.buffer, this.firstOffset, elArr, 0, this.size)
              this.buffer <- elArr
              this.firstOffset <- 0
              //this.version <- this.version + 1
              cpct <- value
          | _ -> ()
        finally
          exitLockIf syncRoot entered

  member this.Comparer with get() = this.comparer

  member this.Clear()=
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      this.version <- this.version + 1
      Array.Clear(this.buffer, 0, this.size)
      this.firstOffset <- 0
      this.size <- 0
    finally
      exitLockIf syncRoot entered

  member this.Count with get() = this.size

  member this.IsEmpty with get() = this.size = 0


//  interface IList<'T> with // // TODO all methods
//    member x.Count with get() = this.size
//    member x.IsReadOnly with get() = true
//    member x.Item 
//        with get index : 'T = this.GetByIndex(index) 
//        and set index value = raise (NotSupportedException("Elements collection is read-only"))
//    member x.Add(k) = raise (NotSupportedException("Elements collection is read-only"))
//    member x.Clear() = raise (NotSupportedException("Elements collection is read-only"))
//    member x.Contains(element:'T) = this.Contains(element)
//    member x.CopyTo(array, arrayIndex) = 
//        Array.Copy(this.elements, 0, array, arrayIndex, this.size)
//    member x.IndexOf(key:'T) = this.IndexOfElement(key)
//    member x.Insert(index, value) = raise (NotSupportedException("Elements collection is read-only"))
//    member x.Remove(key:'T) = raise (NotSupportedException("Elements collection is read-only"))
//    member x.RemoveAt(index:int) = raise (NotSupportedException("Elements collection is read-only"))
//    member x.GetEnumerator() : IEnumerator = x.GetEnumerator() :> IEnumerator
//    member x.GetEnumerator() : IEnumerator<'T> = 
//        let index = ref 0
//        let eVersion = ref this.version
//        let currentKey : 'T ref = ref Unchecked.defaultof<'T>
//        { new IEnumerator<'T> with
//            member e.Current with get() = currentKey.Value
//            member e.Current with get() = box e.Current
//            member e.MoveNext() = 
//                if eVersion.Value <> this.version then
//                    raise (InvalidOperationException("Collection changed during enumeration"))
//                if index.Value < this.size then
//                    currentKey := this.elements.[index.Value]
//                    index := index.Value + 1
//                    true
//                else
//                    index := this.size + 1
//                    currentKey := Unchecked.defaultof<'T>
//                    false
//            member e.Reset() = 
//                if eVersion.Value <> this.version then
//                    raise (InvalidOperationException("Collection changed during enumeration"))
//                index := 0
//                currentKey := Unchecked.defaultof<'T>
//            member e.Dispose() = 
//                index := 0
//                currentKey := Unchecked.defaultof<'T>
//        }


  member this.First 
      with get()=
        let entered = enterLockIf syncRoot  this.isSynchronized
        try
          if this.size = 0 then OptionalValue.Missing
          else OptionalValue(this.GetByIndex(0)) 
        finally
          exitLockIf syncRoot entered

  member this.Last 
      with get() =
        let entered = enterLockIf syncRoot  this.isSynchronized
        try
          if this.size = 0 then OptionalValue.Missing
          else OptionalValue(this.GetByIndex(this.size - 1)) 
        finally
          exitLockIf syncRoot entered

  member this.Contains(element:'T) = this.IndexOfElement(element) >= 0

  member this.Contains(predicate:'T -> bool) = this.IndexOfFirst(predicate) >= 0

  member private this.IndexOfFirst(predicate:'T -> bool) : int =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let mutable res = 0
      let mutable found = false
      while not found do
          if predicate(this.buffer.[this.IndexToOffset res]) then
              found <- true
          else res <- res + 1
      if found then res else -1
    finally
      exitLockIf syncRoot entered

  member this.GetEnumerator() : IEnumerator<'T> =
      let index = ref -1
      let pVersion = ref this.version
      let currentElement : 'T ref = ref Unchecked.defaultof<'T>
      { new IEnumerator<'T> with
        member p.Current with get() = currentElement.Value
        member p.Current with get() = box p.Current

        member p.MoveNext() = 
          let entered = enterLockIf syncRoot  this.isSynchronized
          try
            if pVersion.Value <> this.version then
              raise (InvalidOperationException("IEnumerable changed during MoveNext"))
              
            if index.Value + 1 < this.Count then
              index := index.Value + 1
              currentElement := this.GetByIndex(index.Value)
              true
            else
              index := this.Count + 1
              currentElement := Unchecked.defaultof<'T>
              false
          finally
            exitLockIf syncRoot entered
        member p.Reset() = 
          if pVersion.Value <> this.version then
              raise (InvalidOperationException("IEnumerable changed during Reset"))
          index := 0
          currentElement := Unchecked.defaultof<'T>

        member p.Dispose() = 
          index := 0
          currentElement := Unchecked.defaultof<'T>
      }

    
  member this.Add(element:'T) =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if obj.Equals(element, null) then raise (ArgumentNullException("element"))
      //use lock = makeLock this.SyncRoot
      // TODO Last/Fisrt optimization here
      let index = this.IndexOfElement(element)
      if index >= 0 then raise (ArgumentException("key already exists"))
      try
        let target = ~~~index

        this.InsertAtIndex(~~~index, element)
      with | _ -> failwith ""
    finally
      exitLockIf syncRoot entered
    
  member this.AddLast(element) = 
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      // check that key > last key
      if this.size = 0 || this.Comparer.Compare(element, this.GetByIndex(this.Count - 1)) = 1 then
          this.AddToRight(element)
      else raise (ArgumentException("Element is not bigger/later than the latest existing element"))
    finally
      exitLockIf syncRoot entered

  member this.AddFirst(element) = 
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if this.size = 0 || this.Comparer.Compare(element, this.GetByIndex(0)) = -1 then
          this.AddToLeft(element)
      else raise (ArgumentException("Element is not smaller/earlier than the latest existing element"))
    finally
      exitLockIf syncRoot entered

  member this.IndexOfElement(element:'T) =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if obj.Equals(element, null) then raise (ArgumentNullException("element"))
      let mutable index = 0
      if this.firstOffset + this.size > cpct then // is split
          let c = this.Comparer.Compare(element, this.buffer.[0]) // changed from this.GetByIndex(0) // this.buffer.[0]
          match c with
          | 0 -> index <- this.Capacity - this.firstOffset
          | -1 -> // key in the right part of the buffer
              index <- 
                Array.BinarySearch(this.buffer, this.firstOffset, cpct - this.firstOffset, element, this.comparer) 
          | 1 -> // key in the first part of the buffer
              index <- 
                Array.BinarySearch(this.buffer, 0, this.firstOffset - (cpct - this.size), element, this.comparer)
          | _ -> failwith("nonsense")
      else
          index <- Array.BinarySearch(this.buffer, this.firstOffset, this.size, element, this.comparer) 
      index
    finally
      exitLockIf syncRoot entered

  member this.Item
      with get element =
        let entered = enterLockIf syncRoot  this.isSynchronized
        try
          let index = this.IndexOfElement(element)
          if index >= 0 then
              this.GetByIndex(index)
          else
              raise (KeyNotFoundException())
        finally
          exitLockIf syncRoot entered
      and set oldElement newElement =
        if obj.Equals(oldElement, null) then raise (ArgumentNullException("oldElement"))
        let entered = enterLockIf syncRoot  this.isSynchronized
        try
          let index = this.IndexOfElement(oldElement)
          if index >= 0 then // contains key            
              this.SetByIndex(index, newElement)
          else
              this.InsertAtIndex(~~~index, newElement)
        finally
          exitLockIf syncRoot entered

  member this.Remove(element, [<Out>]result: byref<'T>) : bool =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let index = this.IndexOfElement(element)
      if index >= 0 then this.RemoveAtIndex(index) |> ignore
      index >= 0
    finally
      exitLockIf syncRoot entered

  member this.RemoveLast() = 
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if this.size = 0 then raise (InvalidOperationException("Deque is empty"))
      this.RemoveFromRight()
    finally
      exitLockIf syncRoot entered

  member this.RemoveFirst() =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      if this.size = 0 then raise (InvalidOperationException("Deque is empty"))
      this.RemoveFromLeft()
    finally
      exitLockIf syncRoot entered

  // TODO test each direction and each if-then condition!
  member this.TryFind(element:'T,lookup:Lookup, [<Out>]result: byref<'T>) : int =
    let res = ref Unchecked.defaultof<'T>
    let idx = ref -1
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let index = this.IndexOfElement(element)
      match lookup with
      | Lookup.EQ -> 
          if index >= 0 then
              res := this.GetByIndex(index)
              idx := index
          else
              idx := -1
      | Lookup.LT -> 
          if index > 0 then
              res := this.GetByIndex(index - 1)
              idx := index - 1
          elif index = 0 then
              idx := -1
          else
              let index2 = ~~~index
              if index2 = this.Count then // there are no elements larger than key
                  res := this.GetByIndex(this.Count - 1)
                  idx := this.Count - 1
              elif index2 = 0 then
                  idx := -1
              else //  it is the index of the first element that is larger than value
                  res := this.GetByIndex(index2 - 1)
                  idx := index2 - 1
      | Lookup.LE -> 
          if index >= 0 then
              res := this.GetByIndex(index) // equal
              idx := index
          else
              let index2 = ~~~index
              if index2 = this.Count then // there are no elements larger than key
                  res := this.GetByIndex(this.Count - 1)
                  idx := this.Count - 1
              elif index2 = 0 then
                  idx := -1
              else //  it is the index of the first element that is larger than value
                  res := this.GetByIndex(index2 - 1)
                  idx := index2 - 1
      | Lookup.GT -> 
          if index >= 0 && index < this.Count - 1 then
              res := this.GetByIndex(index + 1)
              idx := index + 1
          elif index >= this.Count - 1 then
              idx := -1
          else
              let index2 = ~~~index
              if index2 = this.Count then // there are no elements larger than key
                  idx := -1
              else //  it is the index of the first element that is larger than value
                  res := this.GetByIndex(index2)
                  idx := index2
      | Lookup.GE ->
          if index >= 0 && index < this.Count then
              res := this.GetByIndex(index) // equal
              idx := index
          else
              let index2 = ~~~index
              if index2 = this.Count then // there are no elements larger than key
                  idx := -1
              else //  it is the index of the first element that is larger than value
                  res := this.GetByIndex(index2)
                  idx := index2
      | _ -> raise (ApplicationException("Wrong lookup direction"))
    finally
      exitLockIf syncRoot entered
    result <- !res
    !idx

  member this.TryGet(element, [<Out>]value: byref<'T>) : bool =
    let entered = enterLockIf syncRoot  this.isSynchronized
    try
      let index = this.IndexOfElement(element)
      if index >= 0 then
          value <- this.GetByIndex(index)
          true
      else
          false
    finally
      exitLockIf syncRoot entered


  //#endregion

  //#region Interfaces 
  interface IEnumerable with
      member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator
  //#endregion
    
  //#region Constructors

  new() = SortedDeque([||], 8, Comparer<'T>.Default)
  new(comparer:IComparer<'T>) = SortedDeque([||], 8, comparer)
  new(capacity:int) = SortedDeque([||], capacity, Comparer<'T>.Default)
  new(capacity:int,comparer:IComparer<'T>) = SortedDeque([||], capacity, comparer)

  //#endregion
