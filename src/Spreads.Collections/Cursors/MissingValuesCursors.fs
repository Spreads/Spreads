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

namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

open Spreads
open Spreads.Collections


// Repeat, fill and other (TODO linear interpolate, cubic splines, trend, .... limited by fantasy and actuals needs: arima forecast, kalman filter)

type internal RepeatCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>) =
 
  let cursor = cursorFactory.Invoke()
  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>

  member this.Clone() = new RepeatCursor<'K,'V>(fun _ -> cursor.Clone())

  interface ICursor<'K,'V> with
    member this.Clone(): ICursor<'K,'V> = this.Clone() :> ICursor<'K,'V>
    member this.Comparer: IComparer<'K> = cursor.Comparer
    member this.Current: KVP<'K,'V> = cursor.Current
    member this.Current: obj = cursor.Current :> obj
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = cursor.CurrentBatch
    member this.CurrentKey: 'K = cursor.CurrentKey
    member this.CurrentValue: 'V = cursor.CurrentValue
    member this.Dispose(): unit = cursor.Dispose()
    member this.IsContinuous: bool = true
    member this.MoveAt(key: 'K, direction: Lookup): bool = cursor.MoveAt(key, direction)
    member this.MoveFirst(): bool = cursor.MoveFirst()
    member this.MoveLast(): bool = cursor.MoveLast()
    member this.MoveNext(cancellationToken: CancellationToken): Task<bool> = cursor.MoveNext(cancellationToken)
    member this.MoveNext(): bool = cursor.MoveNext()
    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = cursor.MoveNextBatch(cancellationToken)
    member this.MovePrevious(): bool = cursor.MovePrevious()
    member this.Reset(): unit = cursor.Reset()
    member this.Source: ISeries<'K,'V> = cursor.Source
    member this.TryGetValue(key: 'K, value: byref<'V>): bool =
      // TODO (perf) optimize the case when key is below the previous one
      
      let ok, v = cursor.TryGetValue(key)
      if ok then
        value <- v
        true
      else
        if lookupCursor = Unchecked.defaultof<ICursor<'K,'V>> then lookupCursor <- cursor.Clone()
        // MoveLast()+MovePrevious() is much slower
//        if lookupCursor.MoveLast() then
//          if lookupCursor.Comparer.Compare(key, lookupCursor.CurrentKey) >= 0 then
//            value <- lookupCursor.CurrentValue
//            true
//          else
//            if lookupCursor.MovePrevious() && lookupCursor.Comparer.Compare(key, lookupCursor.CurrentKey) >= 0 then 
//              value <- lookupCursor.CurrentValue
//              true
//            else
//              if lookupCursor.MoveAt(key, Lookup.LE) then
//                value <- lookupCursor.CurrentValue
//                true
//              else false
//        else false

        if lookupCursor.MoveAt(key, Lookup.LE) then
          value <- lookupCursor.CurrentValue
          true
        else false


  // Repeat().Map() is equivalent to Map().Repeat()
  interface ICanMapSeriesValues<'K,'V> with
    member this.Map<'V2>(f:Func<'V,'V2>): Series<'K,'V2> =
      CursorSeries(fun _ -> new RepeatCursor<'K,'V2>(fun _ -> new BatchMapValuesCursor<'K,'V,'V2>(cursorFactory, f) :> ICursor<'K,'V2>) :> ICursor<_,_>) :> Series<'K,'V2>




type internal FillCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, fillValue:'V) =
 
  let cursor = cursorFactory.Invoke()

  member this.Clone() = new FillCursor<'K,'V>((fun _ -> cursor.Clone()), fillValue)

  interface ICursor<'K,'V> with
    member this.Clone(): ICursor<'K,'V> = this.Clone() :> ICursor<'K,'V>
    member this.Comparer: IComparer<'K> = cursor.Comparer
    member this.Current: KVP<'K,'V> = cursor.Current
    member this.Current: obj = cursor.Current :> obj
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = cursor.CurrentBatch
    member this.CurrentKey: 'K = cursor.CurrentKey
    member this.CurrentValue: 'V = cursor.CurrentValue
    member this.Dispose(): unit = cursor.Dispose()
    member this.IsContinuous: bool = true
    member this.MoveAt(key: 'K, direction: Lookup): bool = cursor.MoveAt(key, direction)
    member this.MoveFirst(): bool = cursor.MoveFirst()
    member this.MoveLast(): bool = cursor.MoveLast()
    member this.MoveNext(cancellationToken: CancellationToken): Task<bool> = cursor.MoveNext(cancellationToken)
    member this.MoveNext(): bool = cursor.MoveNext()
    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = cursor.MoveNextBatch(cancellationToken)
    member this.MovePrevious(): bool = cursor.MovePrevious()
    member this.Reset(): unit = cursor.Reset()
    member this.Source: ISeries<'K,'V> = cursor.Source
    member this.TryGetValue(key: 'K, value: byref<'V>): bool =
      let ok, v = cursor.TryGetValue(key)
      if ok then 
        value <- v
      else
        value <- fillValue
      true

  // Fill().Map() is equivalent to Map().Fill() // See issue #10 before turning this on
//  interface ICanMapSeriesValues<'K,'V> with
//    member this.Map<'V2>(f:Func<'V,'V2>): Series<'K,'V2> =
//      new FillCursor<'K,'V2>((fun _ -> new BatchMapValuesCursor<'K,'V,'V2>(cursorFactory, f) :> ICursor<'K,'V2>), f.Invoke(fillValue)) :> Series<'K,'V2>





//
///// Repeat previous value for all missing keys
//[<SealedAttribute>]
//type internal RepeatCursorOld<'K,'V,'V2>(cursorFactory:Func<ICursor<'K,'V>>, mapper:Func<'V,'V2>) as this =
//  inherit BindCursor<'K,'V,LaggedState<'K,'V>,'V2>(cursorFactory, fun st -> mapper.Invoke(st.Current.Value))
//  do
//    this.IsContinuous <- true
//  // reused when current state is not enough to find a value
//  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>
//
//  // perf counters
//  static let mutable stateHit = 0
//  static let mutable stateMiss  = 0
//  static let mutable stateCreation = 0
//
//  static member StateHit = stateHit
//  static member StateMiss = stateMiss
//  static member StateCreation = stateCreation
//
//  override this.TryCreateState(key, [<Out>] value: byref<LaggedState<'K,'V>>) : bool =
//#if PRERELEASE
//    Interlocked.Increment(&stateCreation) |> ignore
//#endif
//    let current = this.InputCursor.Current
//    if lookupCursor = Unchecked.defaultof<ICursor<'K,'V>> then lookupCursor <- this.InputCursor.Clone()
//    if lookupCursor.MoveAt(key, Lookup.LE) then
//      let previous = lookupCursor.Current
//      value <- LaggedState(current, previous)
//      true
//    else false
//    
//  override this.TryUpdateStateNext(nextKvp, [<Out>] value: byref<LaggedState<'K,'V>>) : bool =
//    value.Previous <- value.Current
//    value.Current <- nextKvp
//    true
//
//  override this.TryUpdateStatePrevious(previousKvp, [<Out>] value: byref<LaggedState<'K,'V>>) : bool =
//    value.Current <- value.Previous
//    value.Previous <- previousKvp
//    true
//
//  //override this.EvaluateState(state) = state.Current.Value
//
//  override this.Map<'V3>(f2:Func<'V2,'V3>): Series<'K,'V3> =
//    CursorSeries(fun _ -> new RepeatCursorOld<'K,'V,'V3>(cursorFactory, CoreUtils.CombineMaps(mapper, f2)) :> ICursor<'K,'V3>) :> Series<'K,'V3>
//
//  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>>) : bool =
//    let repeatedBatch = CursorSeries(fun _ -> new RepeatCursorOld<'K,'V,'V2>(Func<ICursor<'K,'V>>(nextBatch.GetCursor), mapper) :> ICursor<'K,'V2>) :> Series<'K,'V2>
//    value <- repeatedBatch
//    true
//
//  override this.TryGetValue(key:'K, [<Out>] value: byref<'V2>): bool =
//    let current = this.State.Current
//    let previous = this.State.Previous
//    let c = this.InputCursor.Comparer.Compare(key, current.Key)
//    if c = 0 && this.HasValidState then
//#if PRERELEASE
//      Interlocked.Increment(&stateHit) |> ignore
//#endif
//      value <- mapper.Invoke(current.Value)
//      true
//    elif c < 0 && this.InputCursor.Comparer.Compare(key, previous.Key) >= 0 then
//#if PRERELEASE
//      Interlocked.Increment(&stateHit) |> ignore
//#endif
//      value <- mapper.Invoke(previous.Value)
//      true
//    else
//#if PRERELEASE
//      Interlocked.Increment(&stateMiss) |> ignore
//#endif
//      if lookupCursor = Unchecked.defaultof<ICursor<'K,'V>> then lookupCursor <- this.InputCursor.Clone()
//      if lookupCursor.MoveAt(key, Lookup.LE) then
//        value <- mapper.Invoke(lookupCursor.CurrentValue)
//        true
//      else 
//        false
//
//  override this.Clone() = 
//    let clone = new RepeatCursorOld<'K,'V,'V2>(cursorFactory,mapper) :> ICursor<'K,'V2>
//    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
//    clone
//
//  override this.Dispose() = 
//    if lookupCursor <> Unchecked.defaultof<ICursor<'K,'V>> then lookupCursor.Dispose()
//    base.Dispose()