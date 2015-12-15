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


// Bind is in monad sense TODO Undestand/elaborate on weither it is a functor or monad or don't care until it works...

/// A cursor that could perform map, filter, fold, scan and other operations on input cursors.
[<AbstractClassAttribute>]
type SimpleBindCursor<'K,'V,'V2>(cursorFactory:Func<ICursor<'K,'V>>) =
    
    let cursor = cursorFactory.Invoke()

    let mutable hasValidState = false

    let mutable currentKey = Unchecked.defaultof<'K> 
    let mutable currentValue = Unchecked.defaultof<'V2> 

    /// True after any successful move and when CurrentKey/Value are defined
    member this.HasValidState with get() = hasValidState // NB should set it only here, Try... methods return values indicate valid state of child cursors // and internal set (v) = hasValidState <- v

    /// SimpleBindCursor could enable/disable continuous property. E.g. repeat/fill make any input
    /// continuous, while Lag/Window make any input non-continuous.
    abstract IsContinuous : bool with get

    /// An instance of the input cursor that is used for moves of SimpleBindCursor
    member this.InputCursor with get() : ICursor<'K,'V> = cursor

    member this.CurrentKey with get() = currentKey
    member this.CurrentValue with get() = currentValue
    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move
    //abstract CurrentBatch : IReadOnlyOrderedMap<'K,'V2> with get
    member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>> with get, set

    /// For every successful move of the input coursor creates an output value. If direction is not EQ, continues moves to the direction 
    /// until a valid state is created.
    /// NB: For continuous cases it should be optimized for cases when the key is between current
    /// and previous, e.g. Repeat() should keep the previous key and do comparison (2 times) instead of 
    /// searching the source, which is O(log n) for SortedMap - near 20 comparisons for binary search.
    /// Such lookup between the current and previous is often used in ZipCursor.
    /// This is the main method to transform input to output, other methods could be implemented via it. However,
    /// using only this method is highly inefficient, e.g. for StDev(50000) we will waste a 49999*avg(1,50000) moves, 
    /// while with TryUpdateNext we need only 50k moves.
    /// If isMove is false then this method is called from TryGetValue method, not from any move method.
    abstract TryGetValue: key:'K * isMove:bool * [<Out>] value: byref<'V2> -> bool
    

    /// Update state with a new value. Should be optimized for incremental update of the current state in custom implementations.
    /// If HasValidState is false inside this method, this means that the previous move was either TryGetValue or TryUpdateNext,
    /// because any directional move of SimpleBindCursor calls TryGetValue if the state is not valid and then moves next until 
    /// a valid state is achieved.
    abstract TryUpdateNext: next:KVP<'K,'V> * [<Out>] value: byref<'V2> -> bool
    // NB default implementation via TryGetValue is commented out because it is easy to create an O(M^2) TryGetValue implementation
    // which kills any performance hope. Cursor implementers must at least think about TryUpdateNext and copy-paste from TryGetValue
    // to avoid one method call.
    // override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool = this.TryGetValue(next.Key, true, &value)
  

    /// See comment for TryUpdateNext.
    abstract TryUpdatePrevious: previous:KVP<'K,'V> * [<Out>] value: byref<'V2> -> bool
    override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool = 
      Trace.TraceWarning("Using unoptimized TryUpdatePrevious implementation via TryGetValue. If you see this warning often, implement TryUpdatePrevious method in a calling cursor.")
      this.TryGetValue(previous.Key, true, &value)
      

    /// If input and this cursor support batches, then process a batch and store it in CurrentBatch
    abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>> -> bool  
    override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>>) : bool = false

    member this.Reset() = 
      hasValidState <- false
      cursor.Reset()
    abstract Dispose: unit -> unit
    default this.Dispose() =
      // NB! do not forget to clean disposable state in cursor implementations 
      hasValidState <- false
      cursor.Dispose()

    /// Create a copy of the current cursor positioned at the same key
    abstract Clone: unit -> ICursor<'K,'V2> // NB Clone is very important and must be carefully implemented
    

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>] // NB this and below attributes are probably useless, but we want to inline as much as we can
    member this.MoveNext(): bool =
      if hasValidState then
        let mutable value = Unchecked.defaultof<'V2>
        hasValidState <- false // NB hasValidState was true for the previous position of the input cursor, we set it to false until TryUpdateNext returns true
        while not hasValidState && this.InputCursor.MoveNext() do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
#if PRERELEASE
          let before = this.InputCursor.CurrentKey
          hasValidState <- this.TryUpdateNext(this.InputCursor.Current, &value)
          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
#else
          hasValidState <- this.TryUpdateNext(this.InputCursor.Current, &value)
