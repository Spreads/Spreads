namespace Spreads.Period

open System
open System.Collections
open System.Collections.Generic
open System.Linq
open System.Diagnostics

// main TODO - remove DateTimeOffset usage in constructor when supplying parts, it is a performance bottleneck

/// Resolution unit of TimePeriod
type Frequency = // compare to inverted nonlinear Hz,kHz,MHz etc
| Tick = 0
| Millisecond = 1   //          10 000 ticks
| Second = 2        //      10 000 000 ticks
| Minute = 3        //     600 000 000 ticks
| Hour = 4          //  36 000 000 000 ticks
| Day = 5           // 864 000 000 000 ticks
| Month = 6         // variable
| Year = 7


/// TimeFrame is a length of a TimePeriod measured in number of frequency units (periods)
/// E.g. a quarter is 3M TimeFrame - frequency is a month, 3 units of frequency (cycles)
/// TODO: better naming for TimeFrame?
[<Struct>]
[<CustomComparison;CustomEquality>]
type TimeFrame
    private(value:uint16) = // internal used in TimePeriod

    static member FromValue(value:uint16) = TimeFrame(value)

    new(frequency:Frequency, cycles:uint16) =
        let value = (uint16 ( ((int frequency) &&& 7) <<< 12) ) ||| (cycles &&& 4095us)
        TimeFrame(value)

    member internal this.Value = value

    member this.Frequency 
        with get() : Frequency = 
            LanguagePrimitives.EnumOfValue (int( (value >>> 12) &&& 7us )) 
    member this.Cycles = 
        value &&& 4095us

    override this.Equals(other) = value.Equals((other :?> TimeFrame).Value)
    override x.GetHashCode() = int(value)

    interface IComparable<TimeFrame> with
        member this.CompareTo(other:TimeFrame) = value.CompareTo(other.Value)

    interface IComparable with
        member this.CompareTo(other:obj) = value.CompareTo((other :?> TimeFrame).Value)

    interface IEquatable<TimeFrame> with
        member this.Equals(other:TimeFrame) = value.Equals(other.Value)


/// This error is thrown when two TimePeriods with different TimeFrames are compared
/// Such behavior prevents creating sorted collections of TimePeriods with different TimeFrames
type TimePeriodFrameMismatchException() =
    inherit Exception("Could not compare TimePeriods with different frames")

type internal int48 = int64

