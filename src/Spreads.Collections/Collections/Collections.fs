// Copied from: https://github.com/fsprojects/FSharpx.Collections/blob/master/src/FSharpx.Collections/Collections.fs
// License: https://github.com/fsprojects/FSharpx.Collections/blob/master/LICENSE.md
namespace Spreads.Collections


[<AutoOpenAttribute>]
module CollectionsUtils =
  let inline konst a _ = a
  let inline cons hd tl = hd::tl
