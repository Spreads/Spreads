namespace Spreads.RPlugin

open System
open System.Linq
open System.Collections
open System.ComponentModel.Composition

open RDotNet
open RProvider
open RProvider.``base``
open RProvider.zoo

open Spreads
open Spreads.Collections
open Spreads.RPlugin.Conversions

// ------------------------------------------------------------------------------------------------
// IDefaultConvertFromR - convert Deedle frame & time series to R symexpr
// ------------------------------------------------------------------------------------------------

[<Export(typeof<IConvertToR<ISeries<DateTime, double>>>)>]
type TimeSeriesToR() =
  interface IConvertToR<Series<DateTime, double>> with
    member x.Convert(engine, series) = R.zoo(series.Values.ToArray(), series.Keys.ToArray())

// ------------------------------------------------------------------------------------------------
// IDefaultConvertFromR - convert R symexpr to some data frame and return
// ------------------------------------------------------------------------------------------------


[<Export(typeof<IDefaultConvertFromR>)>]
type SeriesDefaultFromR() = 
  interface IDefaultConvertFromR with
    member x.Convert(symExpr) = createDefaultSeries symExpr

// ------------------------------------------------------------------------------------------------
// IConvertFromR - convert Deedle series to R symexpr
// ------------------------------------------------------------------------------------------------

// Time series with DateTime keys

[<Export(typeof<IConvertFromR<Series<DateTime, float>>>)>]
type SeriesDateFloatFromR() =
  interface IConvertFromR<Series<DateTime, float>> with 
    member x.Convert(symExpr) = tryCreateTimeSeries id symExpr


// ------------------------------------------------------------------------------------------------
// Conversions for other primitive types 
// ------------------------------------------------------------------------------------------------

[<Export(typeof<IConvertToR<DateTime>>)>]
type DateTimeConverter() =
  interface IConvertToR<DateTime> with        
    member this.Convert(engine: REngine, x: DateTime) =            
      R.as_POSIXct(String.Format("{0:u}", x.ToUniversalTime(), "UTC"))

[<Export(typeof<IConvertToR<seq<DateTime>>>)>]
type DateTimeSeqConverter() =
  interface IConvertToR<seq<DateTime>> with        
    member this.Convert(engine: REngine, xs: seq<DateTime>) =            
      let dts = xs |> Seq.map (fun dt -> String.Format("{0:u}", dt.ToUniversalTime()))
      R.as_POSIXct(dts, "UTC")

[<Export(typeof<IConvertToR<Decimal>>)>]
type DecimalConverter() =
  interface IConvertToR<Decimal> with        
    member this.Convert(engine: REngine, x: Decimal) =            
      upcast engine.CreateNumericVector [float x] 

[<Export(typeof<IConvertToR<seq<Decimal>>>)>]
type DecimalSeqConverter() =
  interface IConvertToR<seq<Decimal>> with        
    member this.Convert(engine: REngine, x: seq<Decimal>) =            
      upcast engine.CreateNumericVector (Seq.map float x)