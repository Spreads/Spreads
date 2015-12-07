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
// (Continued even later) Removing one callvirt increased performance from 45 mops to 66 mops for SortedMap and from 100 mops to 200 mops for SortedDeque


/// Repeat previous value for all missing keys
[<ObsoleteAttribute("Use RepeatCursor")>]
type CursorRepeat<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>) as this =
  inherit CursorBind<'K,'V,'V>(cursorFactory)
  do
    this.IsContinuous <- true
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V>): bool =
    if isPositioned then
      value <- this.InputCursor.CurrentValue
      true
    else
      let c = this.InputCursor.Comparer.Compare(key, this.InputCursor.CurrentKey)
      //Trace.Assert(c<>0, "isPositioned argument must rule this case out")
      if c = 0 && this.HasValidState then
        value <- this.InputCursor.CurrentValue
        true
      elif c < 0 then
        let mutable laggedCursorOk = true
        // always position lagged cursor one step away from the input current
        if laggedCursor = Unchecked.defaultof<ICursor<'K,'V>> then 
          laggedCursor <- this.InputCursor.Clone()
          if not (laggedCursor.MovePrevious()) then 
            laggedCursor <- Unchecked.defaultof<ICursor<'K,'V>>
            laggedCursorOk <- false

        if laggedCursorOk then // NB micro optimization instead of [laggedCursor <> Unchecked.defaultof<ICursor<'K,'V>>] yields couple of %% performance
          let c2 = this.InputCursor.Comparer.Compare(key, laggedCursor.CurrentKey)
          if c2 >= 0 then // key is between input and lagged cursor, frequent case for CursorZip
            value <- laggedCursor.CurrentValue
            true
          else
            let cursor = this.InputCursor.Clone()
            if cursor.MoveAt(key, Lookup.LE) then
#if PRERELEASE
              Trace.Assert(not <| Unchecked.equals key this.InputCursor.CurrentKey)
#endif
              value <- cursor.CurrentValue
              true
            else false
        else
          false
      else // c > 0 or cursor not started
        // naive implementation, CursorZip must hit c < 0 case (TODO check this)
        let cursor = this.InputCursor.Clone()
        if cursor.MoveAt(key, Lookup.LE) then
#if PRERELEASE
          Trace.Assert(not <| Unchecked.equals key this.InputCursor.CurrentKey)
#endif
          value <- cursor.CurrentValue
          true
        else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if laggedCursor = Unchecked.defaultof<ICursor<'K,'V>> then 
      laggedCursor <- this.InputCursor.Clone()
      if not (laggedCursor.MovePrevious()) then laggedCursor <- Unchecked.defaultof<ICursor<'K,'V>>
    else
      let moved = laggedCursor.MoveNext()
      ()
  #if PRERELEASE
      //Trace.Assert(moved)
  #endif
    value <- next.Value
    true

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    let moved = laggedCursor.MoveNext()
    if not moved then laggedCursor <- Unchecked.defaultof<ICursor<'K,'V>>
    value <- previous.Value
    true

  override this.Dispose() = 
    if laggedCursor <> Unchecked.defaultof<ICursor<'K,'V>> then laggedCursor.Dispose()
    base.Dispose()


type FillCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, fillValue:'V) as this =
  inherit CursorBind<'K,'V,'V>(cursorFactory)
  do
    this.IsContinuous <- true  
  // TODO optimize as Repeat
  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V>): bool =
    if isPositioned then
      value <- this.InputCursor.CurrentValue
      true
    else
      let ok, value2 = this.InputCursor.TryGetValue(key)
      if ok then
        value <- value2
      else
        value <- fillValue
      true

  override this.Clone() = 
    let clone = new FillCursor<'K,'V>(cursorFactory, fillValue) :> ICursor<'K,'V>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone
      
// TODO a lightweight FilterMapValuesCursor
//type MapValuesCursor<'K,'V,'V2>(cursorFactory:Func<ICursor<'K,'V>>, mapF:Func<'V,'V2>) =
//  inherit CursorBind<'K,'V,'V2>(cursorFactory)
//
//  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V2>): bool =
//    if isPositioned then
//      value <- mapF.Invoke(this.InputCursor.CurrentValue)
//      true
//    else
//    // add works on any value, so must use TryGetValue instead of MoveAt
//      let ok, value2 = this.InputCursor.TryGetValue(key)
//      if ok then
//        value <- mapF.Invoke(value2)
//        true
//      else false
//
//  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
//    value <- mapF.Invoke(next.Value)
//    true
//
//  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
//    value <- mapF.Invoke(previous.Value)
//    true
//
//  override this.Clone() = 
//    let clone = new MapValuesCursor<'K,'V,'V2>(cursorFactory, mapF) :> ICursor<'K,'V2>
//    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
//    clone

