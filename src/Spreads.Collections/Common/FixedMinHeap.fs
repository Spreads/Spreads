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

open System.Diagnostics
open System.Collections.Generic

// Adopted from https://github.com/dotnet/corefx/blob/master/src/System.Linq.Parallel/src/System/Linq/Parallel/Utils/FixedMaxHeap.cs
// Reversed direction, added cached max value


/// <summary>
/// Very simple heap data structure, of fixed size.
/// </summary>
/// <typeparam name="TElement"></typeparam>
type FixedMinHeap<'T when 'T:comparison>(maxSize:int, comparer:IComparer<'T>) =
  do
    if comparer = null then invalidArg "comparer" "comparer is null"
  let mutable count = 0
  let elements = Array.zeroCreate<'T> maxSize
  let mutable maxValue = Unchecked.defaultof<'T>
  let f = "b"

  new(maxSize:int) = new FixedMinHeap<'T>(maxSize, Spreads.KeyComparer.GetDefault<'T>())

  /// Retrieve the count (i.e. how many elements are in the heap).
  member this.Count with get() = count

  /// Retrieve the size (i.e. the maximum size of the heap).
  member this.Size with get() = elements.Length
  
  /// Get the current minimum value in the min-heap.
  ///
  /// Note: The heap stores the maximumSize largest elements that were inserted.
  /// So, if the heap is full, the value returned is the maximumSize-th largest
  /// element that was inserted into the heap.
  member this.MinValue 
    with get() =
      if count = 0 then invalidOp "FixedMinHeap has no elements"
      //The minimum element is in the 0th position.
      elements.[0]

  member this.MaxValue 
    with get() = 
      if count = 0 then invalidOp "FixedMinHeap has no elements"
      //The maximum element is in the 0th position.
      maxValue

  /// Removes all elements from the heap.
  member this.Clear() = count <- 0

  /// Inserts the new element, maintaining the heap property.
  ///
  /// Return Value:
  ///     If the element is smaller than the current min element, this function returns
  ///     false without modifying the heap. Otherwise, it returns true.
  member this.Insert(e:'T) : bool = 
    if comparer.Compare(e, maxValue) > 0 then maxValue <- e
    if count < elements.Length then // There is room. We can add it and then max-heapify.
      elements.[count] <- e
      count <- count + 1
      this.HeapifyLastLeaf()
      true
    else
      // No more room. The element might not even fit in the heap. The check
      // is simple: if it's smaller than the minimum element, then it can't be
      // inserted. Otherwise, we replace the head with it and reheapify.
      if comparer.Compare(e, elements.[0]) > 0 then
        elements.[0] <- e
        this.HeapifyRoot()
        true
      else false
     

  /// Replaces the maximum value in the heap with the user-provided value, and restores
  /// the heap property. Returns the removed min.
  member this.ReplaceMin(newValue:'T) : 'T =
    Debug.Assert(count > 0)
    if comparer.Compare(newValue, maxValue) > 0 then maxValue <- newValue
    let min = elements.[0]
    elements.[0] <- newValue
    this.HeapifyRoot()
    min

  /// Removes the maximum value from the heap, and restores the heap property. Returns the removed min.
  member this.RemoveMin() : 'T =  
    Debug.Assert(count > 0)
    let min = elements.[0]
    count <- count - 1
    if count > 0 then
      elements.[0] <- elements.[count];
      this.HeapifyRoot()
    min

  /// Private helpers to swap elements, and to reheapify starting from the root or
  /// from a leaf element, depending on what is needed.
  member private this.Swap(i:int, j:int) = 
    let tmpElement = elements.[i]
    elements.[i] <- elements.[j]
    elements.[j] <- tmpElement

  member private this.HeapifyRoot() : unit =
      // We are heapifying from the head of the list.
      let mutable i = 0
      let mutable cont = true
      let n = count
      while cont && i < n do
        // Calculate the current child node indexes.
        let n0 = ((i + 1) * 2) - 1
        let n1 = n0 + 1
        if n0 < n && comparer.Compare(elements.[i], elements.[n0]) > 0 then
            // We have to select the smaller of the two subtrees, and float
            // the current element down. This maintains the min-heap property.
            if n1 < n && comparer.Compare(elements.[n0], elements.[n1]) > 0 then
                this.Swap(i, n1)
                i <- n1
            else
                this.Swap(i, n0)
                i <- n0
        elif n1 < n && comparer.Compare(elements.[i], elements.[n1]) > 0 then
            // Float down the "right" subtree. We needn't compare this subtree
            // to the "left", because if the element was smaller than that, the
            // first if statement's predicate would have evaluated to true.
            this.Swap(i, n1)
            i <- n1;
        else
          // Else, the current key is in its final position. Break out
          // of the current loop and return.
          cont <- false

  member private this.HeapifyLastLeaf() : unit =
    let mutable cont = true
    let mutable i = count - 1
    while cont && i > 0 do
      let j = ((i + 1) / 2) - 1
      if comparer.Compare(elements.[i], elements.[j]) < 0 then 
        this.Swap(i, j)
        i <- j
      else
        cont <- false

  member this.AsEnumerable() =
    let mutable c = 0
    seq {
      while c < count do
        yield elements.[c]
        c <- c + 1
    }