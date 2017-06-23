// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads

open System
open System.Linq
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

open Spreads
open Spreads.Collections


[<AllowNullLiteral>]
[<Obsolete("Use Series'3 struct")>]
type internal CursorSeries<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>) =
    inherit Series<'K,'V>()

    let mutable cursor : ICursor<'K,'V> = Unchecked.defaultof<_>

    new(iseries:ISeries<'K,'V>) = CursorSeries<_,_>(iseries.GetCursor)
    internal new() = CursorSeries<_,_>(Unchecked.defaultof<Func<ICursor<'K,'V>>>)

    member val SyncRoot = new obj() with get, set

    member private this.C
      with get () : ICursor<'K,'V> = 
        if cursor = Unchecked.defaultof<_> then
          Interlocked.CompareExchange(&cursor, this.GetCursor(), Unchecked.defaultof<_>) |> ignore
        cursor

    override this.GetAt(idx:int) = this.Skip(Math.Max(0, idx-1)).First().Value

    //override this.GetCursor() = new BaseCursorAsync<'K,'V,ICursor<'K,'V>>(cursorFactory) :> ICursor<'K,'V>
    override this.GetCursor() = cursorFactory.Invoke() :> ICursor<'K,'V>

    override this.IsIndexed = lock(this.SyncRoot) (fun _ -> this.C.Source.IsIndexed)
    override this.IsReadOnly = lock(this.SyncRoot) (fun _ -> this.C.Source.IsReadOnly)
    override this.Comparer = lock(this.SyncRoot) (fun _ -> this.C.Comparer)

    // TODO only
    override this.Updated 
      with [<MethodImpl(MethodImplOptions.AggressiveInlining)>] get() : Task<bool> = cursor.Source.Updated

    override this.IsEmpty = lock(this.SyncRoot) (fun _ -> not (this.C.MoveFirst()))

    override this.First
      with get() =
        let sr = this.SyncRoot
        Debug.Assert(sr <> null)
        let entered = enterLockIf sr true
        try
          if this.C.MoveFirst() then this.C.Current else invalidOp "Series is empty"
        finally
          exitLockIf this.SyncRoot entered

    override this.Last 
      with get() =
        let entered = enterLockIf this.SyncRoot true
        try
          if this.C.MoveLast() then this.C.Current else invalidOp "Series is empty"
        finally
          exitLockIf this.SyncRoot entered

    override this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
      let entered = enterLockIf this.SyncRoot true
      try
        if this.C.MoveAt(k, direction) then
          result <- this.C.Current 
          true
        else false
      finally
        exitLockIf this.SyncRoot entered

    override this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
      let entered = enterLockIf this.SyncRoot true
      try
        if this.C.MoveFirst() then
          res <- this.C.Current
          true
        else false
      finally
        exitLockIf this.SyncRoot entered

    override this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) =
      let entered = enterLockIf this.SyncRoot true
      try
        if this.C.MoveLast() then
          res <- this.C.Current
          true
        else false
      finally
        exitLockIf this.SyncRoot entered

    member this.TryGetValue(k, [<Out>] value:byref<'V>) =
      let entered = enterLockIf this.SyncRoot true
      try
        if this.C.IsContinuous then
          this.C.TryGetValue(k, &value)
        else
          let ok = this.C.MoveAt(k, Lookup.EQ)
          if ok then value <- this.C.CurrentValue else value <- Unchecked.defaultof<'V>
          ok
      finally
        exitLockIf this.SyncRoot entered

    override this.Keys 
      with get() =
        // TODO manual impl, seq is slow
        let c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentKey
          c.Dispose()
        }

    override this.Values
      with get() =
        // TODO manual impl, seq is slow
        let c = this.GetCursor()
        seq {
          while c.MoveNext() do
            yield c.CurrentValue
          c.Dispose()
        }


