(*  
    Copyright (c) 2014-2015 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.
        
    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

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

  /// Even if a cursor doesn't have a special method for bulk processing, we accumulate OptimizationSettings.MinVectorLength batch and return it.
  static member val AlwaysBatch = false with get, set
  /// Microoptimizations such as struct cursor and call vs callvirt, avoid GC where possible
  static member val UseCLROptimizations = false with get, set
  /// Use native vectorized calculations if available
  static member val UseNativeOptimizations = false with get, set
  /// When possible, combine Filter() and Map() delegates
  static member val CombineFilterMapDelegates = true with get, set
  /// Minimum array length for which we could use native optimization (when p/invoke cost becomes less than gains from vectorized calcs)
  static member val MinVectorLengthForNative = 100 with get, set // TODO should probably be higher
  /// Default chunk length of SortedChunkedMap
  static member val SCMDefaultChunkLength = 4096 with get, set
  /// Generic array pool implementation. Default is GC.
  /// The extension project has an implementation for doubles, and the performance gain is quite visible in benchmarks.
  static member val ArrayPool =
    {new IArrayPool with
        member x.TakeBuffer<'T>(size) = Array.zeroCreate<'T> size
        member x.ReturnBuffer(buffer): int = 0
        member x.BorrowBuffer(buffer): int = 0
        member x.ReferenceCount(buffer) = 0
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
  abstract AddBatch<'K> : scalar:double * batch: IReadOnlyOrderedMap<'K,double> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,double>> -> bool
  abstract AddBatch<'K> : left: IReadOnlyOrderedMap<'K,double> * right: IReadOnlyOrderedMap<'K,double> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,double>> -> bool


[<SealedAttribute;AbstractClassAttribute>]
type VectorMathProvider() =
  /// An implementation of IVectorMathProvider that is used internally for vectorized calculations
  static member val Default = Unchecked.defaultof<IVectorMathProvider> with get, set





