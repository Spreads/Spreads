(*  
    Copyright (c) 2014-2015 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.
        
    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

namespace Spreads

open System
open System.IO
open System.Threading
open System.Threading.Tasks

/// <summary>
/// This exception is thrown during a cursor movements when new data could affect cursor values:
/// e.g. data was updated at a key before the current cursor position and the cursor is moving forward
/// or the cursor depends on the past.
/// It is easy to recover from this exception using the cursor MoveAt method and CurrentKey/NewKey properties
/// of this exception. E.g. MoveAt(ex.CurrentKey, Lookup.GT) in a catch block is equivalent to ignoring 
/// the exception and continuing to MoveNext. 
/// To replay values that could have been altered by an out-of-order data point, one could use MoveAt(ex.NewKey, Lookup.EQ).
/// It is the responsibility of cursor consumer to recover from this exception, cursors should not implement any "smart"
/// behavior unless it is a part of cursor definition and is explicitly documented.
/// The state of cursor is undefined and invalid after the exception is thrown.
/// </summary>
type DataOutOfOrderException<'K>(currentKey, newKey) =
  inherit Exception()
  /// <summary>
  /// Key/value before arrival of out-of-order data point
  /// </summary>
  member val CurrentKey = currentKey with get
  /// <summary>
  /// Key/value of an out-of-order data point
  /// </summary>
  member val NewKey = newKey with get

