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
// Our benchmark confirms that the slowdown of .Repeat(), .ReadOnly(), .Map(...) and .Filter(...) is quite small 

//
//[<AbstractClassAttribute>]
//type CursorBind<'K,'V,'V2 when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) =
//  
//  let cursor = cursorFactory()
//
//  // TODO make public property, e.g. for random walk generator we must throw if we try to init more than one
//  // this is true for all "vertical" transformations, they start from a certain key and depend on the starting value
//  // safe to call TryUpdateNext/Previous
//  let mutable hasValidState = false
//  member this.HasValidState with get() = hasValidState and set (v) = hasValidState <- v
//
//  // TODO? add key type for the most general case
//  // check if key types are not equal, in that case check if new values are sorted. On first 
//  // unsorted value change output to Indexed
//
//  //member val IsIndexed = false with get, set //source.IsIndexed
//  /// By default, could move everywhere the source moves
//  member val IsContinuous = cursor.IsContinuous with get, set
//
//  /// Source series
//  //member this.InputSource with get() = source
//  member this.InputCursor with get() = cursor
//
//  //abstract CurrentKey:'K with get
//  //abstract CurrentValue:'V2 with get
//  member val CurrentKey = Unchecked.defaultof<'K> with get, set
//  member val CurrentValue = Unchecked.defaultof<'V2> with get, set
//  member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)
//
//  /// Stores current batch for a succesful batch move
//  //abstract CurrentBatch : IReadOnlyOrderedMap<'K,'V2> with get
//  member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>> with get, set
//
//  /// For every successful move of the inut coursor creates an output value. If direction is not EQ, continues moves to the direction 
//  /// until the state is created
//  abstract TryGetValue: key:'K * [<Out>] value: byref<KVP<'K,'V2>> -> bool // * direction: Lookup not needed here
//  // this is the main method to transform input to output, other methods could be implemented via it
//
//
//  /// Update state with a new value. Should be optimized for incremental update of the current state in custom implementations.
//  abstract TryUpdateNext: next:KVP<'K,'V> * [<Out>] value: byref<KVP<'K,'V2>> -> bool
//  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
//    // recreate value from scratch
//    this.TryGetValue(next.Key, &value)
//
//  /// Update state with a previous value. Should be optimized for incremental update of the current state in custom implementations.
//  abstract TryUpdatePrevious: previous:KVP<'K,'V> * [<Out>] value: byref<KVP<'K,'V2>> -> bool
//  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
//    // recreate value from scratch
//    this.TryGetValue(previous.Key, &value)
//
//  /// If input and this cursor support batches, then process a batch and store it in CurrentBatch
//  abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>> -> bool  
//  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>>) : bool =
//    failwith "not implemented"
////    let map = SortedMap<'K,'V2>()
////    let isFirst = ref true
////    for kvp in nextBatch do
////      if !isFirst then
////        isFirst := false
////        let ok, newKvp = this.TryGetValue(kvp.Key)
////        if ok then map.AddLast(newKvp.Key, newKvp.Value)
////      else
////        let ok, newKvp = this.TryUpdateNext(kvp)
////        if ok then map.AddLast(newKvp.Key, newKvp.Value)
////    if map.size > 0 then 
////      value <- map :> IReadOnlyOrderedMap<'K,'V2>
////      true
////    else false
//
//  member this.Reset() = 
//    hasValidState <- false
//    cursor.Reset()
//  member this.Dispose() = 
//    hasValidState <- false
//    cursor.Dispose()
//
//  interface IEnumerator<KVP<'K,'V2>> with    
//    member this.Reset() = this.Reset()
//    member x.MoveNext(): bool =
//      if hasValidState then
//        let mutable found = false
//        while not found && x.InputCursor.MoveNext() do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
//          let ok, value = x.TryUpdateNext(x.InputCursor.Current)
//          if ok then 
//            found <- true
//            x.CurrentKey <- value.Key
//            x.CurrentValue <- value.Value
//        if found then 
//          //hasInitializedValue <- true
//          true 
//        else false
//      else (x :> ICursor<'K,'V2>).MoveFirst()
//    member this.Current with get(): KVP<'K, 'V2> = this.Current
//    member this.Current with get(): obj = this.Current :> obj 
//    member x.Dispose(): unit = x.Dispose()
//
//  interface ICursor<'K,'V2> with
//    member x.Current: KVP<'K,'V2> = KVP(x.CurrentKey, x.CurrentValue)
//    member x.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = x.CurrentBatch
//    member x.CurrentKey: 'K = x.CurrentKey
//    member x.CurrentValue: 'V2 = x.CurrentValue
//    member x.IsContinuous: bool = x.IsContinuous
//    member x.MoveAt(index: 'K, direction: Lookup): bool = 
//      if x.InputCursor.MoveAt(index, direction) then
//        let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
//        if ok then
//          x.CurrentKey <- value.Key
//          x.CurrentValue <- value.Value
//          hasValidState <- true
//          true
//        else
//          match direction with
//          | Lookup.EQ -> false
//          | Lookup.GE | Lookup.GT ->
//            let mutable found = false
//            while not found && x.InputCursor.MoveNext() do
//              let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
//              if ok then 
//                found <- true
//                x.CurrentKey <- value.Key
//                x.CurrentValue <- value.Value
//            if found then 
//              hasValidState <- true
//              true 
//            else false
//          | Lookup.LE | Lookup.LT ->
//            let mutable found = false
//            while not found && x.InputCursor.MovePrevious() do
//              let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
//              if ok then
//                found <- true
//                x.CurrentKey <- value.Key
//                x.CurrentValue <- value.Value
//            if found then 
//              hasValidState <- true
//              true 
//            else false
//          | _ -> failwith "wrong lookup value"
//      else false
//      
//    
//    member x.MoveFirst(): bool = 
//      if x.InputCursor.MoveFirst() then
//        let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
//        if ok then
//          x.CurrentKey <- value.Key
//          x.CurrentValue <- value.Value
//          hasValidState <- true
//          true
//        else
//          let mutable found = false
//          while not found && x.InputCursor.MoveNext() do
//            let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
//            if ok then 
//              found <- true
//              x.CurrentKey <- value.Key
//              x.CurrentValue <- value.Value
//          if found then 
//            hasValidState <- true
//            true 
//          else false
//      else false
//    
//    member x.MoveLast(): bool = 
//      if x.InputCursor.MoveLast() then
//        let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
//        if ok then
//          x.CurrentKey <- value.Key
//          x.CurrentValue <- value.Value
//          hasValidState <- true
//          true
//        else
//          let mutable found = false
//          while not found && x.InputCursor.MovePrevious() do
//            let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
//            if ok then
//              found <- true
//              x.CurrentKey <- value.Key
//              x.CurrentValue <- value.Value
//          if found then 
//            hasValidState <- true
//            true 
//          else false
//      else false
//
//    member x.MovePrevious(): bool = 
//      if hasValidState then
//        let mutable found = false
//        while not found && x.InputCursor.MovePrevious() do
//          let ok, value = x.TryUpdatePrevious(x.InputCursor.Current)
//          if ok then 
//            found <- true
//            x.CurrentKey <- value.Key
//            x.CurrentValue <- value.Value
//        if found then 
//          hasValidState <- true
//          true 
//        else false
//      else (x :> ICursor<'K,'V2>).MoveLast()
//    
//    member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    member x.MoveNextBatchAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    
//    //member x.IsBatch with get() = x.IsBatch
//    member x.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>((x :> ICursor<'K,'V2>).Clone) :> ISeries<'K,'V2>
//    member x.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
//      let ok, v = x.TryGetValue(key)
//      value <- v.Value
//      ok
//    
//    // TODO review + profile. for value types we could just return this
//    member x.Clone(): ICursor<'K,'V2> =
//      // run-time type of the instance, could be derived type
//      let ty = x.GetType()
//      let args = [|cursorFactory :> obj|]
//      // TODO using Activator is a very bad sign, are we doing something wrong here?
//      let clone = Activator.CreateInstance(ty, args) :?> ICursor<'K,'V2> // should not be called too often
//      if hasValidState then clone.MoveAt(x.CurrentKey, Lookup.EQ) |> ignore
//      //Debug.Assert(movedOk) // if current key is set then we could move to it
//      clone
//
//

