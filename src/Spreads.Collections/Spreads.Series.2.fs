namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads

// could extend but not operators
module SpreadsModule =
  type BaseSeries with
    static member private Init() =
      ()
    end

module internal Initializer =
  let internal init() = 
    VectorMathProvider.Default <- new MathProviderImpl()
    Trace.WriteLine("Injected default math provider")
    ()