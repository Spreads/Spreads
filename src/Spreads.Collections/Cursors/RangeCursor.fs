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

namespace Spreads

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


/// Range from start to end key. 
//[<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
type RangeCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, startKey:'K option, endKey:'K option, startLookup: Lookup option, endLookup:Lookup option) =
    
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

  member val CurrentBatch = Unchecked.defaultof<ISeries<'K,'V>> with get, set

  member this.Reset() =
    started <- false
    cursor.Reset()
  abstract Dispose: unit -> unit
  default this.Dispose() = 
    cursor.Dispose()

  interface IEnumerator<KVP<'K,'V>> with    
    member this.Reset() = this.Reset()
    member this.MoveNext(): bool =
      if started then
        if this.InputCursor.MoveNext() && endOk this.InputCursor.CurrentKey then
          true
        else false
      else (this :> ICursor<'K,'V>).MoveFirst()
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
      task { 
        if started then
          let! moved = this.InputCursor.MoveNext(cancellationToken) 
          if moved && endOk this.InputCursor.CurrentKey then
            return true
          else return false
        else
          if (this :> ICursor<'K,'V>).MoveFirst() then return true
          else
            // TODO review this
            let mutable movedFirst = false
            let mutable complete = false
            while not movedFirst && not complete do
              let! moved' = this.InputCursor.MoveNext(cancellationToken)
              complete <- not moved'
              if moved' then
                // this.MF has additional logic
                movedFirst <- (this :> ICursor<'K,'V>).MoveFirst()
            return movedFirst
      }

    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
      Trace.TraceWarning("MoveNextBatch is not implemented in RangeCursor")
      falseTask
      
    member this.Source: ISeries<'K,'V> = CursorSeries<'K,'V2>(Func<ICursor<'K,'V>>((this :> ICursor<'K,'V>).Clone)) :> ISeries<'K,'V>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V>): bool = 
      if inRange key then
        this.InputCursor.TryGetValue(key, &value)
      else false
    
    member this.Clone(): ICursor<'K,'V> =
      let clone = new RangeCursor<'K,'V>(cursorFactory,startKey, endKey, startLookup, endLookup) :>  ICursor<'K,'V> 
      if started then clone.MoveAt(this.CurrentKey, Lookup.EQ) |> ignore
      clone