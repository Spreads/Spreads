namespace Spreads.Internals

open System
open System.Linq
open System.Linq.Expressions
open Spreads

//thanks to http://v2matveev.blogspot.ru/2010/06/f-performance-of-events.html for idea

type internal EventV2<'D, 'A when 'D :> Delegate and 'D : delegate<'A, unit> and 'D : null>() = 
    static let invoker = 
      let d = Expression.Parameter(typeof<'D>, "dlg")
      let sender = Expression.Parameter(typeof<obj>, "sender")
      let arg = Expression.Parameter(typeof<'A>, "arg")
      let lambda = Expression.Lambda<Action<'D,obj,'A>>(Expression.Invoke(d, sender, arg), d, sender, arg)
      lambda.Compile()

    let mutable multicast : 'D = null     
    // this inline gives 10x better performance for SortedMap Add/Insert operation
    member inline x.Trigger(args: 'A) =
        match multicast with
        | null -> ()
        | d -> invoker.Invoke(d, null, args) // Using this instead of d.DynamicInvoke(null,args) |> ignore makes an empty call more than 20x faster 

    member inline x.Publish =
        { new IDelegateEvent<'D> with
            member x.AddHandler(d) =
                multicast <- System.Delegate.Combine(multicast, d) :?> 'D
            member x.RemoveHandler(d) =
                multicast <- System.Delegate.Remove(multicast, d)  :?> 'D }