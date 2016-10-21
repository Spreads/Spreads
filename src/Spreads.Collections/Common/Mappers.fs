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


[<Interface>]
[<AllowNullLiteral>]
type internal IScalarMapping<'V,'V1> =
  abstract ScalarFunc: unit -> ('V->'V1)
  abstract ArrayFunc: unit -> ((ArraySegment<'V>->ArraySegment<'V1>) opt)
  abstract Combine: next:IScalarMapping<'V1,'V2> -> IScalarMapping<'V,'V2> when 'V2 : (new : unit -> 'V2) and 'V2 : struct and 'V2 :> ValueType

type internal 
  SIMDScalarMapping<'V,'V1 
      when 'V : (new : unit -> 'V) and 'V : struct and 'V :> ValueType
      and 'V1 : (new : unit -> 'V1) and 'V1 : struct and 'V1 :> ValueType > =
  struct
    val internal ScalarFunc: 'V -> 'V1
    val internal VectorFunc: (Vector<'V> -> Vector<'V1>) opt
    new(sf, vf) = { ScalarFunc = sf; VectorFunc = vf }
  end

  member inline this.Combine(other:IScalarMapping<'V1,'V2> when 'V2 : (new : unit -> 'V2) and 'V2 : struct and 'V2 :> ValueType) =
    match box other with
    | :? SIMDScalarMapping<'V1,'V2> as simdMapping -> 
      let f = this.ScalarFunc >> simdMapping.ScalarFunc
      let vf = 
        if this.VectorFunc.IsPresent && simdMapping.VectorFunc.IsPresent then
          Present(this.VectorFunc.Present >> simdMapping.VectorFunc.Present)
        else Missing
      let mapping = SIMDScalarMapping(f, vf) 
      box mapping :?> IScalarMapping<'V,'V2>
    | _ -> failwith ""

  interface IScalarMapping<'V,'V1> with
    member this.ScalarFunc() = this.ScalarFunc
    member this.ArrayFunc() = 
      if this.VectorFunc.IsPresent then
        Present(SIMD.mapSegment this.VectorFunc.Present this.ScalarFunc)
      else Missing
    member this.Combine(other) = this.Combine(other)

[<RequireQualifiedAccess>]
module internal ScalarMap =

  let inline addValue (x) (addition: ^ T) = x + addition

  let inline addSegment (addition: ^ T) =
    let vAddition = new Vector<_>(addition)
    let vf = SIMD.mapSegment (fun v -> Vector.Add(v, vAddition)) (addValue addition)
    vf


  let inline multiplyValue (x) (mult: ^ T) = x * mult

  let inline multiplySegment (mult: ^ T) =
    let vf = SIMD.mapSegment (fun v -> Vector.Multiply(v, mult)) (multiplyValue mult)
    vf

  let inline divideValue (x) (denominator: ^ T) = x / denominator

  let inline divideSegment (denominator: ^ T) =
    let vDenominator = new Vector<_>(denominator)
    let vf = SIMD.mapSegment (fun v -> Vector.Divide(v, vDenominator)) (divideValue denominator)
    vf

  let inline subtract (subtraction: ^ T) =
    let vAddition = new Vector<_>(subtraction)
    let vf = SIMD.mapSegment (fun v -> Vector.Subtract(v, vAddition)) (fun x -> x - subtraction)
    vf