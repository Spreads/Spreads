// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks

open Spreads
open Spreads.Collections







type internal ScanCursorState<'K,'V,'R> =
  struct
    [<DefaultValueAttribute(false)>]
    val mutable value : 'R
    [<DefaultValueAttribute>]
    val mutable buffered : bool
    [<DefaultValueAttribute>]
    val mutable buffer : SortedMap<'K,'R>
  end
  static member GetOrMakeBuffer(this:ScanCursorState<'K,'V,'R> byref, cursor:ICursor<'K,'V>, folder:Func<_,_,_,_>) = 
    if not this.buffered then 
      this.buffered <- true
      // TODO here SortedMap must be subscribed to the source 
      // and a CTS must be cancelled when disposing this cursor
      let sm = SortedMap()
      let source = CursorSeries(fun _ -> cursor.Clone()) 
      for kvp in source do
        this.value <- folder.Invoke(this.value, kvp.Key, kvp.Value)
        sm.AddLast(kvp.Key, this.value)
      this.buffer <- sm
    this.buffer


// Scan is not horizontal cursor, this is quick hack to have it 
type internal ScanCursorSlow<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, init:'R, folder:Func<'R,'K,'V,'R>) =
  inherit FunctionalBindCursor<'K,'V,ScanCursorState<'K,'V,'R>,'R>(
    cursorFactory, 
    ScanCursorSlow<'K,'V,'R>.stateCreator(cursorFactory, init, folder), 
    ScanCursorSlow<'K,'V,'R>.stateFoldNext(cursorFactory,folder), 
    ScanCursorSlow<'K,'V,'R>.stateFoldPrevious(cursorFactory,folder),
    Func<ScanCursorState<'K,'V,'R>,'R>(fun x -> x.value),
    false
  )

  static member private stateCreator(cursorFactory, init:'R, folder:Func<'R,'K,'V,'R>):Func<ICursor<'K,'V>,'K,bool,KVP<bool,ScanCursorState<'K,'V,'R>>> = 
    Func<ICursor<'K,'V>,'K,bool, KVP<bool,ScanCursorState<'K,'V,'R>>>(
      fun cursor key couldMove ->
        // this is naive implementation without any perf thoughts
        let movable = if couldMove then cursor else cursor.Clone()
        if movable.MoveFirst() then
          let mutable state = Unchecked.defaultof<ScanCursorState<'K,'V,'R>>
          let first = movable.CurrentKey
          if not state.buffered && cursor.Comparer.Compare(first, key) = 0 then
              state.value <- folder.Invoke(state.value, key, cursor.CurrentValue)
              KVP(true, state)
          else 
            let mutable v = Unchecked.defaultof<_>
            let ok = ScanCursorState.GetOrMakeBuffer(ref state, cursor, folder).TryGetValue(key, &v)
            state.value <- v
            KVP(ok,state)
        else KVP(false,Unchecked.defaultof<ScanCursorState<'K,'V,'R>>)
    )

  static member private stateFoldNext(cursorFactory,folder:Func<'R,'K,'V,'R>):Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>,KVP<bool,ScanCursorState<'K,'V,'R>>> = 
    Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>, KVP<bool,ScanCursorState<'K,'V,'R>>>(
      fun state current ->
        let mutable state = state
        state.value <- folder.Invoke(state.value, current.Key, current.Value)
        KVP(true,state)
    )

  static member private stateFoldPrevious(cursorFactory:Func<ICursor<'K,'V>>,folder:Func<'R,'K,'V,'R>):Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>,KVP<bool,ScanCursorState<'K,'V,'R>>> = 
    Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>, KVP<bool,ScanCursorState<'K,'V,'R>>>(
      fun state current -> 
        let mutable state = state
        if not state.buffered then
          state.buffer <- ScanCursorState.GetOrMakeBuffer(ref state, cursorFactory.Invoke(), folder)
        let mutable v = Unchecked.defaultof<_>
        let ok = state.buffer.TryGetValue(current.Key, &v)
        state.value <- v
        KVP(ok,state)
    )

[<SealedAttribute>]
type ScanCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, init:'R, folder:Func<'R,'K,'V,'R>) as this =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)

  let mutable moveAtCursor = cursorFactory.Invoke()
  let mutable buffered = false
  let mutable state : 'R = init
  let hasValues, first = if moveAtCursor.MoveFirst() then true, moveAtCursor.CurrentKey else false, Unchecked.defaultof<_>

  let mutable buffer = Unchecked.defaultof<SortedMap<'K,'R>>

  let getOrMakeBuffer() = 
    if not buffered then 
      buffered <- true
      // TODO here SortedMap must be subscribed to the source 
      // and a CTS must be cancelled when disposing this cursor
      let sm = SortedMap()
      let source = CursorSeries(cursorFactory) 
      for kvp in source do
        state <- folder.Invoke(state, kvp.Key, kvp.Value)
        sm.AddLast(kvp.Key, state)
      buffer <- sm
    buffer

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    Trace.Assert(hasValues)
    // move first
    if not buffered && isMove && this.InputCursor.Comparer.Compare(first, key) = 0 then
      Trace.Assert(Unchecked.equals state init)
      state <- folder.Invoke(state, key, this.InputCursor.CurrentValue)
      value <- state
      true
    else 
      getOrMakeBuffer().TryGetValue(key, &value)

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if not buffered then
      state <- folder.Invoke(state, next.Key, next.Value)
      value <- state
      true
    else getOrMakeBuffer().TryGetValue(next.Key, &value)
      
  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    getOrMakeBuffer().TryGetValue(previous.Key, &value)

  override this.Clone() = 
    let clone = new ScanCursor<'K,'V, 'R>(cursorFactory, init, folder) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if moveAtCursor <> Unchecked.defaultof<_> then moveAtCursor.Dispose()
    base.Dispose()


//
//[<SealedAttribute>]
//type IgnoreOrderCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>) as this =
//  let c = cursorFactory.Invoke()
//  interface ICursor<'K,'V> with
//    member x.Clone() = failwith "Not implemented yet"
//    member x.Comparer = failwith "Not implemented yet"
//    member x.Current = failwith "Not implemented yet"
//    member x.CurrentBatch = failwith "Not implemented yet"
//    member x.CurrentKey = failwith "Not implemented yet"
//    member x.CurrentValue = failwith "Not implemented yet"
//    member x.Dispose() = failwith "Not implemented yet"
//    member x.IsContinuous = failwith "Not implemented yet"
//    member x.MoveAt(key, direction) = failwith "Not implemented yet"
//    member x.MoveFirst() = failwith "Not implemented yet"
//    member x.MoveLast() = failwith "Not implemented yet"
//    member x.MoveNext(cancellationToken) = failwith "Not implemented yet"
//    member x.MoveNextBatch(cancellationToken) = failwith "Not implemented yet"
//    member x.MovePrevious() = failwith "Not implemented yet"
//    member x.Reset() = failwith "Not implemented yet"
//    member x.Source = failwith "Not implemented yet"
//    member x.TryGetValue(key, value) = failwith "Not implemented yet"
//    member x.MoveNext() = 
//      try c.MoveNext()
//      with
//      | :? OutOfOrderKeyException<'K> as ooex ->
//        // TODO GT? or cache key and hasValidState and then EQ+MN or GE+compare+MN
//        c.MoveAt(ooex.CurrentKey, Lookup.GT)
//  end


