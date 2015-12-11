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

/// Horizontal cursor is a cursor whose state could be re-created at any key with little (bounded) cost 
/// without iterating over all values before the key. (E.g. scan requires running from the first value,
/// therefore it is not horizontal. Even though it could be implemented as such, state creation cost is not bounded).
[<ObsoleteAttribute("This is 5+ times slower than SimpleBindCursor")>]
type internal HorizontalCursor<'K,'V,'State,'V2>
  (
    cursorFactory:Func<ICursor<'K,'V>>,
    stateCreator:Func<ICursor<'K,'V>, 'K, bool, KVP<bool,'State>>, // Factory if state needs its own cursor
    stateFoldNext:Func<'State, KVP<'K,'V>, KVP<bool,'State>>,
    stateFoldPrevious:Func<'State, KVP<'K,'V>, KVP<bool,'State>>,
    stateMapper:Func<'State,'V2>,
    ?isContinuous:bool // stateCreator could be able to create state at any point. TODO: If isContinuous = true but stateCreator returns false, we fail
  ) =
  inherit Series<'K,'V2>()
  let cursor = cursorFactory.Invoke()

  // evaluated only when TryGetValue is called
  let lookupCursor = lazy (cursor.Clone())

  let isContinuous = if isContinuous.IsSome then isContinuous.Value else cursor.IsContinuous

  let mutable okState = Unchecked.defaultof<KVP<bool,'State>>
  let clearState() = 
    match box okState.Value with
    | :? IDisposable as disp -> if disp <> null then disp.Dispose()
    | _ -> ()
    //okState <- Unchecked.defaultof<KVP<bool,'State>> not needed


  let threadId = Environment.CurrentManagedThreadId
  [<DefaultValueAttribute>]
  val mutable started : bool
  override this.GetCursor() =
    this.started <- true
    let cursor = if not this.started && threadId = Environment.CurrentManagedThreadId then this else this.Clone()
    cursor.started <- true
    cursor :> ICursor<'K,'V2>

  member this.Clone() = new HorizontalCursor<'K,'V,'State,'V2>((fun _ -> cursor.Clone()), stateCreator, stateFoldNext, stateFoldPrevious, stateMapper, isContinuous)


  member this.CurrentValue 
    with get() =
      if okState.Key then stateMapper.Invoke(okState.Value)
      else Unchecked.defaultof<'V2>

  member this.Current with get () = KVP(cursor.CurrentKey, this.CurrentValue)

  member this.CurrentBatch with get() = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>>


  member this.Reset() = 
    clearState()
    cursor.Reset()

  member this.Dispose() =
    clearState()
    cursor.Dispose()
    if lookupCursor.IsValueCreated then lookupCursor.Value.Dispose()


  member this.MoveFirst(): bool =
    if okState.Key then clearState() // we are moving from a valid state to new state, must clear existing state
    if cursor.MoveFirst() then
#if PRERELEASE
      let before = cursor.CurrentKey
      okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
      if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
      okState <- stateCreator.Invoke(cursor, cursor.Current)
#endif
      if okState.Key then
        true
      else
        let mutable found = false
        while not found && cursor.MoveNext() do
#if PRERELEASE
          let before = cursor.CurrentKey
          okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
          if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
          okState <- stateCreator.Invoke(cursor, cursor.Current)
#endif
          found <- okState.Key
        found
    else false


  member this.MoveNext(): bool =
      if okState.Key then
        let mutable found = false
        while not found && cursor.MoveNext() do 
          okState <- stateFoldNext.Invoke(okState.Value, cursor.Current)
          found <- okState.Key
        found
      else this.MoveFirst()


  member this.MoveLast(): bool =
    if okState.Key then clearState() // we are moving from a valid state to new state, must clear existing state
    if cursor.MoveLast() then
#if PRERELEASE
      let before = cursor.CurrentKey
      okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
      if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
      okState <- stateCreator.Invoke(cursor, cursor.Current)
#endif
      if okState.Key then
        true
      else
        let mutable found = false
        while not found && cursor.MovePrevious() do
#if PRERELEASE
          let before = cursor.CurrentKey
          okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
          if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
          okState <- stateCreator.Invoke(cursor, cursor.Current)
