namespace Spreads

open Spreads
open System
open System.Numerics
open FSharp.Core
open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core.Operators
open Spreads.SIMDArrayUtils

[<RequireQualifiedAccess>]
module internal ScalarMap =

  let inline addValue (x) (addition: ^ T) = x + addition

  let inline addSegment (addition: ^ T) =
    let vAddition = new Vector<_>(addition)
    let vf = SIMD.mapSegment (fun v -> Vector.Add(v, vAddition)) (addValue addition)
    vf

  let inline subtract (subtraction: ^ T) =
    let vAddition = new Vector<_>(subtraction)
    let vf = SIMD.mapSegment (fun v -> Vector.Subtract(v, vAddition)) (fun x -> x - subtraction)
    vf