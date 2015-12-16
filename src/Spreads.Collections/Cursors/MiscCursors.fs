namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks

open Spreads
open Spreads.Collections

// TODO ensure that TGV on Non-continuous cursor returns false if requested key does not exists in input
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

// (Continued later) Yet still, enumerating SortedList-like contructs is 4-5 times slower than arrays and 2-3 times slower that 
// a list of `KVP<DateTime,double>`s. ILs difference is mostly in callvirt, TODO ask SO if callvirt is really the reason
// and is there a way to increase the speed. Enumerator as a structure will give call vs callvirt - is this is the case when it matters?
// (Continued even later) Removing one callvirt increased performance from 45 MOps to 66 mops for SortedMap and from 100 Mops to 200 mops for SortedDeque



[<SealedAttribute>]
type FilterValuesCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, filterFunc:Func<'V,bool>) =
  inherit SimpleBindCursor<'K,'V,'V>(cursorFactory)

  override this.IsContinuous = this.InputCursor.IsContinuous

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok && filterFunc.Invoke value2 then
      value <- value2
      true
    else false
  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if filterFunc.Invoke next.Value then
      value <- next.Value
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if filterFunc.Invoke previous.Value then
      value <- previous.Value
      true
    else false

  override this.Clone() = 
    let clone = new FilterValuesCursor<'K,'V>(cursorFactory, filterFunc) :> ICursor<'K,'V>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

[<SealedAttribute>]
type FilterMapCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, filterFunc:Func<'K,'V,bool>, mapperFunc:Func<'V,'R>) =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)

  override this.IsContinuous = this.InputCursor.IsContinuous

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok && filterFunc.Invoke(key,value2) then
      value <- mapperFunc.Invoke(value2)
      true
    else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if filterFunc.Invoke(next.Key,next.Value) then
      value <- mapperFunc.Invoke(next.Value)
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if filterFunc.Invoke(previous.Key, previous.Value) then
      value <- mapperFunc.Invoke(previous.Value)
      true
    else false

  override this.Clone() = 
    let clone = new FilterMapCursor<'K,'V,'R>(cursorFactory, filterFunc, mapperFunc) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  interface ICanMapSeriesValues<'K,'R> with
    member this.Map<'R2>(f2:Func<'R,'R2>): Series<'K,'R2> = 
      let mapper2 : Func<'V,'R2> = Func<'V,'R2>(fun r -> f2.Invoke(mapperFunc.Invoke(r)))
      CursorSeries(fun _ -> new FilterMapCursor<'K,'V,'R2>(cursorFactory, filterFunc, mapper2) :> ICursor<'K,'R2>) :> Series<'K,'R2>



type internal LagCursorSlow<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32) =
  inherit HorizontalCursor<'K,'V,ICursor<'K,'V>,'V>( // state is lagged cursor
    cursorFactory, 
    LagCursorSlow<'K,'V>.stateCreator(lag), 
    LagCursorSlow<'K,'V>.stateFoldNext(), 
    LagCursorSlow<'K,'V>.stateFoldPrevious(), 
    LagCursorSlow<'K,'V>.stateMapper(),
    false // lag is defined only at observed points
  )

  static member private stateCreator(lag:uint32):Func<ICursor<'K,'V>,'K, bool, KVP<bool,ICursor<'K,'V>>> = 
    Func<ICursor<'K,'V>,'K,bool,KVP<bool,ICursor<'K,'V>>>(
      fun cursor key couldMove ->
        let mutable steps = 0
        if cursor.Comparer.Compare(cursor.CurrentKey, key) = 0 then // we are creating state at the current position of input cursor
          let laggedCursor = cursor.Clone()
          while steps < int lag && laggedCursor.MovePrevious() do
            steps <- steps + 1
          KVP((steps = int lag), laggedCursor)
        else 
          let movableCursor = if couldMove then cursor else cursor.Clone()
          if movableCursor.MoveAt(key, Lookup.EQ) then
            while steps < int lag && movableCursor.MovePrevious() do
              steps <- steps + 1
            KVP((steps = int lag), movableCursor)
          else KVP(false, movableCursor)
    )

  static member private stateFoldNext():Func<ICursor<'K,'V>, KVP<'K,'V>, KVP<bool,ICursor<'K,'V>>> = 
    Func<ICursor<'K,'V>, KVP<'K,'V>, KVP<bool,ICursor<'K,'V>>>(
      fun laggedCursor _ -> KVP(laggedCursor.MoveNext(), laggedCursor)
    )

  static member private stateFoldPrevious():Func<ICursor<'K,'V>, KVP<'K,'V>, KVP<bool,ICursor<'K,'V>>> = 
    Func<ICursor<'K,'V>, KVP<'K,'V>, KVP<bool,ICursor<'K,'V>>>(
      fun laggedCursor _ -> KVP(laggedCursor.MovePrevious(), laggedCursor)
    )

  static member private stateMapper():Func<ICursor<'K,'V>,'V> = 
    Func<ICursor<'K,'V>,'V>(
      fun laggedCursor -> laggedCursor.CurrentValue
    )

