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
open System.Diagnostics
open System.Runtime.InteropServices
open System.Threading.Tasks



// TODO! ensure that TGV on Non-continuous cursor returns false if requested key does not exists in input
// Could add ContainsKey to IROOM/ICursor interface, it is used very often for dict
// Or define explicit contract that calling TGV is not defined for non-cont series when the key in TGV does not exist

// I had an attempt to manually optimize callvirt and object allocation, both failed badly
// They are not needed, however, in most of the cases, e.g. iterations.
// see https://msdn.microsoft.com/en-us/library/ms973852.aspx
// ...the virtual and interface method call sites are monomorphic (e.g. per call site, the target method does not change over time), 
// so the combination of caching the virtual method and interface method dispatch mechanisms (the method table and interface map 
// pointers and entries) and spectacularly provident branch prediction enables the processor to do an unrealistically effective 
// job calling through these otherwise difficult-to-predict, data-dependent branches. In practice, a data cache miss on any of the 
// dispatch mechanism data, or a branch misprediction (be it a compulsory capacity miss or a polymorphic call site), can and will
//  slow down virtual and interface calls by dozens of cycles.
//
// Our benchmark confirms that the slowdown of .Repeat(), .ReadOnly(), .Map(...) and .Filter(...) is small

// (Continued later) Yet still, enumerating SortedList-like contructs is 4-5 times slower than arrays and 2-3 times slower than
// a list of `KVP<DateTime,double>`s. ILs difference is mostly in callvirt and/or memory access pattern.
// SM enumerator as a structure should be used 
// (Continued even later) Removing one callvirt increased performance from 45 MOps to 66 MOps for SortedMap and from 100 Mops to 200 mops for SortedDeque




