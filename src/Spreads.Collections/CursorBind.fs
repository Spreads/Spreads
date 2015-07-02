namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks

open Spreads

module CursorHelper =
  let inline lIsAhead (cl:ICursor<'K,'V1>) (cr:ICursor<'K,'V2>) = cl.CurrentKey > cr.CurrentKey
  

