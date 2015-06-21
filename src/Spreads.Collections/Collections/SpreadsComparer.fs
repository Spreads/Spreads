namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Diagnostics

open Spreads

[<SerializableAttribute>]
[<AbstractClass>]
type BaseSpreadsComparer<'K when 'K : comparison>() =
  abstract Compare : a:'K * b:'K -> int
  abstract Diff : a:'K * b:'K -> int
  abstract Add : 'K * diff:int -> 'K
  abstract Hash: k:'K -> 'K
  abstract AsUInt64: k:'K -> uint64
  abstract FromUInt64: uint64 -> 'K

  interface IKeyComparer<'K> with
    member x.Compare(a,b) = x.Diff(a,b)
    member x.Diff(a,b) = x.Diff(a,b)
    member x.Add(a,diff) = x.Add(a, diff)
    member x.Hash(k) = x.Hash(k)
    member x.AsUInt64(k) = x.AsUInt64(k)
    member x.FromUInt64(value) = x.FromUInt64(value)

[<Sealed>]
type SpreadsComparerInt64(bucketSize:uint16) =
  inherit BaseSpreadsComparer<int64>()
  do 
    if bucketSize <= 0us then raise (ArgumentOutOfRangeException("bucketSize"))

  member x.BucketSize with get () = bucketSize
  
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = int <| a - b
  override x.Add(a,diff) = a + (int64 diff)
  override x.Hash(k) = 0L //(k / int64(bucketSize)) * int64(bucketSize)
  override x.AsUInt64(k) = uint64 k
  override x.FromUInt64(value) = int64 value

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt64 as sc -> 
      x.BucketSize.Equals(sc.BucketSize)
    | _ -> false
  override x.GetHashCode() = int bucketSize

  new() = SpreadsComparerInt64(1000us)

[<Sealed>]
type SpreadsComparerInt64U(bucketSize:uint16) =
  inherit BaseSpreadsComparer<uint64>()
  do 
    if bucketSize <= 0us then raise (ArgumentOutOfRangeException("bucketSize"))

  member x.BucketSize with get () = bucketSize
  
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = int <| a - b
  override x.Add(a,diff) = a + (uint64 diff)
  override x.Hash(k) = (k / uint64(bucketSize)) * uint64(bucketSize)
  override x.AsUInt64(k) = k
  override x.FromUInt64(value) = value

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt64U as sc -> 
      x.BucketSize.Equals(sc.BucketSize)
    | _ -> false
  override x.GetHashCode() = int bucketSize

  new() = SpreadsComparerInt64U(1000us)

[<Sealed>]
type SpreadsComparerInt32(bucketSize:uint16) =
  inherit BaseSpreadsComparer<int32>()
  do 
    if bucketSize <= 0us then raise (ArgumentOutOfRangeException("bucketSize"))

  member x.BucketSize with get () = bucketSize
  
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = int <| a - b
  override x.Add(a,diff) = a + (int32 diff)
  override x.Hash(k) = (k / int32(bucketSize)) * int32(bucketSize)
  override x.AsUInt64(k) = uint64 k
  override x.FromUInt64(value) = int value

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt32 as sc -> 
      x.BucketSize.Equals(sc.BucketSize)
    | _ -> false
  override x.GetHashCode() = int bucketSize

  new() = SpreadsComparerInt32(1000us)

[<Sealed>]
type SpreadsComparerInt32U(bucketSize:uint16) =
  inherit BaseSpreadsComparer<uint32>()
  do 
    if bucketSize <= 0us then raise (ArgumentOutOfRangeException("bucketSize"))

  member x.BucketSize with get () = bucketSize
  
  override x.Compare(a,b) = a.CompareTo(b)
  override x.Diff(a,b) = int <| a - b
  override x.Add(a,diff) = a + (uint32 diff)
  override x.Hash(k) = (k / uint32(bucketSize)) * uint32(bucketSize)
  override x.AsUInt64(k) = uint64 k
  override x.FromUInt64(value) = uint32 value

  override x.Equals(y) =
    match y with 
    | :? SpreadsComparerInt32U as sc -> 
      x.BucketSize.Equals(sc.BucketSize)
    | _ -> false
  override x.GetHashCode() = int bucketSize

  new() = SpreadsComparerInt32U(1000us)