#endif
          if hasValidState then
            currentKey <- this.InputCursor.CurrentKey
            currentValue <- value
        hasValidState
      else this.MoveFirst()

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveNext(ct:Threading.CancellationToken): Task<bool> =
      task {
        if hasValidState then
          let mutable value = Unchecked.defaultof<'V2>
          hasValidState <- false
          let! moved' = this.InputCursor.MoveNext(ct)
          let mutable moved = moved'
          while not hasValidState  && moved do
  #if PRERELEASE
            let before = this.InputCursor.CurrentKey
            hasValidState <- this.TryUpdateNext(this.InputCursor.Current, &value)
            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
  #else
            hasValidState <- this.TryUpdateNext(this.InputCursor.Current, &value)
  #endif
            if hasValidState then
              currentKey <- this.InputCursor.CurrentKey
              currentValue <- value
            else
              let! moved' = this.InputCursor.MoveNext(ct)
              moved <- moved'
          return hasValidState
        else return this.MoveFirst()
      }

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MoveAt(index: 'K, direction: Lookup): bool =
      // NB Unsucessfull moves invalidate state. A cursor is single-threaded and its state is invalid inside any move method, before confirmed otherwise.
      hasValidState <- false
      if this.InputCursor.MoveAt(index, direction) then
        let mutable value = Unchecked.defaultof<'V2>
#if PRERELEASE
        let before = this.InputCursor.CurrentKey
        hasValidState <- this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
#else
        hasValidState <- this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
        if hasValidState then
          currentKey <- this.InputCursor.CurrentKey
          currentValue <- value
          true
        else
          match direction with
          | Lookup.EQ -> false
          | Lookup.GE | Lookup.GT ->
            while not hasValidState && this.InputCursor.MoveNext() do
#if PRERELEASE
              let before = this.InputCursor.CurrentKey
              hasValidState <- this.TryUpdateNext(this.InputCursor.Current, &value)
              if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
#else
              hasValidState <- this.TryUpdateNext(this.InputCursor.Current, &value)
#endif
              if hasValidState then 
                currentKey <- this.InputCursor.CurrentKey
                currentValue <- value
            hasValidState
          | Lookup.LE | Lookup.LT ->
            while not hasValidState && this.InputCursor.MovePrevious() do
#if PRERELEASE
              let before = this.InputCursor.CurrentKey
              hasValidState <- this.TryUpdatePrevious(this.InputCursor.Current, &value)
              if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
#else
              hasValidState <- this.TryUpdatePrevious(this.InputCursor.Current, &value)
#endif
              if hasValidState then
                currentKey <- this.InputCursor.CurrentKey
                currentValue <- value
            hasValidState
          | _ -> failwith "wrong lookup value"
      else false
      
    member this.MoveFirst(): bool =
      if this.InputCursor.MoveFirst() then this.MoveAt(this.InputCursor.CurrentKey, Lookup.GE)
      else 
        hasValidState <- false
        false
    
    member this.MoveLast(): bool = 
      if this.InputCursor.MoveLast() then this.MoveAt(this.InputCursor.CurrentKey, Lookup.LE)
      else
        hasValidState <- false
        false

    [<MethodImplAttribute(MethodImplOptions.AggressiveInlining)>]
    member this.MovePrevious(): bool = 
      if hasValidState then
        let mutable value = Unchecked.defaultof<'V2>
        hasValidState <- false
        while not hasValidState && this.InputCursor.MovePrevious() do
#if PRERELEASE
          let before = this.InputCursor.CurrentKey
          hasValidState <- this.TryUpdatePrevious(this.InputCursor.Current, &value)
          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
#else
          hasValidState <- this.TryUpdatePrevious(this.InputCursor.Current, &value)
#endif
          if hasValidState then 
            currentKey <- this.InputCursor.CurrentKey
            currentValue <- value
        hasValidState
      else this.MoveLast()


    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
        let mutable v = Unchecked.defaultof<'V2>
#if PRERELEASE
        let before = this.InputCursor.CurrentKey
        let ok = this.TryGetValue(key, false, &v)
        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
#else
        let ok = this.TryGetValue(key, false, &v)
#endif
        value <- v
        ok


    interface IEnumerator<KVP<'K,'V2>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V2> = KVP(this.CurrentKey, this.CurrentValue)
      member this.Current with get(): obj = KVP(this.CurrentKey, this.CurrentValue) :> obj 
      member x.Dispose(): unit = x.Dispose()

    interface ICursor<'K,'V2> with
      member this.Comparer with get() = cursor.Comparer
      member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
      member this.CurrentKey: 'K = this.CurrentKey
      member this.CurrentValue: 'V2 = this.CurrentValue
      member this.IsContinuous: bool = this.IsContinuous
      member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index, direction) 
      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()
      member this.MovePrevious(): bool = this.MovePrevious()
    
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNext(cancellationToken)
      member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
#if PRERELEASE
        Trace.TraceWarning("SimpleBindCursor's MoveNextBatch is not implemented even if its children do implement the method.")
        Task.FromResult(false)
#else
        // TODO implement it via abstract member
        raise (NotImplementedException("MoveNextBatch is not implemented in SimpleBindCursor"))
#endif
        
      //member this.IsBatch with get() = this.IsBatch
      member this.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>(Func<ICursor<'K,'V2>>((this :> ICursor<'K,'V2>).Clone)) :> ISeries<'K,'V2>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  this.TryGetValue(key, &value)
      member this.Clone() = this.Clone()
    
      
