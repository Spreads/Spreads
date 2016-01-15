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

    // monitoring variables
    let totalOutputs = ref 0
    let chunkOutputs = ref 0
    let totalInputs = ref 0
    let chunkInputs = ref 0

    let makeQuoteSource () : Series<DateTime, float> = // data is produced outside
        let mutable value = 1.0
        let sm = SortedMap()
        let now = DateTime.UtcNow
        let mutable trend = -1.0
        let mutable cnt = 0
        let gapMillisecs = int(rng.NextDouble() * 500.0 + 250.0)

        sm.Add(now, value)

        let task = async {
                        while not ct.IsCancellationRequested do
                            do! Async.Sleep gapMillisecs
                            let time = DateTime.UtcNow
                            Interlocked.Increment(totalInputs) |> ignore
                            Interlocked.Increment(chunkInputs) |> ignore
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

    // monitoring - 2s chunks, and total
    let chunkMillisecs = 2000
    let totalStopwatch = new Diagnostics.Stopwatch()
    let chunkStopwatch = new Diagnostics.Stopwatch()
    totalStopwatch.Start()
    chunkStopwatch.Start()

    let printRates (stopwatch : Diagnostics.Stopwatch) inputs outputs =
        let elapsedSeconds = (float)stopwatch.ElapsedMilliseconds / 1000.
        printfn "%A: %d inputs (%f/s) -> %d outputs (%f/s)" DateTime.UtcNow inputs (((float)inputs)/elapsedSeconds) outputs (((float)outputs)/elapsedSeconds) 

    let monitor = async {
                        while not ct.IsCancellationRequested do
                            do! Async.Sleep chunkMillisecs
                            let inputs = Interlocked.Exchange(chunkInputs, 0)
                            let outputs = Interlocked.Exchange(chunkOutputs, 0)
                            printRates chunkStopwatch inputs outputs
                            chunkStopwatch.Restart()
                        printfn "Total:"
                        printRates totalStopwatch !totalInputs !totalOutputs
                        }
    Async.Start(monitor, ct)

    index.Do((fun k v -> 
                // just count the outputs
                Interlocked.Increment(chunkOutputs) |> ignore
                Interlocked.Increment(totalOutputs) |> ignore
                ), ct)

    Thread.Sleep(10000000)
    // Stop it gracefully
    //cts.Cancel()