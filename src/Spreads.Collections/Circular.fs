(*  
    Copyright (c) 2014-2015 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.
        
    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

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
    for i in 0..100 do
      previous <- previous*(1.0 + rng.NextDouble()*0.01 - 0.005)
      sm.Add(now.AddSeconds(-((100-i) |> float)*0.2), previous)
    
    Task.Run((fun _ ->
        while not ct.IsCancellationRequested do
          Thread.Sleep(200)
          previous <- previous*(1.0 + rng.NextDouble()*0.01)
          sm.Add(DateTime.UtcNow, previous)
      ), ct) |> ignore
    sm :> Series<DateTime, float>
     

  member this.CalculateTargetState() : Series<DateTime, float> = // this returns desired position
    let quotes = this.Quotes()
    let sma = quotes.Window(20u, 1u, true).Map(fun inner -> inner.Values.Average())
    let dataSm = quotes.ToSortedMap()
    let smaSm = sma.ToSortedMap()
    let signal = quotes / sma - 1.0
    //let signalSm = signal.ToSortedMap()
    let func = Func<DateTime,float,float,float>(fun _ sgn st -> 
        if sgn > 0.0 then
          st - 1.0
        elif sgn < 0.0 then
          st + 1.0
        else 0.0
      )
    let targetState = signal.Zip(actualPosition.Repeat(), func)
     //.Map(fun s -> if s > 0.0 then -1.0 elif s < 0.0 then 1.0 else 0.0)
    targetState

  member this.Execute() = 
    let targetPosition = this.CalculateTargetState()
    let targetTrade = targetPosition - actualPosition
    let tgtCursor = targetPosition.GetCursor()
    // TODO "Do(...)" extension method
    executorTask <- Task.Run<int>(Func<Task<int>>(fun _ ->
      task {
        let! moved = tgtCursor.MoveNext(ct)
        while moved do
          let! _ =  Task.Delay(20).ContinueWith(fun _ -> 0)
          actualPosition.Add(DateTime.UtcNow, tgtCursor.CurrentValue)
          Console.WriteLine("Added new state from target: " + tgtCursor.CurrentKey.ToString() + " | " + tgtCursor.CurrentValue.ToString())
        return 0
      }
    ), ct)
    // 
    ()