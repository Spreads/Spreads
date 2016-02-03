module internal Spreads.RPlugin.Conversions

open System
open RDotNet
open RDotNet.ActivePatterns
open RProvider
open RProvider.``base``
open RProvider.zoo
open Microsoft.FSharp.Reflection
open Spreads
open Spreads.Collections

// TODO this is a POC for Spreads
// "yyyy-MM-dd HH:mm:ss.ffffff" back and forth parsing should be very slow, 
// we should pass arrays of ticks directly


// ------------------------------------------------------------------------------------------------
// Conversion helpers
// ------------------------------------------------------------------------------------------------

let invcult = System.Globalization.CultureInfo.InvariantCulture
let dateFmt = "yyyy-MM-dd HH:mm:ss.ffffff"

let dateTimeOffsetToStr (dt:DateTimeOffset) =
    dt.ToUniversalTime().ToString(dateFmt, invcult)

let dateTimeToStr (dt:DateTime) =
    dt.ToUniversalTime().ToString(dateFmt, invcult)


    

// ------------------------------------------------------------------------------------------------
// Time series operations
// ------------------------------------------------------------------------------------------------

/// Try converting symbolic expression to a Zoo series.
/// This works for almost everything, but not quite (e.g. lambda functions)
let tryAsZooSeries (symExpr:SymbolicExpression) =
  if Array.exists ((=) "zoo") symExpr.Class then Some symExpr
  else 
    try Some(R.as_zoo(symExpr))
    with :? RDotNet.ParseException -> None

/// Try convert the keys of a specified zoo time series to DateTime
let tryGetDateTimeKeys (zoo:SymbolicExpression) fromDateTime =
  try
    R.strftime(R.index(zoo), "%Y-%m-%d %H:%M:%S").AsCharacter()
    |> Seq.map (fun v -> DateTime.ParseExact(v, "yyyy-MM-dd HH:mm:ss", invcult))
    //|> Seq.map fromDateTime
    |> Seq.toArray
    |> Some
  with :? RDotNet.ParseException | :? RDotNet.EvaluationException -> None

/// Try converting the specified symbolic expression to a time series
let tryCreateTimeSeries fromDateTime (symExpr:SymbolicExpression) : option<Series<DateTime, double>> = 
  tryAsZooSeries symExpr |> Option.bind (fun zoo ->
    // Format the keys as string and turn them into DateTimes
    let keys = tryGetDateTimeKeys zoo fromDateTime
    // If converting keys to datetime worked, return series
    keys |> Option.bind (fun keys ->
      let values = zoo.GetValue<'double[]>()
      Some(SortedMap<DateTime, double>.OfSortedKeysAndValues (keys, values) :> Series<DateTime, double>) ))

/// Try converting the specified symbolic expression to a series with arbitrary keys
let tryCreateSeries (symExpr:SymbolicExpression) : option<SortedMap<DateTime, double>> = 
  tryAsZooSeries symExpr |> Option.map (fun zoo ->
    // Format the keys as string and turn them into DateTimes
    let keys = R.index(zoo).GetValue<'DateTime[]>()
    let values = zoo.GetValue<'double[]>()
    SortedMap<DateTime, double>.OfSortedKeysAndValues (keys, values) )

/// Given symbolic expression, convert it to a time series.
/// Pick the most appropriate key/value type, based on the data.
let createDefaultSeries (symExpr:SymbolicExpression) = 
  tryAsZooSeries symExpr |> Option.bind (fun zoo ->
    let dateKeys = tryGetDateTimeKeys zoo id
    match zoo, dateKeys with
    | NumericVector nums, Some dateKeys -> Some( box <| SortedMap<DateTime, double>.OfSortedKeysAndValues(dateKeys, nums.ToArray()) )
    | _ -> None )