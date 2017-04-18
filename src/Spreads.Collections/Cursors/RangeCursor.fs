// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Internal

open System
open System.Linq
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks

open Spreads
open Spreads.Collections
open System.Threading

// TODO remove this file when nothing depends on it


/// Range from start to end key. 
//[<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
type internal RangeCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, startKey:'K option, endKey:'K option, startLookup: Lookup option, endLookup:Lookup option) =
    
  let cursor = cursorFactory.Invoke()
  let mutable started = false
    
  // EQ just means inclusive
  let firstLookup = if startLookup.IsSome && startLookup.Value <> Lookup.EQ then startLookup.Value else Lookup.GE
  let lastLookup = 
    if endLookup.IsSome && endLookup.Value <> Lookup.EQ then 
      endLookup.Value 
    else Lookup.LE

  // if limits are not set then eny key is ok
  let startOk k = 
    if startKey.IsSome then cursor.Comparer.Compare(k, startKey.Value) >= 0
    else true
  let endOk k = 
    if endKey.IsSome then cursor.Comparer.Compare(k, endKey.Value) <= 0
    else true
  let inRange k = (startOk k) && (endOk k)

  do
    cursor.Reset()

  member this.IsContinuous with get() = cursor.IsContinuous

  member this.InputCursor with get() : ICursor<'K,'V> = cursor

  member this.CurrentKey with get() = cursor.CurrentKey
  member this.CurrentValue with get() = cursor.CurrentValue
  member this.Current with get() = cursor.Current

  member val CurrentBatch = Unchecked.defaultof<_> with get, set

  member this.MoveNext(): bool =
      if started then
        if this.InputCursor.MoveNext() && endOk this.InputCursor.CurrentKey then
          true
        else false
      else (this :> ICursor<'K,'V>).MoveFirst()

  member this.Reset() =
    started <- false
    cursor.Reset()
  abstract Dispose: unit -> unit
  default this.Dispose() = 
    cursor.Dispose()

  interface IEnumerator<KVP<'K,'V>> with    
    member this.Reset() = this.Reset()
    member this.MoveNext(): bool = this.MoveNext()
    member this.Current with get(): KVP<'K,'V> = this.Current
    member this.Current with get(): obj = this.Current :> obj 
    member x.Dispose(): unit = x.Dispose()

  interface ICursor<'K,'V> with
    member this.Comparer with get() = cursor.Comparer
    member this.CurrentBatch = this.CurrentBatch
    member this.CurrentKey: 'K = this.CurrentKey
    member this.CurrentValue: 'V = this.CurrentValue
    member this.IsContinuous: bool = this.IsContinuous
    member this.MoveAt(key: 'K, direction: Lookup): bool = 
      if this.InputCursor.MoveAt(key, direction) && inRange this.InputCursor.CurrentKey then
        started <- true
        true
      else false
      
    member this.MoveFirst(): bool = 
      if (startKey.IsSome && this.InputCursor.MoveAt(startKey.Value, firstLookup) && inRange this.InputCursor.CurrentKey)
        || (startKey.IsNone && this.InputCursor.MoveFirst()) then
        started <- true
        true
      else false
    
    member this.MoveLast(): bool = 
      if (endKey.IsSome && this.InputCursor.MoveAt(endKey.Value, lastLookup) && inRange this.InputCursor.CurrentKey)
        || (endKey.IsNone && this.InputCursor.MoveLast()) then
        started <- true
        true
      else false

    member this.MovePrevious(): bool = 
      if started then
        if this.InputCursor.MovePrevious() && startOk this.InputCursor.CurrentKey then
          true
        else false
      else (this :> ICursor<'K,'V>).MoveLast()
    
    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
      raise (NotSupportedException())
      //task { 
      //  if started then
      //    let! moved = this.InputCursor.MoveNext(cancellationToken) 
      //    if moved && endOk this.InputCursor.CurrentKey then
      //      return true
      //    else return false
      //  else
      //    if (this :> ICursor<'K,'V>).MoveFirst() then return true
      //    else
      //      // TODO review this
      //      let mutable movedFirst = false
      //      let mutable complete = false
      //      while not movedFirst && not complete do
      //        let! moved' = this.InputCursor.MoveNext(cancellationToken)
      //        complete <- not moved'
      //        if moved' then
      //          // this.MF has additional logic
      //          movedFirst <- (this :> ICursor<'K,'V>).MoveFirst()
      //      return movedFirst
      //}

    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
      Trace.TraceWarning("MoveNextBatch is not implemented in RangeCursor")
      falseTask
      
    member this.Source = CursorSeries<'K,'V>(Func<ICursor<'K,'V>>((this :> ICursor<'K,'V>).Clone)) :> IReadOnlySeries<_,_>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V>): bool = 
      if inRange key then
        this.InputCursor.TryGetValue(key, &value)
      else false
    
    member this.Clone(): ICursor<'K,'V> =
      let clone = new RangeCursor<'K,'V>(cursorFactory,startKey, endKey, startLookup, endLookup) :>  ICursor<'K,'V> 
      if started then clone.MoveAt(this.CurrentKey, Lookup.EQ) |> ignore
      clone




