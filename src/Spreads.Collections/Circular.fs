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



type CircularCalculations(state:IOrderedMap<DateTime, float>) = // state is taken from DB or somewhere, here it is our position before trading starts
  let cts = new CancellationTokenSource()
  let ct = cts.Token
  let rng = System.Random()
  let mutable executorTask = Unchecked.defaultof<_>

  member this.Stop() = 
    cts.Cancel()
    //cts.Dispose()

  // flow: state + data -> target state -> new state + new data -> ...

  member this.Data() : Series<DateTime, float> = // data is produced outside
    let mutable previous = 1.0
    let sm = SortedMap()
    for i in 0..1000 do
      previous <- previous*(1.0 + rng.NextDouble()*0.01 - 0.005)
      sm.Add(DateTime.UtcNow.AddSeconds(-(1000-i) |> float), previous)
    
    Task.Run((fun _ ->
        while not ct.IsCancellationRequested do
          previous <- previous*(1.0 + rng.NextDouble()*0.01)
          sm.Add(DateTime.UtcNow, previous)
          Thread.Sleep(500)
      ), ct) |> ignore
    sm :> Series<DateTime, float>
     

  member this.CalculateTargetState() : Series<DateTime, float> = // this returns desired position
    let data = this.Data()
    let sma = data.Window(20u, 1u, true).Map(fun inner -> inner.Values.Average())
    let dataSm = data.ToSortedMap()
    let smaSm = sma.ToSortedMap()
    let signal = data / sma - 1.0
    let signalSm = signal.ToSortedMap()
    let func = Func<float,float,float>(fun sgn st -> 
        if sgn > 0.0 then
          st - 1.0
        elif sgn < 0.0 then
          st + 1.0
        else 0.0
      )
    let targetState = signal.Zip(state.Repeat(), func)
     //.Map(fun s -> if s > 0.0 then -1.0 elif s < 0.0 then 1.0 else 0.0)
    targetState

  member this.Execute() = 
    let targetState = this.CalculateTargetState()
    let tgtCursor = targetState.GetCursor()
    executorTask <- Task.Run((fun _ ->
      while not ct.IsCancellationRequested && tgtCursor.MoveNext() do
        let delay = 100.0
        Thread.Sleep(int delay) // this emulates time for execution
        state.Add(tgtCursor.CurrentKey.AddMilliseconds(delay), tgtCursor.CurrentValue)
        Console.WriteLine("Added new state from target: " + tgtCursor.CurrentKey.ToString() + " | " + tgtCursor.CurrentValue.ToString())
    ), ct)
    // 
    ()