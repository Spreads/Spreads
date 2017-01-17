// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


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
      #if PRERELEASE
      Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,key) = 0 )
      Trace.Assert(laggedCursor.Comparer.Compare(this.InputCursor.CurrentKey,key) = 0 )
      #endif
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
          let moved' = laggedCursor.MoveFirst()
          Trace.Assert(moved', "ZipLagAllowIncompleteCursor: Must check for empty series if this happens")
          cont <- false
      if currentLag = lag then
        value <- laggedCursor.CurrentValue
        true
      else false
    else
      if lookupCursor = Unchecked.defaultof<_> then lookupCursor <- this.InputCursor.Clone()
      let moved = lookupCursor.MoveAt(key, Lookup.EQ)
      let mutable currentLag' = 0u
      let mutable cont = moved
      while currentLag' < lag && cont do
        let moved = lookupCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(lookupCursor.Comparer.Compare(lookupCursor.CurrentKey,key) <= 0 )
#endif
          currentLag' <- currentLag' + 1u
        else
          cont <- false
      if currentLag' = lag then
        value <- lookupCursor.CurrentValue
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
      if currentLag = lag then 
        value <- laggedCursor.CurrentValue
        true
      else false

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
type ZipLagCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32, mapCurrentPrev:Func<'V,'V,'R>) =
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
      #if PRERELEASE
      Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,key) = 0 )
      Trace.Assert(laggedCursor.Comparer.Compare(this.InputCursor.CurrentKey,key) = 0 )
      #endif
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
          let moved' = laggedCursor.MoveFirst()
          Trace.Assert(moved', "ZipLagAllowIncompleteCursor: Must check for empty series if this happens")
          cont <- false
      if currentLag = lag then
        value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
        true
      else false
    else
      if lookupCursor = Unchecked.defaultof<_> then lookupCursor <- this.InputCursor.Clone()
      let moved = lookupCursor.MoveAt(key, Lookup.EQ)
      let mutable currentLag' = 0u
      let mutable cont = moved
      while currentLag' < lag && cont do
        let moved = lookupCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(lookupCursor.Comparer.Compare(lookupCursor.CurrentKey,key) <= 0 )
#endif
          currentLag' <- currentLag' + 1u
        else
          cont <- false
      if currentLag' = lag then
        let hasValue,v = this.InputCursor.TryGetValue(key)
        if hasValue then
          value <- mapCurrentPrev.Invoke(v, lookupCursor.CurrentValue)
          true
        else false
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
    let clone = new ZipLagCursor<'K,'V, 'R>(cursorFactory, lag, mapCurrentPrev) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    if lookupCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    base.Dispose()

  // almost 10% gain on a trivial call: CompareHirizontalCursorWithCursorBind in Benchmarks
  interface ICanMapSeriesValues<'K,'R> with
    member this.Map<'R2>(f2, _): Series<'K,'R2> = 
      let mapCurrentPrev2 : Func<'V,'V,'R2> = Func<'V,'V,'R2>(fun c p -> f2(mapCurrentPrev.Invoke(c, p)))
      CursorSeries(fun _ -> new ZipLagCursor<'K,'V,'R2>(cursorFactory, lag, mapCurrentPrev2) :> ICursor<'K,'R2>) :> Series<'K,'R2>
      

// TODO unit tests with all moves & TGV, easy to fuck up here
/// Apply mapCurrentPrevN to current, lagged values and distance between them
type ZipLagAllowIncompleteCursor<'K,'V,'R>
  (cursorFactory:Func<ICursor<'K,'V>>, zeroBasedLag:uint32, step:uint32, 
    mapCurrentPrevN:Func<KVP<'K,'V>,KVP<'K,'V>,uint32,'R>, allowIncomplete:bool) =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable currentLag = 0u
  let mutable currentSteps = 0u

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    if isMove then
      if laggedCursor = Unchecked.defaultof<_> then 
        laggedCursor <- this.InputCursor.Clone()
      else
        let moved = laggedCursor.MoveAt(key, Lookup.EQ)
        if not moved then raise (ApplicationException("This should not happen by design"))
      #if PRERELEASE
      Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,key) = 0 )
      Trace.Assert(laggedCursor.Comparer.Compare(this.InputCursor.CurrentKey,key) = 0 )
      #endif
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
      if lookupCursor = Unchecked.defaultof<_> then lookupCursor <- this.InputCursor.Clone()
      let moved = lookupCursor.MoveAt(key, Lookup.EQ)
      let mutable currentLag' = 0u
      let mutable cont = moved
      while currentLag' < zeroBasedLag && cont do
        let moved = lookupCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(lookupCursor.Comparer.Compare(lookupCursor.CurrentKey,key) <= 0 )
#endif
          currentLag' <- currentLag' + 1u
        else
          cont <- false
      if currentLag' = zeroBasedLag || allowIncomplete then
        let hasValue,v = this.InputCursor.TryGetValue(key)
        if hasValue then
          value <- mapCurrentPrevN.Invoke(KVP(key,v), laggedCursor.Current, currentLag')
          true
        else false
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
        currentSteps <- currentSteps + 1u
        if currentSteps = step then
          value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
          currentSteps <- 0u
          true
        else
          false
      elif currentLag < zeroBasedLag && allowIncomplete then
        // do not move lagged cursor here
        currentLag <- currentLag + 1u
        currentSteps <- currentSteps + 1u
        if currentSteps = step then
          value <- mapCurrentPrevN.Invoke(this.InputCursor.Current, laggedCursor.Current, currentLag)
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
        // Do not increment steps here
        true
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
      // TODO rewrite this similar to TryUpdateNext (i do not understand this on second read, it is non obvious)
      if this.HasValidState then
        currentSteps <- currentSteps + 1u
        if currentSteps = step then
          currentSteps <- 0u
          true
        else
          false
      else true
    else false

  override this.Clone() = 
    let clone = new ZipLagAllowIncompleteCursor<'K,'V, 'R>(cursorFactory, zeroBasedLag, step, mapCurrentPrevN, allowIncomplete) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    if lookupCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    base.Dispose()

  interface ICanMapSeriesValues<'K,'R> with
    member this.Map<'R2>(f2, _): Series<'K,'R2> = 
      let mapCurrentPrevN2 = Func<_,_,_,_>(fun c p l -> f2(mapCurrentPrevN.Invoke(c, p, l)))
      CursorSeries(fun _ -> new ZipLagAllowIncompleteCursor<'K,'V,'R2>(cursorFactory, zeroBasedLag, step, mapCurrentPrevN2, allowIncomplete) :> ICursor<'K,'R2>) :> Series<'K,'R2>


// TODO (perf) ICanMapValues
// TODO! this is really bad name!
[<SealedAttribute>]
type ScanLagAllowIncompleteCursor<'K,'V,'R>
  (cursorFactory:Func<ICursor<'K,'V>>, width:uint32, step:uint32, initState:Func<'R>, 
    updateStateAddSubstract:Func<'R,KVP<'K,'V>,KVP<'K,'V>,uint32,'R>, allowIncomplete:bool) =
  inherit SimpleBindCursor<'K,'V,'R>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable currentWidth = 0u
  let mutable currentSteps = 0u
  let mutable currentState = initState.Invoke()

  override this.IsContinuous = false

  override this.TryGetValue(key:'K, isMove:bool, [<Out>] value: byref<'R>): bool =
    if isMove then
      if laggedCursor = Unchecked.defaultof<_> then 
        laggedCursor <- this.InputCursor.Clone()
      else
        let moved = laggedCursor.MoveAt(key, Lookup.EQ)
        if not moved then raise (ApplicationException("This should not happen by design"))
      #if PRERELEASE
      Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,key) = 0 )
      Trace.Assert(laggedCursor.Comparer.Compare(this.InputCursor.CurrentKey,key) = 0 )
      #endif
      currentWidth <- 0u
      currentState <- initState.Invoke()

      // current value is added to the state
      currentWidth <- currentWidth + 1u
      currentState <- updateStateAddSubstract.Invoke(currentState, laggedCursor.Current, Unchecked.defaultof<_>, currentWidth)

      let mutable cont = true
      while currentWidth < width && cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          currentWidth <- currentWidth + 1u
          currentState <- updateStateAddSubstract.Invoke(currentState, laggedCursor.Current, Unchecked.defaultof<_>, currentWidth)
        else
//          let moved' = laggedCursor.MoveFirst()
//          if currentLag = 0u then
//            currentState <- updateStateAddSubstract.Invoke(currentState, laggedCursor.Current, Unchecked.defaultof<_>, currentLag)
//          Trace.Assert(moved', "ZipLagAllowIncompleteCursor: Must check for empty series if this happens")
          cont <- false
      if currentWidth = width || allowIncomplete then
        value <- currentState
        true
      else false
    else
//      if this.HasValidState && this.InputCursor.Comparer.Compare(this.CurrentKey, key) = 0 then
//        value <- this.CurrentValue
//        true
//      else
      if lookupCursor = Unchecked.defaultof<_> then lookupCursor <- this.InputCursor.Clone()
      let moved = lookupCursor.MoveAt(key, Lookup.EQ)
      let mutable currentState' = initState.Invoke()
      let mutable currentWidth' = 0u

      // current value is added to the state
      currentWidth' <- currentWidth' + 1u
      currentState' <- updateStateAddSubstract.Invoke(currentState', lookupCursor.Current, Unchecked.defaultof<_>, currentWidth')

      let mutable cont = moved
      while currentWidth' < width && cont do
        let moved = lookupCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(lookupCursor.Comparer.Compare(lookupCursor.CurrentKey,key) <= 0 )
#endif
          currentWidth' <- currentWidth' + 1u //!!!
          currentState' <- updateStateAddSubstract.Invoke(currentState', lookupCursor.Current, Unchecked.defaultof<_>, currentWidth)
        else
