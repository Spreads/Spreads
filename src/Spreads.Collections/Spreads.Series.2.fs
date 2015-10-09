namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Spreads

// could extend but not operators
[<AutoOpenAttribute>]
module SpreadsModule =
  type Series with
    static member private Init() = ()
    // TODO example how to use a static method instead of extension methods, not really useful though
    static member private Zip<'K,'V,'R when 'K : comparison>(resultSelector:Func<'K,'V[],'R>,[<ParamArray>] series: Series<'K,'V> array) =
      CursorSeries(fun _ -> new ZipNCursor<'K,'V,'R>(resultSelector, series |> Array.map (fun s -> s.GetCursor))  :> ICursor<'K,'R>) :> Series<'K,'R>

    end
  

module internal Initializer =
  let internal init() = 
    VectorMathProvider.Default <- new MathProviderImpl()
    Trace.WriteLine("Injected default math provider")
    ()