/// Repeat previous value for all missing keys
type RepeatCursor<'K,'V  when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>) as this =
  inherit CursorBind<'K,'V,'V>(cursorFactory)
  do
    this.IsContinuous <- true
  let mutable previousCursor = Unchecked.defaultof<ICursor<'K,'V>>

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
        let previousPositioned = 
          if previousCursor = Unchecked.defaultof<ICursor<'K,'V>> then 
            previousCursor <- this.InputCursor.Clone()
            previousCursor.MovePrevious()
          else previousCursor.MoveAt(this.InputCursor.CurrentKey, Lookup.LT)
        if previousPositioned then
          value <- previousCursor.CurrentValue
          true
        else
          false
      else // c > 0 or cursor not started
        // naive implementation, CursorZip must hit c < 0 case (TODO check this)
        let c = this.InputCursor.Clone()
        if c.MoveAt(key, Lookup.LE) then
          Debug.Assert(c.CurrentKey <> this.InputCursor.CurrentKey)
          value <- c.CurrentValue
          true
        else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    value <- next.Value
    true

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    value <- previous.Value
    true

  override this.Dispose() = 
    if previousCursor <> Unchecked.defaultof<ICursor<'K,'V>> then previousCursor.Dispose()
    base.Dispose()


type FillCursor<'K,'V  when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>, fillValue:'V) as this =
  inherit CursorBind<'K,'V,'V>(cursorFactory)
  do
    this.IsContinuous <- true  
  // TODO optimize as Repeat
  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V>): bool =
    if isPositioned then
      value <- this.InputCursor.CurrentValue
      true
    else
      value <- fillValue
      true
      

