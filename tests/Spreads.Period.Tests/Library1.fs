namespace Spreads.Period.Tests
open System
open Spreads

type Class1() = 
    let tp = Spreads.TimePeriod(UnitPeriod.Day, 1us, DateTime.Today, TimeZoneInfo.Local)
    tp.Start
    member this.X = "F#"
