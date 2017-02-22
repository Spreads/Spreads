// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Linq


type Parameter(code:string, def:float, min:float, max:float, step:float, ?bigStep:float, ?description:string) =
  [<ThreadStaticAttribute;DefaultValueAttribute>]
  static val mutable private localRnd : Random
  let description = if description.IsSome then description.Value else ""
  let maxSteps = (max - min) / step |> int
  let bigStep = if bigStep.IsSome then bigStep.Value else Math.Round(step * Math.Min(float maxSteps, 10.0), 10)

  do
    if step > bigStep then raise (ArgumentException("step must be smaller or equal to bigstep", "step"))
    if Math.Abs((bigStep/step) - Math.Round(bigStep/step, 0)) > 0.0000001 then raise (ArgumentException("big step must be a multiple of small step", "bigStep"))
  

  let getRnd() = 
    if Parameter.localRnd = Unchecked.defaultof<Random> then
      Parameter.localRnd <- new Random()
    Parameter.localRnd

  let mutable position: float = def
  [<DefaultValueAttribute>]
  val mutable previous: double
  new (code:string, def:float, min:float, max:float, step:float, description:string)= Parameter(code, def, min, max, step, Math.Round(step * Math.Min(Math.Floor((max-min)/step), 10.0), 10), description)
  new(def:float, min:float, max:float, step:float) = Parameter("", def, min, max, step, 0.0, "")
  member this.Default with get () = def
  /// Pre-optimization minimum
  member this.Min with get () = min
  /// Pre-optimization maximum
  member this.Max with get () = max
  /// Step for optimization
  member this.Step with get () = step
  /// Step for pre-optimization
  member this.BigStep with get () = bigStep
  member this.Code with get () = code
  member this.Description with get () = description

  // alternative to enumeration
  member this.Position with get() = position and set(value) = position <- value
  member this.MoveAt(target:float) : bool =
    let offsetOk = Math.Round((target - min)/step) = (target - min)/step
    if offsetOk then position <- target; true else invalidOp("target is not a multiple of offset")
     
  member this.MoveNext() : bool = 
    let nextPosition = Math.Round(position + step,6)
    if  nextPosition <= max then
      this.previous <- position
      position <- nextPosition
      true
    else
      false

  /// Creates an enumerable that starts at (center -epsilon * step) and ends at (center + epsilon * step) via each step
  member this.GetRegion(center:float, epsilon:int) : IEnumerable<double> =
    let first = Math.Max((center - (float epsilon) * step), min)
    let last = Math.Min((center + (float epsilon) * step), max)
    let region = [|first .. step .. last|].Select(fun x->Math.Round(x,5))
   // region :> IEnumerable<double> 
    region
  /// get a random value from a parameter set
  member this.GetRandom() = min + step * (float (getRnd().Next(0, maxSteps + 1)))
  /// round a continuous value to the nearest valid discrete value
  member this.Round(continuous:float) = Math.Max(min, Math.Min(max, Math.Round(continuous/step) * step))

  override this.ToString() = this.ToCode() + (if description = "" then "" else " : " + description)

  member this.ToCode() = 
    if max > min then
      code + ": " + def.ToString() + " [" + min.ToString() + "..(" + bigStep.ToString() + "/" + step.ToString() + ").." + max.ToString() + "]" 
    else
      code + ": " + def.ToString()

  interface IEnumerable<float> with
    member x.GetEnumerator(): IEnumerator = (x :> IEnumerable<float>).GetEnumerator() :> IEnumerator
    member x.GetEnumerator(): IEnumerator<float> = 
      let current = ref min
      let started = ref false
      { new IEnumerator<float> with
          member x.Current: float = if !started then !current else invalidOp("Enumeration not started")
          member x.Current: obj = (!current) :> obj
          member x.Dispose(): unit = started := false
          member x.MoveNext(): bool = 
            if !started then
              let next = !current + step
              if next <= max then
                current := next
                true
              else
                false
            else
              started := true
              current := min
              true
          member x.Reset(): unit = started := false        
      }