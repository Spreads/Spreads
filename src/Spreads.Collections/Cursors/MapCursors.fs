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


// Any map other than BatchMapValues


 /// Map keys and values to new values
type internal MapValuesWithKeysCursor<'K,'V,'V2>(cursorFactory:Func<ICursor<'K,'V>>, f:Func<'K,'V,'V2>)=
  let cursor : ICursor<'K,'V> =  cursorFactory.Invoke()
  let f : Func<'K,'V,'V2> = f

  member this.Current: KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)
  member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> =
    let factory = Func<_>(cursor.CurrentBatch.GetCursor)
    let c() = new MapValuesWithKeysCursor<'K,'V,'V2>(factory, f) :> ICursor<'K,'V2>
    CursorSeries(Func<_>(c)) :> IReadOnlyOrderedMap<'K,'V2>

  member this.CurrentKey: 'K = cursor.CurrentKey
  member this.CurrentValue: 'V2 = f.Invoke(cursor.CurrentKey, cursor.CurrentValue)

  member this.Source: ISeries<'K,'V2> = 
      let factory = Func<_>(cursor.Source.GetCursor)
      let c() = new MapValuesWithKeysCursor<'K,'V,'V2>(cursorFactory, f) :> ICursor<'K,'V2>
      CursorSeries(Func<_>(c)) :> ISeries<'K,'V2>
  member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  
    let mutable v = Unchecked.defaultof<_>
    let ok = cursor.TryGetValue(key, &v)
    if ok then value <- f.Invoke(key, v)
    ok
      
  member this.Clone() = new MapValuesWithKeysCursor<'K,'V,'V2>(Func<_>(cursor.Clone), f) :> ICursor<'K,'V2>

  interface IEnumerator<KVP<'K,'V2>> with    
    member this.Reset() = cursor.Reset()
    member this.MoveNext(): bool = cursor.MoveNext()
    member this.Current with get(): KVP<'K, 'V2> = KVP(this.CurrentKey, this.CurrentValue)
    member this.Current with get(): obj = KVP(this.CurrentKey, this.CurrentValue) :> obj 
    member this.Dispose(): unit = cursor.Dispose()

  interface ICursor<'K,'V2> with
    member this.Comparer with get() = cursor.Comparer
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
    member this.CurrentKey: 'K = this.CurrentKey
    member this.CurrentValue: 'V2 = this.CurrentValue
    member this.IsContinuous: bool = cursor.IsContinuous
    member this.MoveAt(index: 'K, direction: Lookup): bool = cursor.MoveAt(index, direction) 
    member this.MoveFirst(): bool = cursor.MoveFirst()
    member this.MoveLast(): bool = cursor.MoveLast()
    member this.MovePrevious(): bool = cursor.MovePrevious()
    
    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = cursor.MoveNext(cancellationToken)
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = cursor.MoveNextBatch(cancellationToken)
    
    //member this.IsBatch with get() = this.IsBatch
    member this.Source: ISeries<'K,'V2> = this.Source
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  this.TryGetValue(key, &value)
    member this.Clone() = this.Clone()