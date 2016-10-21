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



[<SealedAttribute>]
type FilterValuesCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, filterFunc:Func<'V,bool>) =
  inherit SimpleBindCursor<'K,'V,'V>(cursorFactory)

  override this.IsContinuous = this.InputCursor.IsContinuous

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'V>): bool =
    let mutable v = Unchecked.defaultof<_>
    let ok = this.InputCursor.TryGetValue(key, &v)
    if ok && filterFunc.Invoke v then
      value <- v
      true
    else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if filterFunc.Invoke next.Value then
      value <- next.Value
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if filterFunc.Invoke previous.Value then
      value <- previous.Value
      true
    else false

  override this.Clone() = 
    let clone = new FilterValuesCursor<'K,'V>(cursorFactory, filterFunc) :> ICursor<'K,'V>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone


[<SealedAttribute>]
type FilterMapCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, filterFunc:Func<'K,'V,bool>, mapperFunc:Func<'V,'R>) =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)

  override this.IsContinuous = this.InputCursor.IsContinuous

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let mutable v = Unchecked.defaultof<_>
    let ok = this.InputCursor.TryGetValue(key, &v)
    if ok && filterFunc.Invoke(key,v) then
      value <- mapperFunc.Invoke(v)
      true
    else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if filterFunc.Invoke(next.Key,next.Value) then
      value <- mapperFunc.Invoke(next.Value)
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if filterFunc.Invoke(previous.Key, previous.Value) then
      value <- mapperFunc.Invoke(previous.Value)
      true
    else false

  override this.Clone() = 
    let clone = new FilterMapCursor<'K,'V,'R>(cursorFactory, filterFunc, mapperFunc) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  interface ICanMapSeriesValues<'K,'R> with
    member this.Map<'R2>(f2, _): Series<'K,'R2> = 
      let mapper2 : Func<'V,'R2> = Func<'V,'R2>(mapperFunc.Invoke >> f2)
      CursorSeries(fun _ -> new FilterMapCursor<'K,'V,'R2>(cursorFactory, filterFunc, mapper2) :> ICursor<'K,'R2>) :> Series<'K,'R2>

