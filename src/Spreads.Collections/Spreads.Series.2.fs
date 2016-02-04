(*  
    Copyright (c) 2014-2016 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
        
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

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
    //Trace.WriteLine("Injected default math provider")
    ()