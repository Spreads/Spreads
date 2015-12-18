(*  
    Copyright (c) 2014-2015 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.
        
    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)


namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics

type KVComparer<'K,'V>(keyComparer:IComparer<'K>, valueComparer:IComparer<'V>) = 
  interface IComparer<KV<'K,'V>> with
    member this.Compare(x: KV<'K, 'V>, y: KV<'K, 'V>): int = 
      let c1 = keyComparer.Compare(x.Key, y.Key)
      if c1 = 0 then valueComparer.Compare(x.Value, y.Value)
      else c1
  end

and KVKeyComparer<'K,'V>(keyComparer:IComparer<'K>) = 
  interface IComparer<KV<'K,'V>> with
    member this.Compare(x: KV<'K, 'V>, y: KV<'K, 'V>): int = keyComparer.Compare(x.Key, y.Key)
  end

and
  /// A comparable KeyValuePair
  [<CustomComparison;CustomEquality>]
  KV<'K,'V> =
    struct
      val Key : 'K
      val Value : 'V
      new(key, value) = {Key = key; Value = value}
    end
    override x.Equals(yobj) =
      match yobj with
      | :? KV<'K,_> as y -> (Unchecked.equals x.Key y.Key)
      | _ -> false
    override x.GetHashCode() = Unchecked.hash x.Key
    interface System.IComparable<KV<'K,'V>> with
      member x.CompareTo y = 
        let c1 = Comparer<'K>.Default.Compare(x.Key, y.Key)
        if c1 = 0 then 
          Comparer<'V>.Default.Compare(x.Value, y.Value)
        else c1
    interface System.IComparable with
      member x.CompareTo other = 
        match other with
        | :? KV<'K,'V> as y -> 
          (x :> System.IComparable<KV<'K,'V>>).CompareTo(y)
        | _ -> invalidArg "other" "Cannot compare values of different types"


[<SerializableAttribute>]
type SortedDeque<'T>(comparer:IComparer<'T>, capacity:int) as this=
  [<DefaultValue>] val mutable internal comparer : IComparer<'T> 
  [<DefaultValue>] val mutable internal buffer : 'T[]
  [<DefaultValue>] val mutable internal count : int
  [<DefaultValue>] val mutable internal firstOffset : int
  do
    this.comparer <- if comparer = null then Comparer<'T>.Default :> IComparer<'T> else comparer
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

  new() = new SortedDeque<'T>(Spreads.KeyComparer.GetDefault<'T>(), 2)
  new(comparer:IComparer<'T>) = new SortedDeque<'T>(comparer, 2)
  new(capacity) = new SortedDeque<'T>(Spreads.KeyComparer.GetDefault<'T>(), capacity)

  member inline internal this.IndexToOffset(index) = (index + this.firstOffset) &&& (this.buffer.Length - 1)

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

  /// Offset is the place where a new element must be if we always shift 
  /// existing elements to the right. Here, we could shift existing elements
  /// to the left if doing so is faster, so the new element could end up
  /// at offset-1 place.
  member private this.InsertAtOffset(offset, element) : unit =
    let mutable offset = offset &&& (this.buffer.Length - 1)
    
    if this.count = 0 || (offset = this.firstOffset + this.count) || offset = this.firstOffset + this.count - this.buffer.Length then // add to the right end
      let destination = offset &&& (this.buffer.Length-1) // ofset could have been equal to length
      this.buffer.[destination] <- element
      this.count <- this.count + 1
    elif offset = this.firstOffset then // add to the left end
      this.firstOffset <- (offset + this.buffer.Length - 1) &&& (this.buffer.Length - 1)
      this.buffer.[this.firstOffset] <- element
      this.count <- this.count + 1
    else
      // unchecked, assume that offset is inside existing range
      if this.firstOffset + this.count > this.buffer.Length then // is already a split
        if offset < this.firstOffset then // we are at the left part of the split [__._>    ___]
          Debug.Assert(offset < this.firstOffset + this.count - this.buffer.Length)
          Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.firstOffset + this.count - this.buffer.Length - offset)
        else // we are at the left part of the split [___    <__._]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset - 1, (offset - this.firstOffset) + 1)
          this.firstOffset <- this.firstOffset - 1
          offset <- offset - 1
          Debug.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
      else
        if this.firstOffset = 0 then // avoid split if possible [>_____     ]
          Debug.Assert(offset < this.count)
          Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - offset)
        elif (this.count - (offset - this.firstOffset) <= this.count / 2)  then // [   _______.>__     ]
          if this.firstOffset + this.count = this.buffer.Length then
            this.buffer.[0] <- this.buffer.[this.buffer.Length - 1] // NB! do not lose the last value
            Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - (offset - this.firstOffset) - 1)
          else
            Array.Copy(this.buffer, offset, this.buffer, offset + 1, this.count - (offset - this.firstOffset))
          Debug.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
        else //[   __<._______     ]
          Array.Copy(this.buffer, this.firstOffset, this.buffer, this.firstOffset - 1, offset - this.firstOffset)
          offset <- offset - 1
          this.firstOffset <- this.firstOffset - 1
          Debug.Assert(this.comparer.Compare(element, this.buffer.[offset - 1]) > 0)
      this.buffer.[offset] <- element
      this.count <- this.count + 1

  member private this.RemoveAtOffset(offset) : 'T =
    let mutable offset = offset &&& (this.buffer.Length - 1)
    let element = this.buffer.[offset]
    if this.count = 0 then
      invalidOp "SortedDeque is empty"
    elif (offset = this.firstOffset + this.count - 1) || offset = this.firstOffset + this.count - this.buffer.Length - 1 then // add to the right end
      // at the end: this.count <- this.count - 1
      ()
    elif offset = this.firstOffset then
      this.firstOffset <- (this.firstOffset + 1) &&& (this.buffer.Length - 1)
      // at the end: this.count <- this.count - 1
    else
      // unchecked, assume that offset is inside existing range
      if this.firstOffset + this.count > this.buffer.Length then // is already a split
        if offset < this.firstOffset then // we are at the left part of the split [__._<    ___]
          Array.Copy(this.buffer, offset + 1, this.buffer, offset, this.firstOffset + this.count - this.buffer.Length - offset - 1)
        else // we are at the left part of the split [___    >__._]
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


  member this.Add(element:'T) = 
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      this.InsertAtOffset(this.IndexToOffset(this.count), element)
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      this.InsertAtOffset(this.IndexToOffset(this.count), element)
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      this.InsertAtOffset(this.IndexToOffset(0), element)
    else
      let offset = this.OffsetOfElement(element)
      if offset > 0 then invalidOp "Item already exists"
      else this.InsertAtOffset(~~~offset, element)

  /// Returns the index of added element
  member this.AddWithIndex(element:'T) = 
    let mutable index = 0
    // ensure capacity
    if this.count = this.buffer.Length then doubleCapacity()
    if this.count = 0 then
      this.InsertAtOffset(this.IndexToOffset(this.count), element)
      // index = 0
    elif  this.comparer.Compare(element, this.buffer.[this.IndexToOffset (this.count - 1)]) > 0 then
      // adding to the end
      this.InsertAtOffset(this.IndexToOffset(this.count), element) // NB!&FML! this.count was index before I found that order of lines was wrong, then order of lines became as now, but index remained instead of count - and we were fucked! (TODO (low) remove this comment to a collection of stupid behavior bugs, which rapidly increases in size!)
      index <- this.count 
    elif this.comparer.Compare(element, this.buffer.[this.IndexToOffset (0)]) < 0 then
      // adding to the front
      this.InsertAtOffset(this.IndexToOffset(0), element)
      // index = 0
    else
      let offset = this.OffsetOfElement(element)
      if offset > 0 then invalidOp "Item already exists"
      else this.InsertAtOffset(~~~offset, element)
      index <- (this.buffer.Length + offset- this.firstOffset) &&& (this.buffer.Length - 1) // TODO unit test, looks obvious, but just in case
    index

  member this.First with get() = this.buffer.[this.firstOffset]
  member this.Last with get() = 
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
    this.firstOffset <- (this.firstOffset + 1) &&& (this.buffer.Length - 1)
    this.count <- this.count - 1
    first

  member this.RemoveLast(): 'T = 
    if this.count = 0 then invalidOp "SortedDeque is empty"
    let last = this.buffer.[this.IndexToOffset(this.count - 1)]
    this.count <- this.count - 1
    last

  member this.Remove(element:'T): unit = 
    let offset = this.OffsetOfElement(element)
    if offset < 0 then 
      let offset' =  this.OffsetOfElement(element) // debug
      this.RemoveAtOffset(offset') |> ignore
      invalidOp "Element doesn't exist in the SortedDeque"
    this.RemoveAtOffset(offset) |> ignore

  member this.RemoveAt(idx) = this.RemoveAtOffset(this.IndexToOffset(idx))

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

[<SerializableAttribute>]
[<ObsoleteAttribute("SortedDeque is faster")>]
type SortedList1<'T>
  internal(comparer:IComparer<'T>) as this=
  [<DefaultValue>] val mutable internal comparer : IComparer<'T> 
  [<DefaultValue>] val mutable internal list : List<'T>
  do
    this.comparer <- if comparer = null then Comparer<'T>.Default :> IComparer<'T> else comparer
    this.list <- List<'T>()

  member this.Add(item:'T) = 
    if this.list.Count = 0 || this.comparer.Compare(item, this.list.[this.list.Count - 1]) > 0 then
      this.list.Add(item)
    else
      let idx = this.list.BinarySearch(item, this.comparer)
      if idx >= 0 then invalidOp "Item already exists"
      else this.list.Insert(~~~idx, item)

  member this.Remove(item:'T) = 
    if this.list.Count = 0 then
      false
    elif this.comparer.Compare(item, this.list.[0]) = 0 then
      this.list.RemoveAt(0)
      true
    else
      let idx = this.list.BinarySearch(item, this.comparer)
      if idx >= 0 then 
        this.list.RemoveAt(idx)
        true
      else false

  member this.First with get() = this.list.[0]
  member this.Last with get() = this.list.[this.list.Count - 1]
  member this.Count with get() = this.list.Count

  member this.RemoveFirst() : 'T = 
    let first = this.First
    this.list.RemoveAt(0)
    first
  
  interface IEnumerable with
    member this.GetEnumerator() = this.list.GetEnumerator() :> IEnumerator
  interface IEnumerable<'T> with
    member this.GetEnumerator() = this.list.GetEnumerator() :> IEnumerator<'T>