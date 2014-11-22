module Spreads.Tests

//open Spreads
open NUnit.Framework

[<Test>]
let ``hello returns 43`` () =
  let result = 43 //Library.hello 43
  printfn "%i" result
  Assert.AreEqual(42,result)
