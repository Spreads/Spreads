
#I "../../bin"
#r "Spreads.Core.dll"
#r "Spreads.Collections.dll"

open System
open Spreads
open Spreads.Collections

// value of returned 'K could be different, e.g. for fill/repeat
type FCursor<'K,'V> = ('K -> ('K*'V) option)

type FSeries<'K,'V> = unit -> FCursor<'K,'V>

type FMap<'K,'V,'U> = ('K*'V) option -> ('K*'U) option

// Cursor and series are functors
let map (f:FMap<'K,'V,'U>) (series:FSeries<'K,'V>) : FSeries<'K,'U> = 
  fun () -> let cursor = series() in cursor >> f 

/// wrap Spreads series into simplified functional example
let fromISeries (series:ISeries<'K,'V>) : FSeries<'K,'V> =
  fun () ->
    let cursor = series.GetCursor()
    let fcursor = fun k ->
      let mutable value = Unchecked.defaultof<_>
      // FCursor is purely functional without side effects. Only TryGetValue 
      // method of ICursor is a pure function, Move.. methods change position
      if cursor.TryGetValue(k, &value) then 
        Some(k, value)
      else None
    fcursor


let spreadsMap =
  let sm = SortedMap<int,int>()
  for i in 1..10 do
    sm.AddLast(i, i)
  sm.Add(15, 15)
  sm

/// Original series [1:10;15]
let intSeries : FSeries<int,int> = fromISeries spreadsMap

intSeries()(5)    // 5, 5
intSeries()(17)   // None

let fill fillValue : FMap<'K,'V,'V> = function
  | None -> Some(Unchecked.defaultof<'K>, fillValue)
  | Some (k,v) -> Some(k,v)

let filledOriginal = intSeries |> map (fill 20)
filledOriginal()(14) // 0, 20

let removeEvenFilter : FMap<int,'V,'V> = function
  | None -> None
  | Some (k,v) -> if k % 2 <> 0 then Some(k,v) else None


let onlyOdd = intSeries |> map removeEvenFilter
onlyOdd()(5) // 5,5
onlyOdd()(6) // None
 

let filledOnlyOdd = intSeries |> map (removeEvenFilter >> (fill 20) )
let filledOnlyOdd2 = onlyOdd |> map (fill 20) 
filledOnlyOdd()(6)   // 0, 20
filledOnlyOdd2()(6)  // 0, 20


(*

What if for any missing value we what to get a value at a previous existing key,
i.e. do Less-or-Equal lookup or Repeat operation!?

Repeat operation cannot be represented as FMap:
there is not enough information in `('K*'V) option` to find a previous value
if a value at a requested key does not exists.

In this simplified example cursor does not have navigational capabilities,
so lets assume that we operate in natural numbers space (uint): N = {0, 1, 2, 3, ...}

*)

let inline discreteRepeatCursor (cursor:FCursor<'K,'V>) : FCursor<'K,'V> =
  let rec lessOrEqual k =
    match cursor(k) with
    | Some(k',v') as x -> x
    | None ->
      // our simple FCursor is not navigable for generic 'K,
      // so we simplify and assume that 'K is discrete space 
      // with a step of GenericOne and above GenericZero
      // - unsigned integers in this example
      let tryPreviousK = k - LanguagePrimitives.GenericOne
      if tryPreviousK >= LanguagePrimitives.GenericZero then
        lessOrEqual tryPreviousK
      else None
  lessOrEqual

// repeated series is still lazy, but is defined at any N
let repeated : FSeries<int, int> = 
  fun () ->
    let cursor = intSeries() 
    discreteRepeatCursor cursor

repeated()(4)   // 4, 4
repeated()(20)  // 10, 10
repeated()(0)   // None

#time "on"
repeated()(10000000) // 10, 10
// c. 2.3 sec

// in the repeated series above we transform (map) 
// a cursor to another cursor with different behavior 
// but the same signature, and then wrap the new cursor
type FCursorMap<'K,'V,'U> = FCursor<'K,'V> -> FCursor<'K,'U>

// wrap ("monadic return")
let ret (cursor:FCursor<'K,'V>) : FSeries<'K,'V> = fun () -> cursor

// Series are functors over cursors
let mapCursor (f:FCursorMap<'K,'V,'U>) (series:FSeries<'K,'V>) : FSeries<'K,'U> = 
  let cursor = series() |> f // map cursors
  ret cursor  // return cursor


(*
 Is Series is a monad?

 “Understanding monads is the easiest thing in the world. 
 I know because I've done it thousands of times.” (c) Mark Twain
 
 We have already defined return operation above.

 This is a signature for Bind from F# computation expression:
 Series<'Cursor<T>> * ('Cursor<T> -> Series<Cursor<'U>>) -> Series<Cursor<'U>>
*)

type FCursorBinder<'K,'V,'U> = FCursor<'K,'V> -> FSeries<'K,'U>

/// reorder for easier |>
let inline bind (binder:FCursorBinder<'K,'V,'U>) (series:FSeries<'K,'V>)  =
  series()   // we create new cursor for every returned series
  |> binder  // even if cursors are stateful, bound series use different instances
  
// this function has FCursorBinder<'K,'V,'U> signature
let inline repeatProjection cursor =
  fun () -> cursor |> discreteRepeatCursor


let oddRepeatFill =
  (intSeries |> map removeEvenFilter)   // only odd
  |> bind repeatProjection              // repeat
  |> map (fill -1)                      // 

oddRepeatFill()(-100) // 0, 0
oddRepeatFill()(0) // 0, 0
oddRepeatFill()(1) // 1, 1
oddRepeatFill()(6) // 5, 5
oddRepeatFill()(14) // 9, 9
oddRepeatFill()(17) // 9, 9
oddRepeatFill()(10000000) // 15, 15
// c.2.5 sec


// final picture here


(*
  Actual ISeries returns ICursor via GetCursor() method. 
  ICursor is stateful, it knows its current position.

  ('K -> ('K*'V) option) signature could be implemented 
  via MoveAt + CurrentKey + CurrentValue

  ICursor is a function in mathematical sense:
  it defines a relation between a set of inputs and 
  a set of permissible outputs with the property that 
  each input is related to exactly one output.
  
  It is stateful (knows its current position at least)
  and provides navigation functionality.
*)

open System.Collections
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

// _X for not to conflict with Spreads types

[<Interface>]
[<AllowNullLiteral>]
type ISeriesX<'K,'V> =
  abstract GetCursor : unit -> ICursorX<'K,'V>

and
  [<Interface>]
  ICursorX<'K,'V> =
    inherit IDisposable
    //inherit ISeries<'K,'V>                // Clone() + Reset()  could be used to implement GetCursor()
    abstract Clone: unit -> ICursor<'K,'V>  // could be implemented as .Source.GetCursor() -> MoveAt(.CurrentKey, Lookup.EQ)
    abstract Source : IReadOnlySeriesX<'K,'V> with get

    abstract Comparer: IComparer<'K> with get

    // Navigation capabilities
    abstract MoveAt: key:'K * direction:Lookup -> bool
    abstract MoveFirst: unit -> bool
    abstract MoveLast: unit -> bool
    abstract MoveNext: unit -> bool
    abstract MovePrevious: unit -> bool

    // Mapping from key to value at current position
    abstract CurrentKey:'K with get
    abstract CurrentValue:'V with get
    abstract Current:KeyValuePair<'K,'V> with get

    // Need this for ZipN
    abstract IsContinuous: bool with get

    // Pure functional mapping from 'K -> 'V. After this excercise think to change signatue to return KVP<K,V>
    abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool

    // Real-time is all here
    abstract MoveNextAsync: unit -> Task<bool>

and
  // Base type Series implements this interface
  // One thread-local cursor is enough to implement this interface
  // ISeries implementers are assumed to implement this as well,
  // or there is a CursorSeries type that makes a Series from an ICursor
  [<Interface>]
  [<AllowNullLiteral>]
  IReadOnlySeriesX<'K,'V> =
    inherit ISeries<'K,'V>
    abstract IsEmpty: bool with get
    abstract First : KVP<'K, 'V> with get             // dual to cursor.MoveFirst()
    abstract Last : unit -> KVP<'K, 'V> with get      // dual to cursor.MoveLast()
    abstract Item : 'K -> 'V with get                 // dual to cursor.MoveAt(k, Lookup.EQ)
    abstract GetAt : idx:int -> 'V                    // dual to MoveFirst() + MoveNext() idx times
    abstract Keys : IEnumerable<'K> with get          // dual to MoveFirst() + MoveNext() + yield cursor.CurrentKey
    abstract Values : IEnumerable<'V> with get        // dual to MoveFirst() + MoveNext() + yield cursor.CurrentValue
    abstract TryFind: key:'K * direction:Lookup * [<Out>] value: byref<KVP<'K, 'V>> -> bool // dual to MoveAt(k, direction)

    // avoid exceptions, for ZipN TryGetValue implemented via Try..Catch was a real issue
    abstract TryGetFirst: [<Out>] value: byref<KVP<'K, 'V>> -> bool // dual to cursor.MoveFirst()
    abstract TryGetLast: [<Out>] value: byref<KVP<'K, 'V>> -> bool  // dual to cursor.MoveFirst()
    abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool // dual to cursor.MoveAt(k, Lookup.EQ)


let repeatSpreads (series:ISeries<'K,'V>) : FSeries<'K,'V> =
  fun () ->
    let cursor = series.GetCursor()
    let fcursor = fun k ->
      // Repeat is a move to Less-or-Equal position
      if cursor.MoveAt(k, Lookup.LE) then 
        Some(cursor.CurrentKey, cursor.CurrentValue)
      else None
    fcursor

let repeatedWithSpreads = repeatSpreads spreadsMap
#time "on"
let mutable temp = Unchecked.defaultof<_>
for i in 0..10000000 do
  temp <- repeatedWithSpreads()(Int32.MaxValue) // 15, 15
  ()
// c. 2.9 sec for 10M iterations, or 290 nanosec per lookup (vs. 2.3 sec above)



(*
ZipN implementation
*)

// signature tells the returned type is FSeries<'K,'V[]>
let zipN (series:FSeries<'K,'V>[]) =
  fun () ->
    fun k ->
      let cursors = series |> Array.map (fun x -> x())
      let vArr = Array.zeroCreate series.Length
      let mutable cont = true // plese do breaks fro for/while, they are already imperative!
      let mutable idx = 0
      while cont && idx < cursors.Length do
        match cursors.[idx](k) with
        | None ->
          cont <- false
        | Some(k', v') -> 
          vArr.[idx] <- v'
          idx <- idx + 1
      if cont then Some(k,vArr)
      else None


/// sample binary operator for FSeries
let inline (+) (l:FSeries<'K,'V>) (r:FSeries<'K,'V>) : FSeries<'K,'V> = 
  let zipSeries = zipN [|l;r|]
  let mapper : FMap<'K,'V[],'V> = function
  | None -> None
  | Some (k, vArr) -> Some(k, vArr.[0] + vArr.[1])
  map mapper zipSeries
       
let zipped = zipN [|intSeries;filledOnlyOdd|]
zipped()(6)

let sum = intSeries + filledOnlyOdd

sum()(6) // 6, 26