type private UnionKeysCursor<'K,'V>([<ParamArray>] cursors:ICursor<'K,'V>[]) =
    let cmp = 
      let c' = cursors.[0].Comparer
      for c in cursors do
        if not <| c.Comparer.Equals(c') then invalidOp "ZipNCursor: Comparers are not equal" 
      c'
    // TODO (perf) allocates for the lifetime of cursor
    let movedKeysFlags : bool[] = Array.zeroCreate cursors.Length
    let movedKeys = SortedDeque<_,_>(cursors.Length, KVPComparer(cmp, Unchecked.defaultof<_>)) // , cursors.Length

    let mutable semaphore : SemaphoreSlim = Unchecked.defaultof<_> //  new SemaphoreSlim(0) //, cursors.Length
    // Live counter shows how many cont cursor not yet returned false on MoveNextAsync
    let mutable liveCounter = cursors.Length
    let mutable subscriptions : IDisposable[] = Unchecked.defaultof<_> 
    let mutable outOfOrderKeys : SortedDeque<'K> = Unchecked.defaultof<_>
    // Same meaning as in BingCursor: we have at least one sucessful move and some state for further moves
    let mutable hasValidState = false

    member this.Comparer with get() = cmp
    member this.HasValidState with get() = hasValidState

    member val IsContinuous = false

    member val CurrentKey = Unchecked.defaultof<'K> with get, set

    // NB lazy application of resultSelector, only when current value is requested
    member this.CurrentValue with get() = Unchecked.defaultof<'V>

    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
    member val CurrentBatch = Unchecked.defaultof<IReadOnlySeries<'K,'V>> with get, set

    member this.Reset() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Reset()) |> ignore

    member this.Dispose() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Dispose()) |> ignore
      if semaphore <> Unchecked.defaultof<_> then semaphore.Dispose()
      if subscriptions <> Unchecked.defaultof<_> then
        for s in subscriptions do
          s.Dispose()

    // the smallest key if any moved first, or false
    member this.MoveFirst(): bool =
      let mutable moved = false
      Array.Clear(movedKeysFlags, 0, movedKeysFlags.Length)
      movedKeys.Clear()
      let mutable i = 0
      for c in cursors do
        let moved' = c.MoveFirst()
        if moved' then 
          movedKeysFlags.[i] <- true
          movedKeys.Add(KVP(c.CurrentKey, i)) |> ignore
        moved <- moved || moved'
        i <- i + 1
      if moved then
        this.CurrentKey <- movedKeys.First.Key
        hasValidState <- true
        true
      else false

    // the smallest key if any moved last, or false
    member this.MoveLast(): bool =
      let mutable moved = false
      Array.Clear(movedKeysFlags, 0, movedKeysFlags.Length)
      movedKeys.Clear()
      let mutable i = 0
      for c in cursors do
        movedKeysFlags.[i] <- true
        let moved' = c.MoveFirst()
        if moved' then 
          movedKeysFlags.[i] <- true
          movedKeys.Add(KVP(c.CurrentKey, i)) |> ignore
        moved <- moved || moved'
        i <- i + 1
      if moved then
        this.CurrentKey <- movedKeys.Last.Key
        hasValidState <- true
        true
      else false

    member this.MoveAt(key, direction): bool =
      let mutable moved = false
      Array.Clear(movedKeysFlags, 0, movedKeysFlags.Length)
      movedKeys.Clear()
      let mutable i = 0
      for c in cursors do
        let moved' = c.MoveAt(key, direction)
        if moved' then 
          movedKeysFlags.[i] <- true
          movedKeys.Add(KVP(c.CurrentKey, i)) |> ignore
        moved <- moved || moved'
        i <- i + 1
      if moved then
        match direction with
        | Lookup.EQ ->
          #if DEBUG
          Trace.Assert(cmp.Compare(movedKeys.First.Key, movedKeys.Last.Key) = 0)
          Trace.Assert(cmp.Compare(key, movedKeys.Last.Key) = 0)
          #endif
          this.CurrentKey <- movedKeys.First.Key
        | Lookup.LE | Lookup.LT ->
          this.CurrentKey <- movedKeys.Last.Key
        | Lookup.GE | Lookup.GT ->
          this.CurrentKey <- movedKeys.First.Key
        | _ -> failwith "Wrong lookup direction, should never be there"
        true
      else false

    member this.MoveNext(): bool =
      if not this.HasValidState then this.MoveFirst()
      else
        // try to recover cursors that have not moved before
        if movedKeys.Count < cursors.Length then
          let mutable i = 0
          while i < movedKeysFlags.Length do
            if not movedKeysFlags.[i] then
              let c = cursors.[i]
              let moved' = c.MoveAt(this.CurrentKey, Lookup.GT)
              if moved' then 
                movedKeysFlags.[i] <- true
                movedKeys.Add(KVP(c.CurrentKey, i)) |> ignore
            i <- i + 1

        // ignore cursors that cannot move ahead of frontier during this move, but do 
        // not remove them from movedKeys so that we try to move them again on the next move
        let mutable ignoreOffset = 0
        let mutable leftmostIsAheadOfFrontier = false
        // current key is frontier, we could call MN after MP, etc.
        while ignoreOffset < movedKeys.Count && not leftmostIsAheadOfFrontier do
          //leftmostIsAheadOfFrontier <- not cmp.Compare(movedKeys.First.Key, this.CurrentKey) <= 0
          let initialPosition = movedKeys.[ignoreOffset]
          let cursor = cursors.[initialPosition.Value]

          let mutable shouldMove = cmp.Compare(cursor.CurrentKey, this.CurrentKey) <= 0
          let mutable movedAtLeastOnce = false
          let mutable passedFrontier = not shouldMove
          // try move while could move and not passed the frontier
          while shouldMove do
            let moved = cursor.MoveNext()
            movedAtLeastOnce <- movedAtLeastOnce || moved
            passedFrontier <- cmp.Compare(cursor.CurrentKey, this.CurrentKey) > 0
            shouldMove <- moved && not passedFrontier
          
          if movedAtLeastOnce || passedFrontier then
            if movedAtLeastOnce then
              let newPosition = KVP(cursor.CurrentKey, initialPosition.Value)
              // update positions if the current has changed, regardless of the frontier
              movedKeys.RemoveAt(ignoreOffset) |> ignore
              movedKeys.Add(newPosition)

            // here passedFrontier if for cursor that after remove/add is not at ignoreOffset idx
            if passedFrontier && cmp.Compare(movedKeys.[ignoreOffset].Key, this.CurrentKey) > 0 then
              leftmostIsAheadOfFrontier <- true
          else
            Trace.Assert(not passedFrontier, "If cursor hasn't moved, I couldn't pass the prontier")
            ignoreOffset <- ignoreOffset + 1
            ()
        // end of outer loop
        if leftmostIsAheadOfFrontier then
            this.CurrentKey <- movedKeys.[ignoreOffset].Key
            true
        else
            false

    // NB mirror of MN, do not change separately
    member this.MovePrevious(): bool =
      if not this.HasValidState then this.MoveLast()
      else
        if movedKeys.Count < cursors.Length then
          let mutable i = 0
          while i < movedKeysFlags.Length do
            if not movedKeysFlags.[i] then
              let c = cursors.[i]
              let moved' = c.MoveAt(this.CurrentKey, Lookup.LT)
              if moved' then 
                movedKeysFlags.[i] <- true
                movedKeys.Add(KVP(c.CurrentKey, i)) |> ignore
            i <- i + 1

        let mutable ignoreOffset = movedKeys.Count - 1
        let mutable rightmostIsAheadOfFrontier = false
        while ignoreOffset >= 0 && not rightmostIsAheadOfFrontier do
          let initialPosition = movedKeys.[ignoreOffset]
          let cursor = cursors.[initialPosition.Value]

          let mutable shouldMove = cmp.Compare(cursor.CurrentKey, this.CurrentKey) >= 0
          let mutable movedAtLeastOnce = false
          let mutable passedFrontier = not shouldMove
          // try move while could move and not passed the frontier
          while shouldMove do
            let moved = cursor.MovePrevious()
            movedAtLeastOnce <- movedAtLeastOnce || moved
            passedFrontier <- cmp.Compare(cursor.CurrentKey, this.CurrentKey) < 0
            shouldMove <- moved && not passedFrontier
          
          if movedAtLeastOnce || passedFrontier then
            if movedAtLeastOnce then
              let newPosition = KVP(cursor.CurrentKey, initialPosition.Value)
              // update positions if the current has changed, regardless of the frontier
              movedKeys.RemoveAt(ignoreOffset) |> ignore
              movedKeys.Add(newPosition)

            // here passedFrontier if for cursor that after remove/add is not at ignoreOffset idx
            if passedFrontier && cmp.Compare(movedKeys.[ignoreOffset].Key, this.CurrentKey) < 0 then
              rightmostIsAheadOfFrontier <- true
          else
            Trace.Assert(not passedFrontier, "If cursor hasn't moved, I couldn't pass the prontier")
            ignoreOffset <- ignoreOffset - 1
            ()
        // end of outer loop
        if rightmostIsAheadOfFrontier then
            this.CurrentKey <- movedKeys.[ignoreOffset].Key
            true
        else
            false

    
    member private this.MoveFirst(ct): Task<bool> =
      task {
        let mutable valuesOk = false
        movedKeys.Clear()
        //if not movedFirst then
        let rec moveCursor i = // NB for loop inside task{} is transformed into a computation expression method, avoid it
          let x = cursors.[i]
          let movedFirst' = x.MoveFirst()
          if movedFirst' then
            lock(movedKeys) (fun _ -> movedKeys.Add(KVP(x.CurrentKey, i)) |> ignore )
            semaphore.Release() |> ignore
          else
            // MF returns false only when series is empty, then MNAsync is equivalent to MFAsync
            x.MoveNext(ct).ContinueWith(fun (t:Task<bool>) ->
              match t.Status with
              | TaskStatus.RanToCompletion -> 
                if t.Result then
                  lock(movedKeys) (fun _ -> movedKeys.Add(KVP(x.CurrentKey, i)) |> ignore )
                  semaphore.Release() |> ignore
                else
                  let decremented = Interlocked.Decrement(&liveCounter)
                  if decremented = 0 then semaphore.Release() |> ignore
              | _ -> failwith "TODO remove task{} and process all task results"
            ) |> ignore
          if i + 1 < cursors.Length then moveCursor (i+1) else ()
        moveCursor 0
        // waith for at least one to move
        let! signal = semaphore.WaitAsync(-1, ct)
        if not signal || Interlocked.Add(&liveCounter, 0) = 0 then
          ct.ThrowIfCancellationRequested()
          valuesOk <- false
        else
          this.CurrentKey <- movedKeys.First.Key
          valuesOk <- true
        hasValidState <- valuesOk
        return valuesOk
      }

    member this.MoveNext(ct): Task<bool> =
      let mutable tcs = Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create() //new TaskCompletionSource<_>() //
      let returnTask = tcs.Task // NB! must access this property first
      let rec loop() =
        // we make null comparison even when outOfOrderKeys is empty, and this is a hot path
        // TODO add OOO keys counter or always allocate SD - but counter could take 4 bytes only, while SD is an object with 16+ bytes overhead
        if outOfOrderKeys <> Unchecked.defaultof<_> && outOfOrderKeys.Count > 0 then
          lock (outOfOrderKeys) (fun _ -> 
            this.CurrentKey <- outOfOrderKeys.RemoveFirst()
            tcs.SetResult(true)
          )
        elif this.MoveNext() then
          tcs.SetResult(true)
        elif Interlocked.Add(&liveCounter, 0) = 0 then
          tcs.SetResult(false)
        else
          if semaphore = Unchecked.defaultof<_> then
            semaphore <- new SemaphoreSlim(0)
            subscriptions <- Array.zeroCreate cursors.Length
            let mutable i = 0
            for c in cursors do
                let ii = i
                let cc = c.Clone()
                let sourceObserver = { new IObserver<KVP<'K,'V>> with
                    member x.OnNext(kvp) = 
                      // We must compare a key to the current key and if
                      // kvp.Key is LE that the current one, we should store it
                      // in an out-of-order deque. Then we should check OOO deque and 
                      // consume it. Because OOO deque grows only when new key if LE 
                      // the current one, it is bounded by construction.
                      // Union key then could return repeated keys and OOO keys,
                      // and it is ZipNs responsibility to handle these cases
                      if cmp.Compare(kvp.Key, this.CurrentKey) <= 0 then
                        if outOfOrderKeys = Unchecked.defaultof<_> then outOfOrderKeys <- SortedDeque<_>(2, cmp)
                        lock (outOfOrderKeys) (fun _ -> 
                          // TODO check this, add perf counters for frequence and max size
                          //if not <| outOfOrderKeys.TryAdd(kvp.Key) then
                          //  Console.WriteLine(kvp.Key.ToString() + " - " + kvp.Value.ToString() )
                          outOfOrderKeys.TryAdd(kvp.Key) |> ignore
                        )
                      semaphore.Release() |> ignore
                    member x.OnCompleted() =
                      let decremented = Interlocked.Decrement(&liveCounter)
                      if decremented = 0 then semaphore.Release() |> ignore
                    member x.OnError(exn) = ()
                }
                subscriptions.[i] <- c.Source.Subscribe(sourceObserver)
                i <- i + 1
          let semaphorePeek = semaphore.Wait(0)
          if semaphorePeek then
            // TODO check for live count here
            loop()
          else
            // initial count was zero, could return here only after at least one cursor moved
            let semaphoreTask = semaphore.WaitAsync(50, ct) // TODO return back -1
            let awaiter = semaphoreTask.GetAwaiter()
            awaiter.OnCompleted(fun _ -> 
              match semaphoreTask.Status with
              | TaskStatus.RanToCompletion -> 
                let signal = semaphoreTask.Result
                if Interlocked.Add(&liveCounter, 0) = 0 then
                  ct.ThrowIfCancellationRequested()
                  tcs.SetResult(false)
                else
                  loop()
              | _ -> failwith "TODO process all task results"
              ()
            )
      loop()
      returnTask

    interface IEnumerator<KVP<'K,'V>> with
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K,'V> = KVP(this.CurrentKey, this.CurrentValue)
      member this.Current with get(): obj = this.Current :> obj 
      member this.Dispose(): unit = this.Dispose()

    interface ICursor<'K,'V> with
      member this.Comparer with get() = cmp
      member this.CurrentBatch = Unchecked.defaultof<_>
      member this.CurrentKey: 'K = this.CurrentKey
      member this.CurrentValue: 'V = this.CurrentValue
      member this.IsContinuous: bool = this.IsContinuous
      member this.MoveAt(key: 'K, direction: Lookup) : bool = this.MoveAt(key, direction)
      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()
      member this.MovePrevious(): bool = this.MovePrevious()
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNext(cancellationToken)
      member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        falseTask
      member this.Source = CursorSeries<'K,'V>(Func<ICursor<'K,'V>>((this :> ICursor<'K,'V>).Clone)) :> IReadOnlySeries<_,_>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'V>): bool =
        raise (NotSupportedException("UnionKeysCursor should be used only as a pivot inside continuous ZipN"))
      member this.Clone() = 
        raise (NotSupportedException("UnionKeysCursor should be used only as a pivot inside continuous ZipN"))


and
  // TODO use Span<'V> instead of 'V[]
  internal ZipNCursor<'K,'V,'R>(resultSelector:Func<'K,'V[],'R>, [<ParamArray>] cursorFactories:(unit->ICursor<'K,'V>)[]) as this =
    do
      if cursorFactories.Length < 2 then invalidArg "cursorFactories" "ZipN takes at least two cursor factories"
    let cursorsFactory() = cursorFactories |> Array.map (fun x -> x())
    let mutable cursors = cursorsFactory()

    // Current values of all cursors. We keep them in an array because for continuous cursors there is no current value,
    // they just return TryGetValue at a key. Also applying a resultSelector function to an array is fast.
    let currentValues = Array.zeroCreate cursors.Length // OptimizationSettings.ArrayPool.Take<'V>(cursors.Length)
    
    let cmp = 
      let c' = cursors.[0].Comparer
      for c in cursors do
        if not <| c.Comparer.Equals(c') then invalidOp "ZipNCursor: Comparers are not equal" 
      c'

    // Same meaning as in BingCursor: we have at least one sucessful move and some state for further moves
    let mutable hasValidState = false
    // all keys where discrete cursors are positioned. their intersect define where resulting keys are present.
    let discreteKeysSet = SortedDeque<_,_>(cursorFactories.Length, KVPComparer(cmp, Unchecked.defaultof<_>))

    let isContinuous = cursors |> Array.map (fun x -> x.IsContinuous) |> Array.forall id
    let unionKeys : ICursor<'K,'V> = if isContinuous then new UnionKeysCursor<'K,'V>(cursors) :> ICursor<'K,'V> else Unchecked.defaultof<_>
    
    
    /// TODO(perf) Now using TryGetValue without moving cursors. The idea is that continuous series are usually less frequent than
    /// the pivot ones, e.g. daily vs. minutely/secondly data, so the "depth" of binary search is not too big
    /// However, due to the same fact, one single MoveNext on daily data could cover many pivot points
    /// Continuous cursors should be optimized for the cases when the key in `.TryGetValue(key)` is between
    /// the current and the previous position of the continuous cursor
    let fillContinuousValuesAtKey (key:'K) =
        // we must try to get values from all continuous series, regardless if they are empty or not
        let mutable cont = true
        let mutable c = 0
        while cont && c < cursors.Length do
          if cursors.[c].IsContinuous then
            let mutable v = Unchecked.defaultof<_>
            let ok = cursors.[c].TryGetValue(key, &v)
            if ok then currentValues.[c] <- v
            else cont <- false // cannot get value
          c <- c + 1
        cont

    // return true only if all discrete cursors moved to the same key or they cannot move further
    let rec doMoveNext() =
      let mutable continueMoves = true
      // check if we reached the state where all cursors are at the same position
      while cmp.Compare(discreteKeysSet.FirstUnsafe.Key, discreteKeysSet.LastUnsafe.Key) < 0 && continueMoves do
        // pivotKeysSet is essentially a task queue:
        // we take every cursor that is not at the frontier and try to move it forward until it reaches the frontier
        // if we do this in parallel, the frontier could be moving while we move cursors
        let first = discreteKeysSet.RemoveFirstUnsafe()
        let ac = cursors.[first.Value]
        let mutable moved = true
        let mutable c = -1 // by construction // cmp.Compare(ac.CurrentKey, pivotKeysSet.Max.Key)
        
        // move active cursor forward while it is before the current max key
        // max key of non-cont series is the frontier: we will never get a value before it,
        // and if any pivot moves ahead of the frontier, then it shifts the frontier 
        // and the old one becomes unreachable
        while c < 0 && moved do
          moved <- ac.MoveNext()
          if moved then c <- cmp.Compare(ac.CurrentKey, discreteKeysSet.LastUnsafe.Key)

        if not moved then continueMoves <- false
        // must add it back regardless of moves
        // TODO (perf) should benefit here from RemoveFirstAddLast method, becuase is moved = true, 
        // we add the last value by construction
        discreteKeysSet.AddUnsafe(KVP(ac.CurrentKey, first.Value)) |> ignore

      // now all discrete cursors have moved at or ahead of frontier
      // the loop could stop only when all cursors are at the same key or we cannot move ahead
      if continueMoves then
        // this only possible if all discrete cursors are at the same key
        #if DEBUG
        Trace.Assert(cmp.Compare(discreteKeysSet.FirstUnsafe.Key, discreteKeysSet.LastUnsafe.Key) = 0)
        #endif
        if fillContinuousValuesAtKey(discreteKeysSet.FirstUnsafe.Key) then
          if not isContinuous then
            // now we could access values of discrete keys and fill current values with them
            for kvp in discreteKeysSet do // TODO (perf) Check if F# compiler behaves like C# one, optimizing for structs enumerator. Or just benchmark compared with for loop
              currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
          this.CurrentKey <- discreteKeysSet.FirstUnsafe.Key
          true
        else
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position

          let first = discreteKeysSet.RemoveFirstUnsafe()
          let firstCursor = if isContinuous then unionKeys else cursors.[first.Value]
          if firstCursor.MoveNext() then
            discreteKeysSet.AddUnsafe(KVP(firstCursor.CurrentKey, first.Value)) |> ignore
            doMoveNext() // recursive
          else
            // add back, should not be very often TODO (perf, low) add counter to see if this happens often
            discreteKeysSet.AddUnsafe(KVP(firstCursor.CurrentKey, first.Value)) |> ignore
            false
      else false
    
    // a copy of doMoveNextDiscrete() with changed direction. 
    let rec doMovePrevious() =
      let mutable continueMoves = true
      while cmp.Compare(discreteKeysSet.FirstUnsafe.Key, discreteKeysSet.LastUnsafe.Key) < 0 && continueMoves do
        let last = discreteKeysSet.RemoveLastUnsafe()
        let ac = cursors.[last.Value]
        let mutable moved = true
        let mutable c = +1
        
        // move active cursor forward while it is before the current max key
        // max key of non-cont series is the frontier: we will never get a value before it,
        // and if any pivot moves ahead of the frontier, then it shifts the frontier 
        // and the old one becomes unreachable
        while c > 0 && moved do
          moved <- ac.MovePrevious()
          if moved then c <- cmp.Compare(ac.CurrentKey, discreteKeysSet.FirstUnsafe.Key)

        if not moved then continueMoves <- false
        // must add it back regardless of moves
        discreteKeysSet.AddUnsafe(KVP(ac.CurrentKey, last.Value)) |> ignore

      // now all discrete cursors have moved at or ahead of frontier
      // the loop could stop only when all cursors are at the same key or we cannot move ahead
      if continueMoves then
        // this only possible if all discrete cursors are at the same key
        #if DEBUG
        Trace.Assert(cmp.Compare(discreteKeysSet.FirstUnsafe.Key, discreteKeysSet.LastUnsafe.Key) = 0)
        #endif
        if fillContinuousValuesAtKey(discreteKeysSet.FirstUnsafe.Key) then
          if not isContinuous then
            // now we could access values of discrete keys and fill current values with them
            for kvp in discreteKeysSet do // TODO (perf) Check if F# compiler behaves like C# one, optimizing for structs enumerator. Or just benchmark compared with for loop
              currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
          this.CurrentKey <- discreteKeysSet.LastUnsafe.Key
          true
        else
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position

          let last = discreteKeysSet.RemoveLastUnsafe()
          let lastCursor = if isContinuous then unionKeys else cursors.[last.Value]
          if lastCursor.MovePrevious() then
            discreteKeysSet.AddUnsafe(KVP(lastCursor.CurrentKey, last.Value)) |> ignore
            doMovePrevious() // recursive
          else
            // add back, should not be very often TODO (perf, low) add counter to see if this happens often
            discreteKeysSet.AddUnsafe(KVP(lastCursor.CurrentKey, last.Value)) |> ignore
            false
      else false

    // Manual state machine instead of a task computation expression, this is visibly faster
    let doMoveNextTask(ct:CancellationToken) : Task<bool> =
      #if DEBUG
      //Trace.Assert(this.HasValidState)
      #endif
      let mutable tcs = Runtime.CompilerServices.AsyncTaskMethodBuilder<bool>.Create() //new TaskCompletionSource<_>() //
      let returnTask = tcs.Task // NB! must access this property first
      let mutable firstStep = ref true
      let mutable sourceMoveTask = Unchecked.defaultof<_>
      let mutable initialPosition = Unchecked.defaultof<_>
      let mutable ac : ICursor<'K,'V> = Unchecked.defaultof<_>
      let rec loop(isOuter:bool) : unit =
        if isOuter then
          if not !firstStep 
            && 
              (
              (isContinuous && cmp.Compare(ac.CurrentKey, this.CurrentKey) > 0)
              || 
              (not isContinuous && cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0)
              )
            && fillContinuousValuesAtKey(ac.CurrentKey) then
            this.CurrentKey <- ac.CurrentKey
            if not isContinuous then
              // we set values only here, when we know that we could return
              for kvp in discreteKeysSet do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            tcs.SetResult(true) // the only true exit
          else
            if discreteKeysSet.Count = 0 then invalidOp "discreteKeysSet is empty"
            initialPosition <- discreteKeysSet.RemoveFirstUnsafe()
            ac <- if isContinuous then unionKeys else cursors.[initialPosition.Value]
            loop(false)
        else
          firstStep := false
          let idx = initialPosition.Value
          let cursor = ac
          let inline onMoved() =
            discreteKeysSet.Add(KVP(cursor.CurrentKey, idx)) |> ignore
            loop(true)
          let mutable reachedFrontier = false
          while not reachedFrontier && cursor.MoveNext() do
            if isContinuous then
              reachedFrontier <- 
                if hasValidState then cmp.Compare(cursor.CurrentKey, this.CurrentKey) > 0
                else true
            else
              reachedFrontier <- cmp.Compare(cursor.CurrentKey, this.CurrentKey) >= 0
          if reachedFrontier then
            onMoved()
          else
            // call itself until reached the frontier, then call outer loop
            sourceMoveTask <- cursor.MoveNext(ct)
            // there is a big chance that this task is already completed
            let inline onCompleted() =
              let moved =  sourceMoveTask.Result
              if not moved then
                tcs.SetResult(false) // the only false exit
                ()
              else
                if isContinuous then
                  let c = cmp.Compare(cursor.CurrentKey, this.CurrentKey)
                  if c > 0 then onMoved()
                  else
                    if hasValidState then
                      loop(false)
                    else
                      discreteKeysSet.Add(initialPosition)
                      loop(true)
                else
                  let c = cmp.Compare(cursor.CurrentKey, this.CurrentKey)
                  #if DEBUG
                  Trace.Assert(c > 0)
                  #endif
//                  if c < 0 then
//                    discreteKeysSet.Add(initialPosition)
//                    loop(false)
//                  else
                  onMoved()

            let awaiter = sourceMoveTask.GetAwaiter()
            // NB! do not block, use a callback
            awaiter.OnCompleted(fun _ ->
              // TODO! Test all cases
              if sourceMoveTask.Status = TaskStatus.RanToCompletion then
                onCompleted()
              else
                discreteKeysSet.Add(initialPosition) // TODO! Add/remove only when needed
                if sourceMoveTask.Status = TaskStatus.Canceled then
                  tcs.SetException(OperationCanceledException())
                else
                  tcs.SetException(sourceMoveTask.Exception)
            )
      loop(true)
      returnTask

    
    member this.Comparer with get() = cmp
    member this.HasValidState with get() = hasValidState

    member val IsContinuous = isContinuous

    member val CurrentKey = Unchecked.defaultof<'K> with get, set

    // NB lazy application of resultSelector, only when current value is requested
    member this.CurrentValue 
      with get() = 
      // TODO lazy 
        resultSelector.Invoke(this.CurrentKey, currentValues)

    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
    member val CurrentBatch = Unchecked.defaultof<_> with get, set

    member this.Reset() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Reset()) |> ignore

    member this.Dispose() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Dispose()) |> ignore
      //OptimizationSettings.ArrayPool.Return(currentValues) |> ignore
    
    member this.Clone(): ICursor<'K,'R> =
      // run-time type of the instance, could be derived type
      let clone = new ZipNCursor<'K,'V,'R>(resultSelector, cursorFactories) :> ICursor<'K,'R>
      if hasValidState then 
        // TODO!!! There is a bug inside MoveAt()
        let movedOk = clone.MoveAt(this.CurrentKey, Lookup.EQ)
        Trace.Assert(movedOk) // if current key is set then we could move to it
      clone
         

    member this.MoveFirst(): bool =
      let mutable doContinue = true
      let mutable valuesOk = false
      let mutable movedFirst = false
      discreteKeysSet.Clear()
      while doContinue do
        if not movedFirst then
          if isContinuous then
            movedFirst <- unionKeys.MoveFirst()
            if movedFirst then discreteKeysSet.Add(KVP(unionKeys.CurrentKey, 0)) |> ignore
            doContinue <- movedFirst
          else
            let mutable i = 0
            for x in cursors do
              if not cursors.[i].IsContinuous then 
                let movedFirst' = x.MoveFirst()
                if movedFirst' then
                  discreteKeysSet.Add(KVP(x.CurrentKey, i)) |> ignore 
                else doContinue <- false
              i <- i + 1
            movedFirst <- doContinue
        else
          // all cursors are positioned so that it is possible to get value, but not guaranteed
          // if we are lucky and have equal keys right after MoveFirst of each cursors
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
            if not isContinuous then
              for kvp in discreteKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            this.CurrentKey <- discreteKeysSet.First.Key
            valuesOk <- true
            doContinue <- false 
          else
            // move to max key until min key matches max key so that we can use values
            valuesOk <- doMoveNext()
            doContinue <- valuesOk
      hasValidState <- valuesOk
      valuesOk

    member this.MoveLast(): bool = 
      let mutable doContinue = true
      let mutable valuesOk = false
      let mutable movedLast = false
      discreteKeysSet.Clear()
      while doContinue do
        if not movedLast then
          if isContinuous then
            movedLast <- unionKeys.MoveLast()
            if movedLast then discreteKeysSet.Add(KVP(unionKeys.CurrentKey, 0)) |> ignore
            doContinue <- movedLast
          else
            let mutable i = 0
            for x in cursors do
              if not x.IsContinuous then 
                let movedFirst' = x.MoveLast()
                if movedFirst' then
                  let kv = KVP(x.CurrentKey, i)
                  discreteKeysSet.Add(kv) |> ignore 
                else doContinue <- false
              i <- i + 1
            movedLast <- doContinue
        else
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
            if not isContinuous then
              for kvp in discreteKeysSet do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            this.CurrentKey <- discreteKeysSet.Last.Key
            valuesOk <- true
            doContinue <- false 
          else
            valuesOk <- doMovePrevious()
            doContinue <- valuesOk
      hasValidState <- valuesOk
      valuesOk

    member x.MoveAt(key: 'K, direction: Lookup) : bool =
      let mutable doContinue = true
      let mutable valuesOk = false
      let mutable movedAt = false
      discreteKeysSet.Clear()
      while doContinue do
        if not movedAt then
          if isContinuous then
            movedAt <- unionKeys.MoveAt(key, direction)
            if movedAt then discreteKeysSet.Add(KVP(unionKeys.CurrentKey, 0)) |> ignore
            doContinue <- movedAt
          else
            let mutable i = 0
            for x in cursors do
              if not cursors.[i].IsContinuous then 
                let movedAt' = x.MoveAt(key, direction)
                if movedAt' then
                  discreteKeysSet.Add(KVP(x.CurrentKey, i)) |> ignore 
                else doContinue <- false
              i <- i + 1
            movedAt <- doContinue
        else
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
            if not isContinuous then
              for kvp in discreteKeysSet do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
            this.CurrentKey <- discreteKeysSet.First.Key
            valuesOk <- true
            doContinue <- false 
          else
            match direction with
            | Lookup.EQ -> 
              valuesOk <- false
              doContinue <- false
            | Lookup.LE | Lookup.LT ->
              valuesOk <- doMovePrevious()
            | Lookup.GE | Lookup.GT ->
              valuesOk <- doMoveNext()
            | _ -> failwith "Wrong lookup direction, should never be there"
      hasValidState <- valuesOk
      valuesOk

    member this.MoveNext(): bool =
      if not this.HasValidState then this.MoveFirst()
      else
        let doContinue =
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 then
            let first = discreteKeysSet.RemoveFirstUnsafe()
            let ac = if isContinuous then unionKeys else cursors.[first.Value]
            if ac.MoveNext() then
              discreteKeysSet.Add(KVP(ac.CurrentKey, first.Value)) |> ignore
              true
            else
              discreteKeysSet.Add(first)
              false
          else true
        if doContinue then doMoveNext()
        else false
            
    member this.MovePrevious(): bool = 
      if not this.HasValidState then this.MoveLast()
      else
        let cont =
          if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 then
            let last = discreteKeysSet.RemoveLastUnsafe()
            let ac = if isContinuous then unionKeys else cursors.[last.Value]
            if ac.MovePrevious() then
              discreteKeysSet.Add(KVP(ac.CurrentKey, last.Value)) |> ignore
              true
            else 
              discreteKeysSet.Add(last)
              false
          else true
        if cont then doMovePrevious()
        else false


    member private this.MoveFirst(ct): Task<bool> =
      task {
        let mutable doContinue = true
        let mutable valuesOk = false
        let mutable movedFirst = false
        discreteKeysSet.Clear()
        while doContinue do
          if not movedFirst then
            if isContinuous then
              movedFirst <- unionKeys.MoveFirst()
              if not movedFirst then
                let! movedAsync = unionKeys.MoveNext(ct)
                movedFirst <- movedAsync
              if movedFirst then
                discreteKeysSet.Add(KVP(unionKeys.CurrentKey, 0)) |> ignore
              doContinue <- movedFirst
            else
              let mutable i = 0
              for x in cursors do
                if not cursors.[i].IsContinuous then 
                  let mutable movedFirst' = x.MoveFirst()
                  if not movedFirst' then 
                    let! movedAsync = x.MoveNext(ct)
                    movedFirst' <- movedAsync
                  if movedFirst' then
                    discreteKeysSet.Add(KVP(x.CurrentKey, i)) |> ignore 
                  else doContinue <- false
                i <- i + 1
              movedFirst <- doContinue
          else
            // all cursors are positioned so that it is possible to get value, but not guaranteed
            // if we are lucky and have equal keys right after MoveFirst of each cursors
            if cmp.Compare(discreteKeysSet.First.Key, discreteKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(discreteKeysSet.First.Key) then
              if not isContinuous then
                for kvp in discreteKeysSet.AsEnumerable() do
                  currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- discreteKeysSet.First.Key
              valuesOk <- true
              doContinue <- false
            else
              // move to max key until min key matches max key so that we can use values
              let! movedNext = doMoveNextTask(ct)
              valuesOk <- movedNext
              doContinue <- valuesOk
        hasValidState <- valuesOk
        return valuesOk
      }


    member this.MoveNext(ct:CancellationToken): Task<bool> =
      if this.HasValidState then doMoveNextTask(ct)
      else this.MoveFirst(ct)


    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
      let mutable cont = true
      let values = 
        cursors 
        // NB cursors are single-threaded, all inner cursors were created by this thread
        // if we pass them to a thread pool, the current thread should not touch them until
        // all TGV return. So parallel is probably safe here, but for simple cursors
        // switching costs could be higher. We could benchmark after which N parallel if better
        // for simplest cursors, and later return to the idea of storing internal complexity/depth of 
        // cursors in metadata, e.g. in ConditionalWeakTable (this definitely shouldn't be a part
        // of ICursor, but we could make an internal interface and check if a cursor implements it)
        // CWT is an interesting thing and I want to try using it for metadata of objects, R-like style.
        |> Array.map (fun x ->
          let mutable v = Unchecked.defaultof<_>
          let ok = x.TryGetValue(key, &v)
          if ok then v
          else
            cont <- false
            Unchecked.defaultof<'V>
        )
      if cont then
        value <- resultSelector.Invoke(key, values)
        true
      else false
        
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = falseTask
    
    //member this.IsBatch with get() = this.IsBatch
    member this.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((this :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>

    interface IEnumerator<KVP<'K,'R>> with
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'R> = KVP(this.CurrentKey, this.CurrentValue)
      member this.Current with get(): obj = this.Current :> obj 
      member this.Dispose(): unit = this.Dispose()

    // TODO (perf, low) move implementations directly to interface, that will save one callvirt. That trick has already improved perf in other places, e.g. SortedDeque.

    interface ICursor<'K,'R> with
      member this.Comparer with get() = cmp
      member this.CurrentBatch = this.CurrentBatch
      member this.CurrentKey: 'K = this.CurrentKey
      member this.CurrentValue: 'R = this.CurrentValue
      member this.IsContinuous: bool = this.IsContinuous
      member this.MoveAt(key: 'K, direction: Lookup) : bool = this.MoveAt(key, direction)
      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()
      member this.MovePrevious(): bool = this.MovePrevious()
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNext(cancellationToken)
      member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNextBatch(cancellationToken)
      member this.Source = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((this :> ICursor<'K,'R>).Clone)) :> IReadOnlySeries<_,_>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
        this.TryGetValue(key, &value)
      member this.Clone() = this.Clone()
