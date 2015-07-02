namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks


type KVP<'K,'V> = KeyValuePair<'K,'V>

[<AutoOpenAttribute>]
module internal InternalExtensions = 
  type Task with
      static member inline FromResult(result: 'T) : Task<'T> = 
        let tcs = new TaskCompletionSource<'T>()
        tcs.SetResult(result)
        tcs.Task



// Tuples as value types


type ValueTuple<'V1,'V2> =
  struct
    val Value1 : 'V1
    val Value2 : 'V2
    new(v1 : 'V1, v2 : 'V2) = {Value1 = v1; Value2 = v2}
  end

type ValueTuple<'V1,'V2,'V3> =
  struct
    val Value1 : 'V1
    val Value2 : 'V2
    val Value3 : 'V3
    new(v1 : 'V1, v2 : 'V2, v3 : 'V3) = {Value1 = v1; Value2 = v2; Value3 = v3}
    new(tuple2:ValueTuple<'V1,'V2>, v3 : 'V3) = {Value1 = tuple2.Value1; Value2 = tuple2.Value2; Value3 = v3}
  end