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


module internal KeyHelper = 
  open System.Diagnostics
  
  // repo of IDCs by 
  let diffCalculators = new Dictionary<Type, obj>()
  
  // dc only available for certain types, if we have it, then 
  // for sorted keys the condition defines dc and regularity TODO (doc)
  let isRegular<'K when 'K:comparison> (sortedArray:'K[]) (size:int) (dc:IKeyComparer<'K>) = 
    let lastOffset = size - 1
    Debug.Assert(sortedArray.Length >= size)
    dc.Diff(sortedArray.[lastOffset], sortedArray.[0])  = lastOffset

  let willRemainRegular<'K  when 'K:comparison> (start:'K) (size:int) (dc:IKeyComparer<'K>) (newValue:'K) : bool = 
    if size = 0 then true
    else 
      //      0 || 4    1,2,3
      dc.Diff(newValue, start) = size || dc.Diff(newValue, start) = -1

  let toArray<'K  when 'K:comparison> (start:'K) (size:int) (length:int) (dc:IKeyComparer<'K>) : 'K[] =
    Array.init length (fun i -> if i < size then dc.Add(start, i+1) else Unchecked.defaultof<'K>)
