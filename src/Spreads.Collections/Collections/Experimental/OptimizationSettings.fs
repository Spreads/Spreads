// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Experimantal
//
//open System
//open System.Collections
//open System.Collections.Generic
//open System.Threading
//open System.Threading.Tasks
//open System.Runtime.InteropServices
//open System.Diagnostics
//
//// TODO add config class to S.Extensions project, that class will init 
//// all extensions 
//
//// TODO rename to Settings and move to a separate file or to Core project
//[<SealedAttribute;AbstractClassAttribute>]
//type internal OptimizationSettings() =
//
//  /// Even if a cursor doesn't have a special method for bulk processing, we accumulate OptimizationSettings.MinVectorLength batch and return it.
//  static member val AlwaysBatch = false with get, set
//  /// Microoptimizations such as struct cursor and call vs callvirt, avoid GC where possible
//  static member val UseCLROptimizations = false with get, set
//  /// Use native vectorized calculations if available
//  static member val UseNativeOptimizations = false with get, set
//  /// When possible, combine Filter() and Map() delegates
//  static member val CombineFilterMapDelegates = true with get, set
//  /// Minimum array length for which we could use native optimization (when p/invoke cost becomes less than gains from vectorized calcs)
//  static member val MinVectorLengthForNative = 100 with get, set // TODO should probably be higher
//  /// Default chunk length of SortedChunkedMap
//  static member val SCMDefaultChunkLength = 4096 with get, set
//  /// Print detailed debug information to console
//  static member val Verbose = false with get, set
//  /// Conditional tracing when OptimizationSettings.Verbose is set to true
//  [<ConditionalAttribute("PRERELEASE")>]
//  static member TraceVerbose(message) = System.Diagnostics.Trace.WriteLineIf(OptimizationSettings.Verbose, message)





