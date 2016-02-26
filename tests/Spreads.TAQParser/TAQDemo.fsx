#I "../../bin"
#I "bin"
#r "Spreads.Core.dll"
#r "Spreads.Collections.dll"
#r "Spreads.Extensions.dll"
#r "Dapper.dll"
#r "EntityFramework.dll"
#r "FSharp.Charting.dll"
#r "MySql.Data.dll"
#r "MySql.Data.Entity.EF6.dll"
#r "Newtonsoft.Json.dll"
#r "NodaTime.dll"
#r "Prism.Contracts.dll"
#r "Prism.DataModel.dll"
#r "Prism.Desktop.dll"
#r "Ractor.Persistence.dll"
#r "System.Configuration"
#r "TAQParse.exe"
#r "System.Reactive.Core.dll"
#r "System.Reactive.Interfaces.dll"
#load @"bin/FSharp.Charting.fsx"


open System
open Spreads
open Spreads.Collections
open Prism.DataModel;
open Ractor;
open System.Configuration;
open TAQParse
open System.Linq
open FSharp.Charting
open System.IO

// From Ractor.CLR, applies changes in POCOs automatically
let dbPersistor = DatabasePersistor("taq", new MySqlMigrationsConfiguration(), new MySqlDistributedMigrationsConfiguration());
// Sql backed storage for series, using EF + Dapper
let store = new DbPersistentStore(dbPersistor);

// Lazy series, DB is not touched until data is requested
let aapl = store.GetPersistentOrderedMap<DateTime, TaqTrade>("aapl").Map(fun t -> float(t.TradePrice)/10000.0);
let msft = store.GetPersistentOrderedMap<DateTime, TaqTrade>("msft").Map(fun t -> float(t.TradePrice)/10000.0);


// Speed and memory
#time "on"
// JIT and EF warm up during the first run - 0.4 sec, 0.1 seconds after
let aaplSm = aapl.ToSortedMap()
aapl.Count()
224141.0/0.1

// msft not cached yet on the first call, but we are warmed up
let msftSm = msft.ToSortedMap() // 0.082 not cached, 0.4 cached
msft.Count()
80270.0/0.034


// Basics

aapl.Count()
// Open
aapl.First.Value
// High
let high = aapl.Values.Max()
// Low
let low = aapl.Values.Min()
// Close
aapl.Last.Value
// https://uk.finance.yahoo.com/q/hp?s=AAPL&b=5&a=07&c=2015&e=5&d=07&f=2015&g=d

let plot (series:Series<DateTime, float>) =
  let range = series.Range(DateTime(2015, 8, 5, 12, 30, 0), DateTime(2015, 8, 5, 13, 0, 0)).ToSortedMap()
  let list = range.Map(fun k v -> k,v).Values
  Chart.Line(list).WithYAxis(Min = range.Values.Min() - 0.1, Max = range.Values.Max() + 0.1)

plot aapl
plot msft





let spread = (aapl/msft)
plot spread

// series are irregular, there are few trades that happened at the same microsecond
let ac = aapl.Count()
let mc = msft.Count()
spread.Count()


let spread2 = 
  (aapl.Repeat()/msft.Repeat())
    .ToSortedMap()
plot spread2

let sc2 = spread2.Count
// difference is not 15, because repeat starts from the first point
sc2 - ac - mc

// we need to apply fill and then filter out NaN values
let spread3 = 
  ((aapl.Repeat().Fill(0.0)/msft.Repeat().Fill(0.0)) - 1.0)
    .Map(fun x -> if Double.IsNaN(x) || Double.IsInfinity(x) then 0.0 else x)
    .ToSortedMap()
//plot spread3


let sc3 = spread3.Count
sc3 - ac - mc

let plotStart (series:Series<DateTime, float>) =
  let range = series.Range(series.First.Key, series.First.Key.AddSeconds(1.0)).ToSortedMap()
  let list = range.Map(fun k v -> k,v).Values
  Chart.Line(list)//.WithYAxis(Min = range.Values.Min() - 0.1, Max = range.Values.Max() + 0.1)



spread3.Filter(fun x -> x > 0.0).First.Key
plotStart spread3

let maxTickChange = aapl.ZipLag(1u, fun c p -> c/p - 1.0).Values.Max()


// any time series analysis could be done interactively in fsx
// and then put into production with live streaming data

open System.Threading
open System.Threading.Tasks
open System.Reactive
open System.Reactive.Linq

let cts = new CancellationTokenSource()
let ct = cts.Token
let rng = System.Random()

let quotes : Series<DateTime, float> = // data is produced outside
    
    let sm = aapl.ToSortedMap() // SortedMap()
    let mutable previous = sm.Last.Value
    let now = sm.Last.Key
    let mutable trend = -1.0
    let mutable cnt = 0
 
    Task.Run((fun _ ->
        
        while not ct.IsCancellationRequested do
          Thread.Sleep(250)
          previous <- previous*(1.0 + rng.NextDouble()*0.002 - 0.001 + 0.001 * trend)
          sm.Add(DateTime.UtcNow, previous)
          cnt <- cnt + 1
          if cnt % 25 = 0 then trend <- -trend
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
    if k > DateTime.Today then
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
        //Console.WriteLine(k.ToString() +  " : Paper trade: " + qty.ToString())
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
    if k > DateTime.Today then
      Console.WriteLine("AUM Index: " + k.ToString() + " : " + v.ToString())
  ), ct)

// Stop it gracefully
//cts.Cancel()







// Bugs detected: 
// doesn't work without ToSortedMap() when Range applied to continuous ZipN
// let spread3 = ((aapl.Fill(0.0).Repeat()/msft.Fill(0.0).Repeat()) - 1.0) - throws



//
//
//
//let djiaTickers = 
//  File.ReadAllLines( __SOURCE_DIRECTORY__ + "\DJIA.txt")
//  |> Array.map (fun t -> t.Trim().ToLowerInvariant())
//
//
//let series = 
//  djiaTickers
//  |> Array.map (fun t -> 
//    store
//      .GetPersistentOrderedMap<DateTime, TaqTrade>(t)
//      .Map(fun x -> (float x.TradePrice)/10000.0)
//  )
//  
//
//series.[1].First.Value
