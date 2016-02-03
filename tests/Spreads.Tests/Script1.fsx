#r "../../bin/Spreads.Core.dll"

open Spreads
open System.Runtime.InteropServices

let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x) 

#time "on"
let mutable sum = 0.0
let var : Variant = !> 123.0
for i = 0 to 10000000 do
  let double = !> var
  //sum <- double
  ()

sum

