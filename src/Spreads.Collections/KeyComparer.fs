namespace Spreads

open System
open System.Collections
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

/// <summary>
/// IComparer<'K> with additional methods for regular keys
/// </summary>
[<AllowNullLiteral>]
type internal IKeyComparer<'K>= // when 'K : comparison
  inherit IComparer<'K>
  /// Returns int32 distance between two values when they are stored in 
  /// a regular sorted map. Regular means continuous integers or days or seonds, etc.
  /// This method could be used for IComparer<'K>.Compare implementation.
  abstract Diff : a:'K * b:'K -> int
  /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
  abstract Add : 'K * diff:int -> 'K
  /// Generates an order-preserving hash.
  /// The hashes are used as bucket keys and should be a 
  /// http://en.wikipedia.org/wiki/Monotonic_function
  abstract Hash: k:'K -> 'K
  /// <summary>
  /// Get UInt64 representation of a key.
  /// In general, (a.AsUInt64 - b.AsUInt64) is not equal to ISpreadsComparer.Diff(a,b), e.g. for non-tick TimePeriod.
  /// </summary>
  abstract AsUInt64: k:'K -> uint64
  /// <summary>
  /// Get a key from its UInt64 representation.
  /// In general, (a.AsUInt64 - b.AsUInt64) is not equal to ISpreadsComparer.Diff(a,b), e.g. for non-tick TimePeriod.
  /// </summary>
  abstract FromUInt64: uint64 -> 'K


type internal KeyComparer()=
  static let registeredComparers = ConcurrentDictionary<Type, obj>()
  static do
    // TODO in static constructor load all imlementations of IKeyComparer and retrieve an instance for ty
    ()
  
  static member GetDefault<'K>() = Unchecked.defaultof<IKeyComparer<'K>>