namespace Spreads.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Diagnostics

open Spreads

/// Generates a hash that is monotonically non-decreasing function of a key.
/// The hashes are used as bucket keys and should be a 
/// http://en.wikipedia.org/wiki/Monotonic_function
[<SerializableAttribute>]
type IMonotonicHasher<'K when 'K : comparison> =   
  abstract Hash: k:'K -> 'K

[<SerializableAttribute>]
[<AbstractClass>]
type BaseMonotonicHasher<'K when 'K : comparison>() =
  abstract Hash: k:'K -> 'K
  interface IMonotonicHasher<'K> with
    member this.Hash(k) = this.Hash(k)

[<Sealed>]
type Int64MonotonicHasher(bucketSize:uint16) =
  inherit BaseMonotonicHasher<int64>()
  do 
    if bucketSize <= 0us then raise (ArgumentOutOfRangeException("bucketSize"))
  let bucketSize = bucketSize
  override this.Hash(k) = (k / int64(bucketSize)) 

  interface IMonotonicHasher<int64> with
    member this.Hash(k) = this.Hash(k)
  new() = Int64MonotonicHasher(1024us)

[<Sealed>]
type Int32MonotonicHasher(bucketSize:uint16)=
  inherit BaseMonotonicHasher<int32>()
  do 
    if bucketSize <= 0us then raise (ArgumentOutOfRangeException("bucketSize"))
  override this.Hash(k) = (k / int32(bucketSize)) 
  new() = Int32MonotonicHasher(1024us)


type DateTimeMonotonicHasher(unitPeriod:UnitPeriod, periodLength:int)=
  inherit BaseMonotonicHasher<DateTime>()
  let bucketHash (dt:DateTime): DateTime =
    match unitPeriod with
    | UnitPeriod.Tick -> DateTime(dt.Ticks / (60000L * (int64 periodLength)))
    | UnitPeriod.Millisecond ->
      // group by minute; 60000 ms in a minute
      dt.AddSeconds(float -dt.Second).AddMilliseconds(float -dt.Millisecond)
    | UnitPeriod.Second -> 
      // milliseconds must be zero in secondly data
      Trace.Assert(dt.Millisecond = 0)
      // group by hour; 3600 seconds in an hour
      dt.AddMinutes(float -dt.Minute).AddSeconds(float -dt.Second)
    | UnitPeriod.Minute ->
      // seconds must be zero in minutes data
      Trace.Assert(dt.Second = 0 && dt.Millisecond = 0)
      // group by hour; 3600 seconds in an hour
      dt.Date.AddHours (float dt.Hour)
    | UnitPeriod.Hour ->
      Trace.Assert(dt.Minute = 0 && dt.Second = 0 && dt.Millisecond = 0)
      // group by month, max 744 hours in a month
      dt.Date.AddDays(float -dt.Day)
    | UnitPeriod.Day -> 
      Trace.Assert(dt.Hour = 0 && dt.Minute = 0 && dt.Second = 0 && dt.Millisecond = 0)
      // group by month
      dt.Date.AddDays(float -dt.Day)
    | _ ->
      Trace.Assert(dt.Day = 0 && dt.Hour = 0 && dt.Minute = 0 && dt.Second = 0 && dt.Millisecond = 0)
      // months are all in one place
      DateTime(1900,1,1,0,0,0, dt.Kind)
  override this.Hash(k) = bucketHash k

type DateTimeOffsetMonotonicHasher(unitPeriod:UnitPeriod, periodLength:int)=
  inherit BaseMonotonicHasher<DateTimeOffset>()
  let bucketHash (dto:DateTimeOffset): DateTimeOffset =
    let dt = dto.UtcDateTime
    let dtHash = 
      match unitPeriod with
      | UnitPeriod.Tick -> DateTime(dt.Ticks / (60000L * (int64 periodLength)))
      | UnitPeriod.Millisecond ->
        // group by minute; 60000 ms in a minute
        dt.AddSeconds(float -dt.Second).AddMilliseconds(float -dt.Millisecond)
      | UnitPeriod.Second -> 
        // milliseconds must be zero in secondly data
        Trace.Assert(dt.Millisecond = 0)
        // group by hour; 3600 seconds in an hour
        dt.AddMinutes(float -dt.Minute).AddSeconds(float -dt.Second)
      | UnitPeriod.Minute ->
        // seconds must be zero in minutes data
        Trace.Assert(dt.Second = 0 && dt.Millisecond = 0)
        // group by hour; 3600 seconds in an hour
        dt.Date.AddHours (float dt.Hour)
      | UnitPeriod.Hour ->
        Trace.Assert(dt.Minute = 0 && dt.Second = 0 && dt.Millisecond = 0)
        // group by month, max 744 hours in a month
        dt.Date.AddDays(float -dt.Day)
      | UnitPeriod.Day -> 
        Trace.Assert(dt.Hour = 0 && dt.Minute = 0 && dt.Second = 0 && dt.Millisecond = 0)
        // group by month
        dt.Date.AddDays(float -dt.Day)
      | _ ->
        Trace.Assert(dt.Day = 0 && dt.Hour = 0 && dt.Minute = 0 && dt.Second = 0 && dt.Millisecond = 0)
        // months are all in one place
        DateTime(1900,1,1,0,0,0, dt.Kind)
    DateTimeOffset(dt)
  override this.Hash(k) = bucketHash k

type TimePeriodMonotonicHasher() =
  interface IMonotonicHasher<TimePeriod> with
    member this.Hash(k) = TimePeriod.Hash(k)