#endif
          found <- okState.Key
        found
    else false


  member this.MovePrevious(): bool =
      if okState.Key then
        let mutable found = false
        while not found && cursor.MovePrevious() do 
          okState <- stateFoldPrevious.Invoke(okState.Value, cursor.Current)
          found <- okState.Key
        found
      else this.MoveLast()


  member this.MoveAt(index: 'K, direction: Lookup): bool = 
    if cursor.MoveAt(index, direction) then
      if okState.Key then clearState() // we are going to create new one, clear it
#if PRERELEASE
      let before = cursor.CurrentKey
      okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
      if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
      okState <- stateCreator.Invoke(cursor, cursor.Current)
#endif
      if okState.Key then
        true
      else
        match direction with
        | Lookup.EQ -> false
        | Lookup.GE | Lookup.GT ->
          let mutable found = false
          while not found && cursor.MoveNext() do
#if PRERELEASE
            let before = cursor.CurrentKey
            okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
            if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
            okState <- stateCreator.Invoke(cursor, cursor.Current)
#endif
            found <- okState.Key
          found
        | Lookup.LE | Lookup.LT ->
          let mutable found = false
          while not found && cursor.MovePrevious() do
#if PRERELEASE
            let before = cursor.CurrentKey
            okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
            if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
            okState <- stateCreator.Invoke(cursor, cursor.Current)
#endif
            found <- okState.Key
          found
        | _ -> failwith "wrong lookup value"
    else false
      
  member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> =
    task {
      if this.MoveNext() then return true
      else
        if okState.Key then
          let mutable found = false
          let mutable moved = false
          let! moved' = cursor.MoveNext(cancellationToken) // await input cursor
          moved <- moved'
          while not found && moved do
            okState <- stateFoldNext.Invoke(okState.Value,cursor.Current)
            found <- okState.Key
            if not found then
              if cursor.MoveNext() then moved <- true
              else
                let! moved' = cursor.MoveNext(cancellationToken)
                moved <- moved'
          return found
        else
          let mutable found = false
          let mutable moved = false
          let! moved' = cursor.MoveNext(cancellationToken) // await input cursor
          moved <- moved'
          while not found && moved do
            okState <- stateCreator.Invoke(cursor, cursor.CurrentKey, false)
            found <- okState.Key
            if not found then
              if cursor.MoveNext() then moved <- true
              else
                let! moved' = cursor.MoveNext(cancellationToken)
                moved <- moved'
          return found
    }


  member this.TryGetValueChecked(key: 'K, [<Out>] value: byref<'V2>): bool = 
    let mutable v = Unchecked.defaultof<'V2>
    let before = cursor.CurrentKey
    let ok = this.TryGetValue(key, &v)
    if cursor.Comparer.Compare(before, cursor.CurrentKey) <> 0 then 
      raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
    value <- v
    ok

  member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
    let state = stateCreator.Invoke(lookupCursor.Value, key, true)
    if state.Key then
      value <- stateMapper.Invoke(state.Value)
      true
    else false

  interface IEnumerator<KVP<'K,'V2>> with
    member this.Reset() = this.Reset()
    member this.MoveNext(): bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V2> = KVP(cursor.CurrentKey, this.CurrentValue)
    member this.Current with get(): obj = KVP(cursor.CurrentKey, this.CurrentValue) :> obj 
    member this.Dispose(): unit = this.Dispose()

  interface ICursor<'K,'V2> with
    member this.Comparer with get() = cursor.Comparer
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
    member this.CurrentKey: 'K = cursor.CurrentKey
    member this.CurrentValue: 'V2 = this.CurrentValue
    member this.IsContinuous: bool = isContinuous
    member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index, direction) 
    member this.MoveFirst(): bool = this.MoveFirst()
    member this.MoveLast(): bool = this.MoveLast()
    member this.MovePrevious(): bool = this.MovePrevious()
    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNext(cancellationToken)
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = Task.FromResult(false)
    member this.Source: ISeries<'K,'V2> = this :> ISeries<'K,'V2>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  
#if PRERELEASE
      this.TryGetValueChecked(key, &value)
#else
      this.TryGetValue(key, &value)
#endif
    member this.Clone() = this.Clone() :> ICursor<'K,'V2>

  interface ICanMapSeriesValues<'K,'V2> with
    member this.Map<'V3>(f2:Func<'V2,'V3>): Series<'K,'V3> = 
      let combinedFunc = CoreUtils.CombineMaps(stateMapper, f2)
      new HorizontalCursor<'K,'V,'State,'V3>((fun _ -> cursor.Clone()), stateCreator, stateFoldNext, stateFoldPrevious, combinedFunc, isContinuous) :> Series<'K,'V3>