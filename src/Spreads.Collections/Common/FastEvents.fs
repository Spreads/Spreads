(*  
    Copyright (c) 2014-2016 Victor Baybekov.
        
    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.
        
    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
        
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

namespace Spreads

open System
open System.Collections.Generic
open System.Linq.Expressions
open System.Threading
open System.Threading.Tasks

// TODO (perf) we move event interface to Core and probably this trick is no longer needed, need to profile
//thanks to http://v2matveev.blogspot.ru/2010/06/f-performance-of-events.html for the idea

//and 'D : delegate<'A, unit>
type internal EventV2<'D, 'A when 'D :> Delegate and 'D : null>() = 
  static let invoker =
    let d = Expression.Parameter(typeof<'D>, "dlg")
    //let sender = Expression.Parameter(typeof<obj>, "sender")
    let arg = Expression.Parameter(typeof<'A>, "arg")
    let lambda = Expression.Lambda<Action<'D,'A>>(Expression.Invoke(d, arg), d, arg)
    lambda.Compile()

  let mutable multicast : 'D = null     
  // this inline gives 10x better performance for SortedMap Add/Insert operation
  member inline x.Trigger(args: 'A) =
      match multicast with
      | null -> ()
      | d -> invoker.Invoke(d, args) // Using this instead of d.DynamicInvoke(null,args) |> ignore makes an empty call more than 20x faster 

  member inline x.Publish =
      { new IDelegateEvent<'D> with
          member x.AddHandler(d) =
              multicast <- System.Delegate.Combine(multicast, d) :?> 'D
          member x.RemoveHandler(d) =
              multicast <- System.Delegate.Remove(multicast, d)  :?> 'D }



type internal AsyncManualResetEvent () =
  //http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx
  [<VolatileFieldAttribute>]
  let mutable m_tcs = TaskCompletionSource<bool>()

  member this.WaitAsync() = m_tcs.Task
  member this.Set() = m_tcs.TrySetResult(true)
  member this.Reset() =
          let rec loop () =
              let tcs = m_tcs
              if not tcs.Task.IsCompleted || 
                  Interlocked.CompareExchange(&m_tcs, new TaskCompletionSource<bool>(), tcs) = tcs then
                  ()
              else
                  loop()
          loop ()



type AsyncAutoResetEvent () =
  //http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266923.aspx
  static let mutable s_completed = Task.FromResult(true)
  let m_waits = new Queue<TaskCompletionSource<bool>>()
  let mutable m_signaled = false

  member this.WaitAsync(timeout:int) = 
      Monitor.Enter(m_waits)
      try
          if m_signaled then
              m_signaled <- false
              s_completed
          else
              let ct = new CancellationTokenSource(timeout)
              let tcs = new TaskCompletionSource<bool>()
              ct.Token.Register(Action(fun _ -> tcs.TrySetResult(false) |> ignore)) |> ignore
              m_waits.Enqueue(tcs)
              tcs.Task
      finally
          Monitor.Exit(m_waits)

  member this.Set() = 
      let mutable toRelease = Unchecked.defaultof<TaskCompletionSource<bool>>
      Monitor.Enter(m_waits)
      try
          if m_waits.Count > 0 then
              toRelease <- m_waits.Dequeue() 
          else 
              if not m_signaled then m_signaled <- true
          if toRelease <> null then toRelease.TrySetResult(true) |> ignore
      finally
          Monitor.Exit(m_waits)