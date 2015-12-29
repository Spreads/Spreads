#I "../../bin"
#r "Spreads.dll"
#r "Spreads.Collections.dll"
#r "Spreads.Extensions.dll"

// See this post for details:
// http://hotforknowledge.com/2015/12/29/how-to-write-the-simplest-trading-strategy-using-spreads/

open Spreads
open Spreads.Collections

open System
open System.Threading
open System.Threading.Tasks

let cts = new CancellationTokenSource()
let ct = cts.Token
let rng = System.Random()

let quotes : Series<DateTime, float> = // data is produced outside
    let mutable previous = 1.0
    let sm = SortedMap()
    let now = DateTime.UtcNow
    let mutable trend = -1.0
    let mutable cnt = 0
    for i in 0..500 do
      previous <- previous*(1.0 + rng.NextDouble()*0.002 - 0.001 + 0.001 * trend)
      sm.Add(now.AddSeconds(-((500-i) |> float)*0.2), previous)
      cnt <- cnt + 1
      if cnt % 40 = 0 then trend <- -trend

    Task.Run((fun _ ->
        
        while not ct.IsCancellationRequested do
          Thread.Sleep(500)
          previous <- previous*(1.0 + rng.NextDouble()*0.002 - 0.001 + 0.001 * trend)
          sm.Add(DateTime.UtcNow, previous)
          cnt <- cnt + 1
          if cnt % 40 = 0 then trend <- -trend
      ), ct) |> ignore
    sm :> Series<DateTime, float>

// Print live data
//quotes.Do((fun k v -> 
//    Console.WriteLine("Key: " + k.ToString() + "; value: " + v.ToString())
//  ), ct)

// Calculate SMA
let sma = quotes.SMA(20, true)

// Print live data
//sma.Do((fun k v -> 
//    Console.WriteLine("Key: " + k.ToString() + "; value: " + v.ToString())
//  ), ct)

// Our trading rule is that if the current price is above SMA, we go long,
// and we go short otherwise. This is a classic trending strategy and it works
// quite well on emerging markets and some commodities in the long run

let targetPosition = (quotes / sma - 1.0).Map(fun deviation -> double <| Math.Sign(deviation))

// Print live data
//targetPosition.Do((fun k v -> 
//    Console.WriteLine("Key: " + k.ToString() + "; value: " + v.ToString())
//  ), ct)

// we must keep track of actual position
let actualPositionWritable = SortedMap<DateTime,float>()
let realTrades = SortedMap<DateTime,float>() 
let actualPosition = actualPositionWritable :> Series<_,_>
actualPosition.Do((fun k v -> 
    Console.WriteLine("Actual position: " + k.ToString() + " : " + v.ToString())
  ), ct)


// Trader is "functional", instead of receiving commands it receives desired state 
// and does its best to move actual state to the desired state.
// Here for simplicity we have a very dangerous assumption that trades are executed 
// immediately after a signal, such assumption should be avoided in the real world.
targetPosition.Do(
  (fun k v ->
    if k <= DateTime.UtcNow.AddMilliseconds(-400.0) then
      // simulate historical trading
      let qty = 
        if actualPositionWritable.IsEmpty then v
        else (v - actualPositionWritable.Last.Value)
      if qty <> 0.0 then
        Console.WriteLine(k.ToString() +  " : Paper trade: " + qty.ToString())
        actualPositionWritable.AddLast(k, v)
    else
      // do real trading
      let qty =
        if actualPositionWritable.IsEmpty then failwith "must test strategy before real trading"
        else (v - actualPositionWritable.Last.Value)
      if qty <> 0.0 && k > actualPositionWritable.Last.Key then // protect from executing history
        let tradeTime = DateTime.UtcNow.AddMilliseconds(5.0)
        Console.WriteLine(tradeTime.ToString() +  " : Real trade: " + qty.ToString())
        realTrades.AddLast(tradeTime, qty)
        actualPositionWritable.AddLast(tradeTime, v)
  ), ct)


let returns = quotes.ZipLag(1u, fun c p -> c/p - 1.0)
let myReturns = actualPosition.Repeat() * returns
let myAumIndex = myReturns.Scan(1.0, fun st k v -> st*(1.0 + v))

// Print live data
myAumIndex.Do((fun k v -> 
    Console.WriteLine("AUM Index: " + k.ToString() + " : " + v.ToString())
  ), ct)

// Stop it gracefully
//cts.Cancel()