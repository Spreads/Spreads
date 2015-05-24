#load "Types.fs"
#load "Enums.fs"
#load "Interfaces.fs"
#time "on"

open System
open System.Threading.Tasks
//type Spread =
//  | 

let simple =
  let mutable result = 0
  for i in 0..1000000 do
    result <- result + i
  result


let fromTask =
  let mutable result = 0
  for i in 0..1000000 do
    result <- result + Task.FromResult(i).Result
  result

let fromAsyn =
  let mutable result = 0
  for i in 0..1000000 do
    result <- result + ((Async.AwaitTask (Task.FromResult(i))) |> Async.RunSynchronously)
  result