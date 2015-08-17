open System

type IA =
  abstract Get42 : int with get

[<AbstractClassAttribute>]
type BaseA() =
  static member (+) (first:BaseA,second:BaseA) = 0
  abstract Get42 : int with get
  member x.MyType() = x.GetType()

type A() =
  inherit BaseA()
  override x.Get42 with get () = 42
  member x.BaseType() = base.MyType().BaseType
  //static member (+) (first:A,second:A) = 123
  interface IA with
    member x.Get42 with get () = x.Get42

type B() =
  inherit A()
  override x.Get42 with get () = 43
  static member (+) (first:B,second:B) = 456
//  interface IA with
//    member x.Get42 with get () = x.Get42

A().MyType().Name
B().MyType().Name
B().BaseType().Name

(B() :> IA).Get42

#time "on"

let newVector = [| 
  for v in 1..10000000 ->
    v |] 

let newVector2 = 
  let a = Array.zeroCreate 10000000
  for v in 1..10000000 do
    a.[v-1] <- v
  a

let newVector3 = 
  let a = System.Collections.Generic.List() // do not set capacity
  for v in 1..10000000 do
    a.Add(v)
  a.ToArray()

let seq = seq { for v in 1..10000000 do yield v }
let seqArr = seq |> Seq.toArray


let newVector4 = 
  let a = System.Collections.Generic.List() // do not set capacity
  for v in seq do
    a.Add(v)
  a.ToArray()


open System
open System.Linq
open System.Collections.Generic
let newVector5 =  seq.ToArray()

let ie count = 
  { new IEnumerable<int> with
      member x.GetEnumerator(): Collections.IEnumerator = x.GetEnumerator() :> Collections.IEnumerator
      
      member x.GetEnumerator(): IEnumerator<int> = 
        let c = ref 0
        { new IEnumerator<int> with
            member y.MoveNext() = 
              if !c < count then
                c := !c + 1
                true
              else false

            member y.Current with get() = !c + 1
            member y.Current with get() = !c + 1 :> obj
            member y.Dispose() = () 
            member y.Reset() = ()       
        }
  }


let newVector6 = 
  let a = System.Collections.Generic.List() // do not set capacity
  for v in ie 10000000 do
    a.Add(v)
  a.ToArray()

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

type TestStruct =
  struct
    val Dec : Decimal
    val Int : int
    new(d,i) = {Dec = d; Int = i}
  end

let str = new TestStruct(10M, 1)

let size = Marshal.SizeOf(str)
GCHandle.Alloc(str, GCHandleType.Pinned);
let kvp = KeyValuePair("asd", 1)
let size2 = Marshal.SizeOf(kvp)

let arr = [|str|]
let mutable ptr = Marshal.AllocHGlobal(size)
#time "on"
//let str = new TestStruct(10M, i)
for i in 0..10000000 do
  
  Marshal.StructureToPtr(str,ptr, true)


type MyStruct= 
  struct 
    val value: int
    new(value:int, add:int) = {value = value + add}
  end

let structSize = sizeof<MyStruct>

type MyClass(value:int, add:int)=
  let value = value + add

  member this.WhatWasAdd() = add

let myclass = new MyClass(1,2)
myclass.WhatWasAdd()

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
   data
   |> Seq.filter (fun x -> x%2L = 0L)
   |> Seq.map (fun x -> x * x)
   |> Seq.sum

let spreadValue =
   data
   |> ofArray
   |> filter (fun x -> x%2L = 0L)
   |> map (fun x -> x * x)
   |> sum

let arrayValue =
   data
   |> Array.filter (fun x -> x%2L = 0L)
   |> Array.map (fun x -> x * x)
   |> Array.sum

open System.Linq

let linqValue =   
   data
      .Where(fun x -> x%2L = 0L)
      .Select(fun x -> x * x)
      .Sum()



let Parallel asyncs = (Seq.ofArray((Async.RunSynchronously(Async.Parallel(asyncs)))))

open System.Threading
[<Struct; StructLayout(LayoutKind.Explicit, Size = 64)>]
type MyStruct1 =
    [<FieldOffset(0)>]
    val mutable value : int64
    new(initVal:int64) = { value = initVal }
    member x.Value
        with get() = Interlocked.Read(&(x.value))
        and set(valIn) = Interlocked.Exchange(&(x.value),valIn) |> ignore


