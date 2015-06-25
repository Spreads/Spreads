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

/// Wrap IReadOnlyOrderedMap over ICursor
[<AllowNullLiteral>]
[<Serializable>]
type CursorSeries<'K,'V when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) =
  inherit Series<'K,'V>()
  override this.GetCursor() = cursorFactory()

// cursor factory is like a thunk for Vagabond, could exetute remotely?

//
// could do both map and filter
// need cursor factory and not a cursor to be able to clone, Series.GetCursor is the factory. NB! But Cursor.Clone() is also cursor factory
type internal CursorBind<'K,'V,'V2 when 'K : comparison>(cursor:ICursor<'K,'V>, valuesMapFunc:'K->'V->'V2, filterFunc:'K->'V->bool) =
  
  let mutable started = false

  member this.Current with get() : KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)
  member this.CurrentKey with get() : 'K = cursor.CurrentKey
  member this.CurrentValue with get() :'V2 = valuesMapFunc cursor.CurrentKey cursor.CurrentValue


  // TODO kind of loop fusion and what nessos does with streams, but to opposite direction
  // TODO test must show that this improves performance

//  member val IsIndexed = false with get, set //source.IsIndexed
//  /// By default, could move everywhere the source moves
//  member val IsContinuous = cursor.IsContinuous with get, set
////  abstract IsContinuous: bool with get
////  override this.IsContinuous with get() = c.IsContinuous
//  member val IsBatch = cursor.IsBatch with get, set
////  abstract IsBatch: bool with get
////  override this.IsBatch with get() = c.IsBatch
//
//  /// Source series
//  //member this.InputSource with get() = source
//  member this.InputCursor with get() = cursor
//
//  
//  //member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)
//
//  /// Stores current batch for a succesful batch move
//  //abstract CurrentBatch : IReadOnlyOrderedMap<'K,'V2> with get
//  member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>> with get, set
//
//  member this.Reset() = cursor.Reset()
//  member this.Dispose() = cursor.Dispose()
//
//  interface ICursor<'K,'V2> with
//
//    member x.Current: KVP<'K,'V2> = KVP(x.CurrentKey, x.CurrentValue)
//    member x.Current: obj = (x :> ICursor<'K,'V2>).Current  :> obj
//    member x.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = x.CurrentBatch
//    member x.CurrentKey: 'K = x.CurrentKey
//    member x.CurrentValue: 'V2 = x.CurrentValue
//    member x.Dispose(): unit = x.Dispose()
//    member x.IsContinuous: bool = x.IsContinuous
//    
//    member x.MoveAt(index: 'K, direction: Lookup): bool = 
//      if x.InputCursor.MoveAt(index, direction) then
//        let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//        if ok then
//          x.CurrentKey <- x.InputCursor.CurrentKey
//          x.CurrentValue <- value
//          true
//        else
//          match direction with
//          | Lookup.EQ -> false
//          | Lookup.GE | Lookup.GT ->
//            let mutable found = false
//            while x.InputCursor.MoveNext() && not found do
//              let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//              if ok then 
//                found <- true
//                x.CurrentKey <- x.InputCursor.CurrentKey
//                x.CurrentValue <- value
//            if found then 
//              true 
//            else false
//          | Lookup.LE | Lookup.LT ->
//            let mutable found = false
//            while x.InputCursor.MovePrevious() && not found do
//              let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//              if ok then
//                found <- true
//                x.CurrentKey <- x.InputCursor.CurrentKey
//                x.CurrentValue <- value
//            if found then 
//              true 
//            else false
//          | _ -> failwith "wrong lookup value"
//      else false
//      
//    
//    member x.MoveFirst(): bool = 
//      if x.InputCursor.MoveFirst() then
//        let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//        if ok then
//          x.CurrentKey <- x.InputCursor.CurrentKey
//          x.CurrentValue <- value
//          true
//        else
//          let found = ref false
//          while x.InputCursor.MoveNext() && not !found do
//            let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//            if ok then 
//              found := true
//              x.CurrentKey <- x.InputCursor.CurrentKey
//              x.CurrentValue <- value
//          if !found then 
//            true 
//          else false
//      else false
//    
//    member x.MoveLast(): bool = 
//      if x.InputCursor.MoveLast() then
//        let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//        if ok then
//          x.CurrentKey <- value.Key
//          x.CurrentValue <- value.Value
//          hasInitializedValue <- true
//          true
//        else
//          let found = ref false
//          while x.InputCursor.MovePrevious() && not !found do
//            let ok, value =filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//            if ok then
//              found := true
//              x.CurrentKey <- value.Key
//              x.CurrentValue <- value.Value
//          if !found then 
//            hasInitializedValue <- true
//            true 
//          else false
//      else false
//    
//    member x.MoveNext(): bool =
//      if started then
//        let mutable found = false
//        while x.InputCursor.MoveNext() && not found do
//          let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//          if ok then 
//            found <- true
//            x.CurrentKey <- x.InputCursor.CurrentKey
//            x.CurrentValue <- value
//        if found then 
//          true 
//        else false
//      else (x :> ICursor<'K,'V2>).MoveFirst()
//
//    member x.MovePrevious(): bool = 
//      if started then
//        let found = ref false
//        while x.InputCursor.MovePrevious() && not !found do
//          let ok, value = filterMapFunc.Invoke(x.InputCursor.CurrentKey,x.InputCursor.CurrentValue)
//          if ok then 
//            found := true
//            x.CurrentKey <- x.InputCursor.CurrentKey
//            x.CurrentValue <- value
//        if !found then 
//          true 
//        else false
//      else (x :> ICursor<'K,'V2>).MoveLast()
//    
//    member x.MoveNextAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    member x.MoveNextBatchAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    
//    member x.IsBatch with get() = x.IsBatch
//    member x.Reset(): unit = x.Reset()
//    member x.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>((x :> ICursor<'K,'V2>).Clone) :> ISeries<'K,'V2>
//    member x.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
//      let ok, value2 = x.TryGetValue(key)
//      value <- value2.Value
//      ok
//    
//    // TODO review
//    member x.Clone(): ICursor<'K,'V2> =
//      // run-time type of the instance
//      let ty = x.GetType()
//      let args = [|cursorFactory :> obj|]
//      let clone = CursorBind(cursor.Clone(), filterMapFunc) :?> ICursor<'K,'V2> 
//      if started then clone.MoveAt(x.CurrentKey, Lookup.EQ) |> ignore
//      //Debug.Assert(movedOk) // if current key is set then we could move to it
//      clone



