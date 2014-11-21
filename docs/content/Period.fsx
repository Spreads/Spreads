(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
Period
========================

TimePeriod possible values extend from 1/1/1900 to approx. 1/1/2141, while UTC offset could 
be set only with 30 mins precision. With this practical constraints we could encode not only
time but time periods in 64 bits and represent TimePeriod as a structure with a single int64 field.

TODO: Elaborate on above, hashes, time zones

We loose time zone / offset info because it is recoverable from W3. E.g. for AAPL:US close price we
know that it is EST/UTC-5 because this is a NYSE ticker and NYSE is in NYC.

Why start date, not end date stored in value?
* Start date is inclusive, end date is not: [start1, end1)[start2, end2), where end1 = start2
* Easier to calculate buckets: just apply masks to get bucket index and index


*)
#r "Spreads.Period.dll"
#r "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5\Facades\System.Runtime.dll"
open System
open Spreads


(*
A TimePeriod is defined as a number of unit periods from a start date (inclusive) 
to an end date (exclusive). Unit or based periods are defined as:
```
type UnitPeriod =
  | Tick = 0          //               100 nanosec
  | Millisecond = 1   //              10 000 ticks
  | Second = 2        //          10 000 000 ticks
  | Minute = 3        //         600 000 000 ticks
  | Hour = 4          //      36 000 000 000 ticks
  | Day = 5           //     864 000 000 000 ticks
  | Month = 6         //                  Variable
  | Eternity = 7      //                  Infinity
```
*)
/// Base unit of TimePeriod


let tp = TimePeriod()




(**



*)
