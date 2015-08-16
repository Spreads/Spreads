namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open Spreads
open Spreads.Collections

type MathProviderImpl() =
  interface IVectorMathProvider with
    member this.MapBatch<'K,'V,'V2>(mapF: 'V -> 'V2, batch: IReadOnlyOrderedMap<'K,'V>, value: byref<IReadOnlyOrderedMap<'K,'V2>>): bool =
      match batch with
      | :? SortedMap<'K,'V> as sm -> 
        if sm.size > 0 then
          let values2 = Array.Parallel.map mapF sm.values // TODO what if values are reference types and capacity > size, could throw on nulls
          let keys2 = Array.copy sm.keys
          let sm2 = SortedMap.OfSortedKeysAndValues(keys2, values2, sm.size, sm.Comparer, false, sm.IsRegular)
          value <- sm2 :> IReadOnlyOrderedMap<'K,'V2>
          true
        else false
      | _ ->
        let map = SortedMap<'K,'V2>()
        for kvp in batch do
            let newValue = mapF(kvp.Value)
            map.AddLast(kvp.Key, newValue)
        if map.size > 0 then 
          value <- map :> IReadOnlyOrderedMap<'K,'V2>
          true
        else false
    
    member this.AddVectors(x:'T[],y:'T[],result:'T[]) : unit = failwith "not implemented"
