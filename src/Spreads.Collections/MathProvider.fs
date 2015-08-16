namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

// TODO add config class to S.Extensions project, that class will init 
// all extensions 

[<SealedAttribute;AbstractClassAttribute>]
type internal OptimizationSettings() =
  // For my own sake, please do not do any optimization, add if block where appropriate and throw NotImplementedException()

  /// Even if a cursor doesn't have a special method for bulk processing, we accumulate OptimizationSettings.MinVectorLength batch
  /// and return it. This could help cache locality and calling inlined functions/methods directly inside CursorBinds, not chained via callcirt
  static member val AlwaysBatch = false with get, set
  /// Microoptimizations such as struct cursor and call vs callvirt, avoid GC where possible
  static member val UseCLROptimizations = false with get, set
  /// Use native vectorized calculations if available
  static member val UseNativeOptimizations = false with get, set
  /// Minimum array length for which we could use native optimization (when p/invoke cost becomes less than gains from vectorized calcs)
  static member val MinVectorLengthForNative = 100 with get, set
  /// Default chunk length of SortedChunkedMap
  static member val SCMDefaultChunkLength = 4096 with get, set
  /// Generic array pool implementation. Default is GC.
  /// The extension project has an implementation for doubles, and the perf gain is quite visible in benchmarks.
  static member val ArrayPool = 
    {new IArrayPool with
        member x.ReturnBuffer(arg1: 'T []): unit = ()       
        member x.TakeBuffer<'T>(size) = Array.zeroCreate<'T> size
        member x.Clear(): unit = ()
    } with get, set


// It is tempting to use ILinearAlgebraProvider from MathNet.Numerics, but there are not so many members to wrap around it;
// avoid external binary dependencies in this project

// TODO remove from the collections project

/// Fast operations on numeric arrays
type IVectorMathProvider =
  /// TODO use start and length since we will work with buffer of greater length than payload
  /// 
  abstract AddVectors: x:'T[] * y:'T[] * result:'T[] -> unit
  abstract MapBatch<'K,'V,'V2> : mapF:('V->'V2) * batch: IReadOnlyOrderedMap<'K,'V> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>> -> bool

[<SealedAttribute;AbstractClassAttribute>]
type VectorMathProvider() =
  /// An implementation of IVectorMathProvider that is used internally for vectorized calculations
  static member val Default = Unchecked.defaultof<IVectorMathProvider> with get, set





