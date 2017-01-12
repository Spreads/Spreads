// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads.Collections.Experimental

open System
open System.Linq
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks

open Spreads
open Spreads.Collections



type TheSimplestTradingStrategy(actualPosition:SortedMap<DateTime, float>) = // actualPosition is taken from DB or somewhere, here it is our position before trading starts
  let cts = new CancellationTokenSource()
  let ct = cts.Token
  let rng = System.Random()
  let mutable executorTask = Unchecked.defaultof<_>

  member this.Stop() = cts.Cancel()

  // flow: state + data -> target state -> new state + new data -> ...

  member this.Quotes() : Series<DateTime, float> = // data is produced outside
    let mutable previous = 1.0
    let sm = SortedMap()
    let now = DateTime.UtcNow
    let mutable trend = -1.0
    let mutable cnt = 0
    for i in 0..500 do
      previous <- previous*(1.0 + rng.NextDouble()*0.02 - 0.01 + 0.01 * trend)
      sm.Add(now.AddSeconds(-((500-i) |> float)*0.2), previous)
      cnt <- cnt + 1
      if cnt % 40 = 0 then trend <- -trend

    Task.Run((fun _ ->
        
        while not ct.IsCancellationRequested do
          Thread.Sleep(200)
          previous <- previous*(1.0 + rng.NextDouble()*0.02 - 0.01 + 0.01 * trend)
          sm.Add(DateTime.UtcNow, previous)
          cnt <- cnt + 1
          if cnt % 40 = 0 then trend <- -trend
      ), ct) |> ignore
    sm :> Series<DateTime, float>
     

  member this.CalculateTargetState() : Series<DateTime, float> = // this returns desired position
    let quotes = this.Quotes()
    let sma = quotes.Window(20u, 1u, true).Map(fun inner -> inner.Average(fun x -> x.Value))
    let deviation = quotes / sma - 1.0
    let deviationSm = deviation.Cache()
    let targetState = deviationSm.Map(fun x -> (float <| Math.Sign(x)))
    let targetStateSm = targetState.Cache()
    targetStateSm :> Series<DateTime, float>

  member this.Execute() = 
    let targetPosition = this.CalculateTargetState()
    let targetTrade = (targetPosition - actualPosition).Cache()
    let tgtTradeCursor = targetTrade.GetCursor()
    // TODO "Do(...)" extension method
    executorTask <- Task.Run<int>(Func<Task<int>>(fun _ ->
      task {
        let! moved = tgtTradeCursor.MoveNext(ct)
        while moved do
          let currentPosition = actualPosition.Last.Value
          let tradeAmout = tgtTradeCursor.CurrentValue
          let tradeTime = DateTime.UtcNow
          if tradeAmout <> 0.0 then 
            Console.WriteLine("Traded " + tradeAmout.ToString() + " at " + tradeTime.ToString())
            actualPosition.Add(tradeTime, tgtTradeCursor.CurrentValue)
        return 0
      }
    ), ct)
    ()