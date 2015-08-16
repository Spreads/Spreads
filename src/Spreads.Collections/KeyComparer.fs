namespace Spreads

open System
open System.Collections.Concurrent
open System.Collections.Generic

/// <summary>
/// IComparer<'K> with additional methods Diff and Add for regular keys
/// </summary>
[<AllowNullLiteral>]
type IKeyComparer<'K> =
  inherit IComparer<'K>
  /// Returns int64 distance between two values when they are stored in 
  /// a regular sorted map. Regular means continuous integers or days or seconds, etc.
  /// ## Remarks
  /// This method could be used for IComparer<'K>.Compare implementation, 
  /// but must be checked for int overflow (e.g. compare Diff result to 0L instead of int cast).
  abstract Diff : a:'K * b:'K -> int64
  /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
  abstract Add : 'K * diff:int64 -> 'K

[<AllowNullLiteral>]
type internal IKeySlicer<'K> =
  /// Generates an order-preserving hash.
  /// The hashes are used as bucket keys and should be a 
  /// http://en.wikipedia.org/wiki/Monotonic_function
  abstract Hash: k:'K -> 'K


[<SerializableAttribute>]
[<AbstractClass>]
type BaseSpreadsComparer<'K>() =
  abstract Compare : a:'K * b:'K -> int
  abstract Diff : a:'K * b:'K -> int64
  abstract Add : 'K * diff:int64 -> 'K

  interface IKeyComparer<'K> with
    member x.Compare(a,b) = x.Compare(a,b)
    member x.Diff(a,b) = x.Diff(a,b)
    member x.Add(a,diff) = x.Add(a, diff)

[<Sealed>]
type internal SpreadsComparerInt64() =
  inherit BaseSpreadsComparer<int64>()
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) =  a - b
  override x.Add(a,diff) = a + (int64 diff)

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt64 as sc -> true
    | _ -> false
  override x.GetHashCode() = base.GetHashCode()

[<Sealed>]
type internal SpreadsComparerInt64U() =
  inherit BaseSpreadsComparer<uint64>()
  
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = int64 <| a - b
  override x.Add(a,diff) = a + (uint64 diff)

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt64U as sc -> true
    | _ -> false
  override x.GetHashCode() = base.GetHashCode()

[<Sealed>]
type internal SpreadsComparerInt32() =
  inherit BaseSpreadsComparer<int32>()

  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = int64 <| a - b
  override x.Add(a,diff) = a + (int32 diff)

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt32 as sc ->  true
    | _ -> false
  override x.GetHashCode() = base.GetHashCode()

[<Sealed>]
type internal SpreadsComparerInt32U() =
  inherit BaseSpreadsComparer<uint32>()
  
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = int64 <| a - b
  override x.Add(a,diff) = a + (uint32 diff)

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt32U as sc -> true
    | _ -> false
  override x.GetHashCode() = base.GetHashCode()

[<Sealed>]
type internal DateTimeComparer () =
  inherit BaseSpreadsComparer<DateTime>()
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = a.Ticks - b.Ticks
  override x.Add(a,diff) = a.AddTicks(diff)

  override x.Equals(y) =
    match y with 
    | :? DateTimeComparer as sc -> true 
    | _ -> false
  override x.GetHashCode() = base.GetHashCode()



type KeyComparer()=
  static let registeredComparers = ConcurrentDictionary<Type, obj>()
  static do
    registeredComparers.GetOrAdd(typeof<int64>, (fun _ -> 
      SpreadsComparerInt64() :> obj
    )) |> ignore
    registeredComparers.GetOrAdd(typeof<uint64>, (fun _ -> 
      SpreadsComparerInt64U()  :> obj
    )) |> ignore
    registeredComparers.GetOrAdd(typeof<int>, (fun _ -> 
      SpreadsComparerInt32()  :> obj
    )) |> ignore
    registeredComparers.GetOrAdd(typeof<uint32>, (fun _ -> 
      SpreadsComparerInt32U()  :> obj
    )) |> ignore
    registeredComparers.GetOrAdd(typeof<DateTime>, (fun _ -> 
      DateTimeComparer()  :> obj
    )) |> ignore
    // TODO add other known numeric types and DateTimeOffset
    ()

  // TODO (very low) IKeyComparable same as IComparable

  static member RegisterDefault(keyComparer:IKeyComparer<'K>) =
    registeredComparers.[typeof<'K>] <- keyComparer
  static member GetDefault<'K>() : IComparer<'K> =
    let ok, v = registeredComparers.TryGetValue(typeof<'K>)
    if ok then v :?> IComparer<'K> else Comparer<'K>.Default :> IComparer<'K>
