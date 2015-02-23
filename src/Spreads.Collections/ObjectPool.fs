namespace Spreads

open System
open System.IO
open System.Threading
open System.Collections.Concurrent
open System.Threading.Tasks


type ObjectPool<'T>(objectGenerator:Func<'T>, maxCapacity:int) =
  // in steady state the number of objects could be much higher than 
  // capacity, but at each moment the number of object inside the buffer 
  // is a waste. E.g. 1000 double[1000]s takes 8 Mb

  // for doubles we are interested in 
  // * milliseconds in a second - 1000
  // * seconds in 15 minutes - 900
  // * minutes in a day - 480 in 8 hours, 840 for FORTS
  // * int keyed buckets that divided into 1000 pieces

  let mutable outstanding = 0

  let objects = new ConcurrentBag<'T>()
  let objectGenerator = 
    if objectGenerator = null then raise (new ArgumentNullException("objectGenerator"))
    objectGenerator

  member x.Count with get() = objects.Count
  member x.Outstanding with get() = outstanding

  member x.GetObject() =
    let ok, value = objects.TryTake()
    if ok then value
    else 
      Interlocked.Increment(&outstanding) |> ignore
      objectGenerator.Invoke()

  member x.PutObject(item:'T) =
    Interlocked.Decrement(&outstanding) |> ignore
    if objects.Count < maxCapacity then
      objects.Add(item)
    else
      match box item with
      | :? IDisposable as d -> d.Dispose()
      | _ -> ()

  member x.Dispose() =
      //let isDisposable = typedefof<IDisposable>.IsAssignableFrom(typedefof<'T>);
      let rec removeDisposeItem() =
        let ok, item = objects.TryTake()
        if ok then // && isDisposable
          ((box item) :?> IDisposable).Dispose()
          match box item with
          | :? IDisposable as d -> d.Dispose()
          | _ -> ()
          removeDisposeItem()
        else 
          ()
      removeDisposeItem()

  interface IDisposable with
    member x.Dispose() = x.Dispose()
      
