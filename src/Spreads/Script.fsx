#load "Spreads.fs"
open Spreads
open System

let arr = [|1; 2|]

let fst = arr . [0]

// note that structs could not be recursive, this is more of the definition
// and not actual implementation

//type TypeEnum = byte
//type Flags = int64
//
//type Variant =
//| Leaf of TypeEnum:TypeEnum * Flags:Flags * InlineData:byte[] * Pointer:UIntPtr * Object:obj
//| Array of 

// Leaf is variant that is not an array or map, i.e. not a container 
// Make all containers > 200
// All fixed types < 100 or something like that
// Fixed + length only makes sense for arrays, single value must provide its length in bytes
// if TypeEnum doesn't define it

type Offset = int64<offset>
and [<Measure>] offset

//let o = Offset(123L)


let off : Offset = 123L<offset>

let newOff = off + 1L<offset>

