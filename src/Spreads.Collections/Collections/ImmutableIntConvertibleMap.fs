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

namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Runtime.InteropServices

open Spreads
open Spreads.Collections


[<SerializableAttribute>]
type internal IIntConverter<'T> =
    abstract ToInt64: t:'T -> int64
    abstract FromInt64: i:int64 -> 'T


[<SerializableAttribute>]
type internal DateTimeIntConverter() =
    interface IIntConverter<DateTime> with
        member x.ToInt64(t) = t.Ticks
        member x.FromInt64(i) = DateTime(i)


[<SerializableAttribute>]
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
[<SerializableAttribute>]
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


    interface IImmutableOrderedMap<'K, 'V> with
        member this.Comparer with get() = (this :> IImmutableOrderedMap<'K, 'V>).GetCursor().Comparer
        member this.GetEnumerator() = new MapCursor<'K, 'V>(this) :> IAsyncEnumerator<KVP<'K, 'V>>
        member this.GetCursor() = new MapCursor<'K, 'V>(this) :> ICursor<'K, 'V>
        member this.IsEmpty = map.IsEmpty
        //member this.Count = int map.Size
        member this.IsIndexed with get() = (map :> IImmutableOrderedMap<int64,'V>).IsIndexed
        member this.IsMutable = (map :> IImmutableOrderedMap<int64,'V>).IsMutable
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
                res <- (this :> IImmutableOrderedMap<'K, 'V>).First
                true
            with
            | _ -> 
                res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
                false
            
        member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
            try
                res <- (this :> IImmutableOrderedMap<'K, 'V>).Last
                true
            with
            | _ -> 
                res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
                false
        
        member this.TryGetValue(k, [<Out>] value:byref<'V>) = 
            let success, pair = (this :> IImmutableOrderedMap<'K, 'V>).TryFind(k, Lookup.EQ)
            if success then 
                value <- pair.Value
                true
            else false

//        member this.Count with get() = int map.Size
        member this.Size with get() = map.Size

        member this.SyncRoot with get() = map.SyncRoot

        member this.Add(key, value):IImmutableOrderedMap<'K, 'V> =
            ImmutableIntConvertableMap(map.Add(conv.ToInt64(key), value), conv) :> IImmutableOrderedMap<'K, 'V>

        member this.AddFirst(key, value):IImmutableOrderedMap<'K, 'V> =
            ImmutableIntConvertableMap(map.AddFirst(conv.ToInt64(key), value), conv) :> IImmutableOrderedMap<'K, 'V>

        member this.AddLast(key, value):IImmutableOrderedMap<'K, 'V> =
            ImmutableIntConvertableMap(map.AddLast(conv.ToInt64(key), value), conv) :> IImmutableOrderedMap<'K, 'V>

        member this.Remove(key):IImmutableOrderedMap<'K, 'V> =
            ImmutableIntConvertableMap(map.Remove(conv.ToInt64(key)), conv) :> IImmutableOrderedMap<'K, 'V>

        member this.RemoveLast([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableOrderedMap<'K, 'V> =
            let m,kvp = map.RemoveLast()
            value <- KeyValuePair(conv.FromInt64(kvp.Key), kvp.Value)
            ImmutableIntConvertableMap(m, conv) :> IImmutableOrderedMap<'K, 'V>

        member this.RemoveFirst([<Out>] value: byref<KeyValuePair<'K, 'V>>):IImmutableOrderedMap<'K, 'V> =
            let m,kvp = map.RemoveFirst()
            value <- KeyValuePair(conv.FromInt64(kvp.Key), kvp.Value)
            ImmutableIntConvertableMap(m, conv) :> IImmutableOrderedMap<'K, 'V>

        member this.RemoveMany(key,direction:Lookup):IImmutableOrderedMap<'K, 'V> =
            ImmutableIntConvertableMap(map.RemoveMany(conv.ToInt64(key), direction), conv) :> IImmutableOrderedMap<'K, 'V>


[<AllowNullLiteral>]
[<SerializableAttribute>]
type ImmutableDateTimeMap<'V>
    private(map:ImmutableIntMap64<'V>) =
    inherit ImmutableIntConvertableMap<DateTime, 'V>(map, DateTimeIntConverter())


    static member Empty = ImmutableDateTimeMap<'V>(ImmutableIntMap64<'V>.Empty)
    static member Create() = ImmutableDateTimeMap<'V>.Empty


[<AllowNullLiteral>]
[<SerializableAttribute>]
type ImmutableDateTimeOffsetMap<'V>
    private(map:ImmutableIntMap64<'V>) =
    inherit ImmutableIntConvertableMap<DateTimeOffset, 'V>(map, DateTimeOffsetIntConverter())


    static member Empty = ImmutableDateTimeOffsetMap<'V>(ImmutableIntMap64<'V>.Empty)
    static member Create() = ImmutableDateTimeOffsetMap<'V>.Empty


