// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


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
  member this.CurrentBatch =
    let factory = Func<_>(cursor.CurrentBatch.GetCursor)
    let c() = new MapValuesWithKeysCursor<'K,'V,'V2>(factory, f) :> ICursor<'K,'V2>
    CursorSeries(Func<_>(c)) :> IReadOnlySeries<_,_>

  member this.CurrentKey: 'K = cursor.CurrentKey
  member this.CurrentValue: 'V2 = f.Invoke(cursor.CurrentKey, cursor.CurrentValue)

  member this.Source: IReadOnlySeries<_,_> = 
      let factory = Func<_>(cursor.Source.GetCursor)
      let c() = new MapValuesWithKeysCursor<'K,'V,'V2>(cursorFactory, f) :> ICursor<'K,'V2>
      CursorSeries(Func<_>(c)) :> IReadOnlySeries<_,_>
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
    member this.CurrentBatch = this.CurrentBatch
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
    member this.Source = this.Source
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  this.TryGetValue(key, &value)
    member this.Clone() = this.Clone()