// this is not possible with cursor
//type MapKeysCursor<'K,'V when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>, mapK:Func<'K,'K>) =
//  inherit CursorBind<'K,'V,'V>(cursorFactory.Invoke)
//  // TODO this is wrong
//  // key is after MapK, the simplest way is to buffer
//  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<KVP<'K,'V>>): bool =
//    // add works on any value, so must use TryGetValue instead of MoveAt
//    let ok, value2 = this.InputCursor.TryGetValue(key)
//    if ok then
//      value <- KVP(mapK.Invoke(key), value2)
//      true
//    else false
//  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V>>) : bool =
//    value <- KVP(mapK.Invoke(next.Key), next.Value)
//    true
//
//  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V>>) : bool =
//    value <- KVP(mapK.Invoke(previous.Key), previous.Value)
//    true


type FilterValuesCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, filterFunc:Func<'V,bool>) =
  inherit CursorBind<'K,'V,'V>(cursorFactory)

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

// TODO this is probably wrong and untested 
type LagCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32) =
  inherit CursorBind<'K,'V,'V>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V>): bool =
    if isPositioned then
      laggedCursor <- this.InputCursor.Clone()
      let mutable cont = lag > 0u
      let mutable step = 0
      let mutable lagOk = lag = 0u
      while cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          step <- step + 1
        else
          cont <- false
        if step = int lag then 
          cont <- false
          lagOk <- true
      if lagOk then
        value <- laggedCursor.CurrentValue
        true
      else false
    else
      let c = this.InputCursor.Clone()
      if c.MoveAt(key, Lookup.EQ) then // lag is meaningful for exact match
        let mutable cont =  lag > 0u
        let mutable step = 0
        let mutable lagOk =  lag = 0u
        while cont do
          let moved = c.MovePrevious()
          if moved then
#if PRERELEASE
            Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
            step <- step + 1
          else
            cont <- false
          if step = int lag then 
            cont <- false
            lagOk <- true
        if lagOk then
          value <- c.CurrentValue
          true
        else false
      else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if laggedCursor.MoveNext() then
#if PRERELEASE
      Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
      value <- laggedCursor.CurrentValue
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if laggedCursor.MovePrevious() then
      value <- laggedCursor.CurrentValue
      true
    else false

  override this.Clone() = 
    let clone = new LagCursor<'K,'V>(cursorFactory, lag) :> ICursor<'K,'V>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

/// Apply lagMapFunc to current and lagged value
type ZipLagCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32, mapCurrentPrev:Func<'V,'V,'R>) =
  inherit CursorBind<'K,'V,'R>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'R>): bool =
    if isPositioned then
      laggedCursor <- this.InputCursor.Clone()
      let mutable cont = lag > 0u
      let mutable step = 0
      let mutable lagOk = lag = 0u
      while cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
#if PRERELEASE
          Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
          step <- step + 1
        else
          cont <- false
        if step = int lag then 
          cont <- false
          lagOk <- true
      if lagOk then
        value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
        true
      else false
    else
      let c = this.InputCursor.Clone()
      if c.MoveAt(key, Lookup.EQ) then
        let current = c.CurrentValue
        let mutable cont =  lag > 0u
        let mutable step = 0
        let mutable lagOk = lag = 0u
        while cont do
          let moved = c.MovePrevious()
          if moved then
#if PRERELEASE
            Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
            step <- step + 1
          else
            cont <- false
          if step = int lag then 
            cont <- false
            lagOk <- true
        if lagOk then
          value <- mapCurrentPrev.Invoke(current, c.CurrentValue)
          true
        else false
      else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if laggedCursor.MoveNext() then
#if PRERELEASE
      Trace.Assert(laggedCursor.Comparer.Compare(laggedCursor.CurrentKey,this.InputCursor.CurrentKey) <= 0 )
#endif
      value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'R>) : bool =
    if laggedCursor.MovePrevious() then
      value <- mapCurrentPrev.Invoke(this.InputCursor.CurrentValue, laggedCursor.CurrentValue)
      true
    else false

  override this.Clone() = 
    let clone = new ZipLagCursor<'K,'V, 'R>(cursorFactory, lag, mapCurrentPrev) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone



type AddIntCursor<'K>(cursorFactory:Func<ICursor<'K,int>>, addition:int) =
  inherit CursorBind<'K,int,int>(cursorFactory)

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<int>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- value2 + addition
      true
    else false

  override this.Clone() =
    let clone = new AddIntCursor<'K>(cursorFactory, addition) :> ICursor<'K,int>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

