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
open System.Threading.Tasks

open Spreads
open Spreads.Collections




/// Repack original types into value tuples. Due to the lazyness this only happens for a current value of cursor. ZipN keeps vArr instance and
/// rewrites its values. For value types we will always be in L1/stack, for reference types we do not care that much about performance.
type Zip2Cursor<'K,'V,'V2,'R>(cursorFactoryL:Func<ICursor<'K,'V>>,cursorFactoryR:Func<ICursor<'K,'V2>>, mapF:Func<'K,'V,'V2,'R>) =
  inherit ZipNCursor<'K,ValueTuple<'V,'V2>,'R>(
    Func<'K, ValueTuple<'V,'V2>[],'R>(fun (k:'K) (tArr:ValueTuple<'V,'V2>[]) -> mapF.Invoke(k, tArr.[0].Value1, tArr.[1].Value2)), 
    (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryL, Func<_,_>(fun (x:'V) -> ValueTuple<'V,'V2>(x, Unchecked.defaultof<'V2>)), None) :> ICursor<'K,ValueTuple<'V,'V2>>), 
    (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryR, Func<_,_>(fun (x:'V2) -> ValueTuple<'V,'V2>(Unchecked.defaultof<'V>, x)), None) :> ICursor<'K,ValueTuple<'V,'V2>>)
  )



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
            let ok, value = ScanCursorState.GetOrMakeBuffer(ref state, cursor, folder).TryGetValue(key)
            state.value <- value
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
        let ok, value = state.buffer.TryGetValue(current.Key)
        state.value <- value
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



