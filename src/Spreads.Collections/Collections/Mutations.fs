namespace Spreads.Collections

open System
open System.Diagnostics
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks


open Spreads
open Spreads.Collections


// TODO this is experimental. Should model mutations as interfaces because they go on wire and could be implemented in any way, from ZMP to SBE to custom binary writer



type SetMutation<'K,'V> =
  struct
    val Version : int64
    val Key : 'K
    val Value : 'V
    new(version : int64, key : 'K, value : 'V) = {Version = version; Key = key; Value = value}
  end


type RemoveMutation<'K,'V> =
  struct
    val Version : int64
    val Key : 'K
    val Direction : Lookup
    new(version : int64, key : 'K, direction : Lookup) = {Version = -version; Key = key; Direction = direction} // NB negative version
  end