// TODO (perf) optimize for Sorted/IndexedMap and SCM cursors via index
[<SealedAttribute>]
type LagCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32) =
  inherit SimpleBindCursor<'K,'V,'V>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable currentLag = 0u

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'V>): bool =
    if isMove then
      if laggedCursor = Unchecked.defaultof<_> then
        laggedCursor <- this.InputCursor.Clone()
      else 
        let moved = laggedCursor.MoveAt(key, Lookup.EQ)
        if not moved then raise (ApplicationException("This should not happen by design"))
      currentLag <- 0u
      let mutable cont = true
      while currentLag < lag && cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          currentLag <- currentLag + 1u
        else
          cont <- false
      if currentLag = lag then
        value <- laggedCursor.CurrentValue
        true
      else false
    else
      let c =
        if lookupCursor = Unchecked.defaultof<_> then 
          lookupCursor <- this.InputCursor.Clone()
        lookupCursor
      let mutable currentLag' = 0u
      let mutable cont = true
      while currentLag' < lag && cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          currentLag' <- currentLag' + 1u
        else
          cont <- false
      if currentLag' = lag then
        value <- laggedCursor.CurrentValue
        true
      else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if this.HasValidState then
      if laggedCursor.MoveNext() then
  #if PRERELEASE
        Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
  #endif
        value <- laggedCursor.CurrentValue
        true
      else false
    else
  #if PRERELEASE
      Trace.Assert(currentLag < lag, "This should not happen by design")
  #endif
      // input cursor moved before calling this method, we keep lagged cursor where it was and increment the current lag value
      currentLag <- currentLag + 1u
      if currentLag = lag then true else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    // TODO check this: MovePrevious is called only from MoveLast/MoveAt or valid state, if last was not able to create state, the series is shorter than lag
    if this.HasValidState && laggedCursor.MovePrevious() then
      value <- laggedCursor.CurrentValue
      true
    else false

  override this.Clone() = 
    let clone = new LagCursor<'K,'V>(cursorFactory, lag) :> ICursor<'K,'V>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    if lookupCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    base.Dispose()


