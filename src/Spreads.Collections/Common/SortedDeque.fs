// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Diagnostics

open Spreads

// TODO rework this

//type KVComparer<'K,'V>(keyComparer:IComparer<'K>, valueComparer:IComparer<'V>) = 
//  interface IComparer<KVP<'K,'V>> with
//    member this.Compare(x: KVP<'K, 'V>, y: KVP<'K, 'V>): int = 
//      let c1 = keyComparer.Compare(x.Key, y.Key)
//      if c1 = 0 then valueComparer.Compare(x.Value, y.Value)
//      else c1
//  end

//and KVKeyComparer<'K,'V>(keyComparer:IComparer<'K>) = 
//  interface IComparer<KVP<'K,'V>> with
//    member this.Compare(x: KVP<'K, 'V>, y: KVP<'K, 'V>): int = keyComparer.Compare(x.Key, y.Key)
//  end

//and KVPKeyComparer<'K,'V>(keyComparer:IComparer<'K>) = 
//  interface IComparer<KVP<'K,'V>> with
//    member this.Compare(x: KVP<'K, 'V>, y: KVP<'K, 'V>): int = keyComparer.Compare(x.Key, y.Key)
//  end

//and internal ZipNComparer<'K>(keyComparer:KeyComparer<'K>) = 
//  interface IComparer<KVP<'K,int>> with
//    member this.Compare(x: KVP<'K,int>, y: KVP<'K,int>): int = 
//      let c1 = keyComparer.Compare(x.Key, y.Key)
//      if c1 = 0 then
//        x.Value - y.Value
//      else c1
//  end