//          let moved' = lookupCursor.MoveFirst()
//          if currentLag = 0u then
//            currentState' <- updateStateAddSubstract.Invoke(currentState', lookupCursor.Current, Unchecked.defaultof<_>, currentLag)
          cont <- false
      if currentWidth' = width || allowIncomplete then
        value <- currentState'
        true
      else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if this.HasValidState then
  #if PRERELEASE
      Trace.Assert((currentWidth <= width), "This should not happen by design")
  #endif
      if currentWidth = width then
        // this value will be dropped from a window
        let out = laggedCursor.Current
        let moved = laggedCursor.MoveNext()
  #if PRERELEASE
        Trace.Assert((moved), "This should not happen by design")
  #endif
        currentState <- updateStateAddSubstract.Invoke(currentState, this.InputCursor.Current, out, currentWidth)
        value <- currentState
        currentSteps <- currentSteps + 1u
        if currentSteps = step then
          currentSteps <- 0u
          true
        else
          false
      elif currentWidth < width && allowIncomplete then
        // do not move lagged cursor here
        currentWidth <- currentWidth + 1u //!!!
        currentState <- updateStateAddSubstract.Invoke(currentState, this.InputCursor.Current, Unchecked.defaultof<_>, currentWidth)
        value <- currentState
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
      Trace.Assert(currentWidth <= width, "This should not happen by design")
  #else
      if allowIncomplete then raise (ApplicationException("This should not happen by design"))
  #endif
      // input cursor moved before calling this method, we keep lagged cursor where it was and increment the current lag value
      currentWidth <- currentWidth + 1u //!!!
      currentState <- updateStateAddSubstract.Invoke(currentState, this.InputCursor.Current, Unchecked.defaultof<_>, currentWidth)
      if currentWidth = width then 
        value <- currentState
        true
      else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    // we cannot capture previous Input.Current without additional field, for now just recreate
    this.TryGetValue(previous.Key, true, &value)

  override this.Clone() = 
    let clone = new ScanLagAllowIncompleteCursor<'K,'V,'R>(cursorFactory, width, step, initState, updateStateAddSubstract, allowIncomplete) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    if lookupCursor <> Unchecked.defaultof<_> then laggedCursor.Dispose()
    base.Dispose()


