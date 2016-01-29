// Adopted from Deedle
// Renamed Value to Present to avoid x.Value.Value.Value mind-blowing patterns with KeyValuePairs

// Copyright (c) 2012, BlueMountain Capital Management LLC
//
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, 
// are permitted provided that the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, this list 
// of conditions and the following disclaimer.
//
// Redistributions in binary form must reproduce the above copyright notice, this list 
// of conditions and the following disclaimer in the documentation and/or other materials
// provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT 
// SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, 
// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, 
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, 
// STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF 
// THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


namespace Spreads

open System
open System.Runtime.CompilerServices

[<Struct; CustomEquality; NoComparison>]
type OptionalValue<'T> private (isPresent:bool, value:'T) = 
  /// Gets a value indicating whether the current `OptionalValue<T>` has a value
  member x.IsPresent = isPresent

  member x.IsMissing = not isPresent

  /// Returns the value stored in the current `OptionalValue<T>`. 
  /// Exceptions:
  ///   `InvalidOperationException` - Thrown when `HasValue` is `false`.
  member x.Present = 
    if isPresent then value
    else invalidOp "OptionalValue.Value: Value is not present" 
  
  /// Returns the value stored in the current `OptionalValue<T>` or 
  /// the default value of the type `T` when a value is not present.
  member x.PresentOrDefault = value

  /// Creates a new instance of `OptionalValue<T>` that contains  
  /// the specified `T` value .
  new (value:'T) = OptionalValue(true, value)

  /// Returns a new instance of `OptionalValue<T>` that does not contain a value.
  static member Missing = OptionalValue(false, Unchecked.defaultof<'T>)

  /// Prints the value or "<null>" when the value is present, but is `null`
  /// or "<missing>" when the value is not present (`HasValue = false`).
  override x.ToString() = 
    if isPresent then 
      if Object.Equals(null, value) then "<null>"
      else value.ToString() 
    else "<missing>"

  /// Support structural equality      
  override x.GetHashCode() = 
    match box x.PresentOrDefault with null -> 0 | o -> o.GetHashCode()

  /// Support structural equality      
  override x.Equals(y) =
    match y with 
    | null -> false
    | :? OptionalValue<'T> as y -> Object.Equals(x.PresentOrDefault, y.PresentOrDefault)
    | _ -> false
   
/// Non-generic type that makes it easier to create `OptionalValue<T>` values
/// from C# by benefiting the type inference for generic method invocations.
type OptionalValue =
  /// Creates an `OptionalValue<T>` from a nullable value of type `T?`
  [<CompilerMessage("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member OfNullable(v:Nullable<'T>) =  
    if v.HasValue then OptionalValue(v.Value) else OptionalValue<'T>.Missing
  
  /// Creates an `OptionalValue<'T>` that contains a value `v`
  [<CompilerMessage("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Create(v) = 
    OptionalValue(v)
  
  /// Creates an `OptionalValue<'T>` that does not contain a value
  [<CompilerMessage("This method is not intended for use from F#.", 10001, IsHidden=true, IsError=false)>]
  static member Empty<'T>() = OptionalValue.Missing


/// A type alias for the `OptionalValue<T>` type. The type alias can be used
/// to make F# type definitions that use optional values directly more succinct.
type 'T opt = OptionalValue<'T>



// --------------------------------------------------------------------------------------
// OptionalValue module (to be used from F#)
// --------------------------------------------------------------------------------------

/// Extension methods for working with optional values from C#. These make
/// it easier to provide default values and convert optional values to 
/// `Nullable` (when the contained value is value type)
[<Extension>]
type OptionalValueExtensions =
  
  /// Extension method that converts optional value containing a value type
  /// to a C# friendly `Nullable<T>` or `T?` type.
  [<Extension>]
  static member AsNullable(opt:OptionalValue<'T>) = 
    if opt.IsPresent then Nullable(opt.Present) else Nullable()

  /// Extension method that returns value in the specified optional value
  /// or the provided default value (the second argument).
  [<Extension>]
  static member OrDefault(opt:OptionalValue<'T>, defaultValue) = 
    if opt.IsPresent then opt.Present else defaultValue

/// Provides various helper functions for using the `OptionalValue<T>` type from F#
/// (The functions are similar to those in the standard `Option` module).
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OptionalValue = 

  /// If the `OptionalValue<T>` does not contain a value, then returns a new 
  /// `OptionalValue<R>.Empty`. Otherwise, returns the result of applying the 
  /// function `f` to the value contained in the provided optional value.
  [<CompiledName("Bind")>]
  let inline bind f (input:OptionalValue<'T>) : OptionalValue<'R> = 
    if input.IsPresent then f input.Present
    else OptionalValue<'R>.Missing

  /// If the `OptionalValue<T>` does not contain a value, then returns a new 
  /// `OptionalValue<R>.Empty`. Otherwise, returns the result `OptionalValue<R>`
  /// containing the result of applying the function `f` to the value contained 
  /// in the provided optional value.
  [<CompiledName("Map")>]
  let inline map f (input:OptionalValue<'T>) : OptionalValue<'R> = 
    if input.IsPresent then OptionalValue(f input.Present)
    else OptionalValue<'R>.Missing

  /// Creates `OptionalValue<T>` from a tuple of type `bool * 'T`. This function
  /// can be used with .NET methods that use `out` arguments. For example:
  ///
  ///     Int32.TryParse("42") |> OptionalValue.ofTuple
  ///
  [<CompiledName("OfTuple")>]
  let inline ofTuple (b, value:'T) : 'T opt =
    if b then OptionalValue(value) else OptionalValue<'T>.Missing

  /// Creates `OptionalValue<T>` from a .NET `Nullable<T>` type.
  [<CompiledName("OfNullable")>]
  let inline ofNullable (value:Nullable<'T>) : 'T opt =
    if value.HasValue then OptionalValue(value.Value) else OptionalValue<'T>.Missing

  /// Turns the `OptionalValue<T>` into a corresponding standard F# `option<T>` value
  let inline asOption (value:'T opt) = 
    if value.IsPresent then Some value.Present else None

  /// Turns a standard F# `option<T>` value into a corresponding `OptionalValue<T>`
  let inline ofOption (opt:option<'T>) : 'T opt = 
    match opt with
    | None -> OptionalValue<'T>.Missing
    | Some v -> OptionalValue(v)

  /// Complete active pattern that can be used to pattern match on `OptionalValue<T>`.
  /// For example:
  ///
  ///     let optVal = OptionalValue(42)
  ///     match optVal with
  ///     | OptionalValue.Missing -> printfn "Empty"
  ///     | OptionalValue.Present(v) -> printfn "Contains %d" v
  ///
  let (|Missing|Present|) (optional:'T opt) =
    if optional.IsPresent then Present(optional.Present)
    else Missing

  /// Get the value stored in the specified optional value. If a value is not
  /// available, throws an exception. (This is equivalent to the `Value` property)
  let inline get (optional:'T opt) = optional.Present


module LanguagePrimitives =
  let inline GenericMissing():'T= 
    let ty = typeof<'T>
    let t = Nullable<float>(1.0)
    match ty with
    | t when t = typeof<Double> -> box Double.NaN :?> 'T
    | _ -> Unchecked.defaultof<'T>
  