type SortedDeque<'T> (capacity:int, comparer:KeyComparer<'T>) as this=
  [<DefaultValue>] val mutable internal comparer : KeyComparer<'T>
  [<DefaultValue>] val mutable internal firstOffset : int
  [<DefaultValue>] val mutable internal count : int
  [<DefaultValue>] val mutable internal buffer : 'T[]
  
  do
    this.comparer <- if comparer = null then KeyComparer<'T>.Default else comparer
    this.buffer <- Array.zeroCreate capacity

  /// Sets the total number of elements the internal array can hold without resizing.
  let doubleCapacity() = 
    let copyBuffer size = 
      let newArray = Array.zeroCreate size
      if 0 <> this.firstOffset && this.firstOffset + this.count >= this.buffer.Length then 
          let lengthFromStart = this.buffer.Length - this.firstOffset
          let lengthFromEnd = this.count - lengthFromStart
          Array.Copy(this.buffer, this.firstOffset, newArray, 0, lengthFromStart)
          Array.Copy(this.buffer, 0, newArray, lengthFromStart, lengthFromEnd)
      else Array.Copy(this.buffer, this.firstOffset, newArray, 0, this.count)
      newArray
    let newCapacity = this.buffer.Length * 2
    if newCapacity < this.count then 
        raise <| new InvalidOperationException("Capacity cannot be set to a value less than Count")
    // Set up to use the new buffer.
    this.buffer <- copyBuffer newCapacity
    this.firstOffset <- 0

  new() = new SortedDeque<'T>(2, KeyComparer<'T>.Default)
  new(comparer:IComparer<'T>) = new SortedDeque<'T>(2, KeyComparer<'T>.Create(comparer))
  new(comparer:KeyComparer<'T>) = new SortedDeque<'T>(2, comparer)
  new(capacity) = new SortedDeque<'T>(capacity, KeyComparer<'T>.Default)

  member inline internal this.IndexToOffset(index) = 
    (index + this.firstOffset) % (this.buffer.Length)
  member inline internal this.OffsetToIndex(offset) = 
    // TODO unit test, looks obvious - bacause of that
    if offset >= 0 then
      (this.buffer.Length + offset- this.firstOffset) % (this.buffer.Length)
    else
      ~~~((this.buffer.Length + (~~~offset)- this.firstOffset) % (this.buffer.Length))

  member private this.OffsetOfElement(element:'T) =
    let index = 
      if this.firstOffset + this.count > this.buffer.Length then // is split
        let c = this.comparer.Compare(element, this.buffer.[0])
        match c with
        | 0 -> 0
        | x when x < 0 -> // key in the right part of the buffer
            Array.BinarySearch(this.buffer, this.firstOffset, this.buffer.Length - this.firstOffset, element, this.comparer) 
        | x when x > 0 -> // key in the left part of the buffer
            Array.BinarySearch(this.buffer, 0, this.firstOffset - (this.buffer.Length - this.count), element, this.comparer)
        | _ -> failwith("nonsense")
      else
        Array.BinarySearch(this.buffer, this.firstOffset, this.count, element, this.comparer) 
    index

  member this.IndexOfElement(element:'T) =
    let offset = this.OffsetOfElement(element)
    this.OffsetToIndex(offset)

  /// Offset is the place where a new element must be if we always shift 
  /// existing elements to the right. Here, we could shift existing elements
  /// to the left if doing so is faster, so the new element could end up
  /// at offset-1 place.
  member private this.InsertAtOffset(offset, element) : unit =
    let mutable offset = offset % (this.buffer.Length)
    
    if this.count = 0 || (offset = this.firstOffset + this.count) || offset = this.firstOffset + this.count - this.buffer.Length then // add to the right end
      let destination = offset % (this.buffer.Length) // ofset could have been equal to length
      this.buffer.[destination] <- element
      this.count <- this.count + 1
    elif offset = this.firstOffset then // add to the left end
      this.firstOffset <- (offset + this.buffer.Length - 1) % (this.buffer.Length)
      this.buffer.[this.firstOffset] <- element
      this.count <- this.count + 1
    else
      // unchecked, assume that offset is inside existing range
      if this.firstOffset + this.count > this.buffer.Length then // is already a split
        if offset < this.firstOffset then // we are at the left part of the split [__._>    ___]
          #if PRERELEASE
          Trace.Assert(offset < this.firstOffset + this.count - this.buffer.Length)
          #endif
          Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.firstOffset + this.count - this.buffer.Length - offset)
        else // we are at the left part of the split [___    <__._]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset - 1, (offset - this.firstOffset) + 1)
          this.firstOffset <- this.firstOffset - 1
          offset <- offset - 1
          #if PRERELEASE
          Trace.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
          #endif
      else
        if this.firstOffset = 0 then // avoid split if possible [>_____     ]
          #if PRERELEASE
          Trace.Assert(offset < this.count)
          #endif
          Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - offset)
        elif (this.count - (offset - this.firstOffset) <= this.count / 2)  then // [   _______.>__     ]
          if this.firstOffset + this.count = this.buffer.Length then
            this.buffer.[0] <- this.buffer.[this.buffer.Length - 1] // NB! do not lose the last value
            Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - (offset - this.firstOffset) - 1)
          else
            Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - (offset - this.firstOffset))
          #if PRERELEASE
          Trace.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
          #endif
        else //[   __<._______     ]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset - 1, offset - this.firstOffset)
          offset <- offset - 1
          this.firstOffset <- this.firstOffset - 1
          #if PRERELEASE
          Trace.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
          #endif
      this.buffer.[offset] <- element
      this.count <- this.count + 1

  member private this.RemoveAtOffset(offset) : 'T =
    let mutable offset = offset % (this.buffer.Length)
    let element = this.buffer.[offset]
    if this.count = 0 then
      invalidOp "SortedDeque is empty"
    elif (offset = this.firstOffset + this.count - 1) || offset = this.firstOffset + this.count - this.buffer.Length - 1 then // add to the right end
      // at the end: this.count <- this.count - 1
      ()
    elif offset = this.firstOffset then
      this.firstOffset <- (this.firstOffset + 1) % (this.buffer.Length)
      // at the end: this.count <- this.count - 1
    else
      // unchecked, assume that offset is inside existing range
      if this.firstOffset + this.count > this.buffer.Length then // is already a split
        if offset < this.firstOffset then // we are at the left part of the split [__._<    ___]
          Array.Copy(this.buffer, offset + 1, this.buffer, offset, this.firstOffset + this.count - this.buffer.Length - offset - 1)
        else // we are at the right part of the split [___    >__._]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset + 1, (offset - this.firstOffset))
          this.firstOffset <- this.firstOffset + 1
      else
        if (this.count - (offset - this.firstOffset) <= this.count / 2)  then // [   _______.<__     ]
          Array.Copy(this.buffer, offset + 1, this.buffer, offset, this.count - (offset - this.firstOffset) - 1)
        else //[   __>._______     ]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset + 1, offset - this.firstOffset ) //- 1
          this.firstOffset <- this.firstOffset + 1
    this.count <- this.count - 1
    element

  member this.Set(element:'T) =
    // NB save some cycles instead of this line
    //if this.TryAdd(element) < 0 then invalidOp "Item already exists"
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      let offset = this.IndexToOffset(0)
      this.InsertAtOffset(offset, element)
    else
      let offset = this.OffsetOfElement(element)
      if offset >= 0 then this.buffer.[offset] <- element
      else this.InsertAtOffset(~~~offset, element)

  member this.Add(element:'T) =
    // NB save some cycles instead of this line
    //if this.TryAdd(element) < 0 then invalidOp "Item already exists"
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      let offset = this.IndexToOffset(0)
      this.InsertAtOffset(offset, element)
    else
      let offset = this.OffsetOfElement(element)
      if offset >= 0 then invalidOp "Item already exists"
      else this.InsertAtOffset(~~~offset, element)

  member this.TryAdd(element:'T) : int =
    let mutable index = 0
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
      index <- this.count
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      let offset = this.IndexToOffset(0)
      this.InsertAtOffset(offset, element)
    else
      let offset = this.OffsetOfElement(element)
      if offset >= 0 then index <- ~~~offset
      else 
        this.InsertAtOffset(~~~offset, element)
        index <- this.OffsetToIndex(offset) 
    index

  /// Returns the index of added element
//  member this.AddWithIndex(element:'T) = 
//    let mutable index = 0
//    // ensure capacity
//    if this.count = this.buffer.Length then doubleCapacity()
//    if this.count = 0 then
//      this.InsertAtOffset(this.IndexToOffset(this.count), element)
//      // index = 0
//    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
//      // adding to the end
//      this.InsertAtOffset(this.IndexToOffset(this.count), element) // NB!&FML! this.count was index before I found that order of lines was wrong, then order of lines became as now, but index remained instead of count - and we were fucked! (TODO (low) remove this comment to a collection of stupid behavior bugs, which rapidly increases in size!)
//      index <- this.count 
//    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
//      // adding to the front
//      this.InsertAtOffset(this.IndexToOffset(0), element)
//      // index = 0
//    else
//      let offset = this.OffsetOfElement(element)
//      if offset >= 0 then invalidOp "Item already exists"
//      else this.InsertAtOffset(~~~offset, element)
//      index <- (this.buffer.Length + offset- this.firstOffset) % (this.buffer.Length) // TODO unit test, looks obvious, but just in case
//    index

  member this.First 
    with get() = 
      if this.count = 0 then invalidOp "SortedDeque is empty"
      this.buffer.[this.firstOffset]
  member this.Last 
    with get() =
      if this.count = 0 then invalidOp "SortedDeque is empty"
      let offset = this.IndexToOffset (this.count - 1)
      this.buffer.[offset]
    
  member this.Count with get() = this.count

  member this.Clear() : unit = 
    Array.Clear(this.buffer, 0, this.buffer.Length)
    this.firstOffset <- 0
    this.count <- 0

  member this.RemoveFirst() : 'T = 
    if this.count = 0 then invalidOp "SortedDeque is empty"
    let first = this.buffer.[this.firstOffset]
    this.buffer.[this.firstOffset] <- Unchecked.defaultof<_>
    this.firstOffset <- (this.firstOffset + 1) % (this.buffer.Length)
    this.count <- this.count - 1
    first

  member this.RemoveLast(): 'T = 
    if this.count = 0 then invalidOp "SortedDeque is empty"
    let offset = this.IndexToOffset(this.count - 1)
    let last = this.buffer.[offset]
    this.buffer.[offset] <- Unchecked.defaultof<_>
    this.count <- this.count - 1
    last

  // NB Remove methods return 'T because comparer could take only a part of element
  // e.g. for KV we could remove elements only by key part and returned KV will have a value

  member this.Remove(element:'T): 'T = 
    let offset = this.OffsetOfElement(element)
    if offset < 0 then
      invalidOp "Element doesn't exist in the SortedDeque"
    this.RemoveAtOffset(offset)

  member this.RemoveAt(index) : 'T = 
    if index < 0 || index >= this.count then
      raise (ArgumentOutOfRangeException("index"))
    else
      this.RemoveAtOffset(this.IndexToOffset(index))

  member this.Item with get(idx) = this.buffer.[this.IndexToOffset(idx)]

//  member internal this.AsEnumerable() : IEnumerable<'T>  =
//    { new IEnumerable<'T> with
//        member e.GetEnumerator() = new SortedDequeEnumerator<'T>(-1, this) :> IEnumerator<'T>
////          let idx = ref -1
////          { new IEnumerator<'T> with
////              member __.MoveNext() = 
////                if idx.Value < this.count - 1 then
////                  idx := idx.Value + 1
////                  true
////                else false
////              member __.Current with get() : 'T = this.buffer.[this.IndexToOffset(idx.Value)]
////              member __.Current with get() : obj = box __.Current
////              member __.Dispose() = ()
////              member __.Reset() = idx := -1
////          }
//        member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator
//    }

  member this.GetEnumerator() = new SortedDequeEnumerator<'T>(-1, this)

  member internal this.Reverse() =
    { new IEnumerable<'T> with
        member e.GetEnumerator() =
          let idx = ref (this.count)
          { new IEnumerator<'T> with
              member __.MoveNext() = 
                if idx.Value > 0 then
                  idx := idx.Value - 1
                  true
                else false
              member __.Current with get() : 'T = this.buffer.[this.IndexToOffset(idx.Value)]
              member __.Current with get() : obj = box __.Current
              member __.Dispose() = ()
              member __.Reset() = idx := this.count
          }
        member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator
    }
  
  interface IEnumerable with
    member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator
  interface IEnumerable<'T> with
    member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator<'T>

and SortedDequeEnumerator<'T> =
  struct
    val mutable idx : int
    val source :  SortedDeque<'T>
    internal new(index,source) = {idx = index; source = source}
  end
  member this.MoveNext() = 
    if this.idx < this.source.count - 1 then
      this.idx <- this.idx + 1
      true
    else false
  member this.Current with get() : 'T = this.source.buffer.[this.source.IndexToOffset(this.idx)]
  member this.Dispose() = ()
  member this.Reset() = this.idx <- -1
  interface IEnumerator<'T> with
    member this.MoveNext() = this.MoveNext()
//      if this.idx < this.source.count - 1 then
//        this.idx <- this.idx + 1
//        true
//      else false
    member this.Current with get() : 'T = this.Current //this.source.buffer.[this.source.IndexToOffset(this.idx)]
    member this.Current with get() : obj = box this.Current
    member this.Dispose() = ()
    member this.Reset() = this.Reset()

 


type internal SortedDequeKVP<'K,'V> (capacity:int, comparer:KVPComparer<'K,'V>) as this=
  [<DefaultValue>] val mutable internal comparer : KVPComparer<'K,'V>
  [<DefaultValue>] val mutable internal firstOffset : int
  [<DefaultValue>] val mutable internal count : int
  [<DefaultValue>] val mutable internal buffer : KVP<'K,'V>[]
  
  do
    this.comparer <- if comparer = null then new KVPComparer<_,_>(null, null) else comparer
    this.buffer <- Array.zeroCreate capacity

  /// Sets the total number of elements the internal array can hold without resizing.
  let doubleCapacity() = 
    let copyBuffer size = 
      let newArray = Array.zeroCreate size
      if 0 <> this.firstOffset && this.firstOffset + this.count >= this.buffer.Length then 
          let lengthFromStart = this.buffer.Length - this.firstOffset
          let lengthFromEnd = this.count - lengthFromStart
          Array.Copy(this.buffer, this.firstOffset, newArray, 0, lengthFromStart)
          Array.Copy(this.buffer, 0, newArray, lengthFromStart, lengthFromEnd)
      else Array.Copy(this.buffer, this.firstOffset, newArray, 0, this.count)
      newArray
    let newCapacity = this.buffer.Length * 2
    if newCapacity < this.count then 
        raise <| new InvalidOperationException("Capacity cannot be set to a value less than Count")
    // Set up to use the new buffer.
    this.buffer <- copyBuffer newCapacity
    this.firstOffset <- 0

  new() = new SortedDequeKVP<'K,'V>(2, null)
  new(comparer:KVPComparer<'K,'V>) = new SortedDequeKVP<'K,'V>(2, comparer)
  new(comparer:KeyComparer<'K>) = new SortedDequeKVP<'K,'V>(2, new KVPComparer<_,_>(comparer, null))
  new(capacity) = new SortedDequeKVP<'K,'V>(capacity, null)

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member inline internal this.IndexToOffset(index) = 
    (index + this.firstOffset) % (this.buffer.Length)

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member inline internal this.OffsetToIndex(offset) = 
    // TODO unit test, looks obvious - bacause of that
    if offset >= 0 then
      (this.buffer.Length + offset- this.firstOffset) % (this.buffer.Length)
    else
      ~~~((this.buffer.Length + (~~~offset)- this.firstOffset) % (this.buffer.Length))

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member private this.OffsetOfElement(element:KVP<'K,'V>) =
    let index = 
      if this.firstOffset + this.count > this.buffer.Length then // is split
        let c = this.comparer.Compare(element, this.buffer.[0])
        match c with
        | 0 -> 0
        | x when x < 0 -> // key in the right part of the buffer
            Array.BinarySearch(this.buffer, this.firstOffset, this.buffer.Length - this.firstOffset, element, this.comparer) 
        | x when x > 0 -> // key in the left part of the buffer
            Array.BinarySearch(this.buffer, 0, this.firstOffset - (this.buffer.Length - this.count), element, this.comparer)
        | _ -> failwith("nonsense")
      else
        Array.BinarySearch(this.buffer, this.firstOffset, this.count, element, this.comparer) 
    index

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.IndexOfElement(element:KVP<'K,'V>) =
    let offset = this.OffsetOfElement(element)
    this.OffsetToIndex(offset)

  /// Offset is the place where a new element must be if we always shift 
  /// existing elements to the right. Here, we could shift existing elements
  /// to the left if doing so is faster, so the new element could end up
  /// at offset-1 place.
  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member private this.InsertAtOffset(offset, element) : unit =
    let mutable offset = offset % (this.buffer.Length)
    
    if this.count = 0 || (offset = this.firstOffset + this.count) || offset = this.firstOffset + this.count - this.buffer.Length then // add to the right end
      let destination = offset % (this.buffer.Length) // ofset could have been equal to length
      this.buffer.[destination] <- element
      this.count <- this.count + 1
    elif offset = this.firstOffset then // add to the left end
      this.firstOffset <- (offset + this.buffer.Length - 1) % (this.buffer.Length)
      this.buffer.[this.firstOffset] <- element
      this.count <- this.count + 1
    else
      // unchecked, assume that offset is inside existing range
      if this.firstOffset + this.count > this.buffer.Length then // is already a split
        if offset < this.firstOffset then // we are at the left part of the split [__._>    ___]
          #if PRERELEASE
          Trace.Assert(offset < this.firstOffset + this.count - this.buffer.Length)
          #endif
          Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.firstOffset + this.count - this.buffer.Length - offset)
        else // we are at the left part of the split [___    <__._]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset - 1, (offset - this.firstOffset) + 1)
          this.firstOffset <- this.firstOffset - 1
          offset <- offset - 1
          #if PRERELEASE
          Trace.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
          #endif
      else
        if this.firstOffset = 0 then // avoid split if possible [>_____     ]
          #if PRERELEASE
          Trace.Assert(offset < this.count)
          #endif
          Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - offset)
        elif (this.count - (offset - this.firstOffset) <= this.count / 2)  then // [   _______.>__     ]
          if this.firstOffset + this.count = this.buffer.Length then
            this.buffer.[0] <- this.buffer.[this.buffer.Length - 1] // NB! do not lose the last value
            Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - (offset - this.firstOffset) - 1)
          else
            Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - (offset - this.firstOffset))
          #if PRERELEASE
          Trace.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
          #endif
        else //[   __<._______     ]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset - 1, offset - this.firstOffset)
          offset <- offset - 1
          this.firstOffset <- this.firstOffset - 1
          #if PRERELEASE
          Trace.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
          #endif
      this.buffer.[offset] <- element
      this.count <- this.count + 1

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member private this.RemoveAtOffset(offset) : KVP<'K,'V> =
    let mutable offset = offset % (this.buffer.Length)
    let element = this.buffer.[offset]
    if this.count = 0 then
      invalidOp "SortedDeque is empty"
    elif (offset = this.firstOffset + this.count - 1) || offset = this.firstOffset + this.count - this.buffer.Length - 1 then // add to the right end
      // at the end: this.count <- this.count - 1
      ()
    elif offset = this.firstOffset then
      this.firstOffset <- (this.firstOffset + 1) % (this.buffer.Length)
      // at the end: this.count <- this.count - 1
    else
      // unchecked, assume that offset is inside existing range
      if this.firstOffset + this.count > this.buffer.Length then // is already a split
        if offset < this.firstOffset then // we are at the left part of the split [__._<    ___]
          Array.Copy(this.buffer, offset + 1, this.buffer, offset, this.firstOffset + this.count - this.buffer.Length - offset - 1)
        else // we are at the right part of the split [___    >__._]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset + 1, (offset - this.firstOffset))
          this.firstOffset <- this.firstOffset + 1
      else
        if (this.count - (offset - this.firstOffset) <= this.count / 2)  then // [   _______.<__     ]
          Array.Copy(this.buffer, offset + 1, this.buffer, offset, this.count - (offset - this.firstOffset) - 1)
        else //[   __>._______     ]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset + 1, offset - this.firstOffset ) //- 1
          this.firstOffset <- this.firstOffset + 1
    this.count <- this.count - 1
    element

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Set(element:KVP<'K,'V>) =
    // NB save some cycles instead of this line
    //if this.TryAdd(element) < 0 then invalidOp "Item already exists"
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      let offset = this.IndexToOffset(0)
      this.InsertAtOffset(offset, element)
    else
      let offset = this.OffsetOfElement(element)
      if offset >= 0 then this.buffer.[offset] <- element
      else this.InsertAtOffset(~~~offset, element)

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Add(element:KVP<'K,'V>) =
    // NB save some cycles instead of this line
    //if this.TryAdd(element) < 0 then invalidOp "Item already exists"
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      let offset = this.IndexToOffset(0)
      this.InsertAtOffset(offset, element)
    else
      let offset = this.OffsetOfElement(element)
      if offset >= 0 then invalidOp "Item already exists"
      else this.InsertAtOffset(~~~offset, element)

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.TryAdd(element:KVP<'K,'V>) : int =
    let mutable index = 0
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      let offset = this.IndexToOffset(this.count)
      this.InsertAtOffset(offset, element)
      index <- this.count
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      let offset = this.IndexToOffset(0)
      this.InsertAtOffset(offset, element)
    else
      let offset = this.OffsetOfElement(element)
      if offset >= 0 then index <- ~~~offset
      else 
        this.InsertAtOffset(~~~offset, element)
        index <- this.OffsetToIndex(offset) 
    index

  member this.First 
    with get() = 
      if this.count = 0 then invalidOp "SortedDeque is empty"
      this.buffer.[this.firstOffset]

  member this.Last 
    with get() =
      if this.count = 0 then invalidOp "SortedDeque is empty"
      let offset = this.IndexToOffset (this.count - 1)
      this.buffer.[offset]
    
  member this.Count with get() = this.count

  member this.Clear() : unit = 
    Array.Clear(this.buffer, 0, this.buffer.Length)
    this.firstOffset <- 0
    this.count <- 0

  member this.RemoveFirst() : KVP<'K,'V> = 
    if this.count = 0 then invalidOp "SortedDeque is empty"
    let first = this.buffer.[this.firstOffset]
    this.buffer.[this.firstOffset] <- Unchecked.defaultof<_>
    this.firstOffset <- (this.firstOffset + 1) % (this.buffer.Length)
    this.count <- this.count - 1
    first

  member this.RemoveLast(): KVP<'K,'V> = 
    if this.count = 0 then invalidOp "SortedDeque is empty"
    let offset = this.IndexToOffset(this.count - 1)
    let last = this.buffer.[offset]
    this.buffer.[offset] <- Unchecked.defaultof<_>
    this.count <- this.count - 1
    last

  // NB Remove methods return 'T because comparer could take only a part of element
  // e.g. for KV we could remove elements only by key part and returned KV will have a value

  member this.Remove(element:KVP<'K,'V>): KVP<'K,'V> = 
    let offset = this.OffsetOfElement(element)
    if offset < 0 then
      invalidOp "Element doesn't exist in the SortedDeque"
    this.RemoveAtOffset(offset)

  member this.RemoveAt(index) : KVP<'K,'V> = 
    if index < 0 || index >= this.count then
      raise (ArgumentOutOfRangeException("index"))
    else
      this.RemoveAtOffset(this.IndexToOffset(index))

  member this.Item with get(idx) = this.buffer.[this.IndexToOffset(idx)]

  member this.GetEnumerator() = new SortedDequeKVPEnumerator<'K,'V>(-1, this)

  member internal this.Reverse() =
    { new IEnumerable<KVP<'K,'V>> with
        member e.GetEnumerator() =
          let idx = ref (this.count)
          { new IEnumerator<KVP<'K,'V>> with
              member __.MoveNext() = 
                if idx.Value > 0 then
                  idx := idx.Value - 1
                  true
                else false
              member __.Current with get() : KVP<'K,'V> = this.buffer.[this.IndexToOffset(idx.Value)]
              member __.Current with get() : obj = box __.Current
              member __.Dispose() = ()
              member __.Reset() = idx := this.count
          }
        member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator
    }
  
  interface IEnumerable with
    member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator
  interface IEnumerable<KVP<'K,'V>> with
    member this.GetEnumerator() = this.GetEnumerator() :> IEnumerator<KVP<'K,'V>>

and internal SortedDequeKVPEnumerator<'K,'V> =
  struct
    val mutable idx : int
    val source :  SortedDequeKVP<'K,'V>
    internal new(index,source) = {idx = index; source = source}
  end
  member this.MoveNext() = 
    if this.idx < this.source.count - 1 then
      this.idx <- this.idx + 1
      true
    else false
  member this.Current with get() : KVP<'K,'V> = this.source.buffer.[this.source.IndexToOffset(this.idx)]
  member this.Dispose() = ()
  member this.Reset() = this.idx <- -1
  interface IEnumerator<KVP<'K,'V>> with
    member this.MoveNext() = this.MoveNext()
//      if this.idx < this.source.count - 1 then
//        this.idx <- this.idx + 1
//        true
//      else false
    member this.Current with get() : KVP<'K,'V> = this.Current //this.source.buffer.[this.source.IndexToOffset(this.idx)]
    member this.Current with get() : obj = box this.Current
    member this.Dispose() = ()
    member this.Reset() = this.Reset()