namespace Spreads

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


// we treat continuous cursor as normal ones until they overshoot or cannot move further


type ZipNCursor<'K,'V,'R when 'K : comparison>(resultSelector:Func<'K,'V[],'R>, [<ParamArray>] cursorFactories:(unit->ICursor<'K,'V>)[]) =
  
    let cursorsFactory() = cursorFactories |> Array.map (fun x -> x())
    let mutable cursors = cursorsFactory()
    // positions of cursor, including virtual positions of continuous cursors
    // NB this is probably not needed, one of attempts to deal with continuous cursors
    let positions = Array.zeroCreate<'K> cursors.Length

    //let hasValue = Array.zeroCreate<bool> cursors.Length // NB only two bool arrays per cursor, could use BitArray but "later"(TM)

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

//    let mutable maxKey = Unchecked.defaultof<'K opt>
//    let mutable minKey = Unchecked.defaultof<'K opt>
//    let updateMinMax key = 
//      if maxKey.IsMissing then maxKey <- OptionalValue(key)
//      if minKey.IsMissing then minKey <- OptionalValue(key)
//      if cmp.Compare(key, maxKey.Present) > 0 then maxKey <- OptionalValue(key)
//      if cmp.Compare(key, minKey.Present) < 0 then minKey <- OptionalValue(key)
//    let minMaxAreEqual() = cmp.Compare(minKey.Present, maxKey.Present) = 0

    // indicates that previous move was OK and that a next move should not pre-build a state
    let mutable hasValidState = false
    // for ZipN, valid states are:
    // - all cursors are at the same key (virtually for continuous, they are at the next existing key)
    // - cursors are not at the same position but one of them returned false on move next/previous after 
    //   we tried to move from a valid state. In this state we could call MoveNextAsync and try to call Move Next repeatedly.
    //   current key/values are undefined here because a move returned false

    // all keys where non-continuous cursors are positioned. they define where resulting keys are present
    let pivotKeysSet = SortedDeque(KVComparer(cmp, Comparer<int>.Default))
    let contKeysSet = SortedDeque(KVComparer(cmp, Comparer<int>.Default))


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

//    member private this.DoMoveNext() =
//      let mutable cont = true
//      let mutable activeCursorIdx = 0
//      let mutable wellPositionedCursors = 0 // by definition, at least one cursor is positioned at maximum
//      // check if we reached the state where all cursors are at the same position
//      while wellPositionedCursors < cursors.Length && cont do //
//        let ac = cursors.[activeCursorIdx]
//        let mutable moved = true
//        let mutable c = cmp.Compare(ac.CurrentKey, maxKey.Present)
//        // move active cursor forward while it is before the current max key
//        while c < 0 && moved do
//          moved <- ac.MoveNext()
//          c <- cmp.Compare(ac.CurrentKey, maxKey.Present)
//        if moved then
//          if c = 0 then
//            wellPositionedCursors <- wellPositionedCursors + 1
//            currentValues.[activeCursorIdx] <- ac.CurrentValue
//          else
//            if ac.IsContinuous then
//              // if cursor is continuous, we must try to get value at current max. 
//              // continuous cursors must be optimized for the case when we TryGetValue for keys
//              // between current and previous
//              let canGetValue, valueAtMaxKey = ac.TryGetValue(maxKey.Present)
//              if canGetValue then
//                wellPositionedCursors <- wellPositionedCursors + 1
//                currentValues.[activeCursorIdx] <- valueAtMaxKey
//              else
//                cont <- false // stop, cannot get value
//              ()
//            else
//              wellPositionedCursors <- 1 // this cursor becomes the only well-positioned, because its key is strictly greater
//                                         // than the previous max and it becomes new max
//              // update max value, continue
//              maxKey <- OptionalValue(ac.CurrentKey)
//              currentValues.[activeCursorIdx] <- ac.CurrentValue
//              ()
//        else
//          // TODO?? what is continuous cursor is not moved but other cursors are just longer, e.g. repeat annual series for daily one
//          cont <- false // cannot move, stop sync move next, leave cursors where they are
//        let newActiveCursor = (activeCursorIdx + 1)
//        activeCursorIdx <- if newActiveCursor = cursors.Length then 0 else newActiveCursor // (activeCursor + 1L) % cursors.LongLength // NB be crazy and avoid "expensive" modulo op :)
//      if wellPositionedCursors = cursors.Length then 
//        this.CurrentKey <- maxKey.Present
//        true
//      else false

    // Fill currentValues with values from continuous series
    member private this.FillContinuousValuesAtKey(key:'K) =
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

  

    // this is a single-threaded algotithm that uses a heap/sorted-deque data structure to determine moves priority
    // 
    member private this.DoMoveNextNonCont() =
      let mutable cont = true
      //let mutable activeCursorIdx = 0
      // check if we reached the state where all cursors are at the same position
      while cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) < 0 && cont do //
        // pivotKeysSet is essentially a task queue:
        // we take every cursor that is not at fthe frontier and try to move it forward until it reaches the frontier
        // if we do this in parallel, the frontier could be moving while we are 
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
          // TODO?? what is continuous cursor is not moved but other cursors are just longer, e.g. repeat annual series for daily one
          cont <- false // cannot move, stop sync move next, leave cursors where they are
      if cont then
        if this.FillContinuousValuesAtKey(pivotKeysSet.First.Key) then
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
            this.DoMoveNextNonCont() // recursive
          else false
      else false


//    member private this.DoMoveNextNonContPar() =
//      let mutable cont = true
//
//      cursors |> Array.iteri(fun i c ->
//        //let mutable activeCursorIdx = 0
//        // check if we reached the state where all cursors are at the same position
//        while lock this.Source (fun _ -> cmp.Compare(pivotKeysSet.MinValue.Key, pivotKeysSet.MaxValue.Key) < 0 && cont) do //
//          //let first = pivotKeysSet.RemoveFirst()
//          let kv = KV(c.CurrentKey, i)
//          
//          let ac = c //cursors.[first.Value]
//          let mutable moved = true
//          let mutable c = 
//            lock this.Source (fun _ ->
//              cmp.Compare(ac.CurrentKey, pivotKeysSet.MaxValue.Key)
//            )
////          if c < 0 then 
////            lock this.Source (fun _ ->
////              pivotKeysSet.list.Remove(kv) |> ignore
////            )
//          let shouldRemove = c < 0
//          // move active cursor forward while it is before the current max key
//          while c < 0 && moved do
//            moved <- ac.MoveNext()
//            c <- cmp.Compare(ac.CurrentKey, pivotKeysSet.MaxValue.Key)
//          if moved then
//            currentValues.[i] <- ac.CurrentValue
//            lock this.Source (fun _ ->
//              if shouldRemove && pivotKeysSet.Remove(kv) then
//                 () //|> ignore
//              else 
//                Console.WriteLine("Cannot remove")
//              pivotKeysSet.Add(KV(ac.CurrentKey, i)) |> ignore
//            )
//          else
//            // TODO?? what is continuous cursor is not moved but other cursors are just longer, e.g. repeat annual series for daily one
//            cont <- false // cannot move, stop sync move next, leave cursors where they are
//        ()
//      )
//      
//      if cont then
//        if this.FillContinuousValuesAtKey(pivotKeysSet.First.Key) then
//          this.CurrentKey <- pivotKeysSet.First.Key
//          true
//        else 
//          // cannot get contiuous values at this key
//          // move first non-cont cursor to next position
//          let first =  pivotKeysSet.RemoveFirst()
//          let ac = cursors.[first.Value]
//          if ac.MoveNext() then
//            currentValues.[first.Value] <- ac.CurrentValue
//            pivotKeysSet.Add(KV(ac.CurrentKey, first.Value)) |> ignore
//            this.DoMoveNextNonContPar() // recursive
//          else false
//      else false


    member this.MoveNext(): bool =
      if not this.HasValidState then this.MoveFirst()
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
        if cont then this.DoMoveNextNonCont() // failwith "TODO" // this.DoMoveNext()
        else false
    
    member this.MoveFirst(): bool =
      // do it imperative style - faster and simpler to understand
      let mutable cont = true
      let mutable valuesOk = false
      let mutable allMovedFirst = false
      //Array.Clear(hasValue, 0, hasValue.Length)
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
              //updateMinMax x.CurrentKey
              currentValues.[i] <- x.CurrentValue
              //hasValue.[i] <- true
            else
              cont <- false // series has no values, stop here
          )
          allMovedFirst <- cont
        else
          if isContinuous then
            failwith "TODO"
          else
            if cmp.Compare(pivotKeysSet.First.Key, pivotKeysSet.Last.Key) = 0 
                  && this.FillContinuousValuesAtKey(pivotKeysSet.First.Key) then
              for kvp in pivotKeysSet.AsEnumerable() do
                currentValues.[kvp.Value] <- cursors.[kvp.Value].CurrentValue
              this.CurrentKey <- pivotKeysSet.First.Key
              valuesOk <- true
              cont <- false 
            else
              // move to max key until min key matches max key so that we can use values
              valuesOk <- this.DoMoveNextNonCont() //failwith "TODO" //this.DoMoveNext()
      if valuesOk then 
        this.HasValidState <- true
        true
      else false

    member this.MoveLast(): bool = failwith "TODO"

    member x.MovePrevious(): bool = failwith "not implemented"
