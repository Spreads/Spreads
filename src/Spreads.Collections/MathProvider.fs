namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices


[<SealedAttribute;AbstractClassAttribute>]
type internal OptimizationSettings() =
  ///
  static member val UseCLROptimizations = true with get, set
  static member val UseNativeOptimizations = true with get, set
  /// Minimum array length for which we could native optimization (when p/invoke cost becomes less than gains from vectorized calcs)
  static member val NativeMinVectorLength = 100 with get, set


// It is tempting to use ILinearAlgebraProvider from MathNet.Numerics, but there are not so many members to wrap around it;
// avoid external binary dependencies in this project

// TODO remove from the collections project

/// Fast operations on numeric arrays
type IVectorMathProvider =
  /// 
  abstract AddVectors: x:'T[] * y:'T[] * result:'T[] -> unit


[<SealedAttribute;AbstractClassAttribute>]
type VectorMathProvider() =
  /// An implementation of IVectorMathProvider that is used internally for vectorized calculations
  static member val Default = Unchecked.defaultof<IVectorMathProvider> with get, set