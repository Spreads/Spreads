// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections


type internal IIntConverter<'T> =
    abstract ToInt64: t:'T -> int64
    abstract FromInt64: i:int64 -> 'T


type internal DateTimeIntConverter() =
    interface IIntConverter<DateTime> with
        member x.ToInt64(t) = t.Ticks
        member x.FromInt64(i) = DateTime(i)


type internal DateTimeOffsetIntConverter() =
    let mutable offset = None
    interface IIntConverter<DateTimeOffset> with
        member x.ToInt64(t) = 
            if offset.IsNone then offset <- Some(t.Offset)
            if TimeSpan.Equals(t.Offset, offset.Value) then
                t.UtcTicks
            else
                raise (ArgumentException(""))
        member x.FromInt64(i) = DateTimeOffset(i, offset.Value)


[<AllowNullLiteral>]
type ImmutableIntConvertableMap<'K, 'V when 'K : comparison>
    internal(map:ImmutableIntMap64<'V>,conv:IIntConverter<'K>) =

    let map = map

    member internal this.Map = map


    member this.GetEnumerator() : IEnumerator<KVP<'K, 'V>> = 
      ((map :> IEnumerable<_>).ToArray()
        |> Array.map (fun kv -> KeyValuePair(conv.FromInt64(kv.Key),kv.Value)) :> (KeyValuePair<'K, 'V>) seq).GetEnumerator()


    interface IEnumerable<KeyValuePair<'K, 'V>> with
      member m.GetEnumerator() = m.GetEnumerator()

    interface System.Collections.IEnumerable with
      member m.GetEnumerator() = m.GetEnumerator() :> System.Collections.IEnumerator


    interface IImmutableSeries<'K, 'V> with
        member this.Updated = falseTask
        member this.Subscribe(observer) = raise (NotImplementedException())
        member this.Comparer with get() = (this :> IImmutableSeries<'K, 'V>).GetCursor().Comparer
        member this.GetEnumerator() = new MapCursor<'K, 'V>(this) :> IAsyncEnumerator<KVP<'K, 'V>>
        member this.GetCursor() = new MapCursor<'K, 'V>(this) :> ICursor<'K, 'V>
        member this.IsEmpty = map.IsEmpty
        //member this.Count = int map.Size
        member this.IsIndexed with get() = (map :> IImmutableSeries<int64,'V>).IsIndexed
        member this.IsReadOnly = (map :> IImmutableSeries<int64,'V>).IsReadOnly
        member this.First
            with get() = 
                let f = map.First
                KeyValuePair(conv.FromInt64(f.Key), f.Value)

        member this.Last
            with get() = 
                let l = map.First
                KeyValuePair(conv.FromInt64(l.Key), l.Value)

        member this.Item  with get (k) : 'V = map.Item(conv.ToInt64(k))
        member this.GetAt(idx:int) = this.Skip(Math.Max(0, idx-1)).First().Value

        [<ObsoleteAttribute("Naive impl, optimize if used often")>]
        member this.Keys with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Key)
        [<ObsoleteAttribute("Naive impl, optimize if used often")>]
        member this.Values with get() = (this :> IEnumerable<KVP<'K,'V>>) |> Seq.map (fun kvp -> kvp.Value)


        member this.TryFind(k, direction:Lookup, [<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
            res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
            let tr = map.TryFind(conv.ToInt64(k), direction)
            if (fst tr) then
                let kvp = snd tr
                res <- KeyValuePair(conv.FromInt64(kvp.Key), kvp.Value)
                true
            else
                false

        member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
            try
                res <- (this :> IImmutableSeries<'K, 'V>).First
                true
            with
            | _ -> 
                res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
                false
            
        member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
            try
                res <- (this :> IImmutableSeries<'K, 'V>).Last
                true
            with
            | _ -> 
                res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
                false

//        member this.Count with get() = int map.Size
        member this.Size with get() = map.Size

        member this.SyncRoot with get() = map.SyncRoot

        member this.Add(key, value):IImmutableSeries<'K, 'V> =
            ImmutableIntConvertableMap(map.Add(conv.ToInt64(key), value), conv) :> IImmutableSeries<'K, 'V>

        member this.AddFirst(key, value):IImmutableSeries<'K, 'V> =
            ImmutableIntConvertableMap(map.AddFirst(conv.ToInt64(key), value), conv) :> IImmutableSeries<'K, 'V>

        member this.AddLast(key, value):IImmutableSeries<'K, 'V> =
            ImmutableIntConvertableMap(map.AddLast(conv.ToInt64(key), value), conv) :> IImmutableSeries<'K, 'V>

        member this.Remove(key):IImmutableSeries<'K, 'V> =
            ImmutableIntConvertableMap(map.Remove(conv.ToInt64(key)), conv) :> IImmutableSeries<'K, 'V>

        member this.RemoveLast([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableSeries<'K, 'V> =
            let m,kvp = map.RemoveLast()
            value <- KeyValuePair(conv.FromInt64(kvp.Key), kvp.Value)
            ImmutableIntConvertableMap(m, conv) :> IImmutableSeries<'K, 'V>

        member this.RemoveFirst([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableSeries<'K, 'V> =
            let m,kvp = map.RemoveFirst()
            value <- KeyValuePair(conv.FromInt64(kvp.Key), kvp.Value)
            ImmutableIntConvertableMap(m, conv) :> IImmutableSeries<'K, 'V>

        member this.RemoveMany(key,direction:Lookup):IImmutableSeries<'K, 'V> =
            ImmutableIntConvertableMap(map.RemoveMany(conv.ToInt64(key), direction), conv) :> IImmutableSeries<'K, 'V>


[<AllowNullLiteral>]
type ImmutableDateTimeMap<'V>
    private(map:ImmutableIntMap64<'V>) =
    inherit ImmutableIntConvertableMap<DateTime, 'V>(map, DateTimeIntConverter())


    static member Empty = ImmutableDateTimeMap<'V>(ImmutableIntMap64<'V>.Empty)
    static member Create() = ImmutableDateTimeMap<'V>.Empty


[<AllowNullLiteral>]
type ImmutableDateTimeOffsetMap<'V>
    private(map:ImmutableIntMap64<'V>) =
    inherit ImmutableIntConvertableMap<DateTimeOffset, 'V>(map, DateTimeOffsetIntConverter())


    static member Empty = ImmutableDateTimeOffsetMap<'V>(ImmutableIntMap64<'V>.Empty)
    static member Create() = ImmutableDateTimeOffsetMap<'V>.Empty