/// Apply lagMapFunc to current and lagged value
type internal ZipLagCursorSlow<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32, mapCurrentPrev:Func<'V,'V,'R>) =
  inherit HorizontalCursor<'K,'V,KVP<ICursor<'K,'V>,'V>,'R>( // state is lagged cursor and current value
    cursorFactory, 
    ZipLagCursorSlow<'K,'V,'R>.stateCreator(lag), 
    ZipLagCursorSlow<'K,'V,'R>.stateFoldNext(), 
    ZipLagCursorSlow<'K,'V,'R>.stateFoldPrevious(), 
    ZipLagCursorSlow<'K,'V,'R>.stateMapper(mapCurrentPrev),
    false
  )

  static member private stateCreator(lag:uint32):Func<ICursor<'K,'V>,'K,bool, KVP<bool,KVP<ICursor<'K,'V>,'V>>> = 
    Func<ICursor<'K,'V>,'K,bool, KVP<bool,KVP<ICursor<'K,'V>,'V>>>(
      fun cursor key couldMove ->
        let mutable steps = 0
        if cursor.Comparer.Compare(cursor.CurrentKey, key) = 0 then // we are creating state at the current position of input cursor
          let laggedCursor = cursor.Clone()
          while steps < int lag && laggedCursor.MovePrevious() do
            steps <- steps + 1
          KVP((steps = int lag), KVP(laggedCursor, cursor.CurrentValue))
        else 
          let movableCursor = if couldMove then cursor else cursor.Clone()
          if movableCursor.MoveAt(key, Lookup.EQ) then
            let valueAtKey = movableCursor.CurrentValue
            while steps < int lag && movableCursor.MovePrevious() do
              steps <- steps + 1
            KVP((steps = int lag), KVP(movableCursor,valueAtKey))
          else KVP(false, Unchecked.defaultof<_>)
    )

  static member private stateFoldNext():Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>> = 
    Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>>(
      fun state current -> KVP(state.Key.MoveNext(), KVP(state.Key, current.Value))
    )

  static member private stateFoldPrevious():Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>> = 
    Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>>(
      fun state current -> KVP(state.Key.MovePrevious(), KVP(state.Key, current.Value))
    )

  static member private stateMapper(mapCurrentPrev):Func<KVP<ICursor<'K,'V>,'V>,'R> = 
    Func<KVP<ICursor<'K,'V>,'V>,'R>(
      fun laggedCursor_CurrVal -> mapCurrentPrev.Invoke(laggedCursor_CurrVal.Value, laggedCursor_CurrVal.Key.CurrentValue)
    )


