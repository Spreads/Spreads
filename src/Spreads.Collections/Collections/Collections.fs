// CollectionsUtils copied from: https://github.com/fsprojects/FSharpx.Collections/blob/master/src/FSharpx.Collections/Collections.fs
// License: https://github.com/fsprojects/FSharpx.Collections/blob/master/LICENSE.md
namespace Spreads.Collections

open System
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices

open Spreads

[<AutoOpenAttribute>]
module CollectionsUtils =
  let inline konst a _ = a
  let inline cons hd tl = hd::tl


module internal KeyHelper = 
  
  // repo of IDCs by 
  let diffCalculators = new Dictionary<Type, obj>()
  
  // dc only available for certain types, if we have it, then 
  // for sorted keys the condition defines dc and regularity TODO (doc)
  let isRegular<'K when 'K:comparison> (sortedArray:'K[]) (size:int) (dc:IKeyComparer<'K>) = 
    let lastOffset = size - 1
    Debug.Assert(sortedArray.Length >= size)
    dc.Diff(sortedArray.[lastOffset], sortedArray.[0])  = lastOffset

  let willRemainRegular<'K  when 'K:comparison> (start:'K) (size:int) (dc:IKeyComparer<'K>) (newValue:'K) : bool = 
    if size = 0 then true
    else 
      //      0 || 4    1,2,3
      dc.Diff(newValue, start) = size || dc.Diff(newValue, start) = -1

  let toArray<'K  when 'K:comparison> (start:'K) (size:int) (length:int) (dc:IKeyComparer<'K>) : 'K[] =
    Array.init length (fun i -> if i < size then dc.Add(start, i+1) else Unchecked.defaultof<'K>)



