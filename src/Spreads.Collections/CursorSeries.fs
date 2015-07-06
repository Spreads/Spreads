namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading.Tasks

open Spreads
//open Spreads.Collections



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
type RepeatCursor<'K,'V  when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) as this =
  inherit CursorBind<'K,'V,'V>(cursorFactory)
  do
    this.IsContinuous <- true  

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,'V>>): bool =
    // naive implementation, easy optimizable 
    if this.InputCursor.MoveAt(key, Lookup.LE) then
      value <- this.InputCursor.Current
      true
    else false
      

type MapValuesCursor<'K,'V,'V2 when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>, mapF:'V -> 'V2) =
  inherit CursorBind<'K,'V,'V2>(cursorFactory)

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,'V2>>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- KVP(key, mapF(value2))
      true
    else false
  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
    value <- KVP(next.Key, mapF(next.Value))
    true

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
    value <- KVP(previous.Key, mapF(previous.Value))
    true


type FilterValuesCursor<'K,'V when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>, filterFunc:'V -> bool) =
  inherit CursorBind<'K,'V,'V>(cursorFactory)

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,'V>>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok && filterFunc value2 then
      value <- KVP(key, value2)
      true
    else false
  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V>>) : bool =
    if filterFunc next.Value then
      value <- KVP(next.Key, next.Value)
      true
    else false

  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V>>) : bool =
    if filterFunc previous.Value then
      value <- KVP(previous.Key, previous.Value)
      true
    else false

type AddIntCursor<'K when 'K : comparison>(cursorFactory:unit->ICursor<'K,int>, addition:int) =
  inherit CursorBind<'K,int,int>(cursorFactory)

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,int>>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- KVP(key, value2 + addition)
      true
    else false


[<SealedAttribute>]
type AddInt64Cursor<'K when 'K : comparison>(cursorFactory:unit->ICursor<'K,int64>, addition:int64) =
  inherit CursorBind<'K,int64,int64>(cursorFactory)

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,int64>>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- KVP(key, value2 + addition)
      true
    else false
  // Implementing this increase performance from 20mops to 35 mops
  // TODO map is very optimizable 
  override this.TryUpdateNext(next:KVP<'K,int64>, [<Out>] value: byref<KVP<'K,int64>>) : bool =
    value <- KVP(next.Key, next.Value+ addition)
    true


/// Repeat previous value for all missing keys
type LogCursor<'K when 'K : comparison>(cursorFactory:unit->ICursor<'K,int64>) =
  inherit CursorBind<'K,int64,double>(cursorFactory)

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,double>>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- KVP(key, Math.Exp(Math.Log(Math.Exp(Math.Log(double value2)))))
      true
    else false



[<Extension>]
type SeriesExtensions () =
    /// Wraps any series into CursorSeries that implements only the IReadOnlyOrderedMap interface
    [<Extension>]
    static member inline ReadOnly(source: Series<'K,'V>) : Series<'K,'V> = 
      CursorSeries(fun _ -> source.GetCursor()) :> Series<'K,'V>

    /// TODO check if input cursor is MapValuesCursor or FilterValuesCursor cursor and repack them into
    /// a single mapFilter cursor with nested funcs. !!! Check if this gives any per gain !!! 
    [<Extension>]
    static member inline Map(source: Series<'K,'V>, mapFunc:Func<'V,'V2>) : Series<'K,'V2> =
      CursorSeries(fun _ -> new MapValuesCursor<'K,'V,'V2>(source.GetCursor, mapFunc.Invoke) :> ICursor<'K,'V2>) :> Series<'K,'V2>

    [<Extension>]
    static member inline Filter(source: Series<'K,'V>, filterFunc:'V->bool) : Series<'K,'V> = 
      CursorSeries(fun _ -> new FilterValuesCursor<'K,'V>(source.GetCursor, filterFunc) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Repeat(source: Series<'K,'V>) : Series<'K,'V> = 
      CursorSeries(fun _ -> new RepeatCursor<'K,'V>(source.GetCursor) :> ICursor<'K,'V>) :> Series<'K,'V>

    [<Extension>]
    static member inline Add(source: Series<'K,int>, addition:int) : Series<'K,int> = 
      CursorSeries(fun _ -> new AddIntCursor<'K>(source.GetCursor,addition) :> ICursor<'K,int>) :> Series<'K,int>

    [<Extension>]
    static member inline Add(source: Series<'K,int64>, addition:int64) : Series<'K,int64> = 
      CursorSeries(fun _ -> new AddInt64Cursor<'K>(source.GetCursor,addition) :> ICursor<'K,int64>) :> Series<'K,int64>
    [<Extension>]
    static member inline Log(source: Series<'K,int64>) : Series<'K,double> = 
      CursorSeries(fun _ -> new LogCursor<'K>(source.GetCursor) :> ICursor<'K,double>) :> Series<'K,double>



// TODO generators