[<AbstractClassAttribute>]
type CursorProjection<'K,'V,'V2 when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) =
  
  let cursor = cursorFactory()

  // TODO make public property, e.g. for random walk generator we must throw if we try to init more than one
  // this is true for all "vertical" transformations, they start from a certain key and depend on the starting value
  // safe to call TryUpdateNext/Previous
  let mutable hasInitializedValue = false

  // TODO? add key type for the most general case
  // check if key types are not equal, in that case check if new values are sorted. On first 
  // unsorted value change output to Indexed

  member val IsIndexed = false with get, set //source.IsIndexed
  /// By default, could move everywhere the source moves
  member val IsContinuous = cursor.IsContinuous with get, set
//  abstract IsContinuous: bool with get
//  override this.IsContinuous with get() = c.IsContinuous
  member val IsBatch = cursor.IsBatch with get, set
//  abstract IsBatch: bool with get
//  override this.IsBatch with get() = c.IsBatch

  /// Source series
  //member this.InputSource with get() = source
  member this.InputCursor with get() = cursor

  //abstract CurrentKey:'K with get
  //abstract CurrentValue:'V2 with get
  member val CurrentKey = Unchecked.defaultof<'K> with get, set
  member val CurrentValue = Unchecked.defaultof<'V2> with get, set
  member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

  /// Stores current batch for a succesful batch move
  //abstract CurrentBatch : IReadOnlyOrderedMap<'K,'V2> with get
  member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>> with get, set

  /// For every successful move of the inut coursor creates an output value. If direction is not EQ, continues moves to the direction 
  /// until the state is created
  abstract TryGetValue: key:'K * [<Out>] value: byref<KVP<'K,'V2>> -> bool // * direction: Lookup not needed here
  // this is the main method to transform input to output, other methods could be implemented via it


  /// Update state with a new value. Should be optimized for incremental update of the current state in custom implementations.
  abstract TryUpdateNext: next:KVP<'K,'V> * [<Out>] value: byref<KVP<'K,'V2>> -> bool
  override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
    // recreate value from scratch
    this.TryGetValue(next.Key, &value)

  /// Update state with a previous value. Should be optimized for incremental update of the current state in custom implementations.
  abstract TryUpdatePrevious: previous:KVP<'K,'V> * [<Out>] value: byref<KVP<'K,'V2>> -> bool
  override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
    // recreate value from scratch
    this.TryGetValue(previous.Key, &value)

  /// If input and this cursor support batches, then process a batch and store it in CurrentBatch
  abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>> -> bool  
  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>>) : bool =
    let map = SortedMap<'K,'V2>()
    let isFirst = ref true
    for kvp in nextBatch do
      if !isFirst then
        isFirst := false
        let ok, newKvp = this.TryGetValue(kvp.Key)
        if ok then map.AddLast(newKvp.Key, newKvp.Value)
      else
        let ok, newKvp = this.TryUpdateNext(kvp)
        if ok then map.AddLast(newKvp.Key, newKvp.Value)
    if map.size > 0 then 
      value <- map :> IReadOnlyOrderedMap<'K,'V2>
      true
    else false

  member this.Reset() = 
    hasInitializedValue <- false
    cursor.Reset()
  member this.Dispose() = 
    hasInitializedValue <- false
    cursor.Dispose()

  interface ICursor<'K,'V2> with

    member x.Current: KVP<'K,'V2> = KVP(x.CurrentKey, x.CurrentValue)
    member x.Current: obj = (x :> ICursor<'K,'V2>).Current  :> obj
    member x.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = x.CurrentBatch
    member x.CurrentKey: 'K = x.CurrentKey
    member x.CurrentValue: 'V2 = x.CurrentValue
    member x.Dispose(): unit = x.Dispose()
    member x.IsContinuous: bool = x.IsContinuous
    
    member x.MoveAt(index: 'K, direction: Lookup): bool = 
      if x.InputCursor.MoveAt(index, direction) then
        let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
        if ok then
          x.CurrentKey <- value.Key
          x.CurrentValue <- value.Value
          hasInitializedValue <- true
          true
        else
          match direction with
          | Lookup.EQ -> false
          | Lookup.GE | Lookup.GT ->
            let found = ref false
            while x.InputCursor.MoveNext() && not !found do
              let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
              if ok then 
                found := true
                x.CurrentKey <- value.Key
                x.CurrentValue <- value.Value
            if !found then 
              hasInitializedValue <- true
              true 
            else false
          | Lookup.LE | Lookup.LT ->
            let found = ref false
            while x.InputCursor.MovePrevious() && not !found do
              let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
              if ok then
                found := true
                x.CurrentKey <- value.Key
                x.CurrentValue <- value.Value
            if !found then 
              hasInitializedValue <- true
              true 
            else false
          | _ -> failwith "wrong lookup value"
      else false
      
    
    member x.MoveFirst(): bool = 
      if x.InputCursor.MoveFirst() then
        let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
        if ok then
          x.CurrentKey <- value.Key
          x.CurrentValue <- value.Value
          hasInitializedValue <- true
          true
        else
          let found = ref false
          while x.InputCursor.MoveNext() && not !found do
            let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
            if ok then 
              found := true
              x.CurrentKey <- value.Key
              x.CurrentValue <- value.Value
          if !found then 
            hasInitializedValue <- true
            true 
          else false
      else false
    
    member x.MoveLast(): bool = 
      if x.InputCursor.MoveLast() then
        let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
        if ok then
          x.CurrentKey <- value.Key
          x.CurrentValue <- value.Value
          hasInitializedValue <- true
          true
        else
          let found = ref false
          while x.InputCursor.MovePrevious() && not !found do
            let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
            if ok then
              found := true
              x.CurrentKey <- value.Key
              x.CurrentValue <- value.Value
          if !found then 
            hasInitializedValue <- true
            true 
          else false
      else false
    
    member x.MoveNext(): bool =
      if hasInitializedValue then
        let mutable found = false
        while x.InputCursor.MoveNext() && not found do
          let ok, value = x.TryUpdateNext(x.InputCursor.Current)
          if ok then 
            found <- true
            x.CurrentKey <- value.Key
            x.CurrentValue <- value.Value
        if found then 
          //hasInitializedValue <- true
          true 
        else false
      else (x :> ICursor<'K,'V2>).MoveFirst()

    member x.MovePrevious(): bool = 
      if hasInitializedValue then
        let found = ref false
        while x.InputCursor.MovePrevious() && not !found do
          let ok, value = x.TryUpdatePrevious(x.InputCursor.Current)
          if ok then 
            found := true
            x.CurrentKey <- value.Key
            x.CurrentValue <- value.Value
        if !found then 
          hasInitializedValue <- true
          true 
        else false
      else (x :> ICursor<'K,'V2>).MoveLast()
    
    member x.MoveNextAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    member x.MoveNextBatchAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    
    member x.IsBatch with get() = x.IsBatch
    member x.Reset(): unit = x.Reset()
    member x.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>((x :> ICursor<'K,'V2>).Clone) :> ISeries<'K,'V2>
    member x.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
      let ok, value2 = x.TryGetValue(key)
      value <- value2.Value
      ok
//      let ok, value = x.SourceCoursor.TryGetValue(key)
//      if ok then
//        let ok2, value2 = x.TryGetValue(KVP(key, value))
//        if ok2 then
//          x.CurrentKey <- value2.Key
//          x.CurrentValue <- value2.Value
//          true
//        else
//          false
//      else false
    
    // TODO review
    member x.Clone(): ICursor<'K,'V2> =
      // run-time type of the instance
      let ty = x.GetType()
      let args = [|cursorFactory :> obj|]
      let clone = Activator.CreateInstance(ty, args) :?> ICursor<'K,'V2> // should not be called too often
      if hasInitializedValue then clone.MoveAt(x.CurrentKey, Lookup.EQ) |> ignore
      //Debug.Assert(movedOk) // if current key is set then we could move to it
      clone


/// Repeat previous value for all missing keys
type RepeatCursor<'K,'V  when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) as this =
  inherit CursorProjection<'K,'V,'V>(cursorFactory)
  do
    this.IsContinuous <- true  

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,'V>>): bool =
    // naive implementation, easy optimizable 
    if this.InputCursor.MoveAt(key, Lookup.LE) then
      value <- this.InputCursor.Current
      true
    else false
      

type AddIntCursor<'K when 'K : comparison>(cursorFactory:unit->ICursor<'K,int>, addition:int) =
  inherit CursorProjection<'K,int,int>(cursorFactory)

  override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,int>>): bool =
    // add works on any value, so must use TryGetValue instead of MoveAt
    let ok, value2 = this.InputCursor.TryGetValue(key)
    if ok then
      value <- KVP(key, value2 + addition)
      true
    else false

[<SealedAttribute>]
type AddInt64Cursor<'K when 'K : comparison>(cursorFactory:unit->ICursor<'K,int64>, addition:int64) =
  inherit CursorProjection<'K,int64,int64>(cursorFactory)

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
  inherit CursorProjection<'K,int64,double>(cursorFactory)

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