//        let cl = x.InputCursorL
//        let cr = x.InputCursorR
//        if hasValidState then
//          let mutable found = false
//          while not found && x.InputCursorL.MovePrevious() do
//            let ok, value = x.TryUpdatePrevious(x.InputCursorL.Current)
//            if ok then 
//              found <- true
//              x.CurrentKey <- value.Key
//              x.CurrentValue <- value.Value
//          if found then 
//            hasValidState <- true
//            true 
//          else false
//        else (x :> ICursor<'K,'R>).MoveLast()
    
    member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    member x.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    
    //member x.IsBatch with get() = x.IsBatch
    member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((x :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
      // TODO should keep a lazy array of cursors that is initiated on first call to this function
      // and then is reused on evey call
      failwith "Not implemented yet"


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
      member x.MoveAt(index: 'K, direction: Lookup): bool = failwith "not implemented"

      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = failwith "not implemented"

      member x.MovePrevious(): bool = failwith "not implemented"
    
      member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        failwith "Not implemented yet"
      member x.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        failwith "Not implemented yet"
    
      //member x.IsBatch with get() = x.IsBatch
      member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((x :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
        // TODO should keep a lazy array of cursors that is initiated on first call to this function
        // and then is reused on evey call
        failwith "Not implemented yet"
    
      member this.Clone() = this.Clone()