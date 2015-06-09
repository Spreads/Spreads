namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

// It is tempting to use ILinearAlgebraProvider from MathNet.Numerics, but there are not so many member to wrap around it 
// do not want external binary dependencies in this project


/// Fast operations on numeric arrays
type IVectorMathProvider =
  /// 
  abstract IsSupportedType<'T> : unit -> bool
  abstract AddVectors: x:'T[] * y:'T[] * result:'T[] -> unit


[<SealedAttribute;AbstractClassAttribute>]
type VectorMathProvider() =
  /// An implementation of IVectorMathProvider that is used internally for vectorized calculations
  static member val Default = Unchecked.defaultof<IVectorMathProvider> with get, set