module TimePeriodModule =
    // Variables:
    // tpv - time period value : int64
    // freq - value of Frequency enum :int
    // ticks - DateTimeOffset ticks representation : int64
    // timeFrame - last 16 bits of tpv as uint16
    
    //#region Constants

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
    let millisecondsOffset = 0
    [<Literal>]
    let secondsOffset = 10
    [<Literal>]
    let minutesOffset = 16
    [<Literal>]
    let hoursOffset = 22
    [<Literal>]
    let daysOffset = 27
    [<Literal>]
    let monthsOffset = 32
    [<Literal>]
    let yearsOffset = 36
    [<Literal>]
    let timeFrameOffset = 48
    [<Literal>]
    let periodsOffset = 48
    [<Literal>]
    let freqOffset = 60

    [<Literal>]
    let ticksMask = 1152921504606846975L //(1UL <<< 60) - 1UL
    [<Literal>]
    let ticksBucketMask = 65535L //(1UL <<< 16) - 1UL
    [<Literal>]
    let partsMask = 281474976710655L //(1UL <<< 48) - 1UL
    [<Literal>]
    let timeFrameMask = 65535L //(1UL <<< 16) - 1UL
    [<Literal>] 
    let millisecondsMask = 1023L //(1UL <<< 10) - 1UL
    [<Literal>] // ms in a second
    let millisecondsBucketMask = 65535L //(1UL <<< (10 + 6)) - 1UL
    [<Literal>] 
    let secondsMask = 63L // (1UL <<< 6) - 1UL
    [<Literal>] // seconds in an hour
    let secondsBucketMask = 4095L // (1UL <<< (6+6)) - 1UL
    [<Literal>] 
    let minutesMask = 63L // (1UL <<< 6) - 1UL
    [<Literal>] // minutes in a day
    let minutesBucketMask = 63L // (1UL <<< (6+5)) - 1UL
    [<Literal>] 
    let hoursMask = 31L // (1UL <<< 5) - 1UL
    [<Literal>] 
    let daysMask = 31L // (1UL <<< 5) - 1UL
    [<Literal>] 
    let monthsMask = 15L // (1UL <<< 4) - 1UL
    [<Literal>] 
    let yearsMask = 4095L // (1UL <<< 12) - 1UL
    [<Literal>] 
    let periodsMask = 4095L // (1UL <<< 12) - 1UL
    [<Literal>] 
    let freqMask = 7L // (1UL <<< 3) - 1UL
    //#endregion Constants

    let isTick (tpv:int64) : bool = (0L = (tpv >>> freqOffset))

    let ticks (tpv:int64) : int64 = 
        Debug.Assert((isTick tpv))
        int64(tpv &&& ticksMask)

    //#region Parts

    let internal partsValue (tpv:int64) : int48 = 
        Debug.Assert(not (isTick tpv))
        int64(tpv &&& partsMask)

    let milliseconds (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(millisecondsMask &&& (tpv >>> millisecondsOffset))
    
    let seconds (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(secondsMask &&& (tpv >>> secondsOffset))

    let minutes (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(minutesMask &&& (tpv >>> minutesOffset))

    let hours (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(hoursMask &&& (tpv >>> hoursOffset))

    let days (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(daysMask &&& (tpv >>> daysOffset))

    let months (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(monthsMask &&& (tpv >>> monthsOffset))

    let years (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(yearsMask &&& (tpv >>> yearsOffset))

    let internal timeFrameValue (tpv:int64) : uint16 = 
        Debug.Assert(not (isTick tpv))
        uint16(timeFrameMask &&& (tpv >>> timeFrameOffset))

    let periods (tpv:int64) : int = 
        Debug.Assert(not (isTick tpv))
        int(periodsMask &&& (tpv >>> periodsOffset))

    let internal freq (tpv:int64) : int = 
        int(freqMask &&& (tpv >>> freqOffset))

    let frequency (tpv:int64) : Frequency =
        LanguagePrimitives.EnumOfValue <| freq tpv

    //#endregion


    /// Assume that inputs are not checked for logic
    let ofPartsUnsafe 
        (freq:int) (periods:int) 
        (year:int) (month:int) (day:int) 
        (hour:int) (minute:int) (second:int) (millisecond:int) : int64 =
            let mutable value : int64 = 0L
            value <- value ||| ( (int64(freq) &&& freqMask) <<< freqOffset )
            value <- value ||| ( (int64(periods) &&& periodsMask) <<< periodsOffset )
            value <- value ||| ( (int64(year) &&& yearsMask) <<< yearsOffset )
            value <- value ||| ( (int64(month) &&& monthsMask) <<< monthsOffset )
            value <- value ||| ( (int64(day) &&& daysMask) <<< daysOffset )
            value <- value ||| ( (int64(hour) &&& hoursMask) <<< hoursOffset )
            value <- value ||| ( (int64(minute) &&& minutesMask) <<< minutesOffset )
            value <- value ||| ( (int64(second) &&& secondsMask) <<< secondsOffset )
            value <- value ||| ( (int64(millisecond) &&& millisecondsMask) <<< millisecondsOffset )
            value


//    let private partsToDTO (year:int) (month:int) (day:int) 
//        (hour:int) (minute:int) (second:int) (millisecond:int) : DateTimeOffset =
//            DateTimeOffset(year, month, day, hour, minute, second, millisecond, TimeSpan.Zero)

    let internal partsValueToDTO (partsValue:int48) : DateTimeOffset =
            DateTimeOffset(years partsValue, months partsValue, days partsValue, 
                hours partsValue, minutes partsValue, seconds partsValue, milliseconds partsValue, TimeSpan.Zero)
    
    let internal DTOtoPartsValue (dto:DateTimeOffset) : int48 =
        ofPartsUnsafe 0 0 dto.Year dto.Month dto.Day dto.Hour dto.Minute dto.Second dto.Millisecond
    

    //#region Changes to partsValue

    let internal addMillisecondsToPartsValue (partsValue:int48) (milliseconds:int) : int48 =
        DTOtoPartsValue ((partsValueToDTO partsValue).AddTicks(int64(milliseconds) * ticksPerMillisecond))
    
    let internal addSecondsToPartsValue (partsValue:int48) (seconds:int) : int48 =
        DTOtoPartsValue ((partsValueToDTO partsValue).AddTicks(int64(seconds) * ticksPerSecond))
    
    let internal addMinutesToPartsValue (partsValue:int48) (minutes:int) : int48 =
        DTOtoPartsValue ((partsValueToDTO partsValue).AddTicks(int64(minutes) * ticksPerMinute))

    let internal addHoursToPartsValue (partsValue:int48) (hours:int) : int48 =
        DTOtoPartsValue ((partsValueToDTO partsValue).AddTicks(int64(hours) * ticksPerHour))

    let internal addDaysToPartsValue (partsValue:int48) (days:int) : int48 =
        DTOtoPartsValue ((partsValueToDTO partsValue).AddTicks(int64(days) * ticksPerDay))

    let internal addMonthsToPartsValue (partsValue:int48) (months:int) : int48 =
        DTOtoPartsValue ((partsValueToDTO partsValue).AddMonths(months))

    let internal addYearsToPartsValue (partsValue:int48) (years:int) : int48 =
        DTOtoPartsValue ((partsValueToDTO partsValue).AddYears(years))

    //#endregion


//    let private partsValueOfPeriodEnd periodStartPartsValue freq periods =
//        ()


    let ofTicksUnsafe (ticks:int64) : int64 = (int64 Frequency.Tick <<< freqOffset) ||| int64(ticks)
    
    let internal ofTimeFrameAndPartsValue (tf:TimeFrame) (partsValue:int48) :int64 =
        (int64(tf.Value) <<< timeFrameOffset) ||| partsValue

    let ofDateTimeOffset (dto:DateTimeOffset) : int64 =
        ofTicksUnsafe dto.UtcTicks

    let ofDateTimeOffsetStart (freq:uint16) (periods:uint16) (dtoStart:DateTimeOffset) : int64 =
        Debug.Assert((freq < 7us))
        Debug.Assert((0us < periods && periods <= 4095us))
        ofPartsUnsafe (int freq) (int periods) 
                        dtoStart.Year dtoStart.Month dtoStart.Day 
                        dtoStart.Hour dtoStart.Minute dtoStart.Second dtoStart.Millisecond

    let internal ofDateTimeOffsetEnd (freq:int) (periods:int) (dtoEnd:DateTimeOffset) : int64 =
        // TODO (endDTO+1tick)-1Frame
        raise (NotImplementedException())
            
    

    // shift period by number of frames: next period is +1 frame, previous period is -1 frames, etc
    let shiftByNumberOfFrames (frames:int) (tpv:int64) : int64 =
        if frames = 0 then tpv
        else
            let tf = TimeFrame.FromValue(timeFrameValue tpv)
            match tf.Frequency with
            | _ as x when x = Frequency.Tick -> 
                ofTicksUnsafe (ticks tpv) + int64(frames)
            | _ as x when x = Frequency.Millisecond ->
                ofTimeFrameAndPartsValue tf (addMillisecondsToPartsValue (partsValue tpv) frames)
            | _ as x when x = Frequency.Second ->
                ofTimeFrameAndPartsValue tf (addSecondsToPartsValue (partsValue tpv) frames)       
            | _ as x when x = Frequency.Minute ->
                ofTimeFrameAndPartsValue tf (addMinutesToPartsValue (partsValue tpv) frames)  
            | _ as x when x = Frequency.Hour ->
                ofTimeFrameAndPartsValue tf (addHoursToPartsValue (partsValue tpv) frames)          
            | _ as x when x = Frequency.Day ->
                ofTimeFrameAndPartsValue tf (addDaysToPartsValue (partsValue tpv) frames)  
            | _ as x when x = Frequency.Month ->
                ofTimeFrameAndPartsValue tf (addMonthsToPartsValue (partsValue tpv) frames)        
            | _ as x when x = Frequency.Year ->
                ofTimeFrameAndPartsValue tf (addYearsToPartsValue (partsValue tpv) frames) 
            | _ -> failwith "wrong frequency"


    let periodStart (tpv:int64) : DateTimeOffset =
        if isTick tpv then DateTimeOffset(ticks tpv, TimeSpan.Zero)
        else partsValueToDTO (partsValue tpv)

    /// period end is one tick before the start of the next period
    let periodEnd (tpv:int64) : DateTimeOffset =
        if isTick tpv then DateTimeOffset(ticks tpv, TimeSpan.Zero)
        else (partsValueToDTO (partsValue (shiftByNumberOfFrames 1 tpv))).AddTicks(-1L)

    let timeSpan (tpv:int64) : TimeSpan =
        if isTick tpv then TimeSpan(1L)
        else TimeSpan((periodEnd tpv).Ticks - (periodStart tpv).Ticks + 1L)

    // the bigger the frame the less important grouping becomes for a single series (but still needed if there are many short series)
    // because the total number of points is limited (e.g. in 10 years there are 86,400 hours ~ 10,800 buckets by 8 trading hours or 3,600 buckets by 24 hours)
    // and the bigger bucket density should be expected
    // 100 000 buckets take 2.8 Mb (key + pointer + array overhead per bucket)
    // 1Gb of data = 100 Mn items in buckets (10 bytes per item: 2 key + 8 value)
    // 1Gb ~ 10 seconds of data per every tick, 1526 buckets 43kb
    // 1Gb ~ 28 hours of millisecondly data, 1667 buckets 47kb
    // 1Gb ~ 38 months of secondly data, 27778 buckets 0.78Mb
    // 1Gb ~ 192 years of minutely data, 69444 buckets 1.94Mb
    let bucketHash (tpv:int64) : int64 =
        let tf = TimeFrame.FromValue(timeFrameValue tpv)
        match tf.Frequency with
        | _ as x when x = Frequency.Tick -> 
            (tpv >>> 16) <<< 16 // 65536 ticks per bucket
        | _ as x when x = Frequency.Millisecond ->
            (tpv >>> minutesOffset) <<< minutesOffset  // group by minute; 60000 ms in a minute
        | _ as x when x = Frequency.Second ->
            (tpv >>> hoursOffset) <<< hoursOffset // group by hour; 3600 seconds in an hour
        | _ as x when x = Frequency.Minute ->
            (tpv >>> daysOffset) <<< daysOffset // group by day; 1440 minutes in a day
        | _ as x when x = Frequency.Hour ->
            (tpv >>> daysOffset) <<< daysOffset // group by day; 24 in a day         
        | _ as x when x = Frequency.Day ->
            (tpv >>> monthsOffset) <<< monthsOffset // group by month; 28-31 day in a month
        | _ as x when x = Frequency.Month ->
            (tpv >>> yearsOffset) <<< yearsOffset // group by year; 12 months in a year       
        | _ as x when x = Frequency.Year ->
            (tpv >>> timeFrameOffset) <<< timeFrameOffset // group by time frame, up to 4095 years
        | _ -> failwith "wrong frequency"

    let bucketSubIndex (tpv:int64) : uint16 =
        let tf = TimeFrame.FromValue(timeFrameValue tpv)
        match tf.Frequency with
        | _ as x when x = Frequency.Tick -> 
            uint16(tpv &&& ticksBucketMask) // 65536 ticks per bucket
        | _ as x when x = Frequency.Millisecond ->
            uint16(tpv &&& millisecondsBucketMask) // group by minute; 60000 ms in a minute
        | _ as x when x = Frequency.Second ->
            uint16( (minutes tpv)*60 + (seconds tpv) ) // group by hour; 3600 seconds in an hour
        | _ as x when x = Frequency.Minute ->
            uint16( (hours tpv)*60 + (minutes tpv) ) // group by day; 1440 minutes in a day
        | _ as x when x = Frequency.Hour ->
            uint16(hours tpv) // group by day; 24 in a day         
        | _ as x when x = Frequency.Day ->
            uint16(days tpv) // group by month; 28-31 day in a month
        | _ as x when x = Frequency.Month ->
            uint16(months tpv) // group by year; 12 months in a year       
        | _ as x when x = Frequency.Year ->
            uint16(years tpv) // group by time frame, up to 4095 years
        | _ -> failwith "wrong frequency"

    let bucketIndex (bucket:int64) (subIndex:uint16) : int64 =
        let tf = TimeFrame.FromValue(timeFrameValue bucket)
        let pv = partsValue bucket
        match tf.Frequency with
        | _ as x when x = Frequency.Tick -> 
            bucket ||| int64(subIndex)
        | _ as x when x = Frequency.Millisecond ->
            ofTimeFrameAndPartsValue tf (addMillisecondsToPartsValue pv (int subIndex))
        | _ as x when x = Frequency.Second ->
            ofTimeFrameAndPartsValue tf (addSecondsToPartsValue pv (int subIndex))
        | _ as x when x = Frequency.Minute ->
            ofTimeFrameAndPartsValue tf (addMinutesToPartsValue pv (int subIndex))
        | _ as x when x = Frequency.Hour ->
            ofTimeFrameAndPartsValue tf (addHoursToPartsValue pv (int subIndex))
        | _ as x when x = Frequency.Day ->
            ofTimeFrameAndPartsValue tf (addDaysToPartsValue pv (int subIndex))
        | _ as x when x = Frequency.Month ->
            ofTimeFrameAndPartsValue tf (addMonthsToPartsValue pv (int subIndex))
        | _ as x when x = Frequency.Year ->
            ofTimeFrameAndPartsValue tf (addYearsToPartsValue pv (int subIndex))
        | _ -> failwith "wrong frequency"

    //let inline AddTick
     //let inline periodEnd 


[<Struct>]
//TODO[<CustomComparison;CustomEquality>]
type TimePeriod
    private(value:int64) = // internal used in TimePeriodIntConverter()
    
    member internal this.Value = value

    member this.TimeFrame with get():TimeFrame = TimeFrame.FromValue(TimePeriodModule.timeFrameValue value)

    member this.Start with get() = TimePeriodModule.periodStart value

    member this.End with get() : DateTimeOffset = TimePeriodModule.periodEnd value

    member this.TimeSpan with get() : TimeSpan = TimePeriodModule.timeSpan value

    member this.Next with get() : TimePeriod = TimePeriod(TimePeriodModule.shiftByNumberOfFrames 1 value)

    member this.Previous with get() : TimePeriod = TimePeriod(TimePeriodModule.shiftByNumberOfFrames -1 value)

    member this.AddFrames(frames:int) : TimePeriod = TimePeriod(TimePeriodModule.shiftByNumberOfFrames frames value)


    //#region Buckets
    member internal this.BucketHash 
        with get() : TimePeriod = 
            TimePeriod(TimePeriodModule.bucketHash value)
    member internal this.BucketSubKey 
        with get() : uint16 = TimePeriodModule.bucketSubIndex value
    static member internal BucketKey(bucket:TimePeriod, subIndex:uint16) : TimePeriod =
        TimePeriod(TimePeriodModule.bucketIndex bucket.Value subIndex)
    //#endregion

    // Constructors

    new(frequency:Frequency, cycles:uint16, start:DateTimeOffset) =
        TimePeriod(TimePeriodModule.ofDateTimeOffsetStart (uint16 frequency) cycles start)

    // Creators
    static member internal FromValue(value:int64) = 
        TimePeriod(value)
//    static member FromDateTime = ()
//    static member FromDateTimeOffset = ()
//    static member FromTicks = ()
//    static member FromStart = () // same as the only constructor
//    static member FromEnd = ()
