// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Linq
open System.Linq.Expressions
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open System.Numerics

open Spreads
open Spreads.Collections

[<AllowNullLiteral>]
[<AbstractClassAttribute>]
//[<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
type MutableSeries<'K,'V> internal() as this =
  inherit Series<'K,'V>()

  abstract member Add : key:'TKey * value:'TValue -> unit
  abstract member AddFirst : key:'TKey * value:'TValue -> unit
  abstract member AddLast : key:'TKey * value:'TValue -> unit
  abstract member Append : appendMap:IReadOnlySeries<'TKey,'TValue> * option:AppendOption -> int
  abstract member Complete : unit -> unit
  abstract member Count : int64
  abstract member Item : 'TValue with get, set
  abstract member Remove : key:'TKey -> bool
  abstract member RemoveFirst : kvp:byref<KeyValuePair<'TKey,'TValue>> -> bool
  abstract member RemoveLast : kvp:byref<KeyValuePair<'TKey,'TValue>> -> bool
  abstract member RemoveMany : key:'TKey * direction:Lookup -> bool
  abstract member Version : int64 with get

  interface IReadOnlySeries<'K,'V> with
    member x.Comparer = this.Comparer
    member x.First = this.First
    member x.GetAt(idx) = this.GetAt(idx)
    member x.GetCursor() = this.GetCursor()
    member x.GetEnumerator(): Collections.IEnumerator = this.GetCursor() :> Collections.IEnumerator
    member x.GetEnumerator(): IAsyncEnumerator<KeyValuePair<'K,'V>> = this.GetCursor() :> IAsyncEnumerator<KeyValuePair<'K,'V>>
    member x.GetEnumerator(): IEnumerator<KeyValuePair<'K,'V>> = this.GetCursor() :> IEnumerator<KeyValuePair<'K,'V>>
    member x.IsEmpty = this.IsEmpty
    member x.IsIndexed = this.IsIndexed
    member x.IsReadOnly = this.IsReadOnly
    member x.Item with get (key) = this.Item(key)
    member x.Keys = this.Keys
    member x.Last = this.Last
    member x.Subscribe(observer) = this.Subscribe(observer)
    member x.SyncRoot = this.SyncRoot
    member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = this.TryFind(k, direction, &result)
    member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = this.TryGetFirst(&res)
    member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = this.TryGetLast(&res)
    member this.TryGetValue(k, [<Out>] value:byref<'V>) = this.TryGetValue(k, &value)
    member x.Values = this.Values

  interface IMutableSeries<'K,'V> with
    member this.Complete() = this.Complete()
    member this.Version with get() = this.Version
    member this.Count with get() = this.Count
    member this.Item with get k = this.Item(k) and set (k:'K) (v:'V) = this.Item(k,v)
    member this.Add(k, v) = this.Add(k,v)
    member this.AddLast(k, v) = this.AddLast(k, v)
    member this.AddFirst(k, v) = this.AddFirst(k, v)
    member this.Append(appendMap:IReadOnlySeries<'K,'V>, option:AppendOption) = this.Append(appendMap, option)
    member this.Remove(k) = this.Remove(k)
    member this.RemoveFirst([<Out>] result: byref<KeyValuePair<'K, 'V>>) = this.RemoveFirst(&result)
    member this.RemoveLast([<Out>] result: byref<KeyValuePair<'K, 'V>>) = this.RemoveLast(&result)
    member this.RemoveMany(key:'K,direction:Lookup) = this.RemoveMany(key, direction)


[<AllowNullLiteral>]
//[<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
type internal MutableSeriesWrapper<'K,'V> internal(inner:IMutableSeries<'K,'V>) as this =
  inherit MutableSeries<'K,'V>()
  let foo = "a"