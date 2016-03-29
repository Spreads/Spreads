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


#time "on"

let asyncCalc = 
  async {
    let mutable sum = 0
    for i in 0..1000000 do
      do! System.Threading.Tasks.Task.Run(fun _ -> sum <- sum + 1) |> Async.AwaitTask
    return sum
  }

let sumAsync = asyncCalc |> Async.RunSynchronously



type IInc =
  abstract Inc : unit -> int
  abstract Value: int with get

type MyInc =
  struct 
    val mutable value : int
    new(v:int) = {value = v}
  end
  member this.Inc() = this.value <- (this.value + 1);this.value
  member this.Value with get() = this.value
  interface IInc with
    member this.Inc() = this.value <- this.value + 1;this.value
    member this.Value with get() = this.value

[<StructAttribute>]
type MyInc2 =
  val mutable v : int
  new(v:int) = {v = v}

  member this.Inc() = this.v <- this.v + 1;this.v
  member this.Value with get() = this.v

  interface IInc with
    member this.Inc() = this.v <- this.v + 1;this.v
    member this.Value with get() = this.v

let mi = (MyInc(0))
mi.Inc()
mi.Value

let mint = MyInc(0) :> IInc
mint.Inc()
mint.Value


//type MyEnumerator =
//  struct 
//    val mutable value : int
//    new(v:int) = {value = v}
//  end
//
//  interface IEnumerator<int> with
//    


#time "on"
let now = System.DateTime.Now
let later = System.DateTime.Now.AddMinutes(1.0)
for i in 0..1000000 do
  if now > later then failwith "no way"



#time "on"
type MyRecType() = 
  
  let list = System.Collections.Generic.List()

  member this.DoWork() =
    let mutable tcs = (System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int>.Create())
    let returnTask = tcs.Task // NB! must access this property first
    let mutable local = 1

    let rec outerLoop() =
      if local < 1000000 then
        innerLoop(1)
      else
        tcs.SetResult(local)
        ()

    and innerLoop(inc:int) =
      if local % 2 = 0 then
        local <- local + inc
        outerLoop()
      else
        list.Add(local) // just fake access to a field to illustrate the pattern
        local <- local + 1
        innerLoop(inc)

    outerLoop()

    returnTask

  member this.DoWork2() =
    let mutable tcs = (System.Runtime.CompilerServices.AsyncTaskMethodBuilder<int>.Create())
    let returnTask = tcs.Task // NB! must access this property first
    let mutable local = 1
    let rec loop(isOuter:bool, inc:int) =
      if isOuter then
        if local < 1000000 then
          loop(false,1)
        else
          tcs.SetResult(local)
          ()
      else
        if local % 2 = 0 then
          local <- local + inc
          loop(true,1)
        else
          list.Add(local) // just fake access to a field to illustrate the pattern
          local <- local + 1
          loop(false,1)

    loop(true,1)

    returnTask
        

let instance = MyRecType()

instance.DoWork2().Result

//      
//let ser1 = Series<DateTime,'V1>()
//let ser2 = Series<DateTime,'V2>()
//
//type ValueTuple2<'V1,'V2> =
//  struct
//    val Value1 : 'V1
//    val Value2 : 'V2
//    new(v1 : 'V1, v2 : 'V2) = {Value1 = v1; Value2 = v2}
//  end
//
//type ValueTuple3<'V1,'V2,'V3> =
//  struct
//    val Value1 : 'V1
//    val Value2 : 'V2
//    val Value3 : 'V3
//    new(v1 : 'V1, v2 : 'V2) = {Value1 = v1; Value2 = v2}
//  end
//
//
//ser1.Map(fun x -> ValueType2(x, Unchecked.defaultof<_>))
//ser2.Map(fun x -> ValueType2(Unchecked.defaultof<_>, x))
//
//let originalLambda = fun (x:'V1) (y:'V2) -> failwith "todo"
//let f tuppleArr = failwith ""
//let fakeLambda (tuppleArr:ValueTuple2<'V1,'V2>[]) = originalLambda tuppleArr.[0].Value1 tuppleArr.[1].Value2 




#time "on"
open System
let f : Func<double,double> = Func<double,double>(fun x -> x + 123456.0)
let f2 : Func<double,double> = Func<double,double>(fun x -> x/789.0)

let fastFunc = Func<double,double>(fun x -> f2.Invoke(f.Invoke(x)))
let slowFunc = Func<double,double>(f.Invoke >> f2.Invoke)

let mutable sum = 0.0
for i in 0..100000000 do
  sum <- sum + fastFunc.Invoke(double i)

let mutable sum2 = 0.0
for i in 0..100000000 do
  sum2 <- sum2 + slowFunc.Invoke(double i)



#time "on"
open System
type I =
  abstract Add: int -> int

[<AbstractClassAttribute>]
type Root() =
  abstract Add: int -> int
  interface I with
    member this.Add(i) = this.Add(i)

[<SealedAttribute>]
type RootChild() =
  inherit Root()
  override this.Add(i) = i + 1


type RootImplementer() =
  member this.Add(i) = i + 1
  interface I with
    member this.Add(i) = this.Add(i)
    

type RootDelegate(f:Func<int,int>) = 
  member this.Add(i) = f.Invoke(i)
  interface I with
    member this.Add(i) = this.Add(i)
    



let IChild = RootChild() :> I
let IImplementer = RootImplementer() :> I
let IDelegate = RootDelegate(fun x -> x + 1) :> I


let mutable acc = 0
for i in 0..100000000 do
  acc <- IChild.Add(acc)

let mutable acc2 = 0
for i in 0..100000000 do
  acc2 <- IImplementer.Add(acc2)

let mutable acc3 = 0
for i in 0..100000000 do
  acc3 <- IDelegate.Add(acc3)




type VirtualRoot() =
  abstract Add: int -> int
  override this.Add(i) = i + 1
  interface I with
    member this.Add(i) = this.Add(i)

  member this.AddMap(f:Func<int,int>) =
    {new VirtualRoot() with
      member x.Add(i) = f.Invoke(this.Add(i))
    }

let VirtualRoot = VirtualRoot()//.AddMap(fun i -> i * 2)

let mutable acc4 = 0
for i in 0..100000000 do
  acc4 <- VirtualRoot.Add(i) * 2


#time "on"
// no difference with mod operation
let mutable modSum = 0L
for i in 0L..10000000L do
  modSum <- modSum + (i &&& (128L-1L))
modSum


modSum <- 0L
for i in 0L..10000000L do
  modSum <- modSum + (i % (125L))
modSum



type SubmessageStr =
  struct
    val mutable var1 : int
    val mutable var2 : int64
  end

type SubmessageRecord =
  {
    mutable var1 : int;
    mutable var2 : int64
  }

[<RequireQualifiedAccessAttribute>]
type Message = 
  | Submessage of var1:int * var2:int64
  | SubmessageR of SubmessageRecord
  | SubmessageStr of SubmessageStr
  | SubmessageFlyweight of SubmessageStr


let myMessage = Message.Submessage(1, 2L)
let mutable myStr = new SubmessageStr()
myStr.var1 <- 1
myStr.var2 <- 2L
let myMessageStr = Message.SubmessageStr(myStr)

let myRec = { var1 = 1; var2 = 2L }
let myMessageRec = Message.SubmessageR(myRec)

let v1 = let (Message.SubmessageStr str2) = myMessageStr in str2.var1
let (Message.Submessage (v1',v2') ) = myMessage

