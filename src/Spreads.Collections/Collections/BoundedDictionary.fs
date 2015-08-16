namespace Spreads.Collections

open System
open System.Collections.Generic
open System.Collections.Concurrent

open Spreads

[<ObsoleteAttribute("Prefer sorted double deque?")>]
type internal BoundedDictionary<'K,'V when 'K : equality>
    (capacity:int) =

    // TODO TryAdd/TryTake methods (TryEnqueue/TryDequeue)
    let mutable capacity = capacity
    let dic = Dictionary<'K,'V>()
    let queue = Queue<'K>()

    member this.ContainsKey(k) = 
        dic.ContainsKey(k)

    member this.Item 
        with get k = dic.Item(k)
        and set k v =
            use lock = makeLock dic
            if dic.ContainsKey(k) then
                dic.[k] <- v
            else
                queue.Enqueue(k)
                dic.Add(k, v)
                while queue.Count > capacity do
                    dic.Remove(queue.Dequeue()) |> ignore

    member this.Capacity 
        with get() = capacity
        and set(value) = capacity <- value

    new() = BoundedDictionary(Int32.MaxValue)

