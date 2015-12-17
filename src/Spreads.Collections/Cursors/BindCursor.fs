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


/// BindCursor is a cursor whose state could be re-created at any key with little (bounded) cost 
/// without iterating over all values before the key. (E.g. scan requires running from the first value,
/// therefore it is not horizontal. Even though it could be implemented as such, state creation cost is not bounded).

type BindCursor<'K,'V,'State,'R>(cursorFactory:Func<ICursor<'K,'V>>) =
  inherit Series<'K,'R>()

  let threadId = Environment.CurrentManagedThreadId

  let inputCursor = cursorFactory.Invoke()

  let mutable hasValidState = false
  let mutable internalState = Unchecked.defaultof<'State> 

  let clearState(state:'State) = 
    match box state with
    | :? IDisposable as disp -> if disp <> null then disp.Dispose()
    | _ -> ()

  [<DefaultValueAttribute>]
  val mutable started : bool
  override this.GetCursor() =
    this.started <- true
    let cursor = if not this.started && threadId = Environment.CurrentManagedThreadId then this else this.Clone()
    cursor.started <- true
    cursor :> ICursor<'K,'R>

  abstract IsContinuous : bool with get
  default this.IsContinuous = inputCursor.IsContinuous

  /// True after any successful move and when CurrentKey/Value are defined
  member this.HasValidState with get() = hasValidState

  abstract EvaluateState: 'State -> 'R
  default this.EvaluateState(state) = raise (NotImplementedException())

  abstract TryCreateState: key:'K * isMove:bool * [<Out>] state: byref<'State> -> bool
  default this.TryCreateState(key:'K, isMove:bool, [<Out>] state: byref<'State>): bool = raise (NotImplementedException())

  abstract TryUpdateNext: next:KVP<'K,'V> * state: byref<'State> -> bool
  default this.TryUpdateNext(next:KVP<'K,'V>, state: byref<'State>) : bool = 
    Trace.TraceWarning("Using unoptimized TryUpdateNext implementation via TryCreateState. If you see this warning often, implement TryUpdateNext method in a calling cursor.")
    clearState(state)
    this.TryCreateState(next.Key, true, &state)

  abstract TryUpdatePrevious: previous:KVP<'K,'V> * state: byref<'State> -> bool
  default this.TryUpdatePrevious(previous:KVP<'K,'V>, state: byref<'State>) : bool = 
    Trace.TraceWarning("Using unoptimized TryUpdatePrevious implementation via TryCreateState. If you see this warning often, implement TryUpdatePrevious method in a calling cursor.")
    clearState(state)
    this.TryCreateState(previous.Key, true, &state)

  abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] batch: byref<IReadOnlyOrderedMap<'K,'R>> -> bool  
  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] batch: byref<IReadOnlyOrderedMap<'K,'R>>) : bool = false


  member this.Clone() = 
    { new BindCursor<'K,'V,'State,'R>(Func<_>(inputCursor.Clone)) with //, stateCreator, stateFoldNext, stateFoldPrevious, stateMapper, isContinuous)
        override cl.EvaluateState(state) = this.EvaluateState(state)
        override cl.TryCreateState(key:'K, isMove:bool, [<Out>] state: byref<'State>): bool = 
          this.TryCreateState(key, isMove, &state)
        override cl.TryUpdateNext(next:KVP<'K,'V>, state: byref<'State>) : bool = 
          this.TryUpdateNext(next, &state)
        override cl.TryUpdatePrevious(previous:KVP<'K,'V>, state: byref<'State>) : bool = 
          this.TryUpdatePrevious(previous, &state)
        override cl.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] batch: byref<IReadOnlyOrderedMap<'K,'R>>) : bool = 
          this.TryUpdateNextBatch(nextBatch, &batch)
    }

  member this.CurrentValue 
    with get() =
      if hasValidState then  this.EvaluateState(internalState)
      else Unchecked.defaultof<'R>

  member this.Current with get () = KVP(inputCursor.CurrentKey, this.CurrentValue)

  member this.CurrentBatch with get() = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>>

  member this.State with get() = internalState

  /// An instance of the input cursor that is used for moves of SimpleBindCursor
  member this.InputCursor with get() : ICursor<'K,'V> = inputCursor

  member this.Reset() = 
    clearState(internalState)
    internalState <- Unchecked.defaultof<_>
    inputCursor.Reset()

  member this.Dispose() =
    clearState(internalState)
    internalState <- Unchecked.defaultof<_>
    inputCursor.Dispose()


  member this.MoveNext(): bool =
    if hasValidState then
      let mutable found = false
      let mutable moved = true 
      while not found && moved do 
        moved <- inputCursor.MoveNext()
        if moved then

          #if PRERELEASE
          let before = inputCursor.CurrentKey
          #endif

          // if TryUpdateNext returns false, it MUST NOT change state TODO assert this
          found <- this.TryUpdateNext(inputCursor.Current, &internalState)

          #if PRERELEASE
          if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
          #endif
        else
          // if we are unable to move, state must remain valid TODO link to contracts spec
          ()
      if not moved then 
        #if PRERELEASE
        Trace.Assert(hasValidState, "When MN returns false due to the end of series, valid state should remain ")
        #endif
        false // we cannot move, but keep state as valid and unchanged for a potential retry
      else 
        #if PRERELEASE
        Trace.Assert(found && hasValidState, "When MN returns false due to the end of series, valid state should remain ")
        #endif
        true
    else this.MoveFirst()

  member this.MoveFirst(): bool =
    // regaldless os state validity, we try to create state at input first and then move forward until we can or exhaust input

    if hasValidState then 
      clearState(internalState) // we are moving from a valid state to new state, must clear existing state
      hasValidState <- false

    if inputCursor.MoveFirst() then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryCreateState(inputCursor.CurrentKey, true, &internalState)
      
      #if PRERELEASE
      if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
      #endif

      if hasValidState then
        true
      else
        //let mutable found = false
        while not hasValidState && inputCursor.MoveNext() do
          #if PRERELEASE
          let before = inputCursor.CurrentKey
          #endif
          
          // NB we could modify internalState directly here, but this is hard to assert
          hasValidState <- this.TryUpdateNext(inputCursor.Current, &internalState)

          #if PRERELEASE
          if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
          #endif
        hasValidState
    else 
      hasValidState <- false
      false

  member this.MoveLast(): bool =
    // regaldless os state validity, we try to create state at input first and then move forward until we can or exhaust input

    if hasValidState then 
      clearState(internalState) // we are moving from a valid state to new state, must clear existing state
      hasValidState <- false

    if inputCursor.MoveLast() then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryCreateState(inputCursor.CurrentKey, true, &internalState)
      
      #if PRERELEASE
      if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
      #endif

      if hasValidState then
        true
      else
        //let mutable found = false
        while not hasValidState && inputCursor.MovePrevious() do
          #if PRERELEASE
          let before = inputCursor.CurrentKey
          #endif
          
          // NB we could modify internalState directly here, but this is hard to assert
          hasValidState <- this.TryUpdatePrevious(inputCursor.Current, &internalState)

          #if PRERELEASE
          if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
          #endif
        hasValidState
    else 
      hasValidState <- false
      false

    
  member this.MovePrevious(): bool =
    if hasValidState then
      let mutable found = false
      let mutable moved = true 
      while not found && moved do 
        moved <- inputCursor.MovePrevious()
        if moved then

          #if PRERELEASE
          let before = inputCursor.CurrentKey
          #endif

          // if TryUpdateNext returns false, it MUST NOT change state TODO assert this
          found <- this.TryUpdatePrevious(inputCursor.Current, &internalState)

          #if PRERELEASE
          if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
          #endif
        else
          // if we are unable to move, state must remain valid TODO link to contracts spec
          ()
      if not moved then 
        #if PRERELEASE
        Trace.Assert(hasValidState, "When MP returns false due to the end of series, valid state should remain ")
        #endif
        false // we cannot move, but keep state as valid and unchanged for a potential retry
      else 
        #if PRERELEASE
        Trace.Assert(found && hasValidState, "When MP returns false due to the end of series, valid state should remain ")
        #endif
        true
    else this.MoveLast()



  member this.MoveAt(index: 'K, direction: Lookup): bool =
    // regaldless os state validity, we try to create state at input first and then move until we can or exhaust input

    if hasValidState then 
      clearState(internalState) // we are moving from a valid state to new state, must clear existing state
      hasValidState <- false

    if (inputCursor.Comparer.Compare(index, this.InputCursor.CurrentKey) = 0 && not (direction = Lookup.LT || direction = Lookup.GT)) 
        || this.InputCursor.MoveAt(index, direction) then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryCreateState(inputCursor.CurrentKey, true, &internalState)
      
      #if PRERELEASE
      if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
      #endif

      if hasValidState then
        true
      else
        match direction with
        | Lookup.EQ -> false
        | Lookup.GE | Lookup.GT ->
          //let mutable found = false
          while not hasValidState && inputCursor.MoveNext() do
            #if PRERELEASE
            let before = inputCursor.CurrentKey
            #endif
          
            // NB we could modify internalState directly here, but this is hard to assert
            hasValidState <- this.TryUpdateNext(inputCursor.Current, &internalState)

            #if PRERELEASE
            if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
            #endif
          hasValidState
        | Lookup.LE | Lookup.LT ->
          //let mutable found = false
          while not hasValidState && inputCursor.MovePrevious() do
            #if PRERELEASE
            let before = inputCursor.CurrentKey
            #endif
          
            // NB we could modify internalState directly here, but this is hard to assert
            hasValidState <- this.TryUpdatePrevious(inputCursor.Current, &internalState)

            #if PRERELEASE
            if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
            #endif
          hasValidState
        | _ -> failwith "wrong lookup value"
    else false

    
    
  member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> =
    task {
      if hasValidState then
        let mutable found = false
        let mutable moved = true 
        while not found && moved do 
          let! moved' = inputCursor.MoveNext(cancellationToken)
          moved <- moved'
          if moved then

            #if PRERELEASE
            let before = inputCursor.CurrentKey
            #endif

            // if TryUpdateNext returns false, it MUST NOT change state TODO assert this
            found <- this.TryUpdateNext(inputCursor.Current, &internalState)

            #if PRERELEASE
            if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
            #endif
          else
            // if we are unable to move, state must remain valid TODO link to contracts spec
            ()
        if not moved then 
          #if PRERELEASE
          Trace.Assert(hasValidState, "When MN returns false due to the end of series, valid state should remain ")
          #endif
          return false // we cannot move, but keep state as valid and unchanged for a potential retry
        else 
          #if PRERELEASE
          Trace.Assert(found && hasValidState, "When MN returns false due to the end of series, valid state should remain ")
          #endif
          return true
      else return this.MoveFirst()
    }

  member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool = 
    let mutable state = Unchecked.defaultof<'State>
    #if PRERELEASE
    let before = inputCursor.CurrentKey
    #endif
    try
      if this.TryCreateState(key, false, &state) then
        value <- this.EvaluateState(state)
        true
      else false
    finally 
      #if PRERELEASE
      if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
      #endif
      ()

  interface IEnumerator<KVP<'K,'R>> with
    member this.Reset() = this.Reset()
    member this.MoveNext(): bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'R> = KVP(inputCursor.CurrentKey, this.CurrentValue)
    member this.Current with get(): obj = KVP(inputCursor.CurrentKey, this.CurrentValue) :> obj 
    member this.Dispose(): unit = this.Dispose()

  interface ICursor<'K,'R> with
    member this.Comparer with get() = inputCursor.Comparer
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'R> = this.CurrentBatch
    member this.CurrentKey: 'K = inputCursor.CurrentKey
    member this.CurrentValue: 'R = this.CurrentValue
    member this.IsContinuous: bool = this.IsContinuous
    member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index, direction) 
    member this.MoveFirst(): bool = this.MoveFirst()
    member this.MoveLast(): bool = this.MoveLast()
    member this.MovePrevious(): bool = this.MovePrevious()
    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNext(cancellationToken)
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = Task.FromResult(false)
    member this.Source: ISeries<'K,'R> = this :> ISeries<'K,'R>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool = this.TryGetValue(key, &value)
    member this.Clone() = this.Clone() :> ICursor<'K,'R>

  interface ICanMapSeriesValues<'K,'R> with
    member this.Map<'R2>(f2:Func<'R,'R2>): Series<'K,'R2> = 
      { new BindCursor<'K,'V,'State,'R2>((fun _ -> inputCursor.Clone())) with
          override cl.EvaluateState(state) = 
            (CoreUtils.CombineMaps<'State,'R,'R2>(Func<'State,'R>(this.EvaluateState), f2)).Invoke(state)
          override cl.TryCreateState(key:'K, isMove:bool, [<Out>] state: byref<'State>): bool = 
            this.TryCreateState(key, isMove, &state)
          override cl.TryUpdateNext(next:KVP<'K,'V>, state: byref<'State>) : bool = 
            this.TryUpdateNext(next, &state)
          override cl.TryUpdatePrevious(previous:KVP<'K,'V>, state: byref<'State>) : bool = 
            this.TryUpdatePrevious(previous, &state)
          override cl.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] batch: byref<IReadOnlyOrderedMap<'K,'R2>>) : bool = 
            let ok, batch' = this.TryUpdateNextBatch(nextBatch)
            if ok then 
              batch <- new BatchMapValuesCursor<'K,'R,'R2>(Func<_>(batch'.GetCursor), f2) :> Series<'K,'R2>
              true
            else false

      } :> Series<'K,'R2>
