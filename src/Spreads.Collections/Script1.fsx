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