[<SealedAttribute>]
type WindowCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, width:uint32, step:uint32, allowIncomplete:bool) =
  inherit ZipLagAllowIncompleteCursor<'K,'V,Series<'K,'V>>(
    cursorFactory, 
    width - 1u, // NB! & TODO (low) ZipLagAllowIncompleteCursor accepts zero-based width, was to lazy to reimplement. We check widths/step in extension method
    step,
    (fun c p n -> 
      let startPoint = Some(p.Key)
      let endPoint = Some(c.Key)
      // TODO this is wrong, 
      let rangeCursorFactory() = new RangeCursor<'K,'V>(cursorFactory, startPoint, endPoint, None, None) :> ICursor<'K,'V>
      let windowDefinition = CursorSeries(Func<ICursor<'K,'V>>(rangeCursorFactory)) :> Series<'K,'V>
      windowDefinition
    ),
    allowIncomplete)







/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////                              OBSOLETE                         //////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



[<ObsoleteAttribute>]
type internal LagCursorSlow<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32) =
  inherit FunctionalBindCursor<'K,'V,ICursor<'K,'V>,'V>( // state is lagged cursor
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



/// Apply lagMapFunc to current and lagged value
[<ObsoleteAttribute>]
type internal ZipLagCursorSlow<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32, mapCurrentPrev:Func<'V,'V,'R>) =
  inherit FunctionalBindCursor<'K,'V,KVP<ICursor<'K,'V>,'V>,'R>( // state is lagged cursor and current value
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




// TODO input should be buffered for at least width + 1 for performance
// TODO this is incomplete implementation. doesn't account for cases when step > width (quite valid case)
// TODO use Window vs Chunk like in Deedle, there is logical issue with overlapped windows - if we ask for a point x, it could be different when enumerated
// But the same is true for chunk - probably we must create a buffer in one go
[<SealedAttribute>]
[<ObsoleteAttribute>]
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