/// Apply lagMapFunc to current and lagged value
[<SealedAttribute>]
type internal ZipLagCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32, mapCurrentPrev:Func<'V,'V,'R>) =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable currentLag = 0u

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    if isMove then
      if laggedCursor = Unchecked.defaultof<_> then
        laggedCursor <- this.InputCursor.Clone()
      else 
        let moved = laggedCursor.MoveAt(key, Lookup.EQ)
        if not moved then raise (ApplicationException("This should not happen by design"))
      currentLag <- 0u
      let mutable cont = true
      while currentLag < lag && cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          currentLag <- currentLag + 1u
        else
          cont <- false
      if currentLag = lag then
        value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
        true
      else false
    else
      let c =
        if lookupCursor = Unchecked.defaultof<_> then 
          lookupCursor <- this.InputCursor.Clone()
        lookupCursor
      let mutable currentLag' = 0u
      let mutable cont = true
      while currentLag' < lag && cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          currentLag' <- currentLag' + 1u
        else
          cont <- false
      if currentLag' = lag then
        value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
        true
      else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if this.HasValidState then
      if laggedCursor.MoveNext() then
  #if PRERELEASE
        Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
  #endif
        value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
        true
      else false
    else
  #if PRERELEASE
      Trace.Assert(currentLag < lag, "This should not happen by design")
  #endif
      // input cursor moved before calling this method, we keep lagged cursor where it was and increment the current lag value
      currentLag <- currentLag + 1u
      if currentLag = lag then
        value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
        true 
      else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    // TODO check this: MovePrevious is called only from MoveLast/MoveAt or valid state, if last was not able to create state, the series is shorter than lag
    if this.HasValidState && laggedCursor.MovePrevious() then
      value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
      true
    else false

  override this.Clone() = 
    let clone = new ZipLagCursorSlow<'K,'V, 'R>(cursorFactory, lag, mapCurrentPrev) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    if lookupCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    base.Dispose()

  interface ICanMapSeriesValues<'K,'R> with
    member this.Map<'R2>(f2:Func<'R,'R2>): Series<'K,'R2> = 
      let mapCurrentPrev2 : Func<'V,'V,'R2> = Func<'V,'V,'R2>(fun c p -> f2.Invoke(mapCurrentPrev.Invoke(c, p)))
      CursorSeries(fun _ -> new ZipLagCursor<'K,'V,'R2>(cursorFactory, lag, mapCurrentPrev2) :> ICursor<'K,'R2>) :> Series<'K,'R2>
      

// TODO unit tests with all moves & TGV, easy to fuck up here
/// Apply mapCurrentPrevN to current, lagged values and distance between them
type internal ZipLagAllowIncompleteCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, zeroBasedLag:uint32, step:uint32, mapCurrentPrevN:Func<KVP<'K,'V>,KVP<'K,'V>,uint32,'R>, allowIncomplete:bool) =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable currentLag = 0u
  let mutable currentSteps = 0u
  // e.g. for width 7 and step 3 we start with [1], [1-4], [1-7], [4-10] so that the step of ending value is constant
  //let minWidth = lag % step

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    if isMove then
      if laggedCursor = Unchecked.defaultof<_> then
        laggedCursor <- this.InputCursor.Clone()
      else 
        let moved = laggedCursor.MoveAt(key, Lookup.EQ)
        if not moved then raise (ApplicationException("This should not happen by design"))
      currentLag <- 0u
      let mutable cont = true
      while currentLag < zeroBasedLag && cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          currentLag <- currentLag + 1u
        else
          let moved' = laggedCursor.MoveFirst()
          Trace.Assert(moved', "ZipLagAllowIncompleteCursor: Must check for empty series if this happens")
          cont <- false
      if currentLag = zeroBasedLag || allowIncomplete then
        value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
        true
      else false
    else
      let c =
        if lookupCursor = Unchecked.defaultof<_> then 
          lookupCursor <- this.InputCursor.Clone()
        lookupCursor
      let mutable currentLag' = 0u
      let mutable cont = true
      while currentLag' < zeroBasedLag && cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          currentLag' <- currentLag' + 1u
        else
          cont <- false
      if currentLag' = zeroBasedLag || allowIncomplete then
        value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag')
        true
      else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if this.HasValidState then
  #if PRERELEASE
      Trace.Assert((currentLag <= zeroBasedLag), "This should not happen by design")
  #endif
      if currentLag = zeroBasedLag then
        let moved = laggedCursor.MoveNext()
  #if PRERELEASE
        Trace.Assert((moved), "This should not happen by design")
  #endif
        value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
        currentSteps <- currentSteps + 1u
        if currentSteps = step then
          currentSteps <- 0u
          true
        else
          false
      elif currentLag < zeroBasedLag && allowIncomplete then
        // do not move lagged cursor here
        currentLag <- currentLag + 1u
        value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
        currentSteps <- currentSteps + 1u
        if currentSteps = step then
          currentSteps <- 0u
          true
        else
          false
      else false
    else
  #if PRERELEASE
      Trace.Assert(not allowIncomplete, "This should not happen by design")
      Trace.Assert(currentLag <= zeroBasedLag, "This should not happen by design")
  #else
      if allowIncomplete then raise (ApplicationException("This should not happen by design"))
  #endif
      // input cursor moved before calling this method, we keep lagged cursor where it was and increment the current lag value
      currentLag <- currentLag + 1u
      if currentLag = zeroBasedLag then 
        value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
        //currentSteps <- currentSteps + 1u
        true
//        if currentSteps = step then
//          currentSteps <- 0u
//          true
//        else
//          false 
      else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if this.HasValidState && laggedCursor.MovePrevious() then
      value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
      currentSteps <- currentSteps + 1u
      if currentSteps = step then
        currentSteps <- 0u
        true
      else
        false
    elif allowIncomplete && currentLag > 0u then
      currentLag <- currentLag - 1u
      value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
      currentSteps <- currentSteps + 1u
      if currentSteps = step then
        currentSteps <- 0u
        true
      else
        false
    else false

  override this.Clone() = 
    let clone = new ZipLagAllowIncompleteCursor<'K,'V, 'R>(cursorFactory, zeroBasedLag, step, mapCurrentPrevN, allowIncomplete) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    if lookupCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    base.Dispose()

//  interface ICanMapSeriesValues<'K,'R> with
//    member this.Map<'R2>(f2:Func<'R,'R2>): Series<'K,'R2> = 
//      let mapCurrentPrev2 : Func<'V,'V,'R2> = Func<'V,'V,'R2>(fun c p -> f2.Invoke(mapCurrentPrevN.Invoke(c, p, currentLag)))
//      CursorSeries(fun _ -> new ZipLagCursor<'K,'V,'R2>(cursorFactory, lag, mapCurrentPrev2) :> ICursor<'K,'R2>) :> Series<'K,'R2>



/// Repack original types into value tuples. Due to the lazyness this only happens for a current value of cursor. ZipN keeps vArr instance and
/// rewrites its values. For value types we will always be in L1/stack, for reference types we do not care that much about performance.
type Zip2Cursor<'K,'V,'V2,'R>(cursorFactoryL:Func<ICursor<'K,'V>>,cursorFactoryR:Func<ICursor<'K,'V2>>, mapF:Func<'K,'V,'V2,'R>) =
  inherit ZipNCursor<'K,ValueTuple<'V,'V2>,'R>(
    Func<'K, ValueTuple<'V,'V2>[],'R>(fun (k:'K) (tArr:ValueTuple<'V,'V2>[]) -> mapF.Invoke(k, tArr.[0].Value1, tArr.[1].Value2)), 
    (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryL, Func<_,_>(fun (x:'V) -> ValueTuple<'V,'V2>(x, Unchecked.defaultof<'V2>)), OptionalValue.Missing) :> ICursor<'K,ValueTuple<'V,'V2>>), 
    (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryR, Func<_,_>(fun (x:'V2) -> ValueTuple<'V,'V2>(Unchecked.defaultof<'V>, x)), OptionalValue.Missing) :> ICursor<'K,ValueTuple<'V,'V2>>)
  )



type internal ScanCursorState<'K,'V,'R> =
  struct
    [<DefaultValueAttribute(false)>]
    val mutable value : 'R
    [<DefaultValueAttribute>]
    val mutable buffered : bool
    [<DefaultValueAttribute>]
    val mutable buffer : SortedMap<'K,'R>
  end
  static member GetOrMakeBuffer(this:ScanCursorState<'K,'V,'R> byref, cursor:ICursor<'K,'V>, folder:Func<_,_,_,_>) = 
    if not this.buffered then 
      this.buffered <- true
      // TODO here SortedMap must be subscribed to the source 
      // and a CTS must be cancelled when disposing this cursor
      let sm = SortedMap()
      let source = CursorSeries(fun _ -> cursor.Clone()) 
      for kvp in source do
        this.value <- folder.Invoke(this.value, kvp.Key, kvp.Value)
        sm.AddLast(kvp.Key, this.value)
      this.buffer <- sm
    this.buffer


// Scan is not horizontal cursor, this is quick hack to have it 
type internal ScanCursorSlow<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, init:'R, folder:Func<'R,'K,'V,'R>) =
  inherit HorizontalCursor<'K,'V,ScanCursorState<'K,'V,'R>,'R>(
    cursorFactory, 
    ScanCursorSlow<'K,'V,'R>.stateCreator(cursorFactory, init, folder), 
    ScanCursorSlow<'K,'V,'R>.stateFoldNext(cursorFactory,folder), 
    ScanCursorSlow<'K,'V,'R>.stateFoldPrevious(cursorFactory,folder),
    Func<ScanCursorState<'K,'V,'R>,'R>(fun x -> x.value),
    false
  )

  static member private stateCreator(cursorFactory, init:'R, folder:Func<'R,'K,'V,'R>):Func<ICursor<'K,'V>,'K,bool,KVP<bool,ScanCursorState<'K,'V,'R>>> = 
    Func<ICursor<'K,'V>,'K,bool, KVP<bool,ScanCursorState<'K,'V,'R>>>(
      fun cursor key couldMove ->
        // this is naive implementation without any perf thoughts
        let movable = if couldMove then cursor else cursor.Clone()
        if movable.MoveFirst() then
          let mutable state = Unchecked.defaultof<ScanCursorState<'K,'V,'R>>
          let first = movable.CurrentKey
          if not state.buffered && cursor.Comparer.Compare(first, key) = 0 then
              state.value <- folder.Invoke(state.value, key, cursor.CurrentValue)
              KVP(true, state)
          else 
            let ok, value = ScanCursorState.GetOrMakeBuffer(ref state, cursor, folder).TryGetValue(key)
            state.value <- value
            KVP(ok,state)
        else KVP(false,Unchecked.defaultof<ScanCursorState<'K,'V,'R>>)
    )

  static member private stateFoldNext(cursorFactory,folder:Func<'R,'K,'V,'R>):Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>,KVP<bool,ScanCursorState<'K,'V,'R>>> = 
    Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>, KVP<bool,ScanCursorState<'K,'V,'R>>>(
      fun state current ->
        let mutable state = state
        state.value <- folder.Invoke(state.value, current.Key, current.Value)
        KVP(true,state)
    )

  static member private stateFoldPrevious(cursorFactory:Func<ICursor<'K,'V>>,folder:Func<'R,'K,'V,'R>):Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>,KVP<bool,ScanCursorState<'K,'V,'R>>> = 
    Func<ScanCursorState<'K,'V,'R>, KVP<'K,'V>, KVP<bool,ScanCursorState<'K,'V,'R>>>(
      fun state current -> 
        let mutable state = state
        if not state.buffered then
          state.buffer <- ScanCursorState.GetOrMakeBuffer(ref state, cursorFactory.Invoke(), folder)
        let ok, value = state.buffer.TryGetValue(current.Key)
        state.value <- value
        KVP(ok,state)
    )

[<SealedAttribute>]
type ScanCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, init:'R, folder:Func<'R,'K,'V,'R>) as this =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)

  let mutable moveAtCursor = cursorFactory.Invoke()
  let mutable buffered = false
  let mutable state : 'R = init
  let hasValues, first = if moveAtCursor.MoveFirst() then true, moveAtCursor.CurrentKey else false, Unchecked.defaultof<_>

  let mutable buffer = Unchecked.defaultof<SortedMap<'K,'R>>

  let getOrMakeBuffer() = 
    if not buffered then 
      buffered <- true
      // TODO here SortedMap must be subscribed to the source 
      // and a CTS must be cancelled when disposing this cursor
      let sm = SortedMap()
      let source = CursorSeries(cursorFactory) 
      for kvp in source do
        state <- folder.Invoke(state, kvp.Key, kvp.Value)
        sm.AddLast(kvp.Key, state)
      buffer <- sm
    buffer

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    Trace.Assert(hasValues)
    // move first
    if not buffered && isMove && this.InputCursor.Comparer.Compare(first, key) = 0 then
      Trace.Assert(Unchecked.equals state init)
      state <- folder.Invoke(state, key, this.InputCursor.CurrentValue)
      value <- state
      true
    else 
      getOrMakeBuffer().TryGetValue(key, &value)

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if not buffered then
      state <- folder.Invoke(state, next.Key, next.Value)
      value <- state
      true
    else getOrMakeBuffer().TryGetValue(next.Key, &value)
      
  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    getOrMakeBuffer().TryGetValue(previous.Key, &value)

  override this.Clone() = 
    let clone = new ScanCursor<'K,'V, 'R>(cursorFactory, init, folder) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if moveAtCursor <> Unchecked.defaultof<_> then moveAtCursor.Dispose()
    base.Dispose()



//type internal WindowCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, width:uint32, step:uint32, allowIncomplete:bool) =
//  inherit HorizontalCursor<'K,'V,KVP<ICursor<'K,'V>,'V>,Series<'K,'V>>( // state is lagged cursor and current value
//    cursorFactory, 
//    WindowCursor<'K,'V,'R>.stateCreator(lag), 
//    WindowCursor<'K,'V,'R>.stateFoldNext(), 
//    WindowCursor<'K,'V,'R>.stateFoldPrevious(), 
//    WindowCursor<'K,'V,'R>.stateMapper(mapCurrentPrev),
//    false
//  )
//
//  static member private stateCreator(width:uint32, step:uint32, allowIncomplete:bool):Func<ICursor<'K,'V>,'K,bool, KVP<bool,KVP<ICursor<'K,'V>,'V>>> = 
//    Func<ICursor<'K,'V>,'K,bool, KVP<bool,KVP<ICursor<'K,'V>,'V>>>(
//      fun cursor key couldMove ->
//        let mutable steps = 0
//        if cursor.Comparer.Compare(cursor.CurrentKey, key) = 0 then // we are creating state at the current position of input cursor
//          let laggedCursor = cursor.Clone()
//          while steps < int lag && laggedCursor.MovePrevious() do
//            steps <- steps + 1
//          KVP((steps = int lag), KVP(laggedCursor, cursor.CurrentValue))
//        else 
//          let movableCursor = if couldMove then cursor else cursor.Clone()
//          if movableCursor.MoveAt(key, Lookup.EQ) then
//            let valueAtKey = movableCursor.CurrentValue
//            while steps < int lag && movableCursor.MovePrevious() do
//              steps <- steps + 1
//            KVP((steps = int lag), KVP(movableCursor,valueAtKey))
//          else KVP(false, Unchecked.defaultof<_>)
//    )
//
//  static member private stateFoldNext():Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>> = 
//    Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>>(
//      fun state current -> KVP(state.Key.MoveNext(), KVP(state.Key, current.Value))
//    )
//
//  static member private stateFoldPrevious():Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>> = 
//    Func<KVP<ICursor<'K,'V>,'V>, KVP<'K,'V>, KVP<bool,KVP<ICursor<'K,'V>,'V>>>(
//      fun state current -> KVP(state.Key.MovePrevious(), KVP(state.Key, current.Value))
//    )
//
//  static member private stateMapper(mapCurrentPrev):Func<KVP<ICursor<'K,'V>,'V>,'R> = 
//    Func<KVP<ICursor<'K,'V>,'V>,'R>(
//      fun laggedCursor_CurrVal -> mapCurrentPrev.Invoke(laggedCursor_CurrVal.Value, laggedCursor_CurrVal.Key.CurrentValue)
//    )



[<SealedAttribute>]
type internal WindowCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, width:uint32, step:uint32, allowIncomplete:bool) =
  inherit ZipLagAllowIncompleteCursor<'K,'V,Series<'K,'V>>(
    cursorFactory, 
    width - 1u, // NB! & TODO (low) ZipLagAllowIncompleteCursor accepts zero-based width, was to lazy to reimplement. We check widths/step in extension method
    step,
    (fun c p n -> 
      let startPoint = Some(p.Key)
      let endPoint = Some(c.Key)
      let rangeCursor() = new RangeCursor<'K,'V>(cursorFactory, startPoint, endPoint, None, None) :> ICursor<'K,'V>
      let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
      window
    ),
    allowIncomplete)


// TODO input should be buffered for at least width + 1 for performance
// TODO this is incomplete implementation. doesn't account for cases when step > width (quite valid case)
// TODO use Window vs Chunk like in Deedle, there is logical issue with overlapped windows - if we ask for a point x, it could be different when enumerated
// But the same is true for chunk - probably we must create a buffer in one go
[<SealedAttribute>]
type internal WindowCursorOld<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, width:uint32, step:uint32, allowIncomplete:bool) =
  inherit SimpleBindCursor<'K,'V,Series<'K,'V>>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable moves = 0
  // distance from lagged cursor to current
  let mutable currentLag = 0

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<Series<'K,'V>>): bool =
    let ok, activeLaggedCursor = 
      if isMove then
        laggedCursor <- this.InputCursor.Clone()
        true, laggedCursor
      else
        let c = this.InputCursor.Clone()
        if c.MoveAt(key, Lookup.EQ) then // Window is only defined at an existing position
          true, c
        else false, Unchecked.defaultof<_>
    if ok then
      //let mutable cont = true
      let mutable lag = 1 // NB! current value counts toward the width, lag = 1
      while lag < int width && activeLaggedCursor.MovePrevious() do
        lag <- lag + 1
      if lag = int width then // reached width
#if PRERELEASE
        Trace.Assert(laggedCursor.Comparer.Compare(activeLaggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
        // NB! freeze bounds for the range cursor
        let startPoint = Some(activeLaggedCursor.CurrentKey)
        let endPoint = Some(this.InputCursor.CurrentKey)
        let rangeCursor() = new RangeCursor<'K,'V>(cursorFactory, startPoint, endPoint, None, None) :> ICursor<'K,'V>
        let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
        value <- window
        moves <- 0
        true
      else 
        if allowIncomplete then
          activeLaggedCursor.MoveAt(key, Lookup.EQ) |> ignore
          //let mutable cont = true
          let mutable lag = 1 // NB! current value counts toward the width, lag = 1
          while lag < int step && activeLaggedCursor.MovePrevious() do
            lag <- lag + 1
          if lag = int step then // reached width
#if PRERELEASE
            Trace.Assert(laggedCursor.Comparer.Compare(activeLaggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
            // NB! freeze bounds for the range cursor
            let startPoint = Some(activeLaggedCursor.CurrentKey)
            let endPoint = Some(this.InputCursor.CurrentKey)
            let rangeCursor() = new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
            let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
            value <- window
            currentLag <- lag
            moves <- 0
            true
          else false
        else
          false
    else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<Series<'K,'V>>) : bool =
    if allowIncomplete then
      let moved = 
        if currentLag < int width then
          currentLag <- currentLag + 1
          true
        else
          laggedCursor.MoveNext()
      if moved then
        moves <- moves + 1
        if Math.Abs(moves) = int step then
          // NB! freeze bounds for the range cursor
          let startPoint = Some(laggedCursor.CurrentKey)
          let endPoint = Some(this.InputCursor.CurrentKey)
          let rangeCursor() = new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
        
          let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
          value <- window
          moves <- 0
          true
        else false
      else false
    else
      if laggedCursor.MoveNext() then
        moves <- moves + 1
        if Math.Abs(moves) = int step then
          // NB! freeze bounds for the range cursor
          let startPoint = Some(laggedCursor.CurrentKey)
          let endPoint = Some(this.InputCursor.CurrentKey)
          let rangeCursor() = new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
        
          let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
          value <- window
          moves <- 0
          true
        else false
      else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<Series<'K,'V>>) : bool =
    if laggedCursor.MovePrevious() then
      moves <- moves - 1
      if Math.Abs(moves) = int step then
        // NB! freeze bounds for the range cursor
        let startPoint = Some(laggedCursor.CurrentKey)
        let endPoint = Some(this.InputCursor.CurrentKey)
        let rangeCursor() = new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
        let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
        value <- window
        moves <- 0
        true
      else false
    else false

  override this.Clone() = 
    let clone = new WindowCursorOld<'K,'V>(cursorFactory, width, step, allowIncomplete) :> ICursor<'K,Series<'K,'V>>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    base.Dispose()