namespace Spreads.Internals

open System
open System.Linq
open System.Linq.Expressions
open Spreads

//thanks to http://v2matveev.blogspot.ru/2010/06/f-performance-of-events.html

// helper type that will perform invocation
type internal Invoker<'D, 'A> = delegate of 'D * obj * 'A -> unit
type internal UpdateHandler<'K,'V> = delegate of KVP<'K,'V> -> unit

module internal EventHelper =
  let inline invoker<'D, 'A when 'D :> Delegate and 'D : delegate<'A, unit> and 'D : null> = 
    let d = Expression.Parameter(typeof<'D>, "dlg")
    let sender = Expression.Parameter(typeof<obj>, "sender")
    let arg = Expression.Parameter(typeof<'A>, "arg")
    let lambda = Expression.Lambda<Invoker<'D, 'A>>(Expression.Invoke(d, sender, arg), d, sender, arg)
    lambda.Compile()

type internal EventV2<'D, 'A when 'D :> Delegate and 'D : delegate<'A, unit> and 'D : null>() = 
    let mutable multicast : 'D = null     
    // this inline gives 10x better performance for SortedMap Add/Insert operation
    member inline x.Trigger(args: 'A) =
        match multicast with
        | null -> ()
        | d -> EventHelper.invoker.Invoke(d, null, args) // DelegateEvent used: d.DynamicInvoke(args) |> ignore

//    member x.Publish =
//        { new IDelegateEvent<'D> with
//            member x.AddHandler(d) =
//                multicast <- System.Delegate.Combine(multicast, d) :?> 'D
//            member x.RemoveHandler(d) =
//                multicast <- System.Delegate.Remove(multicast, d)  :?> 'D }            
//    member x.Publish =
//        { new IEvent<'D,'A> with
//            member x.Subscribe(observer: IObserver<'A>): IDisposable = 
//              failwith "Not implemented yet"
//            member x.AddHandler(d) =
//                multicast <- System.Delegate.Combine(multicast, d) :?> 'D
//            member x.RemoveHandler(d) =
//                multicast <- System.Delegate.Remove(multicast, d)  :?> 'D } 
    member x.Publish =
        { new IEvent<'A> with
            member x.Subscribe(observer: IObserver<'A>): IDisposable = 
              failwith "Not implemented yet"
            member x.AddHandler(d) =
                multicast <- System.Delegate.Combine(multicast, d) :?> 'D
            member x.RemoveHandler(d) =
                multicast <- System.Delegate.Remove(multicast, d)  :?> 'D } 