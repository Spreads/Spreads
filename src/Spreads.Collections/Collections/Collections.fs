// CollectionsUtils copied from: https://github.com/fsprojects/FSharpx.Collections/blob/master/src/FSharpx.Collections/Collections.fs
// License: https://github.com/fsprojects/FSharpx.Collections/blob/master/LICENSE.md
namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices


[<AutoOpenAttribute>]
module CollectionsUtils =
  let inline konst a _ = a
  let inline cons hd tl = hd::tl