type MapValuesCursor<'K,'V,'V2 when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>, mapF:Func<'V,'V2>) =
  inherit CursorBind<'K,'V,'V2>(cursorFactory)

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V2>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- mapF.Invoke(value2)
      true
    else false
  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
    value <- mapF.Invoke(next.Value)
    true

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
    value <- mapF.Invoke(previous.Value)
    true

// this is not possible with cursor, we won't be able 
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


type FilterValuesCursor<'K,'V when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>, filterFunc:Func<'V,bool>) =
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


// TODO this is probably wrong and untested 
type LagCursor<'K,'V when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>, lag:uint32) =
  inherit CursorBind<'K,'V,'V>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<'V>): bool =
    if isPositioned then
      laggedCursor <- this.InputCursor.Clone()
      let mutable cont = true
      let mutable step = 0
      let mutable lagOk = false
      while cont do
        let moved = laggedCursor.MovePrevious()
        if moved then
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
      if c.MoveAt(key, Lookup.LE) then
        let mutable cont = true
        let mutable step = 0
        let mutable lagOk = false
        while cont do
          let moved = c.MovePrevious()
          if moved then
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
      value <- laggedCursor.CurrentValue
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V>) : bool =
    if laggedCursor.MovePrevious() then
      value <- laggedCursor.CurrentValue
      true
    else false

