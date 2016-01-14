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
    let numQuoteSources = 2

    let cts = new CancellationTokenSource()
    let ct = cts.Token
    let rng = System.Random()

    let makeQuoteSource () : Series<DateTime, float> = // data is produced outside
        let mutable value = 1.0
        let sm = SortedMap()
        let now = DateTime.UtcNow
        let mutable trend = -1.0
        let mutable cnt = 0
        let gapMillisecs = int(rng.NextDouble() * 500.0 + 250.0)

        sm.Add(now, value)
        cnt <- cnt + 1
        if cnt % 40 = 0 then trend <- -trend

        let task = async {
                        while not ct.IsCancellationRequested do
                            do! Async.Sleep gapMillisecs
                            let time = DateTime.UtcNow
                            value <- value*(1.0 + rng.NextDouble()*0.002 - 0.001 + 0.001 * trend)
                            sm.Add(time, value)
                            cnt <- cnt + 1
                            if cnt % 40 = 0 then trend <- -trend
                    }
        Async.Start(task, ct)
        sm :> Series<DateTime, float>

    let arrayOfContinuousSeries = Array.init numQuoteSources (fun i -> (makeQuoteSource ()).Repeat())

    let index : Series<DateTime, float> = 
        arrayOfContinuousSeries.Zip(fun k vArr -> vArr |> Array.average)

    index.Do((fun k v -> 
                printfn "Index: %A = %f" k v
                ), ct)

    Thread.Sleep(10000000)
    // Stop it gracefully
    //cts.Cancel()