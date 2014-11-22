// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

[<EntryPoint>]
let main argv = 
    for i in 0..9 do
      Spreads.Tests.Collections.Benchmarks.CollectionsBenchmarks.SHM_run()

    printfn "%A" argv
    0 // return an integer exit code
