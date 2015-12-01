

type ValueTuple<'V1,'V2> =
  struct
    val Value1 : 'V1
    val Value2 : 'V2
    new(v1 : 'V1, v2 : 'V2) = {Value1 = v1; Value2 = v2}
  end



type Stream<'T> = ('T -> unit) -> unit

// reversed stream: get enumerator -> (move next -> current)
type  Spread<'T> = unit -> (unit -> ValueTuple<bool,'T>)

let inline ofArray (source: 'T[]) : Spread<'T> =
  fun _ -> (
      let mutable i = 0
      fun _ -> 
        if i < source.Length then
          let value = ValueTuple(true,source.[i])
          i <- i + 1
          value
        else ValueTuple(false, Unchecked.defaultof<_>)
     )
//   fun _ ->
//      let mutable i = 0
//      while i < source.Length do
//            k source.[i]
//            i <- i + 1

let inline filter (predicate: 'T -> bool) (spread: Spread<'T>) : Spread<'T> =
  fun _ -> (
    let enumerator = spread()
    fun _ ->
      let rec filter' (enumerator:(unit -> ValueTuple<bool,'T>)) =
        let next' = enumerator()
        if next'.Value1 then
          if predicate next'.Value2 then next'
          else filter' enumerator
        else ValueTuple(false, Unchecked.defaultof<_>)
      filter' enumerator
  )

let inline map (mapF: 'T -> 'U) (spread: Spread<'T>) : Spread<'U> =
  fun _ -> (
    let enumerator = spread()
    fun _ ->
      let next = enumerator()
      if next.Value1 then ValueTuple(next.Value1, mapF next.Value2)
      else ValueTuple(false, Unchecked.defaultof<_>)
  )

let inline fold (foldF:'State->'T->'State) (state:'State) (spread:Spread<'T>) : 'State =
  let enumerator = spread()
  let acc = ref state
  let rec fold' () =
    let next = enumerator()
    if next.Value1 then
      acc := foldF !acc next.Value2
      fold'()
    else acc.Value
  fold'()

let inline sum (spread:Spread< ^T>) : ^T
      when ^T : (static member Zero : ^T)
      and ^T : (static member (+) : ^T * ^T -> ^T) =
        fold (+) LanguagePrimitives.GenericZero spread

#time "on" // Turns on timing in F# Interactive

let data = [|1L..1000000L|]

let seqValue = 
  for i in 0..10 do
    data
    |> Seq.filter (fun x -> x%2L = 0L)
    |> Seq.map (fun x -> x * x)
    |> Seq.sum

let spreadValue =
  for i in 0..10 do
    data
    |> ofArray
    |> filter (fun x -> x%2L = 0L)
    |> map (fun x -> x * x)
    |> sum

let arrayValue =
  for i in 0..10 do
    data
    |> Array.filter (fun x -> x%2L = 0L)
    |> Array.map (fun x -> x * x)
    |> Array.sum

open System.Linq

let linqValue =
  for i in 0..10 do
    data
      .Where(fun x -> x%2L = 0L)
      .Select(fun x -> x * x)
      .Sum()

