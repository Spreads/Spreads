namespace Spreads

// That was a good idea in theory, but using Func<> mapper makes it much slower than SimpleBindCursor. Map applied to SimpleBindCursor is as fast and simpler.
// Return to EvaluateState as a method, make it virtual and try to use object expression for ICanMapValues:

//// TODO we could make this class virtual and implement ICanMapValue interface via object expressions and virtual method
//// See Script1.fsx RootVirtual example. Func in never inlined, interesting to check if object expression could help.

// ...........................................................................................................................


//open System
//open System.Linq
//open System.Collections
//open System.Collections.Generic
//open System.Diagnostics
//open System.Runtime.InteropServices
//open System.Runtime.CompilerServices
//open System.Threading.Tasks
//
//open Spreads
//open Spreads.Collections
//
//
//// TODO All cursors that are intended to be used with CursorSeries should just inherit Series
//

//
//
///// A cursor that could perform map, filter, fold, scan or any other projection operations on input cursors.
//[<AbstractClassAttribute>]
//type BindCursor<'K,'V,'State,'R>(cursorFactory:Func<ICursor<'K,'V>>, stateMapper:Func<'State,'R>) =
//  //inherit Series<'K,'R>()
//  let cursor = cursorFactory.Invoke()
//
//  [<DefaultValueAttribute>]
//  val mutable internal stateMapper :Func<'State,'R>
//
//  let mutable hasValidState = false
//  let mutable state = Unchecked.defaultof<'State>
//
//  /// True after any successful move and when CurrentKey is defined
//  member this.HasValidState with get() = hasValidState
//  //member val IsIndexed = false with get, set //source.IsIndexed
//  
//  abstract IsContinuous : bool with get
//
//  member this.InputCursor with get() : ICursor<'K,'V> = cursor
//    
//  member val CurrentKey = Unchecked.defaultof<'K> with get, set
//  member this.CurrentValue 
//    with get() =
//      if hasValidState then this.EvaluateState state
//      else Unchecked.defaultof<'R>
//
//  member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)
//  /// Stores current batch for a succesful batch move
//  //abstract CurrentBatch : IReadOnlyOrderedMap<'K,'V2> with get
//  member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>> with get, set
//
//  member this.State with get() = state
//
//  /// Map state to value at current InputCursor position
//  member this.EvaluateState(state) = stateMapper.Invoke(state)
//
//  /// Creates new state at a key.
//  abstract TryCreateState: key:'K * [<Out>] state: byref<'State> -> bool
//  /// Updates state with next value of input
//  abstract TryUpdateStateNext: next:KVP<'K,'V> * value: byref<'State> -> bool
//  /// Updates state with next value of input
//  //abstract TryUpdateStateNextAsync: next:KVP<'K,'V> * value: byref<'State> -> Task<bool>
//  /// Updates state with previous value of input
//  abstract TryUpdateStatePrevious: next:KVP<'K,'V> * value: byref<'State> -> bool
//  override this.TryUpdateStatePrevious(next, [<Out>] value) = this.TryCreateState(next.Key, &value)
//
//  abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'R>> -> bool  
//  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'R>>) : bool =
////      let mutable batchState = Unchecked.defaultof<'State>
////      use bc = nextBatch.GetCursor()
////      if bc.MoveFirst() && this.TryCreateState(bc.CurrentKey, &batchState) then
////        // TODO (peft) check if nextBatch is sorted map to set size and if it is immutable to reuse keys
////        let sm = SortedMap()
////        sm.
////      else 
//      // TODO this could be done lazily via fold, if we could move bind below fold
//      false
//
//  abstract TryGetValue: key:'K * [<Out>] value: byref<'R> -> bool 
////  override this.TryGetValue(key, [<Out>] value: byref<'V2>) : bool = 
////    if hasValidState && this.InputCursor.Comparer.Compare(key, this.InputCursor.CurrentKey) = 0 then
////      value <- this.EvaluateState(state)
////      true
////    else
////      let ok, state' = this.TryCreateState(key)
////      if ok then
////        value <- this.EvaluateState(state')
////        true
////      else false
//
//  abstract Clone: unit -> ICursor<'K,'R>
//
//  /// Usually implemented by constructing self with a new combined function because we cannot create a derived class from base class
//  //abstract Map<'V3> : mapper:Func<'V2,'V3> -> Series<'K,'V3>
//
//  member this.Reset() = 
//    hasValidState <- false
//    cursor.Reset()
//  abstract Dispose: unit -> unit
//  override this.Dispose() = 
//    hasValidState <- false
//    cursor.Dispose()
//
//
//
//  member this.MoveNext(): bool =
//      if hasValidState then
//        let mutable found = false
//        while not found && this.InputCursor.MoveNext() do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
//#if PRERELEASE
//          let before = this.InputCursor.CurrentKey
//          let ok = this.TryUpdateStateNext(this.InputCursor.Current, &state)
//          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//          let ok = this.TryUpdateStateNext(this.InputCursor.Current, &state)
//#endif
//          if ok then
//            found <- true
//            this.CurrentKey <- this.InputCursor.CurrentKey
//            //this.CurrentValue <- this.EvaluateState(state)
//        if found then 
//          //hasInitializedValue <- true
//          true 
//        else false
//      else this.MoveFirst()
//
//  member this.MoveAt(index: 'K, direction: Lookup): bool = 
//    if this.InputCursor.MoveAt(index, direction) then
//#if PRERELEASE
//      let before = this.InputCursor.CurrentKey
//      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//      if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//#endif
//      if ok then
//        this.CurrentKey <- this.InputCursor.CurrentKey
//        //this.CurrentValue <- this.EvaluateState(state)
//        hasValidState <- true
//        true
//      else
//        match direction with
//        | Lookup.EQ -> false
//        | Lookup.GE | Lookup.GT ->
//          let mutable found = false
//          while not found && this.InputCursor.MoveNext() do
//#if PRERELEASE
//            let before = this.InputCursor.CurrentKey
//            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//#endif
//            if ok then
//              found <- true
//              this.CurrentKey <- this.InputCursor.CurrentKey
//              //this.CurrentValue <- this.EvaluateState(state)
//          if found then 
//            hasValidState <- true
//            true
//          else false
//        | Lookup.LE | Lookup.LT ->
//          let mutable found = false
//          while not found && this.InputCursor.MovePrevious() do
//#if PRERELEASE
//            let before = this.InputCursor.CurrentKey
//            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//#endif
//            if ok then
//              found <- true
//              this.CurrentKey <- this.InputCursor.CurrentKey
//              //this.CurrentValue <- this.EvaluateState(state)
//          if found then 
//            hasValidState <- true
//            true 
//          else false
//        | _ -> failwith "wrong lookup value"
//    else false
//      
//    
//  member this.MoveFirst(): bool = 
//    if this.InputCursor.MoveFirst() then
//#if PRERELEASE
//      let before = this.InputCursor.CurrentKey
//      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//      if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//#endif
//      if ok then
//        this.CurrentKey <- this.InputCursor.CurrentKey
//        //this.CurrentValue <- this.EvaluateState(state)
//        hasValidState <- true
//        true
//      else
//        let mutable found = false
//        while not found && this.InputCursor.MoveNext() do
//#if PRERELEASE
//          let before = this.InputCursor.CurrentKey
//          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//#endif
//          if ok then 
//            found <- true
//            this.CurrentKey <- this.InputCursor.CurrentKey
//            //this.CurrentValue <- this.EvaluateState(state)
//        if found then 
//          hasValidState <- true
//          true 
//        else false
//    else false
//    
//  member this.MoveLast(): bool = 
//    if this.InputCursor.MoveLast() then
//#if PRERELEASE
//      let before = this.InputCursor.CurrentKey
//      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//      if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//#endif
//      if ok then
//        this.CurrentKey <- this.InputCursor.CurrentKey
//        //this.CurrentValue <- this.EvaluateState(state)
//        hasValidState <- true
//        true
//      else
//        let mutable found = false
//        while not found && this.InputCursor.MovePrevious() do
//#if PRERELEASE
//          let before = this.InputCursor.CurrentKey
//          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//#endif
//          if ok then
//            found <- true
//            this.CurrentKey <- this.InputCursor.CurrentKey
//            //this.CurrentValue <- this.EvaluateState(state)
//        if found then 
//          hasValidState <- true
//          true
//        else false
//    else false
//
//  member this.MovePrevious(): bool = 
//    if hasValidState then
//      let mutable found = false
//      while not found && this.InputCursor.MovePrevious() do
//#if PRERELEASE
//        let before = this.InputCursor.CurrentKey
//        let ok = this.TryUpdateStatePrevious(this.InputCursor.Current, &state)
//        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//#else
//        let ok = this.TryUpdateStatePrevious(this.InputCursor.Current, &state)
//#endif
//        if ok then 
//          found <- true
//          this.CurrentKey <- this.InputCursor.CurrentKey
//          //this.CurrentValue <- this.EvaluateState(state)
//      if found then 
//        hasValidState <- true
//        true 
//      else false
//    else (this :> ICursor<'K,'R>).MoveLast()
//
//  // TODO! this is first draft. At least do via rec function, later implement all binds in C#
//  member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> =
//    task {
//      if hasValidState then
//        let mutable found = false
//        let mutable moved = false
//        if this.InputCursor.MoveNext() then moved <- true
//        else
//          let! moved' = this.InputCursor.MoveNext(cancellationToken)
//          moved <- moved'
//        while not found && moved do
//          let ok = this.TryUpdateStateNext(this.InputCursor.Current, &state)
//          if ok then
//            found <- true
//            this.CurrentKey <- this.InputCursor.CurrentKey
//          else
//            if this.InputCursor.MoveNext() then moved <- true
//            else
//              let! moved' = this.InputCursor.MoveNext(cancellationToken)
//              moved <- moved'
//        if found then 
//          return true 
//        else return false
//      else
//        let mutable found = false
//        let mutable moved = false
//        if this.InputCursor.MoveNext() then moved <- true
//        else
//          let! moved' = this.InputCursor.MoveNext(cancellationToken)
//          moved <- moved'
//        while not found && moved do
//          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
//          if ok then
//            found <- true
//            this.CurrentKey <- this.InputCursor.CurrentKey
//            //this.CurrentValue <- this.EvaluateState(state)
//            hasValidState <- true
//          else
//            if this.InputCursor.MoveNext() then moved <- true
//            else
//              let! moved' = this.InputCursor.MoveNext(cancellationToken)
//              moved <- moved'
//        if found then 
//          //hasInitializedValue <- true
//          return true 
//        else return false
//    }
//
//
//  member this.TryGetValueChecked(key: 'K, [<Out>] value: byref<'R>): bool = 
//    let mutable v = Unchecked.defaultof<'R>
//    let before = this.InputCursor.CurrentKey
//    let ok = this.TryGetValue(key, &v)
//    if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then 
//      raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//    value <- v
//    ok
//
//
//  interface IEnumerator<KVP<'K,'R>> with
//    member this.Reset() = this.Reset()
//    member this.MoveNext(): bool = this.MoveNext()
//    member this.Current with get(): KVP<'K, 'R> = KVP(this.CurrentKey, this.CurrentValue)
//    member this.Current with get(): obj = KVP(this.CurrentKey, this.CurrentValue) :> obj 
//    member x.Dispose(): unit = x.Dispose()
//
//  interface ICursor<'K,'R> with
//    member this.Comparer with get() = cursor.Comparer
//    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'R> = this.CurrentBatch
//    member this.CurrentKey: 'K = this.CurrentKey
//    member this.CurrentValue: 'R = this.CurrentValue
//    member this.IsContinuous: bool = this.IsContinuous
//    member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index, direction) 
//    member this.MoveFirst(): bool = this.MoveFirst()
//    member this.MoveLast(): bool = this.MoveLast()
//    member this.MovePrevious(): bool = this.MovePrevious()
//    
//    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNext(cancellationToken)
//    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = failwith "TODO Not implemented yet"
//    
//    //member this.IsBatch with get() = this.IsBatch
//    member this.Source: ISeries<'K,'R> = CursorSeries<'K,'R>(Func<ICursor<'K,'R>>((this :> ICursor<'K,'R>).Clone)) :> ISeries<'K,'R>
//    member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =  
//#if PRERELEASE
//      this.TryGetValueChecked(key, &value)
//#else
//      this.TryGetValue(key, &value)
//#endif
//    member this.Clone() = this.Clone()
//
//
////
////  interface ICanMapSeriesValues<'K,'V2> with
////    member this.Map<'V3>(f2:Func<'V2,'V3>): Series<'K,'V3> = this.Map<'V3>(f2:Func<'V2,'V3>)
//////      let func = CoreUtils.CombineMaps(stateMapper, f2)
//////      new BindCursor<'K,'V,'State,'V3>(cursorFactory, func) :> Series<'K,'V3>