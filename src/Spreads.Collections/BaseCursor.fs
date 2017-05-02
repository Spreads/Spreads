// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open Spreads
open Microsoft.FSharp.Control

// TODO (cleanup) remove this completely

// TODO rename back to MapCursor - this is an original cursor backed by some map, it does not represent series itself
[<AbstractClassAttribute>]
type BaseCursor<'K,'V>(source:IReadOnlySeries<'K,'V>) =
      
  abstract Comparer: KeyComparer<'K> with get
  override this.Comparer with get() = source.Comparer

  abstract MoveAt: index:'K * direction:Lookup -> bool

  abstract MoveFirst: unit -> bool

  abstract MoveLast: unit -> bool

  abstract MoveNext : unit -> bool
  
  abstract MovePrevious: unit -> bool

  abstract Current:KVP<'K,'V> with get
  override this.Current with get(): KeyValuePair<'K, 'V> = KVP(this.CurrentKey, this.CurrentValue)

  abstract CurrentKey:'K with get

  abstract CurrentValue:'V with get

  abstract Dispose: unit -> unit
  override this.Dispose() = ()

  abstract member Reset : unit -> unit

  abstract member MoveNext : CancellationToken -> Task<bool>
  override this.MoveNext(ct) = falseTask
    
  abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
  abstract CurrentBatch: IReadOnlySeries<'K,'V> with get
  abstract Source : IReadOnlySeries<'K,'V> with get
  override this.Source with get() = source :> IReadOnlySeries<'K,'V>
  abstract Clone: unit -> ICursor<'K,'V>
  abstract IsContinuous: bool with get

  abstract TryGetValue: 'K * [<Out>]value: byref<'V> -> bool

  interface IDisposable with
    member this.Dispose() = this.Dispose()

  interface IEnumerator<KVP<'K,'V>> with
    member this.Reset() = this.Reset()
    member this.MoveNext():bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V> = this.Current
    member this.Current with get(): obj = this.Current :> obj

  interface IAsyncEnumerator<KVP<'K,'V>> with
    member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNext(cancellationToken) 

  interface ICursor<'K,'V> with
    member this.Comparer with get() = this.Comparer
    member this.CurrentBatch = this.CurrentBatch
    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatchAsync(cancellationToken)
    member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
    member this.MoveFirst():bool = this.MoveFirst()
    member this.MoveLast():bool =  this.MoveLast()
    member this.MovePrevious():bool = this.MovePrevious()
    member this.CurrentKey with get():'K = this.CurrentKey
    member this.CurrentValue with get():'V = this.CurrentValue
    member this.Source with get() = this.Source
    member this.Clone() = this.Clone()
    member this.IsContinuous with get() = this.IsContinuous
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = this.TryGetValue(key, &value)

  interface ISpecializedCursor<'K,'V, BaseCursor<'K,'V>> with
    member this.Initialize() = 
      let c = this.Clone()
      c.Reset()
      c :?> BaseCursor<'K,'V>
    member this.Clone() = this.Clone() :?> BaseCursor<'K,'V>

/// Uses IReadOnlySeries's TryFind method, doesn't know anything about underlying sequence
type MapCursor<'K,'V>(map:IReadOnlySeries<'K,'V>) =
  inherit BaseCursor<'K,'V>(map)
  [<DefaultValue>] 
  val mutable private currentPosition : bool * KeyValuePair<'K,'V>

  let mutable isReset = true

  override this.MoveAt(index:'K, lookup:Lookup) = 
    isReset <- false
    this.currentPosition <- map.TryFind(index, lookup)
    fst this.currentPosition

  override this.MoveFirst():bool = 
    try
      this.MoveAt(map.First.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  override this.MoveLast():bool =
    try
      this.MoveAt(map.Last.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  override this.MoveNext():bool = 
    if isReset then this.MoveFirst()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.GT)
      fst this.currentPosition
  
  override this.MovePrevious():bool = 
    if isReset then this.MoveLast()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.LT)
      fst this.currentPosition

  override this.Current 
    with get(): KeyValuePair<'K, 'V> = 
      snd this.currentPosition

  override this.CurrentKey with get():'K = this.Current.Key

  override this.CurrentValue with get():'V = this.Current.Value

  override this.Reset() = isReset <- true

  override this.CurrentBatch = raise (NotSupportedException("IReadOnlySeries do not support batches, override the method in a map implementation"))
  override this.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> = raise (NotSupportedException("IReadOnlySeries do not support batches, override the method in a map implementation"))

  override this.Source with get() = map

  override this.Clone() =
    let c = new MapCursor<'K,'V>(map)
    c.currentPosition <- this.currentPosition
    c :> ICursor<'K,'V>

  override this.IsContinuous with get() = false

  override this.TryGetValue(key, [<Out>]value: byref<'V>) : bool =
    let mutable tmp = Unchecked.defaultof<_>
    let found = map.TryFind(key, Lookup.EQ, &tmp)
    if found then
      value <- tmp.Value
      true
    else false

  interface ISpecializedCursor<'K,'V, MapCursor<'K,'V>> with
    member this.Initialize() = 
      let c = this.Clone()
      c.Reset()
      c :?> MapCursor<'K,'V>
    member this.Clone() = this.Clone() :?> MapCursor<'K,'V>
