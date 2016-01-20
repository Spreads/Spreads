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
    member this.Map<'R2>(f2:Func<'R,'R2>): Series<'K,'R2> = 
      let mapper2 : Func<'V,'R2> = Func<'V,'R2>(fun r -> f2.Invoke(mapperFunc.Invoke(r)))
      CursorSeries(fun _ -> new FilterMapCursor<'K,'V,'R2>(cursorFactory, filterFunc, mapper2) :> ICursor<'K,'R2>) :> Series<'K,'R2>