type AddIntCursor<'K when 'K : comparison>(cursorFactory:Func<ICursor<'K,int>>, addition:int) =
  inherit CursorBind<'K,int,int>(cursorFactory)

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<int>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- value2 + addition
      true
    else false


[<SealedAttribute>]
type AddInt64Cursor<'K when 'K : comparison>(cursorFactory:Func<ICursor<'K,int64>>, addition:int64) =
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


/// Repeat previous value for all missing keys
type LogCursor<'K when 'K : comparison>(cursorFactory:Func<ICursor<'K,int64>>) =
  inherit CursorBind<'K,int64,double>(cursorFactory)

  override this.TryGetValue(key:'K, isPositioned:bool, [<Out>] value: byref<double>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- Math.Exp(Math.Log(Math.Exp(Math.Log(double value2))))
      true
    else false




type ZipValuesCursor<'K,'V,'V2,'R when 'K : comparison>(cursorFactoryL:Func<ICursor<'K,'V>>,cursorFactoryR:Func<ICursor<'K,'V2>>, mapF:Func<'V,'V2,'R>) =
  inherit CursorZip<'K,'V,'V2,'R>(cursorFactoryL.Invoke,cursorFactoryR.Invoke)

  override this.TryZip(key:'K, v, v2, [<Out>] value: byref<'R>): bool =
    value <- mapF.Invoke(v,v2)
    true





type ScanCursor<'K,'V,'R  when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>, init:'R, folder:Func<'R,'K,'V,'R>) as this =
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
    Debug.Assert(hasValues)
    // move first
    if not buffered && isPositioned && this.InputCursor.Comparer.Compare(first, key) = 0 then
      Debug.Assert(Unchecked.equals state init)
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

type WindowCursor<'K,'V when 'K : comparison>(cursorFactory:Func<ICursor<'K,'V>>, width:uint32, step:uint32) =
  inherit CursorBind<'K,'V,Series<'K,'V>>(cursorFactory)
  let mutable laggedCursor = Unchecked.defaultof<ICursor<'K,'V>>
  let mutable moves = 0

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
      let mutable cont = true
      let mutable lag = 1 // NB! current value counts toward the width, lag = 1
      //lag <- 0
      let mutable lagOk = false
      while cont do
        let moved = activeLaggedCursor.MovePrevious()
        if moved then
          lag <- lag + 1
        else
          cont <- false
        if lag = int width then 
          cont <- false
          lagOk <- true
      if lagOk then
        // NB! freeze bounds for the range cursor
        let startPoint = Some(activeLaggedCursor.CurrentKey)
        let endPoint = Some(this.InputCursor.CurrentKey)
        let rangeCursor() = new CursorRange<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
        let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
        value <- window
        moves <- 0
        true
      else false
    else false

  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<Series<'K,'V>>) : bool =
    if laggedCursor.MoveNext() then
      moves <- moves + 1
      if Math.Abs(moves) = int step then
        // NB! freeze bounds for the range cursor
        let startPoint = Some(laggedCursor.CurrentKey)
        let endPoint = Some(this.InputCursor.CurrentKey)
        let rangeCursor() = new CursorRange<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
        
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
        let rangeCursor() = new CursorRange<'K,'V>(Func<ICursor<'K,'V>>(cursorFactory.Invoke), startPoint, endPoint, None, None) :> ICursor<'K,'V>
        let window = CursorSeries(Func<ICursor<'K,'V>>(rangeCursor)) :> Series<'K,'V>
        value <- window
        moves <- 0
        true
      else false
    else false




// TODO extensions on ISeries but return series, to keep operators working

// TODO check performance impact of Func instead of FSharpFunc

[<Extension>]
type SeriesExtensions () =
    /// Wraps any series into CursorSeries that implements only the IReadOnlyOrderedMap interface
    [<Extension>]
    static member inline ReadOnly(source: ISeries<'K,'V>) : Series<'K,'V> = 
      CursorSeries(fun _ -> source.GetCursor()) :> Series<'K,'V>

    /// TODO check if input cursor is MapValuesCursor or FilterValuesCursor cursor and repack them into
    /// a single mapFilter cursor with nested funcs. !!! Check if this gives any per gain !!! 
    [<Extension>]
    static member inline Map(source: ISeries<'K,'V>, mapFunc:Func<'V,'V2>) : Series<'K,'V2> =
      CursorSeries(fun _ -> new MapValuesCursor<'K,'V,'V2>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> ICursor<'K,'V2>) :> Series<'K,'V2>

//    [<Extension>]
//    static member inline Map(source: Series<'K,'V>, mapFunc:Func<'K,'K>) : Series<'K,'V> =
//      CursorSeries(fun _ -> new MapKeysCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), mapFunc) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Zip(source: ISeries<'K,'V>, other: ISeries<'K,'V2>, mapFunc:Func<'V,'V2,'R>) : Series<'K,'R> =
      CursorSeries(fun _ -> new ZipValuesCursor<'K,'V,'V2,'R>(Func<ICursor<'K,'V>>(source.GetCursor), Func<ICursor<'K,'V2>>(other.GetCursor), mapFunc) :> ICursor<'K,'R>) :> Series<'K,'R>

    [<Extension>]
    static member inline Filter(source: ISeries<'K,'V>, filterFunc:Func<'V,bool>) : Series<'K,'V> = 
      CursorSeries(fun _ -> new FilterValuesCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), filterFunc) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Repeat(source: ISeries<'K,'V>) : Series<'K,'V> = 
      CursorSeries(fun _ -> new RepeatCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor)) :> ICursor<'K,'V>) :> Series<'K,'V>

    /// Fill missing values with the given value
    [<Extension>]
    static member inline Fill(source: ISeries<'K,'V>, fillValue:'V) : Series<'K,'V> = 
      CursorSeries(fun _ -> new FillCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), fillValue) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Lag(source: ISeries<'K,'V>, lag:uint32) : Series<'K,'V> = 
      CursorSeries(fun _ -> new LagCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), lag) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Window(source: ISeries<'K,'V>, width:uint32, step:uint32) : Series<'K,Series<'K,'V>> = 
      CursorSeries(fun _ -> new WindowCursor<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), width, step) :> ICursor<'K,Series<'K,'V>>) :> Series<'K,Series<'K,'V>>

    [<Extension>]
    static member inline Add(source: ISeries<'K,int>, addition:int) : Series<'K,int> = 
      CursorSeries(fun _ -> new AddIntCursor<'K>(Func<ICursor<'K,int>>(source.GetCursor),addition) :> ICursor<'K,int>) :> Series<'K,int>

    [<Extension>]
    static member inline Add(source: ISeries<'K,int64>, addition:int64) : Series<'K,int64> = 
      CursorSeries(fun _ -> new AddInt64Cursor<'K>(Func<ICursor<'K,int64>>(source.GetCursor),addition) :> ICursor<'K,int64>) :> Series<'K,int64>

    [<Extension>]
    static member inline Log(source: ISeries<'K,int64>) : Series<'K,double> = 
      CursorSeries(fun _ -> new LogCursor<'K>(Func<ICursor<'K,int64>>(source.GetCursor)) :> ICursor<'K,double>) :> Series<'K,double>

    /// Enumerates the source into SortedMap<'K,'V> as Series<'K,'V>. Similar to LINQ ToArray/ToList methods.
    [<Extension>]
    [<Obsolete("Use `ToSortedMap` method instead")>]
    static member inline Evaluate(source: ISeries<'K,'V>) : SortedMap<'K,'V> =
      let sm = SortedMap()
      for kvp in source do
        sm.AddLast(kvp.Key, kvp.Value)
      sm

    /// Enumerates the source into SortedMap<'K,'V> as Series<'K,'V>. Similar to LINQ ToArray/ToList methods.
    [<Extension>]
    static member inline ToSortedMap(source: ISeries<'K,'V>) : SortedMap<'K,'V> =
      let sm = SortedMap()
      for kvp in source do
        sm.AddLast(kvp.Key, kvp.Value)
      sm

    [<Extension>]
    static member inline Fold(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'K,'V,'R>) : 'R = 
      let mutable state = init
      for kvp in source do
        state <- folder.Invoke(state, kvp.Key, kvp.Value)
      state

    [<Extension>]
    static member inline Fold(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'V,'R>) : 'R = 
      let mutable state = init
      for kvp in source do
        state <- folder.Invoke(state, kvp.Value)
      state

    [<Extension>]
    static member inline Fold(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'K,'R>) : 'R = 
      let mutable state = init
      for kvp in source do
        state <- folder.Invoke(state, kvp.Key)
      state

    [<Extension>]
    static member inline Scan(source: ISeries<'K,'V>, init:'R, folder:Func<'R,'K,'V,'R>) : Series<'K,'R> = 
      CursorSeries(fun _ -> new ScanCursor<'K,'V,'R>(Func<ICursor<'K,'V>>(source.GetCursor), init, folder) :> ICursor<'K,'R>) :> Series<'K,'R>
      
    [<Extension>]
    static member inline Range(source: ISeries<'K,'V>, startKey:'K, endKey:'K) : Series<'K,'V> = 
      CursorSeries(fun _ -> new CursorRange<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), Some(startKey), Some(endKey), None, None) :> ICursor<'K,'V>) :> Series<'K,'V>
      
    [<Extension>]
    static member inline After(source: ISeries<'K,'V>, startKey:'K, lookup:Lookup) : Series<'K,'V> = 
      CursorSeries(fun _ -> new CursorRange<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), Some(startKey), None, Some(lookup), None) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Before(source: ISeries<'K,'V>, endKey:'K, lookup:Lookup) : Series<'K,'V> = 
      CursorSeries(fun _ -> new CursorRange<'K,'V>(Func<ICursor<'K,'V>>(source.GetCursor), None, Some(endKey), None, Some(Lookup.GT)) :> ICursor<'K,'V>) :> Series<'K,'V>
      

// TODO generators