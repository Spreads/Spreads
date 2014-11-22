module Spreads.Period.Tests
open System
open Spreads
open NUnit.Framework


[<Test>]
let tp() = 
    let tp = Spreads.TimePeriod(UnitPeriod.Day, 1us, DateTime.Today, TimeZoneInfo.Local)
    tp.Start