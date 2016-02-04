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
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks


type KVP<'K,'V> = KeyValuePair<'K,'V>


// Tuples as value types


type ValueTuple<'V1,'V2> =
  struct
    val Value1 : 'V1
    val Value2 : 'V2
    new(v1 : 'V1, v2 : 'V2) = {Value1 = v1; Value2 = v2}
  end

type ValueTuple<'V1,'V2,'V3> =
  struct
    val Value1 : 'V1
    val Value2 : 'V2
    val Value3 : 'V3
    new(v1 : 'V1, v2 : 'V2, v3 : 'V3) = {Value1 = v1; Value2 = v2; Value3 = v3}
    new(tuple2:ValueTuple<'V1,'V2>, v3 : 'V3) = {Value1 = tuple2.Value1; Value2 = tuple2.Value2; Value3 = v3}
  end


type DummyDisposable private() =
  static let instance = new DummyDisposable()
  interface IDisposable with
    member x.Dispose() = ()
  static member Instance with get() = instance :> IDisposable