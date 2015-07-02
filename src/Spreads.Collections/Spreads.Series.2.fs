namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads

// could extend but not operators
//
module SpreadsModuile =
//  type Series<'K,'V when 'K : comparison> with
////    static member private Init() =
////      System.Windows.Forms.MessageBox.Show("Init was called!")
//      
//    end

  type BaseSeries with
    static member private Init() =
      System.Windows.Forms.MessageBox.Show("Init was called!")
    end
    
module internal Initializer =
  let internal init() = 
    //System.Windows.Forms.MessageBox.Show("Init was called!")
    //BaseSeries.MapImpl <- SeriesExtensions.Map
    ()