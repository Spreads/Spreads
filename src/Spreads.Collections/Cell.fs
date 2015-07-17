namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Linq
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

open Spreads
open Spreads.Collections

[<RequireQualifiedAccess>]
type Cell =
  | Empty
  | Number of float
  | Text of string
  | DateTime of DateTime
  | Error of string

  static member op_Explicit(cell: Cell) : float = 
    match cell with
    | Number n -> n
    | _ -> raise (ArgumentException("Cell in not a number"))

  static member op_Explicit(cell: Cell) : string = 
    match cell with
    | Number n -> n.ToString()
    | Text t -> t
    | DateTime d -> d.ToString()
    | Empty -> ""
    | Error e -> raise (Exception(e))

  static member op_Explicit(cell: Cell) : DateTime = 
    match cell with
    | DateTime d -> d
    | _ -> raise (ArgumentException("Cell in not a DateTime"))

  static member op_Explicit(cell: Cell) : obj = 
    match cell with
    | Number n -> box n
    | Text t -> box t
    | DateTime d -> box d
    | Empty -> box ""
    | Error e -> raise (Exception(e))

  static member op_Explicit(value:float) : Cell = Cell.Number(value)

  static member op_Explicit(value:string) : Cell = Cell.Text(value)

  static member op_Explicit(value:DateTime) : Cell= Cell.DateTime(value)

  static member op_Explicit(value:obj) : Cell = 
    match value with
    | :? DateTime as dt -> Cell.DateTime(dt)
    | :? float as f -> Cell.Number(f)
    | :? string as s -> if s = "" then Cell.Empty else Cell.Text(s)
    | _ -> Cell.Error("Wrong type for Cell conversion, Cells only support float, string and DateTime types")


[<Extension>]
type CellExtensions =
  [<Extension>]
  static member ToCellSeries<'K,'V when 'K : comparison>(series:Spreads.Series<'K,'V>) =
    let sm = new SortedMap<Cell,Cell>()
    for kvp in series do
      let k =  Cell.op_Explicit(box kvp.Key)
      let v =  Cell.op_Explicit(box kvp.Value)
      sm.Add(k, v)
      ()
    sm

  [<Extension>]
  static member ToObjectList<'K,'V when 'K : comparison>(series:Spreads.Series<'K,'V>) =
    let sm = new SortedList<obj,obj>()
    for kvp in series do
      let k =  (box kvp.Key)
      let v =  (box kvp.Value)
      sm.Add(k, v)
      ()
    sm