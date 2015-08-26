namespace Spreads.Tests.Series


open FsUnit
open NUnit.Framework

open System
open System.Linq
open System.Collections.Generic
open System.Diagnostics
open Spreads
open Spreads.Collections
open Spreads.Collections.Experimental
open System.Threading

type CircularTestsModule() =
    
  [<Test>]
  member this.``Circular Calculations Work``() =
    let state = SortedMap()
    state.Add(DateTime.UtcNow.AddDays(-1.0), 0.0)
    let circular = CircularCalculations(state)

    circular.Execute()

    Thread.Sleep(60000);

    ()