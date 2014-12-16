namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Diagnostics

/// Base unit of TimePeriod
type UnitPeriod =
  | Tick = 0          //               100 nanosec
  | Millisecond = 1   //              10 000 ticks
  | Second = 2        //          10 000 000 ticks
  | Minute = 3        //         600 000 000 ticks
  | Hour = 4          //      36 000 000 000 ticks
  | Day = 5           //     864 000 000 000 ticks
  | Month = 6         //                  Variable
  | Eternity = 7      //                  Infinity


[<CustomComparison;CustomEquality>]
type TimePeriod =
  struct 
    val internal value : int64
    internal new(value:int64) = {value = value}
  end
  override x.Equals(yobj) =
    match yobj with
    | :? TimePeriod as y -> (x.value = y.value)
    | _ -> false
  override x.GetHashCode() = x.value.GetHashCode()
  override x.ToString() = x.value.ToString()
  interface System.IComparable<TimePeriod> with
    member x.CompareTo y = x.value.CompareTo(y.value)
  interface System.IComparable with
    member x.CompareTo other = 
      match other with
      | :? TimePeriod as y -> x.value.CompareTo(y.value)
      | _ -> invalidArg "other" "Cannot compare values of different types"

  static member op_Explicit(value:int64) : TimePeriod =  TimePeriod(value)
  static member op_Explicit(timePeriod:TimePeriod) : int64  = timePeriod.value
  

