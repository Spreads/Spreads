// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads

open System
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open System.Runtime.CompilerServices

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal Utils =
     
  let inline readLockIf (nextVersion:int64 byref) (currentVersion:int64 byref) (condition:bool) (f:unit -> 'T) : 'T =
    let mutable value = Unchecked.defaultof<'T>
    let mutable doSpin = true
    let sw = new SpinWait()
    while doSpin do
      let version = if condition then Volatile.Read(&currentVersion) else currentVersion
      value <- f()
      if condition then
        let nextVersion = Volatile.Read(&nextVersion)
        if version = nextVersion then doSpin <- false
        else sw.SpinOnce()
      else doSpin <- false
    value
    
  let inline readLock (nextVersion:int64 byref) (currentVersion:int64 byref) (f:unit -> 'T) : 'T =
    let mutable value = Unchecked.defaultof<'T>
    let mutable doSpin = true
    let sw = new SpinWait()
    while doSpin do
      let version = Volatile.Read(&currentVersion)
      value <- f()
      let nextVersion = Volatile.Read(&nextVersion)
      if version = nextVersion then doSpin <- false
      else sw.SpinOnce()
    value

  let inline increment (value:byref<_>) = value <- value + LanguagePrimitives.GenericOne

  let inline decrement (value:byref<_>) = value <- value - LanguagePrimitives.GenericOne

  // TODO remove that, was just a scratchpad

  //let inline compare<'u when 'u :> IComparable<'u>> (a:'u) (b:'u) : int =
  //  let x  = (unbox(box a):double) + unbox(box a)
  //  a.CompareTo(b)

  //let inline addF (a:'T) (b:'T) : 'T = a + b

  //let inline constrain<'T,'TC,'TResult> (f:'TC->'TC->'TResult) (a:'TC) (b:'TC) =
  //  // to make it work all special types must be handled explicitly just as in C#
  //  if LanguagePrimitives.PhysicalEquality typeof<'T> typeof<'TC> then
  //    unbox(box((f (unbox(box a):'TC) (unbox(box b):'TC)))):'TResult
  //  // ...
  //  // long chain of explicit concrete type comparisons instead of 'TC
  //  // ...
  //  elif LanguagePrimitives.PhysicalEquality typeof<'T> typeof<double> then
  //    // instead of `f` we need to use addF directly, then f could be removed
  //    unbox(box((addF (unbox(box a):double) (unbox(box b):double)))):'TResult
  //  else 
  //    // In C# one could cast to `dynamic` and apply operators or call a method
  //    // blindly on it. Cost of it is just a cached delegate call, performance
  //    // is approx. 10x lower for numerics.
  //    Unchecked.defaultof<'TResult>
    
  //// 'T becomes constrained due to addF
  //let inline twice (x:'T) :'T = constrain<'T,'T,'T> addF x x

  //// resultT is int -> int without inline, with inline constrain goes viral
  //let inline resultT (x:'T) : 'T = twice x

  //// this is nice from F#, but without breaking the constraint chain 
  //// it would be unusable from C#.
  //let resultInt = resultT 123
  //let resultDouble = resultT 123.0
  //let resultObject = resultT obj() // breaks

  // it would be nice if F#'s type magic could allow to avoid long 
  // mega methods that do the same thing over and over