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
//type AppendCursor<'K,'V>(cursorFactoryL:Func<ICursor<'K,'V>>, cursorFactoryR:Func<ICursor<'K,'V>>) =
//    
//  let cursorL = cursorFactoryL.Invoke()
//  let cursorR = cursorFactoryR.Invoke()
//  let mutable activeCursor = cursorL
//  let mutable switched = false
//  let mutable started = false
//
//  member this.IsContinuous with get() = cursorL.IsContinuous && cursorR.IsContinuous
//
//  member this.InputCursor with get() : ICursor<'K,'V> = cursorL
//
//  member this.CurrentKey with get() = activeCursor.CurrentKey
//  member this.CurrentValue with get() = activeCursor.CurrentValue
//  member this.Current with get() = activeCursor.Current
//
//  member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>> with get, set
//
//  member this.Reset() =
//    switched <- false
//    cursorL.Reset()
//    cursorR.Reset()
//    activeCursor <- cursorL
//  abstract Dispose: unit -> unit
//  default this.Dispose() = 
//    cursorL.Dispose()
//    cursorR.Dispose()
//
//  interface IEnumerator<KVP<'K,'V>> with    
//    member this.Reset() = this.Reset()
//    member this.MoveNext(): bool =
//      let moved = 
//        if switched then
//          cursorR.MoveNext()
//        else 
//          if cursorL.MoveNext() then true
//          else // do switch
//            activeCursor <- cursorR
//            cursorR.MoveNext()
//      if not started then started <- moved
//      moved
//    member this.Current with get(): KVP<'K,'V> = this.Current
//    member this.Current with get(): obj = this.Current :> obj 
//    member x.Dispose(): unit = x.Dispose()
//
//  interface ICursor<'K,'V> with
//    member this.Comparer with get() = cursorL.Comparer
//    member this.Current: KVP<'K,'V> = this.Current
//    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = this.CurrentBatch
//    member this.CurrentKey: 'K = this.CurrentKey
//    member this.CurrentValue: 'V = this.CurrentValue
//    member this.IsContinuous: bool = this.IsContinuous
//    member this.MoveAt(key: 'K, direction: Lookup): bool = 
//      if this.InputCursor.MoveAt(key, direction) && inRange this.InputCursor.CurrentKey then
//        switched <- true
//        true
//      else false
//      
//    member this.MoveFirst(): bool = 
//      if hasValues && this.InputCursor.MoveAt(first, firstLookup) then
//        switched <- true
//        true
//      else false
//    
//    member this.MoveLast(): bool = 
//      if hasValues && this.InputCursor.MoveAt(last, lastLookup) then
//        switched <- true
//        true
//      else false
//
//    member this.MovePrevious(): bool = 
//      if switched then
//        if this.InputCursor.MovePrevious() && this.InputCursor.Comparer.Compare(this.InputCursor.CurrentKey, first) >= 0 then
//          true
//        else false
//      else (this :> ICursor<'K,'V>).MoveLast()
//    
//    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    
//    member this.Source: ISeries<'K,'V> = CursorSeries<'K,'V2>(Func<ICursor<'K,'V>>((this :> ICursor<'K,'V>).Clone)) :> ISeries<'K,'V>
//    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V>): bool = 
//      if inRange key then
//        this.InputCursor.TryGetValue(key, &value)
//      else false
//    
//    member this.Clone(): ICursor<'K,'V> =
//      let clone = new AppendCursor<'K,'V>(cursorFactoryL,cursorFactoryR) :>  ICursor<'K,'V> 
//      if started then clone.MoveAt(this.CurrentKey, Lookup.EQ) |> ignore
//      clone