module internal TimePeriodModule =
  //#region Constants
  /// Ticks of DateTime(1900, 1, 1)
  [<Literal>]
  let zeroTicks = 599266080000000000L
  [<Literal>]
  let ticksPerMillisecond = 10000L
  [<Literal>]
  let ticksPerSecond = 10000000L
  [<Literal>]
  let ticksPerMinute = 600000000L
  [<Literal>]
  let ticksPerHour = 36000000000L
  [<Literal>]
  let ticksPerDay = 864000000000L

  [<Literal>]
  let msecPerDay = 86400000L
  [<Literal>]
  let msecPerHour = 3600000L
  [<Literal>]
  let msecPerMinute = 60000L
  [<Literal>]
  let msecPerSec = 1000L

  [<Literal>]
  let msecOffset = 6
  [<Literal>]
  let daysOffset = 33
  [<Literal>]
  let monthsOffset = 38
  [<Literal>]
  let lengthOffset = 50
  [<Literal>]
  let unitPeriodOffset = 60
  [<Literal>]
  let tickOffset = 6

  [<Literal>]
  let ticksMask = 9223372036854775744L // ((1L <<< 57) - 1L) <<< 6
  [<Literal>]
  let msecMask = 8589934528L // ((1L <<< 27)  - 1L) <<< 6
  [<Literal>]
  let msecMaskWithUnused = 8589934591L // ((1L <<< (27+6))  - 1L)
  [<Literal>]
  let daysMask = 266287972352L // ((1L <<< 5) - 1L) <<< 33
  [<Literal>]
  let monthsMask =  1125625028935680L // ((1L <<< 12) - 1L) <<< 38
  [<Literal>]
  let lengthMask = 1151795604700004352L // ((1L <<< 10) -1L) <<< 50
  [<Literal>]
  let unitPeriodMask = 8070450532247928832L // ((1L <<< 3) - 1L) <<< 60
  
  //#endregion
   
  let inline getMsecInDay value = (value &&& msecMask) >>> msecOffset
  let inline setMsecInDay msecs value = 
    (msecs <<< msecOffset) ||| (value &&& ~~~msecMask)

  let inline getDays value = (value &&& daysMask) >>> daysOffset
  let inline setDays days value = (days <<< daysOffset) ||| (value &&& ~~~daysMask)

  // 0 based as count from 1/1/1900 as 0
  let inline getMonths value = (value &&& monthsMask) >>> monthsOffset
  let inline setMonths months value = (months <<< monthsOffset) ||| (value &&& ~~~monthsMask)

  let inline getLength value = (value &&& lengthMask) >>> lengthOffset
  let inline setLength length value = (length <<< lengthOffset) ||| (value &&& ~~~lengthMask)

  let inline isTick (value) : bool = (1L = (value >>> 63))
  let inline getTicks (value) = (value &&& ticksMask) >>> tickOffset
  let inline setTicks ticks value = (ticks <<< tickOffset) ||| (value &&& ~~~ticksMask)

  let inline getUnitPeriod value = 
    if isTick value then 0L
    else (value &&& unitPeriodMask) >>> unitPeriodOffset
  let inline setUnitPeriod unitPeriod value = 
    if isTick value then value
    else (unitPeriod <<< unitPeriodOffset) ||| (value &&& ~~~unitPeriodMask) 


  let inline isActual value : bool = (value &&& 1L) = 1L
  let inline getPeriod value = (value &&& (lengthMask ||| unitPeriodMask )) >>> lengthOffset
  let inline setPeriod period value = (period <<< lengthOffset) ||| (value &&& ~~~(lengthMask ||| unitPeriodMask ))


  let inline milliseconds (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    getMsecInDay(tpv) % 1000L
  let inline seconds (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    (getMsecInDay(tpv) / msecPerSec) % 60L
  let inline minutes (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    (getMsecInDay(tpv) / msecPerMinute) % 60L
  let inline hours (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv) && getMsecInDay(tpv) < msecPerDay)
    (getMsecInDay(tpv) / msecPerHour) % 24L
  let inline days (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    (getDays(tpv)) + 1L
  // 1 based like in calendar
  let inline months (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    (getMonths(tpv) % 12L) + 1L
  let inline years (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    (getMonths(tpv) / 12L) + 1900L
  let inline length (tpv:int64) : int64 = 
    Debug.Assert(not (isTick tpv))
    getLength(tpv)
  let inline unitPeriod (tpv:int64) : UnitPeriod =
    LanguagePrimitives.EnumOfValue <| int (getUnitPeriod tpv)

  /// Assume that inputs are not checked for logic
  let inline ofPartsUnsafe // TODO make a copy of it ofPartsSafe with arg checks and use it in the ctors.
    (unitPeriod:UnitPeriod) (length:int) 
    (year:int) (month:int) (day:int) 
    (hour:int) (minute:int) (second:int) (millisecond:int) : int64 =
      match unitPeriod with
      | UnitPeriod.Tick -> 
        let mutable value : int64 = 1L <<< 63
        value <- value |> setTicks (DateTime(year, month, day, hour, minute, second, millisecond).Ticks - zeroTicks)
        value
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setMonths (int64((year - 1900) * 12 + (month-1)))
        value <- value |> setDays (int64(day - 1))
        let millisInDay = 
          (int64 hour) * msecPerHour + (int64 minute) * msecPerMinute 
          + (int64 second) * msecPerSec + (int64 millisecond)
        value <- value |> setMsecInDay millisInDay
        value
        
  /// Convert datetime to TimePeriod with Windows built-in time zone infos
  /// Windows updates TZ info with OS update patches, could also use NodaTime for this
  let inline ofStartDateTimeWithZoneUnsafe (unitPeriod:UnitPeriod) (length:int)  (startDate:DateTime) (tzi:TimeZoneInfo) =
    // number of 30 minutes intervals, with 24 = UTC/zero offset
    // TODO test with India
    let startDtUtc =  DateTimeOffset(startDate,tzi.GetUtcOffset(startDate))
    match unitPeriod with
      | UnitPeriod.Tick -> 
        let mutable value : int64 = 1L <<< 63
        value <- value |> setTicks (startDtUtc.Ticks - zeroTicks)
        value
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setMonths (int64 <| (startDtUtc.Year - 1900) * 12 + (startDtUtc.Month - 1))
        value <- value |> setDays (int64 <| startDtUtc.Day - 1)
        value <- value |> setMsecInDay (int64 <| startDtUtc.TimeOfDay.TotalMilliseconds)
        value

  let inline ofStartDateTimeOffset (unitPeriod:UnitPeriod) (length:int)  (startDto:DateTimeOffset) =
    match unitPeriod with
      | UnitPeriod.Tick -> 
        let mutable value : int64 = 1L <<< 63
        value <- value |> setTicks (startDto.UtcTicks - zeroTicks)
        value
      | _ ->
        let mutable value : int64 = 0L
        value <- value |> setUnitPeriod (int64 unitPeriod)
        value <- value |> setLength (int64 length)
        value <- value |> setMonths (int64 <| (startDto.UtcDateTime.Year - 1900) * 12 + (startDto.UtcDateTime.Month - 1))
        value <- value |> setDays (int64 <| startDto.UtcDateTime.Day - 1)
        value <- value |> setMsecInDay (int64 <| startDto.UtcDateTime.TimeOfDay.TotalMilliseconds)
        value
            
    

  let inline addPeriods (numPeriods:int64) (tpv:int64) : int64 =
    if numPeriods = 0L then tpv
    else
      let unit = unitPeriod tpv
      let len = getLength tpv
      let addDay (numDays:int64) (tpv':int64) : int64 =
        let date = DateTime(int <| years tpv', int <|months tpv', int <| days tpv').AddDays(float numDays)
        let withDays = setDays (int64 <| date.Day - 1) tpv'
        setMonths (int64 <| (date.Year - 1900) * 12 + (date.Month - 1)) withDays
      let addIntraDay (numPeriods':int64) (tpv':int64) (multiple:int64) : int64 =
        let msecs = (getMsecInDay tpv') + numPeriods'*len*multiple
        if msecs >= msecPerDay then
          let msecsInDay = msecs % msecPerDay
          let numDays = msecs / msecPerDay
          let withMsecs = setMsecInDay msecsInDay tpv'
          addDay numDays withMsecs
        else if msecs < 0L then
          let msecsInDay = msecPerDay + (msecs % msecPerDay) // last part is negative, so (+)
          let numDays = (msecs / msecPerDay) - 1L
          let withMsecs = setMsecInDay msecsInDay tpv'
          addDay numDays withMsecs
        else
          setMsecInDay msecs tpv'
      match unit with
      | UnitPeriod.Tick -> 
        let ticks = (getTicks tpv) + numPeriods
        setTicks ticks tpv
      | UnitPeriod.Millisecond -> addIntraDay numPeriods tpv 1L
      | UnitPeriod.Second -> addIntraDay numPeriods tpv msecPerSec
      | UnitPeriod.Minute -> addIntraDay numPeriods tpv msecPerMinute
      | UnitPeriod.Hour -> addIntraDay numPeriods tpv msecPerHour     
      | UnitPeriod.Day -> addDay numPeriods tpv
      | UnitPeriod.Month ->
          let months = (getMonths tpv) + len * numPeriods
          setMonths months tpv
      | UnitPeriod.Eternity ->  tpv
      | _ -> failwith "wrong unit period, never hit this"

  let inline periodStart (tpv:int64) : DateTimeOffset =
    if isTick tpv then DateTimeOffset(getTicks tpv, TimeSpan.Zero)
    else 
      DateTimeOffset(
        int <| years tpv, 
        int <| months tpv, 
        int <| days tpv,0,0,0,0,
        TimeSpan.Zero).AddMilliseconds(float <| getMsecInDay tpv)

  /// period end is the start of the next period, exclusive (epsilon to the start of the next period)
  let inline periodEnd (tpv:int64) : DateTimeOffset =
    if isTick tpv then DateTimeOffset(getTicks tpv, TimeSpan.Zero)
    else 
      periodStart (addPeriods 1L tpv)

  let inline timeSpan (tpv:int64) : TimeSpan =
    if isTick tpv then TimeSpan(1L)
    else TimeSpan((periodEnd tpv).Ticks - (periodStart tpv).Ticks)

  // the bigger a period the less important grouping becomes for a single series (but still needed if there are many short series)
  // because the total number of points is limited (e.g. in 10 years there are 86,400 hours ~ 10,800 buckets by 8 trading hours or 3,600 buckets by 24 hours)
  // and the bigger bucket density should be expected
  // 100 000 buckets take 2.8 Mb (key + pointer + array overhead per bucket)
  // 1Gb of data = 100 Mn items in buckets (10 bytes per item: 2 key + 8 value)
  // 1Gb ~ 10 seconds of data per every tick, 1526 buckets 43kb
  // 1Gb ~ 28 hours of millisecondly data, 1667 buckets 47kb
  // 1Gb ~ 38 months of secondly data, 27778 buckets 0.78Mb
  // 1Gb ~ 192 years of minutely data, 69444 buckets 1.94Mb
  let inline bucketHash (tpv:int64) (targetUnit:UnitPeriod): int64 =
    let periodOnly tpv' = (tpv' &&& (~~~(msecMask ||| daysMask ||| monthsMask)))
    let originalUnit = unitPeriod tpv
    if targetUnit < originalUnit then invalidOp "Cannot map period to smaller base periods that original"
    if targetUnit > originalUnit then 
      Printf.sprintf "%s" ("target" + (int targetUnit).ToString()) 
      Printf.sprintf "%s" ("originalUnit" +  (int originalUnit).ToString()) 
      raise (NotImplementedException("TODO targetUnit > originalUnit"))
    match targetUnit with
    | UnitPeriod.Tick ->
      // group by some large number of ticks
      ((( (tpv &&& ticksMask) >>> 6) / 60000L) <<< 6) ||| (1L <<< 63)
    | UnitPeriod.Millisecond ->
      // group by minute; 60000 ms in a minute
      (tpv &&& ~~~msecMaskWithUnused) ||| ( (getMsecInDay tpv)/(msecPerMinute)  <<< msecOffset )
    | UnitPeriod.Second ->
      // group by hour; 3600 seconds in an hour
      (tpv &&& ~~~msecMaskWithUnused) ||| ( (getMsecInDay tpv)/(msecPerHour) <<< msecOffset )
    | UnitPeriod.Minute ->
      // group by day; 1440 minutes in a day
      (tpv &&& ~~~msecMaskWithUnused) // NB: different with Hours because period bits are different
    | UnitPeriod.Hour ->
      // group by month, max 744 hours in a month
      (tpv &&& ~~~(msecMaskWithUnused ||| daysMask))
    | UnitPeriod.Day -> 
      // group by month
      (tpv >>> monthsOffset) <<< monthsOffset
    | _ -> 
      // months are all in one place
      periodOnly (tpv)
  let addressSubIndex (tpv:int64) (targetUnit:UnitPeriod): uint16 =
    let originalUnit = unitPeriod tpv
    if targetUnit < originalUnit then invalidOp "Cannot map period to smaller base periods that original"
    if targetUnit > originalUnit then raise (NotImplementedException("TODO"))
    match targetUnit with
    | UnitPeriod.Tick ->
      uint16 <| ((tpv >>> 6) &&& ((1L <<< 16) - 1L))
    | UnitPeriod.Millisecond ->
      // group by minute; 60000 ms in a minute
      // msec in a minute
      uint16 <| ((getMsecInDay tpv) % (msecPerMinute)) 
    | UnitPeriod.Second ->
      // second in hour
      uint16 <| ((getMsecInDay tpv) % (msecPerHour))/msecPerSec
    | UnitPeriod.Minute ->
      // minute in a day
      uint16 <| (getMsecInDay tpv)/(msecPerMinute)
    | UnitPeriod.Hour ->
      // hour in a month
      uint16 <| ((getMsecInDay tpv)/(msecPerHour) + (getDays tpv) * 24L )
    | UnitPeriod.Day ->
      // day in a month
      uint16 <| days tpv
    | _ -> 
      // months are all in one place
      uint16 (months tpv)

  let addressToTimePeriodValue (bucket:int64) (subIndex:uint16) : int64 =
    let unit = unitPeriod bucket
    let sub = int64 subIndex
    match unit with
    | UnitPeriod.Tick -> ((bucket >>> 6) + sub) <<< 6
    | UnitPeriod.Millisecond -> ((bucket >>> 6) + sub) <<< 6
    | UnitPeriod.Second -> ((bucket >>> 6) + sub * msecPerSec) <<< 6
    | UnitPeriod.Minute -> ((bucket >>> 6) + sub * msecPerMinute) <<< 6
    | UnitPeriod.Hour -> setDays (sub/24L) ((bucket >>> 6) + (sub % 24L) * msecPerHour) <<< 6
    | UnitPeriod.Day -> ((bucket >>> daysOffset) + sub) <<< daysOffset
    | _ -> ((bucket >>> monthsOffset) + sub) <<< monthsOffset


open TimePeriodModule


type TimePeriod with
  member this.Period with get(): UnitPeriod * int = unitPeriod this.value, int <| length this.value
  member this.Start with get(): DateTimeOffset = periodStart this.value
  member this.End with get() : DateTimeOffset = periodEnd this.value
  member this.TimeSpan with get() : TimeSpan = timeSpan this.value
  member this.Next with get() : TimePeriod = TimePeriod(addPeriods 1L this.value)
  member this.Previous with get() : TimePeriod = TimePeriod(addPeriods -1L this.value)

  static member op_Explicit(timePeriod:TimePeriod) : DateTimeOffset = timePeriod.Start
  static member op_Explicit(timePeriod:TimePeriod) : DateTime = timePeriod.Start.DateTime

  static member Hash(tp:TimePeriod) = TimePeriod(bucketHash (tp.value) (unitPeriod (tp.value)))
  static member SubKey(tp:TimePeriod) = addressSubIndex (tp.value) (unitPeriod (tp.value))
  static member Key(bucket:TimePeriod, subIndex:uint16) = TimePeriod(addressToTimePeriodValue (bucket.value) subIndex)

  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startTime:DateTimeOffset) =
    {value =
      ofStartDateTimeOffset unitPeriod (int numberOfUnitPeriods) startTime}
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startTime:DateTime, tzi:TimeZoneInfo) =
    {value =
      ofStartDateTimeWithZoneUnsafe unitPeriod (int numberOfUnitPeriods) startTime tzi}

  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int, startMinute:int, startSecond:int, startMillisecond:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour startMinute startSecond startMillisecond
        }

  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int, startMinute:int, startSecond:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour startMinute startSecond 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int, startMinute:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour startMinute 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int, 
      startHour:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay startHour 0 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int, startDay:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth startDay 0 0 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int, startMonth:int) =
        {value =
          ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear startMonth 0 0 0 0 0
        }
  /// Read this as "numberOfUnitPeriods unitPeriods started on startTime",
  /// as in financial statements: "for 12 months started on 12/31/2015"
  new(unitPeriod:UnitPeriod, numberOfUnitPeriods:int, startYear:int) =
    {value =
      ofPartsUnsafe unitPeriod (int numberOfUnitPeriods) startYear 0 0 0 0 0 0
    }




