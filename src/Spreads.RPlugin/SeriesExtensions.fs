namespace Spreads.RPlugin

open System
open System.Linq
open System.Collections
open System.ComponentModel.Composition

open Spreads
open Spreads.Collections
open Spreads.RPlugin.Conversions

open RDotNet
open RProvider
open RProvider.``base``
open RProvider.zoo
open RProvider.Spreads


type RUtils() =
  static member Deprecated with get() = true
//  static member Call(name:string, x:Series<DateTime, double>) =
//    let args = 
//      namedParams [
//        "name", box name;
//        "x", box x;]
//    R.spreads__call(args).GetValue<SortedMap<DateTime, double>>()
//
//  static member Call(name:string, x:double[]) : double[] =
//    let args = 
//      namedParams [
//        "name", box name;
//        "x", box x;]
//    R.spreads__call(args).GetValue<double[]>()

