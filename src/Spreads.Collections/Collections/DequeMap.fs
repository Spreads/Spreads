namespace Spreads.Collections.Experimental

open System
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks


open Spreads
open Spreads.Collections

[<AbstractClassAttribute>]
[<AllowNullLiteral>]
[<SerializableAttribute>]
type SortedDequeMap<'K,'V>
  internal(dictionary:IDictionary<'K,'V> option, capacity:int option, comparerOpt:IComparer<'K> option) as this=
  inherit Series<'K,'V>()

  // util fields
  [<NonSerializedAttribute>]
  // This type is logically immutable. This field is only mutated during deserialization. 
  let mutable comparer : IComparer<'K> = 
    if comparerOpt.IsNone || Comparer<'K>.Default.Equals(comparerOpt.Value) then
      let kc = KeyComparer.GetDefault<'K>()
      if kc = Unchecked.defaultof<_> then Comparer<'K>.Default :> IComparer<'K> 
      else kc
    else comparerOpt.Value // do not try to replace with KeyComparer if a comparer was given

  let sd = new SortedDeque<KV<'K,'V>>(KVKeyComparer(comparer))

  do
    if dictionary.IsSome then
      for kvp in dictionary.Value do
        sd.Add(KV(kvp.Key, kvp.Value))