[<SealedAttribute>]
type AddInt64Cursor<'K>(cursorFactory:Func<ICursor<'K,int64>>, addition:int64) =
  inherit CursorBind<'K,int64,int64>(cursorFactory)

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<int64>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- value2 + addition
      true
    else false
  // Implementing this increase performance from 20mops to 35 mops
  // TODO map is very optimizable 
  override this.TryUpdateNext(next:KVP<'K,int64>, [<Out>] value: byref<int64>) : bool =
    value <- next.Value+ addition
    true

  override this.Clone() = 
    let clone = new AddInt64Cursor<'K>(cursorFactory, addition) :> ICursor<'K,int64>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone



/// Repack original types into value tuples. Due to the lazyness this only happens for a current value of cursor. ZipN keeps vArr instance and
/// rewrites its values. For value types we will always be in L1/stack, for reference types we do not care that much about performance.
type Zip2Cursor<'K,'V,'V2,'R>(cursorFactoryL:Func<ICursor<'K,'V>>,cursorFactoryR:Func<ICursor<'K,'V2>>, mapF:Func<'K,'V,'V2,'R>) =
  inherit ZipNCursor<'K,ValueTuple<'V,'V2>,'R>(
    Func<'K, ValueTuple<'V,'V2>[],'R>(fun (k:'K) (tArr:ValueTuple<'V,'V2>[]) -> mapF.Invoke(k, tArr.[0].Value1, tArr.[1].Value2)), 
    (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryL, Func<_,_>(fun (x:'V) -> ValueTuple<'V,'V2>(x, Unchecked.defaultof<'V2>)), OptionalValue.Missing) :> ICursor<'K,ValueTuple<'V,'V2>>), 
    (fun () -> new BatchMapValuesCursor<_,_,_>(cursorFactoryR, Func<_,_>(fun (x:'V2) -> ValueTuple<'V,'V2>(Unchecked.defaultof<'V>, x)), OptionalValue.Missing) :> ICursor<'K,ValueTuple<'V,'V2>>)
  )



type ScanCursor<'K,'V,'R>(cursorFactory:Func<ICursor<'K,'V>>, init:'R, folder:Func<'R,'K,'V,'R>) as this =
  inherit CursorBind<'K,'V,'R>(cursorFactory)
  do
    this.IsContinuous <- false // scan is explicitly not continuous

  let mutable moveAtCursor = cursorFactory.Invoke()
  let mutable buffered = false
  let mutable state : 'R = init
  let hasValues, first = if moveAtCursor.MoveFirst() then true, moveAtCursor.CurrentKey else false, Unchecked.defaultof<_>

  let mutable previousCursor = Unchecked.defaultof<ICursor<'K,'V>>

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

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'R>): bool =
    Trace.Assert(hasValues)
    // move first
    if not buffered && isPositioned && this.InputCursor.Comparer.Compare(first, key) = 0 then
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

  override this.Dispose() = 
    if previousCursor <> Unchecked.defaultof<ICursor<'K,'V>> then previousCursor.Dispose()
    base.Dispose()

  override this.Clone() = 
    let clone = new ScanCursor<'K,'V, 'R>(cursorFactory, init, folder) :> ICursor<'K,'R>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone


// TODO this is incomplete implementation. doesn't account for cases when step > width (quite valid case)
// TODO use Window vs Chunk like in Deedle, there is logical issue with overlapped windows - if we ask for a point x, it could be different then enumerated
// But the same is tru for chunk - probably we must create a uffer in one go
type WindowCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>, width:uint32, step:uint32, allowIncomplete:bool) =
  inherit CursorBind<'K,'V,Series<'K,'V>>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable moves = 0
  // distance from lagged cursor to current
  let mutable lagDistance = 0

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<Series<'K,'V>>): bool =
    let ok, activeLaggedCursor = 
      if isPositioned then
        laggedCursor <- this.InputCursor.Clone()
        true, laggedCursor
      else
        let c = this.InputCursor.Clone()
        if c.MoveAt(key, Lookup.LE) then
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
        let rangeCursor() = new RangeCursor<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
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
            lagDistance <- lag
            moves <- 0
            true
          else false
        else
          false
    else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<Series<'K,'V>>) : bool =
    if allowIncomplete then
      let moved = 
        if lagDistance < int width then
          lagDistance <- lagDistance + 1
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
    let clone = new WindowCursor<'K,'V>(cursorFactory, width, step, allowIncomplete) :> ICursor<'K,Series<'K,'V>>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

