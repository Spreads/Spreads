namespace Spreads.Collections.Experimental

open System
open System.Linq
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks

open Spreads
open Spreads.Collections

// reminder: cursor is always at some position or not started or finished, series have multiple cursors
// if we are starting move, than we are free to move any cursors received from cursor factories
// cursor is single threaded and thread-unsafe. Only one thread could move cursor at time


// TODO(perf) parallel should partition cursors by N elements
// N depends on value size, e.g. for double we could suffer from false sharing if we update current values too often from
// diffent thread. But we write to currentValues only when we are at a frontier, so the impact could be not too big
// Another issue is cost of task scheduling. We should somehow estimate cost of cursor moves and partition cursors only
// when cost of each cursor is higher than the cost of task scheduling.
// We could add a property Complexity to ICursor, that is measured as a multiple of MoveNext on SortedMap (which is also close to a cost of FLOP)
// and then decide on degree of concurrency based on the complexity of cursors. By doing so, we will process cheap cursors on the same thread
// and will parallelize expensive cursors.


type ZipNCursor<'K,'V,'R>(resultSelector:Func<'K,'V[],'R>, [<ParamArray>] cursorFactories:(unit->ICursor<'K,'V>)[]) as this =
  
    let cursorsFactory() = cursorFactories |> Array.map (fun x -> x())
    let mutable cursors = cursorsFactory()
    // positions of cursor, including virtual positions of continuous cursors
    // NB this is probably not needed, one of attempts to deal with continuous cursors
    let positions = Array.zeroCreate<'K> cursors.Length

    // current values of all cursor. we keep them in an array because for continuous cursors there is no current value,
    // they just return TryGetValue at a key. Also, applying a resultSelector function to an array is fast
    let currentValues = Array.zeroCreate<'V> cursors.Length
    
    let cmp = 
      let c' = cursors.[0].Comparer
      for c in cursors do
        if not <| c.Comparer.Equals(c') then invalidOp "ZipNCursor: Comparers are not equal" 
      c'

    let continuous = cursors |> Array.map (fun x -> x.IsContinuous) // NB only two bool arrays per cursor, could use BitArray but "later"(TM)
    let isContinuous = continuous |> Array.forall id

    // indicates that previous move was OK and that a next move should not pre-build a state
    let mutable hasValidState = false
    // for ZipN, valid states are:
    // - all cursors are at the same key (virtually for continuous, they are at the next existing key)
    // - cursors are not at the same position but one of them returned false on move next/previous after 
    //   we tried to move from a valid state. In this state we could call MoveNextAsync and try to call Move Next repeatedly.
    //   current key/values are undefined here because a move returned false

    // all keys where non-continuous cursors are positioned. they define where resulting keys are present
    let pivotKeysSet = SortedDeque(KVComparer(cmp, Comparer<int>.Default))
    // active continuous cursors
    let contKeysSet = SortedDeque(KVComparer(cmp, Comparer<int>.Default))
    

    /// TODO(perf) Now using TryGetValue without moving cursors. The idea is that continuous series are usually less frequent than
    /// the pivot ones, e.g. daily vs. minutely/secondly data, so the "depth" of binary search is not too big
    /// However, due to the same fact, one single MoveNext on daily data could cover many pivot points
    /// Continuous cursors should be optimized for the cases when the key in `.TryGetValue(key)` is between
    /// the current and the previous position of the continuous cursor
    let fillContinuousValuesAtKey (key:'K) =
      if contKeysSet.Count = 0 then true
      else
        let mutable cont = true
        let mutable c = 0
        while cont && c < cursors.Length do
          if continuous.[c] then
            let ok, value = cursors.[c].TryGetValue(key)
            if ok then currentValues.[c] <- value
            else cont <- false // cannot get value
          c <- c + 1
        cont

    // do... functions do move at least one cursor, so they should only be called
    // when state is valid or when it is proven invalid nad we must find the first valid position
    // MoveFirst/Last/At ust try to check the initial position before calling the do... functions

    // non-cont
    // this is a single-threaded algotithm that uses a SortedDeque data structure to determine moves priority
    let rec doMoveNextNonContinuous() =
      let mutable cont = true
      // check if we reached the state where all cursors are at the same position
      while cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) < 0 && cont do
        // pivotKeysSet is essentially a task queue:
        // we take every cursor that is not at fthe frontier and try to move it forward until it reaches the frontier
        // if we do this in parallel, the frontier could be moving while we move cursors
        let first = pivotKeysSet.RemoveFirst()
        let ac = cursors.[first.Value]
        let mutable moved = true
        let mutable c = -1 // by construction // cmp.Compare(ac.CurrentKey, pivotKeysSet.Max.Key)
        
        // move active cursor forward while it is before the current max key
        // max key of non-cont series is the frontier: we will never get a value before it,
        // and if any pivot moves ahead of the frontier, then it shifts the frontier 
        // and the old one becomes unreachable

        while c < 0 && moved do
          moved <- ac.MoveNext()
          c <- cmp.Compare(ac.CurrentKey, pivotKeysSet.Last.Key)

        if moved then
          currentValues.[first.Value] <- ac.CurrentValue
          pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
        else
          cont <- false // cannot move, stop sync move next, leave cursors where they are
      if cont then
        if fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
          this.CurrentKey <- pivotKeysSet.First.Key
          true
        else 
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position
          let first =  pivotKeysSet.RemoveFirst()
          let ac = cursors.[first.Value]
          if ac.MoveNext() then
            currentValues.[first.Value] <- ac.CurrentValue
            pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
            doMoveNextNonContinuous() // recursive
          else false
      else false

    // Continuous
    let doMoveNextContinuousSlow(frontier:'K) =
      
      // frontier is the current key. on each zip move we must move at least one cursor ahead 
      // of the current key, and the position of this cursor is the new key
      //    [---x----x-----x-----x-------x---]
      //    [-----x--|--x----x-----x-------x-]
      //    [-x----x-|---x-----x-------x-----]

      // found all values
      let mutable valuesOk = false
      let cksEnumerator = contKeysSet.AsEnumerable().GetEnumerator()
      let mutable found = false
      while not found && cksEnumerator.MoveNext() do
        let position = cksEnumerator.Current
        let cursor = cursors.[position.Value]
        let mutable moved = true
        while cmp.Compare(cursor.CurrentKey, frontier) <= 0 && moved && not found do
          moved <- cursor.MoveNext()
          if moved then // cursor moved
            contKeysSet.Remove(position)
            contKeysSet.Add(KV(cursor.CurrentKey, position.Value))
            
            if cmp.Compare(cursor.CurrentKey, frontier) > 0  // ahead of the previous key
              && fillContinuousValuesAtKey(cursor.CurrentKey) then // and we could get all values at the new position
              found <- true
              valuesOk <- true
              this.CurrentKey <- cursor.CurrentKey
      valuesOk

    // Continuous
    let doMoveNextContinuous(frontier:'K) =
      
      // frontier is the current key. on each zip move we must move at least one cursor ahead 
      // of the current key, and the position of this cursor is the new key
      //    [---x----x-----x-----x-------x---]
      //    [-----x--|--x----x-----x-------x-]
      //    [-x----x-|---x-----x-------x-----]

      // found all values
      let mutable valuesOk = false
      let mutable found = false
      //let cksEnumerator = contKeysSet.AsEnumerable().GetEnumerator()
      while not found do
        let mutable firstKeyAfterTheCurrentFrontier = Unchecked.defaultof<'K>
        let mutable firstKeyAfterTheCurrentFrontierIsSet = false
        let mutable cidx = 0 // cursor index
        let mutable step = 0
        while step < contKeysSet.Count do
          let initialPosition = contKeysSet.[cidx]
          let cursor = cursors.[initialPosition.Value]
          let mutable shouldMove = cmp.Compare(cursor.CurrentKey, frontier) <= 0
          let mutable moved = false
          while shouldMove do
            moved <- cursor.MoveNext()
            shouldMove <- moved && cmp.Compare(cursor.CurrentKey, frontier) <= 0
          if moved then // cursor moved
            if not firstKeyAfterTheCurrentFrontierIsSet then
              firstKeyAfterTheCurrentFrontierIsSet <- true
              firstKeyAfterTheCurrentFrontier <- cursor.CurrentKey
            elif cmp.Compare(cursor.CurrentKey, firstKeyAfterTheCurrentFrontier) < 0 then
              // if there is a key that above the frontier but less than previously set
              firstKeyAfterTheCurrentFrontier <- cursor.CurrentKey
            let newPosition = KV(cursor.CurrentKey, initialPosition.Value)
            if contKeysSet.comparer.Compare(newPosition, initialPosition) > 0 then
              contKeysSet.Remove(initialPosition)
              contKeysSet.Add(newPosition)
            else cidx <- cidx + 1
          else
            if firstKeyAfterTheCurrentFrontierIsSet && cmp.Compare(cursor.CurrentKey, firstKeyAfterTheCurrentFrontier) < 0 then
              firstKeyAfterTheCurrentFrontier <- cursor.CurrentKey
            cidx <- cidx + 1
          step <- step + 1
        if firstKeyAfterTheCurrentFrontierIsSet then
          found <- fillContinuousValuesAtKey(firstKeyAfterTheCurrentFrontier) 
          if found then 
            valuesOk <- true
            this.CurrentKey <- firstKeyAfterTheCurrentFrontier
        else
          found <- true // cannot move past existing frontier
          valuesOk <- false
      valuesOk
      

    let rec doMovePrevNonCont() =
      let mutable cont = true
      //let mutable activeCursorIdx = 0
      // check if we reached the state where all cursors are at the same position
      while cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) < 0 && cont do //
        // pivotKeysSet is essentially a task queue:
        // we take every cursor that is not at fthe frontier and try to move it forward until it reaches the frontier
        // if we do this in parallel, the frontier could be moving while we are 
        let last = pivotKeysSet.RemoveLast()
        let ac = cursors.[last.Value]
        let mutable moved = true
        let mutable c = +1 // by construction 

        // move active cursor backward while it is before the current min key
        // ... see move next

        while c > 0 && moved do
          moved <- ac.MovePrevious()
          c <- cmp.Compare(ac.CurrentKey, pivotKeysSet.First.Key)

        if moved then
          currentValues.[last.Value] <- ac.CurrentValue
          pivotKeysSet.Add(KV(ac.CurrentKey, last.Value)) |> ignore // TODO(low) SortedDeque AddFirst optimization similar to last.
        else
          cont <- false // cannot move, stop sync move next, leave cursors where they are
      if cont then
        if fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
          this.CurrentKey <- pivotKeysSet.First.Key
          true
        else
          // cannot get contiuous values at this key
          // move first non-cont cursor to next position
          let first =  pivotKeysSet.RemoveFirst()
          let ac = cursors.[first.Value]
          if ac.MoveNext() then
            currentValues.[first.Value] <- ac.CurrentValue
            pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
            doMoveNextNonContinuous() // recursive
          else false
      else false

    let doMovePrevContinuous(frontier:'K) =
      
      // found all values
      let mutable valuesOk = false
      let cksEnumerator = contKeysSet.Reverse().GetEnumerator()
      let mutable found = false
      while not found && cksEnumerator.MoveNext() do // need to update contKeysSet!!!!!!!!!!!!!!!!!!!
        let position = cksEnumerator.Current
        let cursor = cursors.[position.Value]
        let mutable moved = true
        while cmp.Compare(cursor.CurrentKey, frontier) >= 0 && moved && not found do
          moved <- cursor.MovePrevious()
          if moved then // cursor moved
            contKeysSet.Remove(position)
            contKeysSet.Add(KV(cursor.CurrentKey, position.Value))
            
            if cmp.Compare(cursor.CurrentKey, frontier) < 0  // ahead of the previous key
              && fillContinuousValuesAtKey(cursor.CurrentKey) then // and we could get all values at the new position
              found <- true
              valuesOk <- true
              this.CurrentKey <- cursor.CurrentKey
      valuesOk
    

    member this.Comparer with get() = cmp
    member this.HasValidState with get() = hasValidState and set (v) = hasValidState <- v

    member val IsContinuous = isContinuous

    member val CurrentKey = Unchecked.defaultof<'K> with get, set

    // NB lazy application of resultSelector, only when current value is requested
    member this.CurrentValue with get() = resultSelector.Invoke(this.CurrentKey, currentValues)

    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
    member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>> with get, set

    member this.Reset() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Reset()) |> ignore

    member this.Dispose() = 
      hasValidState <- false
      cursors |> Array.map (fun x -> x.Dispose()) |> ignore
    
    member this.Clone(): ICursor<'K,'R> =
      // run-time type of the instance, could be derived type
      let clone = new ZipNCursor<'K,'V,'R>(resultSelector, cursorFactories) :> ICursor<'K,'R>
      if hasValidState then 
        let movedOk = clone.MoveAt(this.CurrentKey, Lookup.EQ)
        Trace.Assert(movedOk) // if current key is set then we could move to it
      clone
         

    member this.MoveNext(): bool =
      if not this.HasValidState then this.MoveFirst()
      else
        if isContinuous then
          doMoveNextContinuous(this.CurrentKey)
        else
          let cont =
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 then
              let first = pivotKeysSet.RemoveFirst()
              let ac = cursors.[first.Value]
              if ac.MoveNext() then
                currentValues.[first.Value] <- ac.CurrentValue
                pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
                true
              else false
            else true
          if cont then doMoveNextNonContinuous()
          else false
    
    member x.MovePrevious(): bool = 
      if not this.HasValidState then this.MoveLast()
      else
        if isContinuous then
          doMovePrevContinuous(this.CurrentKey)
        else
          let cont =
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 then
              let last = pivotKeysSet.RemoveLast()
              let ac = cursors.[last.Value]
              if ac.MovePrevious() then
                currentValues.[last.Value] <- ac.CurrentValue
                pivotKeysSet.Add(KV(ac.CurrentKey, last.Value)) |> ignore
                true
              else false
            else true
          if cont then doMovePrevNonCont()
          else false

    member this.MoveFirst(): bool =
      let mutable cont = true
      let mutable valuesOk = false
      let mutable allMovedFirst = false
      pivotKeysSet.Clear()
      contKeysSet.Clear()
      while cont do
        if not allMovedFirst then
          cursors 
          |> Array.iteri (fun i x -> 
            let movedFirst = x.MoveFirst()
            if movedFirst then
              if continuous.[i] then 
                contKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              else
                pivotKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              currentValues.[i] <- x.CurrentValue
            else
              cont <- false // series has no values, stop here
          )
          allMovedFirst <- cont
        else
          if isContinuous then
            if fillContinuousValuesAtKey(contKeysSet.First.Key) then
              this.CurrentKey <- contKeysSet.First.Key
              valuesOk <- true
              cont <- false
            else
              valuesOk <- doMoveNextContinuous(contKeysSet.First.Key)
          else
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
              for kvp in pivotKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- pivotKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              // move to max key until min key matches max key so that we can use values
              valuesOk <- doMoveNextNonContinuous()
              cont <- not valuesOk
      if valuesOk then 
        this.HasValidState <- true
        true
      else false

    member this.MoveLast(): bool = 
      let mutable cont = true
      let mutable valuesOk = false
      let mutable allMovedLast = false
      pivotKeysSet.Clear()
      contKeysSet.Clear()
      while cont do
        if not allMovedLast then
          cursors 
          |> Array.iteri (fun i x -> 
            let movedLast = x.MoveLast()
            if movedLast then
              if continuous.[i] then 
                contKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              else
                pivotKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              currentValues.[i] <- x.CurrentValue
            else
              cont <- false // series has no values, stop here
          )
          allMovedLast <- cont
        else
          if isContinuous then
            if fillContinuousValuesAtKey(contKeysSet.Last.Key) then
              this.CurrentKey <- contKeysSet.Last.Key
              valuesOk <- true
              cont <- false
            else
              valuesOk <- doMovePrevContinuous(contKeysSet.Last.Key)
          else
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
              for kvp in pivotKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- pivotKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              // move to max key until min key matches max key so that we can use values
              valuesOk <- doMovePrevNonCont() //failwith "TODO" //this.DoMoveNext()
              cont <- not valuesOk
      if valuesOk then 
        this.HasValidState <- true
        true
      else false

    member x.MoveAt(key: 'K, direction: Lookup) : bool =
      let mutable cont = true
      let mutable valuesOk = false
      let mutable allMovedAt = false
      pivotKeysSet.Clear()
      contKeysSet.Clear()
      while cont do
        if not allMovedAt then
          cursors 
          |> Array.iteri (fun i x -> 
            let movedAt = x.MoveAt(key, direction)
            if movedAt then
              if continuous.[i] then 
                contKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              else
                pivotKeysSet.Add(KV(x.CurrentKey, i)) |> ignore
              currentValues.[i] <- x.CurrentValue
            else
              cont <- false // series has no values, stop here
          )
          allMovedAt <- cont
        else
          if isContinuous then
            // this condition is applied to all directions
            if cmp.Compare(contKeysSet.First.Key, contKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(contKeysSet.First.Key) then
              this.CurrentKey <- contKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              match direction with
              | Lookup.EQ -> 
                valuesOk <- false
                cont <- false
              | Lookup.LE | Lookup.LT ->
                if fillContinuousValuesAtKey(contKeysSet.Last.Key) then
                  this.CurrentKey <- contKeysSet.Last.Key
                  valuesOk <- true
                  cont <- false
                else
                  valuesOk <- doMovePrevContinuous(contKeysSet.Last.Key)
                  cont <- not valuesOk
              | Lookup.GE | Lookup.GT ->
                if fillContinuousValuesAtKey(contKeysSet.First.Key) then
                  this.CurrentKey <- contKeysSet.First.Key
                  valuesOk <- true
                  cont <- false 
                else
                  valuesOk <- doMoveNextContinuous(contKeysSet.First.Key)
                  cont <- not valuesOk
              | _ -> failwith "Wrong lookup direction, should never be there"
          else
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 
                  && fillContinuousValuesAtKey(pivotKeysSet.First.Key) then
              for kvp in pivotKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- pivotKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              match direction with
              | Lookup.EQ -> 
                valuesOk <- false
                cont <- false
              | Lookup.LE | Lookup.LT ->
                valuesOk <- doMovePrevNonCont()
              | Lookup.GE | Lookup.GT ->
                valuesOk <- doMoveNextNonContinuous()
              | _ -> failwith "Wrong lookup direction, should never be there"
      if valuesOk then 
        this.HasValidState <- true
        true
      else false

    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
      let mutable cont = true
      let values = 
        cursors 
        |> Array.map (fun x ->  // TODO instead of Array.Parallel, use PLINQ, it is smart and tested, I do not have the same confidence in F#.Core
          let ok, value = x.TryGetValue(key)
          if ok then value 
          else 
            cont <- false
            Unchecked.defaultof<'V>
        )
      if cont then
        value <- resultSelector.Invoke(key, values)
        true
      else false
    
    
    member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    member x.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    
    //member x.IsBatch with get() = x.IsBatch
    member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((x :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>

    interface IEnumerator<KVP<'K,'R>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'R> = this.Current
      member this.Current with get(): obj = this.Current :> obj 
      member x.Dispose(): unit = x.Dispose()

    interface ICursor<'K,'R> with
      member this.Comparer with get() = cmp
      member x.Current: KVP<'K,'R> = KVP(x.CurrentKey, x.CurrentValue)
      member x.CurrentBatch: IReadOnlyOrderedMap<'K,'R> = x.CurrentBatch
      member x.CurrentKey: 'K = x.CurrentKey
      member x.CurrentValue: 'R = x.CurrentValue
      member x.IsContinuous: bool = x.IsContinuous
      member x.MoveAt(key: 'K, direction: Lookup) : bool = x.MoveAt(key, direction)

      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()

      member this.MovePrevious(): bool = this.MovePrevious()
    
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNext(cancellationToken)
 
       member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        this.MoveNextBatch(cancellationToken)
     
      //member x.IsBatch with get() = x.IsBatch
      member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((x :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>
      
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
        // TODO should keep a lazy array of cursors that is initiated on first call to this function
        // and then is reused on evey call
        this.TryGetValue(key, &value)
    
      member this.Clone() = this.Clone()