namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads

// TODO see benchmark for ReadOnly. Reads are very slow while iterations are not affected (GetCursor() returns original cursor) in release mode. Optimize 
// reads of this wrapper either here by type-checking the source of the cursor and using direct methods on the source
// or make cursor thread-static and initialize it only once (now it is called on each method)

// TODO duplicate IReadOnlyOrderedMap methods as an instance method to avoid casting in F#. That will require ovverrides in all children or conflict
// check how it is used from C# (do tests in C# in general)

[<AllowNullLiteral>]
[<Serializable>]
[<AbstractClassAttribute>]
type Series<'K,'V when 'K : comparison>() as this =
  abstract GetCursor : unit -> ICursor<'K,'V>

  interface IEnumerable<KeyValuePair<'K, 'V>> with
    member this.GetEnumerator() = this.GetCursor() :> IEnumerator<KeyValuePair<'K, 'V>>
  interface System.Collections.IEnumerable with
    member this.GetEnumerator() = (this.GetCursor() :> System.Collections.IEnumerator)
  interface ISeries<'K, 'V> with
    member this.GetCursor() = this.GetCursor()
    member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
    member this.IsIndexed with get() = this.GetCursor().Source.IsIndexed
    member this.SyncRoot with get() = this.GetCursor().Source.SyncRoot

  interface IReadOnlyOrderedMap<'K,'V> with
    member this.IsEmpty = not (this.GetCursor().MoveFirst())
    //member this.Count with get() = map.Count
    member this.First with get() = 
      let c = this.GetCursor()
      if c.MoveFirst() then c.Current else failwith "Series is empty"

    member this.Last with get() =
      let c = this.GetCursor()
      if c.MoveLast() then c.Current else failwith "Series is empty"

    member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      let c = this.GetCursor()
      if c.MoveAt(k, direction) then 
        result <- c.Current 
        true
      else failwith "Series is empty"

    member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      try
        res <- (this :> IReadOnlyOrderedMap<'K,'V>).First
        true
      with
      | _ -> 
        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        false

    member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      try
        res <- (this :> IReadOnlyOrderedMap<'K,'V>).Last
        true
      with
      | _ -> 
        res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
        false

    member this.TryGetValue(k, [<Out>] value:byref<'V>) = 
      let c = this.GetCursor()
      if c.IsContinuous then
        c.TryGetValue(k, &value)
      else
        let v = ref Unchecked.defaultof<KVP<'K,'V>>
        let ok = c.MoveAt(k, Lookup.EQ)
        if ok then value <- c.CurrentValue else value <- Unchecked.defaultof<'V>
        ok

    member this.Item 
      with get k = 
        let ok, v = (this :> IReadOnlyOrderedMap<'K,'V>).TryGetValue(k)
        if ok then v else raise (KeyNotFoundException())

    member this.Keys 
      with get() =
        let c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentKey
        }

    member this.Values
      with get() =
        let c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentValue
        }