/// Range from start to end key. 
//[<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
[<Sealed>]
type internal RangeSeries<'K,'V,'TCursor when 'TCursor :> ICursor<'K,'V>>(origin:ISeries<'K,'V>, startKey:'K option, endKey:'K option, startInclusive: bool, endInclusive:bool) as this =
  inherit AbstractCursorSeries<'K,'V,RangeSeries<'K,'V,'TCursor>>()
  do
    if origin.IsIndexed then raise (NotSupportedException("RangeSeries is not supported for indexed series, only for sorted ones."))
  // NB mutable because 'TCursor could be a struct
  let mutable cursor = origin.GetCursor() :?> 'TCursor
    
  // range with limits could reach bounds while underlying series still has data
  let mutable atTheStart = false
  let mutable atTheEnd = false

  // EQ just means inclusive
  let firstLookup = if startInclusive then Lookup.GE else Lookup.GT
  let lastLookup = if endInclusive then Lookup.LE else Lookup.LT

  // if limits are not set then eny key is ok
  let startOk k =
    if startKey.IsSome then
      let c = cursor.Comparer.Compare(k, startKey.Value)
      if startInclusive then c >= 0 else c > 0
    else true
  let endOk k =
    if endKey.IsSome then
      let c = cursor.Comparer.Compare(k, endKey.Value)
      if endInclusive then c <= 0 else c < 0
    else true
  let inRange k = (startOk k) && (endOk k)

  override this.IsIndexed = false

  override this.IsReadOnly = origin.IsReadOnly

  override this.Comparer = origin.Comparer

  override this.Clone() =
    if this.state = CursorState.None && this.threadId = Environment.CurrentManagedThreadId then
      this.state <- CursorState.Initialized
      this
    else
      let clone = new RangeSeries<_,_,_>(origin, startKey, endKey, startInclusive, endInclusive)
      if this.state = CursorState.Moving then clone.MoveAt(this.CurrentKey, Lookup.EQ) |> ignore
      else clone.state <- CursorState.Initialized
      clone

  override this.Updated 
    with [<MethodImpl(MethodImplOptions.AggressiveInlining)>] get() : Task<bool> = 
      if not atTheEnd && (this.state <> CursorState.Moving || endOk cursor.CurrentKey) then 
        origin.Updated
      else TaskEx.FalseTask

  override this.IsContinuous with get() = cursor.IsContinuous

  override this.CurrentKey with get() = cursor.CurrentKey
  override this.CurrentValue with get() = cursor.CurrentValue
  override this.Current with get() = cursor.Current

  override val CurrentBatch = Unchecked.defaultof<_> with get

  override this.MoveNext(): bool =
    if int this.state >= int CursorState.Moving then
      if endKey.IsNone then 
        cursor.MoveNext()
      elif atTheEnd then false
      else
        let beforeMove = cursor.CurrentKey
        let moved = cursor.MoveNext()
        if endOk cursor.CurrentKey then
          moved
        else 
          if moved then
            cursor.MoveAt(beforeMove, Lookup.EQ) |> ignore
            atTheEnd <- true
          false
    else (this :> ICursor<'K,'V>).MoveFirst()

  override this.MovePrevious(): bool = 
    if int this.state >= int CursorState.Moving then
      if startKey.IsNone then
        cursor.MovePrevious()
      elif atTheStart then false
      else
        let beforeMove = cursor.CurrentKey
        let moved = cursor.MovePrevious()
        if startOk cursor.CurrentKey then
          moved
        else 
          if moved then
            cursor.MoveAt(beforeMove, Lookup.EQ) |> ignore
            atTheStart <- true
          false
    else (this :> ICursor<'K,'V>).MoveLast()

  override this.Reset() =
    this.state <- CursorState.Initialized
    cursor.Reset()

  override this.Dispose() = 
    this.state <- CursorState.None
    cursor.Dispose()

  override this.MoveAt(key: 'K, direction: Lookup): bool = 
    // must return to the position if false move
    let beforeMove = cursor.CurrentKey
    let moved = cursor.MoveAt(key, direction)
    if inRange cursor.CurrentKey then
      Debug.Assert(int this.state > 0)
      // keep navigating state unchanged
      if this.state = CursorState.Initialized then this.state <- CursorState.Moving
      atTheEnd <- false
      atTheStart <- false
      moved
    else
      if moved then cursor.MoveAt(beforeMove, Lookup.EQ) |> ignore
      false
      
  override this.MoveFirst(): bool = 
    if (startKey.IsSome && cursor.MoveAt(startKey.Value, firstLookup) && inRange cursor.CurrentKey)
      || (startKey.IsNone && cursor.MoveFirst()) then
      Debug.Assert(int this.state > 0)
      if this.state = CursorState.Initialized then this.state <- CursorState.Moving
      atTheEnd <- false
      atTheStart <- false
      true
    else
      if this.state = CursorState.Initialized then this.state <- CursorState.Moving
      // at last is not the same as atTheEnd
      false
    
  override this.MoveLast(): bool = 
    if (endKey.IsSome && cursor.MoveAt(endKey.Value, lastLookup) && inRange cursor.CurrentKey)
      || (endKey.IsNone && cursor.MoveLast()) then
      Debug.Assert(int this.state > 0)
      if this.state = CursorState.Initialized then this.state <- CursorState.Moving
      atTheEnd <- false
      atTheStart <- false
      true
    else
      // cannot move, empty range TODO test
      this.state <- CursorState.Initialized
      false

  override this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
    Trace.TraceWarning("MoveNextBatch is not implemented in RangeCursor")
    falseTask
      
  member this.TryGetValue(key: 'K, [<Out>] value: byref<'V>): bool = 
    if inRange key then
      cursor.TryGetValue(key, &value)
    else false
    
  
  interface ICursor<'K,'V> with
    member this.Clone(): ICursor<'K,'V> = this.Clone() :> ICursor<'K,'V>
    member this.Comparer = this.Comparer
    member this.Current: KeyValuePair<'K,'V> = this.Current
    member this.Current: obj = this.Current :> obj
    member this.CurrentBatch: IReadOnlySeries<'K,'V> = this.CurrentBatch
    member this.CurrentKey: 'K = this.CurrentKey
    member this.CurrentValue: 'V = this.CurrentValue
    member this.Dispose(): unit = this.Dispose()
    member this.IsContinuous: bool = this.IsContinuous
    member this.MoveAt(key: 'K, direction: Lookup): bool = this.MoveAt(key, direction)
    member this.MoveFirst(): bool = this.MoveFirst()
    member this.MoveLast(): bool = this.MoveLast()
    member this.MoveNext(cancellationToken: CancellationToken): Task<bool> = raise (NotSupportedException())
    member this.MoveNext(): bool = this.MoveNext()
    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatch(cancellationToken)
    member this.MovePrevious(): bool = this.MovePrevious()
    member this.Reset(): unit = this.Reset()
    member this.Source: IReadOnlySeries<'K,'V> = this.Source
    member this.TryGetValue(key: 'K, value: byref<'V>): bool = this.TryGetValue(key, &value)