/// Bind cursors
[<AbstractClassAttribute>]
type BindCursor<'K,'V,'State,'R>(cursorFactory:Func<ICursor<'K,'V>>) =

  let inputCursor = cursorFactory.Invoke()

  let mutable hasValidState = false
  let mutable moveState = Unchecked.defaultof<'State> 

  let clearState(state:'State) = 
    match box state with
    | :? IDisposable as disp -> if disp <> null then disp.Dispose()
    | _ -> ()

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

  abstract Clone: unit -> ICursor<'K,'R>

  member this.CurrentKey 
    with get() = inputCursor.CurrentKey

  member this.CurrentValue 
    with get() =
      if hasValidState then  this.EvaluateState(moveState)
      else Unchecked.defaultof<'R>

  member this.Current with get () = KVP(inputCursor.CurrentKey, this.CurrentValue)

  member this.CurrentBatch with get() = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>>

  member this.State with get() = moveState

  /// An instance of the input cursor that is used for moves of SimpleBindCursor
  member this.InputCursor with get() : ICursor<'K,'V> = inputCursor

  abstract Dispose: unit -> unit
  default this.Dispose() =
    hasValidState <- false
    clearState(moveState)
    moveState <- Unchecked.defaultof<_>
    inputCursor.Dispose()

  member this.Reset() = 
    hasValidState <- false
    clearState(moveState)
    moveState <- Unchecked.defaultof<_>
    inputCursor.Reset()


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

          found <- this.TryUpdateNext(inputCursor.Current, &moveState)

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
      clearState(moveState) // we are moving from a valid state to new state, must clear existing state
      hasValidState <- false

    if inputCursor.MoveFirst() then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryCreateState(inputCursor.CurrentKey, true, &moveState)
      
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
          hasValidState <- this.TryUpdateNext(inputCursor.Current, &moveState)

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
      clearState(moveState) // we are moving from a valid state to new state, must clear existing state
      hasValidState <- false

    if inputCursor.MoveLast() then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryCreateState(inputCursor.CurrentKey, true, &moveState)
      
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
          hasValidState <- this.TryUpdatePrevious(inputCursor.Current, &moveState)

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
          found <- this.TryUpdatePrevious(inputCursor.Current, &moveState)

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
      clearState(moveState) // we are moving from a valid state to new state, must clear existing state
      hasValidState <- false

    if (inputCursor.Comparer.Compare(index, this.InputCursor.CurrentKey) = 0 && not (direction = Lookup.LT || direction = Lookup.GT)) 
        || this.InputCursor.MoveAt(index, direction) then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryCreateState(inputCursor.CurrentKey, true, &moveState)
      
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
            hasValidState <- this.TryUpdateNext(inputCursor.Current, &moveState)

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
            hasValidState <- this.TryUpdatePrevious(inputCursor.Current, &moveState)

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
            found <- this.TryUpdateNext(inputCursor.Current, &moveState)

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
    
    #if PRERELEASE
    let before = inputCursor.CurrentKey
    #endif
    try
      if this.HasValidState && this.InputCursor.Comparer.Compare(inputCursor.CurrentKey, key) = 0 then
        // TODO (perf) add perf counter here
        value <- this.EvaluateState(moveState)
        true
      else
        let mutable state = Unchecked.defaultof<'State>
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
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = falseTask
    member this.Source: ISeries<'K,'R> = CursorSeries(fun _ -> this.Clone() :> ICursor<_,_> ) :> ISeries<'K,'R>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool = this.TryGetValue(key, &value)
    member this.Clone() = this.Clone()




/// Bind cursors
[<AbstractClassAttribute>]
type SimpleBindCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>) =

  let inputCursor = cursorFactory.Invoke()

  let mutable hasValidState = false
  let mutable moveState = Unchecked.defaultof<'R> 

  let clearState(value:'R) = 
    match box value with
    | :? IDisposable as disp -> if disp <> null then disp.Dispose()
    | _ -> ()

  abstract IsContinuous : bool with get
  default this.IsContinuous = inputCursor.IsContinuous

  /// True after any successful move and when CurrentKey/Value are defined
  member this.HasValidState with get() = hasValidState

  abstract TryGetValue: key:'K * isMove:bool * [<Out>] value: byref<'R> -> bool
  default this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool = raise (NotImplementedException())

  abstract TryUpdateNext: next:KVP<'K,'V> * [<Out>] value: byref<'R> -> bool
  default this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool = 
    Trace.TraceWarning("Using unoptimized TryUpdateNext implementation via TryCreateState. If you see this warning often, implement TryUpdateNext method in a calling cursor.")
    clearState(value)
    this.TryGetValue(next.Key, true, &value)

  abstract TryUpdatePrevious: previous:KVP<'K,'V> * [<Out>] value: byref<'R> -> bool
  default this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool = 
    Trace.TraceWarning("Using unoptimized TryUpdatePrevious implementation via TryCreateState. If you see this warning often, implement TryUpdatePrevious method in a calling cursor.")
    clearState(value)
    this.TryGetValue(previous.Key, true, &value)

  abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] batch: byref<IReadOnlyOrderedMap<'K,'R>> -> bool  
  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] batch: byref<IReadOnlyOrderedMap<'K,'R>>) : bool = false

  abstract Clone: unit -> ICursor<'K,'R>

  member this.CurrentKey 
    with get() = inputCursor.CurrentKey

  member this.CurrentValue with get() = moveState

  member this.Current with get () = KVP(inputCursor.CurrentKey, moveState)

  member this.CurrentBatch with get() = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>>

  member this.State with get() = moveState

  /// An instance of the input cursor that is used for moves of SimpleBindCursor
  member this.InputCursor with get() : ICursor<'K,'V> = inputCursor

  abstract Dispose: unit -> unit
  default this.Dispose() =
    hasValidState <- false
    clearState(moveState)
    moveState <- Unchecked.defaultof<_>
    inputCursor.Dispose()

  member this.Reset() = 
    hasValidState <- false
    clearState(moveState)
    moveState <- Unchecked.defaultof<_>
    inputCursor.Reset()


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

          found <- this.TryUpdateNext(inputCursor.Current, &moveState)

          #if PRERELEASE
          if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
          #endif
        else
          // if we are unable to move, value must remain valid TODO link to contracts spec
          ()
      if not moved then 
        #if PRERELEASE
        Trace.Assert(hasValidState, "When MN returns false due to the end of series, valid value should remain ")
        #endif
        false // we cannot move, but keep value as valid and unchanged for a potential retry
      else 
        #if PRERELEASE
        Trace.Assert(found && hasValidState, "When MN returns false due to the end of series, valid value should remain ")
        #endif
        true
    else this.MoveFirst()

  member this.MoveFirst(): bool =
    // regaldless os value validity, we try to create value at input first and then move forward until we can or exhaust input

    if hasValidState then 
      clearState(moveState) // we are moving from a valid value to new value, must clear existing value
      hasValidState <- false

    if inputCursor.MoveFirst() then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryGetValue(inputCursor.CurrentKey, true, &moveState)
      
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
          hasValidState <- this.TryUpdateNext(inputCursor.Current, &moveState)

          #if PRERELEASE
          if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
          #endif
        hasValidState
    else 
      hasValidState <- false
      false

  member this.MoveLast(): bool =
    // regaldless os value validity, we try to create value at input first and then move forward until we can or exhaust input

    if hasValidState then 
      clearState(moveState) // we are moving from a valid value to new value, must clear existing value
      hasValidState <- false

    if inputCursor.MoveLast() then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryGetValue(inputCursor.CurrentKey, true, &moveState)
      
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
          hasValidState <- this.TryUpdatePrevious(inputCursor.Current, &moveState)

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

          // if TryUpdateNext returns false, it MUST NOT change value TODO assert this
          found <- this.TryUpdatePrevious(inputCursor.Current, &moveState)

          #if PRERELEASE
          if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
          #endif
        else
          // if we are unable to move, value must remain valid TODO link to contracts spec
          ()
      if not moved then 
        #if PRERELEASE
        Trace.Assert(hasValidState, "When MP returns false due to the end of series, valid value should remain ")
        #endif
        false // we cannot move, but keep value as valid and unchanged for a potential retry
      else 
        #if PRERELEASE
        Trace.Assert(found && hasValidState, "When MP returns false due to the end of series, valid value should remain ")
        #endif
        true
    else this.MoveLast()



  member this.MoveAt(index: 'K, direction: Lookup): bool =
    // regaldless os value validity, we try to create value at input first and then move until we can or exhaust input

    if hasValidState then 
      clearState(moveState) // we are moving from a valid value to new value, must clear existing value
      hasValidState <- false

    // TODO we must MoveAt, otherwise TryGetValue could throw assertion failure. Probably just need to add .HasValidState check
//    if (inputCursor.Comparer.Compare(index, this.InputCursor.CurrentKey) = 0 && not (direction = Lookup.LT || direction = Lookup.GT)) 
//        || this.InputCursor.MoveAt(index, direction) then
    if this.InputCursor.MoveAt(index, direction) then
      #if PRERELEASE
      let before = inputCursor.CurrentKey
      #endif

      hasValidState <- this.TryGetValue(inputCursor.CurrentKey, true, &moveState)
      
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
            hasValidState <- this.TryUpdateNext(inputCursor.Current, &moveState)

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
            hasValidState <- this.TryUpdatePrevious(inputCursor.Current, &moveState)

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

            // if TryUpdateNext returns false, it MUST NOT change value TODO assert this
            found <- this.TryUpdateNext(inputCursor.Current, &moveState)

            #if PRERELEASE
            if inputCursor.Comparer.Compare(before, inputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("SimpleBindCursor's TryGetValue implementation must not move InputCursor"))
            #endif
          else
            // if we are unable to move, value must remain valid TODO link to contracts spec
            ()
        if not moved then 
          #if PRERELEASE
          Trace.Assert(hasValidState, "When MN returns false due to the end of series, valid value should remain ")
          #endif
          return false // we cannot move, but keep value as valid and unchanged for a potential retry
        else 
          #if PRERELEASE
          Trace.Assert(found && hasValidState, "When MN returns false due to the end of series, valid value should remain ")
          #endif
          return true
      else return this.MoveFirst()
    }

  member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool = 
    #if PRERELEASE
    let before = inputCursor.CurrentKey
    #endif
    try
      if this.HasValidState && this.InputCursor.Comparer.Compare(inputCursor.CurrentKey, key) = 0 then
        // TODO (perf) add perf counter here
        value <- this.CurrentValue
        true
      else
        this.TryGetValue(key, false, &value) 
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
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = falseTask
    member this.Source: ISeries<'K,'R> = CursorSeries(fun _ -> this.Clone() :> ICursor<_,_> ) :> ISeries<'K,'R>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool = this.TryGetValue(key, &value)
    member this.Clone() = this.Clone()