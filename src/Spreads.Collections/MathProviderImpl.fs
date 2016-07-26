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
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open Spreads
open Spreads.Collections

type MathProviderImpl() =
  member this.MapBatch<'K,'V,'V2>(mapF: 'V -> 'V2, batch: IReadOnlyOrderedMap<'K,'V>, value: byref<IReadOnlyOrderedMap<'K,'V2>>): bool =
      match batch with
      | :? SortedMap<'K,'V> as sm -> 
        if sm.size > 0 then
          let values2 = Array.map mapF sm.values //  // TODO what if values are reference types and capacity > size, could throw on nulls
          // NB when savings on key memory matters most, they are usually already regular
          // but it still helps to avoid copying, churning caches and just save memory
          let keys2 = Array.copy sm.keys
          // TODO keys array reuse should be at SM level, not at ArrayPool
//            if not sm.IsReadOnly then Array.copy sm.keys
//            else 
//              OptimizationSettings.ArrayPool.Borrow(sm.keys) |> ignore
//              sm.keys // NB borrow keys from the source batch
          let sm2 = SortedMap.OfSortedKeysAndValues(keys2, values2, sm.size, sm.Comparer, false, sm.IsRegular)
          // NB source was mutable or we have created a copy that is supposed to be accessed as IReadOnlyOrderedMap externally
          sm2.Complete()
          value <- sm2 :> IReadOnlyOrderedMap<'K,'V2>
          true
        else false
      | _ ->
        // TODO! MapBatchCursor (to be lazy) without bacthing function (to avoid circular links)
        let map = new SortedMap<'K,'V2>()
        for kvp in batch do
            let newValue = mapF(kvp.Value)
            map.AddLast(kvp.Key, newValue)
        if map.size > 0 then
          value <- map :> IReadOnlyOrderedMap<'K,'V2>
          true
        else false

  interface IVectorMathProvider with
    member x.AddBatch(left: IReadOnlyOrderedMap<'K,double>, right: IReadOnlyOrderedMap<'K,double>, value: byref<IReadOnlyOrderedMap<'K,double>>): bool = 
      failwith "Not implemented yet"
    
    member this.MapBatch<'K,'V,'V2>(mapF: 'V -> 'V2, batch: IReadOnlyOrderedMap<'K,'V>, value: byref<IReadOnlyOrderedMap<'K,'V2>>): bool =
       this.MapBatch(mapF, batch, &value)
    
    member this.AddVectors(x:'T[],y:'T[],result:'T[]) : unit = failwith "not implemented"

    member this.AddBatch(scalar:double, batch: IReadOnlyOrderedMap<'K,double>, value: byref<IReadOnlyOrderedMap<'K,double>>) =
      this.MapBatch<'K, double, double>(
        (fun (x:double) ->
            x + scalar
        ), 
        batch, &value)
      