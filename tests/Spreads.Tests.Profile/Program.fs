// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.
open System
open System.Collections.Generic

open Spreads
open Spreads.Collections

[<EntryPoint>]
let main argv = 
  
//  GC.GetTotalMemory(false).ToString() |> Console.WriteLine
//  let wrArr = [| WeakReference(Array.init 10000000 id) |]
//  GC.GetTotalMemory(false).ToString() |> Console.WriteLine
//  GC.Collect(2, GCCollectionMode.Forced, true)
//  if wrArr.[0].IsAlive then Console.WriteLine "Alive" else Console.WriteLine "Dead" 
//
//  Console.ReadKey() |> ignore
//  if wrArr.[0].IsAlive then Console.WriteLine "Alive" else Console.WriteLine "Dead"
//  Console.ReadKey() |> ignore
//
  let count = 1000000L
  for n in 0..9 do
    let shmt = ref (SortedHashMap(TimePeriodSortableHasher()))
    let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
    for i in 0..(int count) do
      shmt.Value.AddLast(TimePeriod(UnitPeriod.Second, 1, 
                          initDTO.AddSeconds(float i)), i)
    for i in 0..(int count) do
      let res = shmt.Value.Item(TimePeriod(UnitPeriod.Second, 1, 
                    initDTO.AddSeconds(float i)))
      ()
    Console.WriteLine(n.ToString())

//  let shmt = ref (SortedHashMap(TimePeriodSortableHasher()))
//  let initDTO = DateTimeOffset(2014,11,23,0,0,0,0, TimeSpan.Zero)
//  for i in 0..(int count) do
//    shmt.Value.Add(TimePeriod(UnitPeriod.Second, 1, 
//                      initDTO.AddSeconds(float i)), i)
//  for i in 0..9 do
//    for ii in shmt.Value do
//      let res = ii.Value
//      ()
//    Console.WriteLine(i.ToString())

  printfn "%A" argv
  0 // return an integer exit code
