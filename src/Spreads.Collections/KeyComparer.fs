namespace Spreads

open System
open System.Collections
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

/// <summary>
/// IComparer<'K> with additional methods Diff and Add for regular keys and Hash method for chunking
/// </summary>
[<AllowNullLiteral>]
type IKeyComparer<'K when 'K : comparison>=  
  inherit IComparer<'K>
  /// Returns int64 distance between two values when they are stored in 
  /// a regular sorted map. Regular means continuous integers or days or seconds, etc.
  /// This method could be used for IComparer<'K>.Compare implementation.
  abstract Diff : a:'K * b:'K -> int64
  /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
  abstract Add : 'K * diff:int64 -> 'K

[<AllowNullLiteral>]
type internal IKeySlicer<'K when 'K : comparison>=  
  /// Generates an order-preserving hash.
  /// The hashes are used as bucket keys and should be a 
  /// http://en.wikipedia.org/wiki/Monotonic_function
  abstract Hash: k:'K -> 'K


[<SerializableAttribute>]
[<AbstractClass>]
type BaseSpreadsComparer<'K when 'K : comparison>() =
  abstract Compare : a:'K * b:'K -> int
  abstract Diff : a:'K * b:'K -> int64
  abstract Add : 'K * diff:int64 -> 'K

  interface IKeyComparer<'K> with
    member x.Compare(a,b) = int (x.Diff(a,b))
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
  
//  static let ticksPer15min = TimeSpan.TicksPerHour/4L
//  static let ticksPer8hours = TimeSpan.TicksPerHour/4L // NB in UTC, FORST and NYSE are pretty well fit to be regular

  //member x.UnitPeriod with get () = unitPeriodOpt

//  static member AdaptiveDateTimeComparer(dateTimes:DateTime seq) =
//    let arr = dateTimes |> Seq.toArray
//    if arr.Length < 5 then raise (ArgumentException("dateTimes sequence must contain at least 5 elements"))
//    let mutable diffs : int64 = 0L
//    for i in 1..arr.Length-1 do
//      diffs <- diffs + (arr.[i]-arr.[i-1]).Ticks
//    diffs <- diffs/(int64 (arr.Length - 2))
//    let ts = TimeSpan.FromTicks(diffs)
//    if ts.TotalDays >= 28.0 then DateTimeComparer(OptionalValue(UnitPeriod.Month))
//    elif ts.TotalDays >= 1.0 then DateTimeComparer(OptionalValue(UnitPeriod.Day))
//    elif ts.TotalHours >= 1.0 then DateTimeComparer(OptionalValue(UnitPeriod.Hour))
//    elif ts.TotalMinutes >= 1.0 then DateTimeComparer(OptionalValue(UnitPeriod.Minute))
//    elif ts.TotalSeconds >= 1.0 then DateTimeComparer(OptionalValue(UnitPeriod.Second))
//    elif ts.TotalMilliseconds >= 1.0 then DateTimeComparer(OptionalValue(UnitPeriod.Millisecond))
//    else failwith "unsupported dateTimes sequence"

  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = a.Ticks - b.Ticks
  override x.Add(a,diff) = a.AddTicks(diff)
//  override x.Hash(k) = 
//    match unitPeriodOpt with
//    | OptionalValue.Missing -> failwith ""
//    | OptionalValue.Present(unitPeriod) ->
//      match unitPeriod with
//      | UnitPeriod.Tick | UnitPeriod.Millisecond  -> 
//        // round down to nearest second
//        k.AddTicks( - (k.Ticks % TimeSpan.TicksPerSecond));
//      | UnitPeriod.Second -> 
//        // round down to nearest 15 minutes
//        k.AddTicks( - (k.Ticks % ticksPer15min));
//      | UnitPeriod.Minute -> 
//        // round down to nearest 8 hours
//        k.AddTicks( - (k.Ticks % ticksPer8hours));
//      | UnitPeriod.Hour -> 
//        // round down to nearest month
//        new DateTime(k.Year, k.Month, 1, 0, 0, 0, k.Kind);
//      | UnitPeriod.Day -> 
//        // round down to nearest year
//        new DateTime(k.Year, 1, 1, 0, 0, 0, k.Kind);
//      | UnitPeriod.Month | UnitPeriod.Eternity -> DateTime.MinValue // all in one place
//      | _ -> failwith "unknown UnitPeriod"
//    // TODO adaptive comparer based on training set

  override x.Equals(y) =
    match y with 
    | :? DateTimeComparer as sc -> 
      // NB! not `x.UnitPeriod.Equals(sc.UnitPeriod)`
      // For all purposes other than Hash this is the same comparer
      // Hash is only relevant for persistence optimization, and once a series is persisted 
      // its unit period must be stored
      true 
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

  static member RegisterDefault(keyComparer:IKeyComparer<'K>) =
    registeredComparers.[typeof<'K>] <- keyComparer
  static member GetDefault<'K when 'K : comparison>() =
    let ok, v = registeredComparers.TryGetValue(typeof<'K>)
    if ok then v :?> IKeyComparer<'K> else  Unchecked.defaultof<IKeyComparer<'K>>
