﻿namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks


type KVP<'K,'V> = KeyValuePair<'K,'V>

// Needed for .NET < 4.5
//[<AutoOpenAttribute>]
//module internal InternalExtensions = 
//  type Task with
//      static member inline FromResult(result: 'T) : Task<'T> = 
//        let tcs = new TaskCompletionSource<'T>()
//        tcs.SetResult(result)
//        tcs.Task



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


/// used for Zip2Cursor to emulate varr
type internal MutableValueTuple<'V1,'V2> =
  struct
    val mutable Value1 : 'V1
    val mutable Value2 : 'V2
    new(v1 : 'V1, v2 : 'V2) = {Value1 = v1; Value2 = v2}
  end

  member this.Item 
    with set (index:int) (value:MutableValueTuple<'V1,'V2>) =
      if index = 0 then
        this.Value1 <- value.Value1
      elif index = 1 then
        this.Value2 <- value.Value2
      else raise (IndexOutOfRangeException(""))