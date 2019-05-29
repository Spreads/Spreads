// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads

open System.Threading

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
