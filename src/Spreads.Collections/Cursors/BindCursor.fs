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


// TODO fold cursor, foldValue that uses only values property of series (much faster)


 // TODO internal
  /// A cursor that could perform map, filter, fold, scan operations on input cursors.
[<AbstractClassAttribute>]
//  [<DebuggerTypeProxy(typeof<SeriesDebuggerProxy<_,_>>)>]
[<ObsoleteAttribute("TODO Rewrite this as BindCursor to fix not only name, but alos separate state 
    creation and transformation. Add TryCreateState and TryUpdateState, and then a function that transforms 
    the state to value should be applied in the value getter. By doing so, we could even inherit from BaseCursor
    and reuse MoveNext in MoveNextAsync")>]
type  CursorBind<'K,'V,'V2>(cursorFactory:Func<ICursor<'K,'V>>) =
    
    let cursor = cursorFactory.Invoke()

    // TODO make public property, e.g. for random walk generator we must throw if we try to init more than one
    // this is true for all "vertical" transformations, they start from a certain key and depend on the starting value
    // safe to call TryUpdateNext/Previous
    let mutable hasValidState = false
    /// True after any successful move and when CurrentKey is defined
    member this.HasValidState with get() = hasValidState and set (v) = hasValidState <- v

    // TODO? add key type for the most general case
    // check if key types are not equal, in that case check if new values are sorted. On first 
    // unsorted value change output to Indexed

    //member val IsIndexed = false with get, set //source.IsIndexed
    /// By default, could move everywhere the source moves
    member val IsContinuous = cursor.IsContinuous with get, set

    /// Source series
    //member this.InputSource with get() = source
    member this.InputCursor with get() : ICursor<'K,'V> = cursor

    //abstract CurrentKey:'K with get
    //abstract CurrentValue:'V2 with get
    member val CurrentKey = Unchecked.defaultof<'K> with get, set
    member val CurrentValue = Unchecked.defaultof<'V2> with get, set
    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move
    //abstract CurrentBatch : IReadOnlyOrderedMap<'K,'V2> with get
    member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>> with get, set

    /// For every successful move of the input coursor creates an output value. If direction is not EQ, continues moves to the direction 
    /// until the state is created.
    /// NB: For continuous cases it should be optimized for cases when the key is between current
    /// and previous, e.g. Repeat() should keep the previous key and do comparison (2 times) instead of 
    /// searching the source, which if O(log n) for SortedMap or near 20 comparisons for binary search.
    /// Such lookup between the current and previous is heavilty used in CursorZip.
    abstract TryGetValue: key:'K * isPositioned:bool * [<Out>] value: byref<'V2> -> bool // * direction: Lookup not needed here
    // this is the main method to transform input to output, other methods could be implemented via it

    //inline
//    member this.TryGetValueChecked(key:'K, isPositioned:bool, [<Out>] value: byref<'V2>): bool =
//#if PRERELEASE
//      let before = this.InputCursor.CurrentKey
//      let res = this.TryGetValue(key, isPositioned, &value)
//      if before <> this.InputCursor.CurrentKey then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//      else res
//#else
//      this.TryGetValue(key, &value)
//#endif

    /// Update state with a new value. Should be optimized for incremental update of the current state in custom implementations.
    abstract TryUpdateNext: next:KVP<'K,'V> * [<Out>] value: byref<'V2> -> bool
    override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
      // recreate value from scratch
      this.TryGetValue(next.Key, true, &value)

//    member inline this.TryUpdateNextChecked(next:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
//#if PRERELEASE
//      let before = this.InputCursor.CurrentKey
//      let res = this.TryUpdateNext(next, &value)
//      if before <> this.InputCursor.CurrentKey then raise (InvalidOperationException("CursorBind's TryUpdateNext implementation must not move InputCursor"))
//      else res
//#else
//      this.TryUpdateNext(next, true, &value)
//#endif

    /// Update state with a previous value. Should be optimized for incremental update of the current state in custom implementations.
    abstract TryUpdatePrevious: previous:KVP<'K,'V> * [<Out>] value: byref<'V2> -> bool
    override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
      // recreate value from scratch
      this.TryGetValue(previous.Key, true, &value)

//    member inline this.TryUpdatePreviousChecked(next:KVP<'K,'V>, [<Out>] value: byref<'V2>) : bool =
//#if PRERELEASE
//      let before = this.InputCursor.CurrentKey
//      let res = this.TryUpdatePrevious(next, &value)
//      if before <> this.InputCursor.CurrentKey then raise (InvalidOperationException("CursorBind's TryUpdatePrevious implementation must not move InputCursor"))
//      else res
//#else
//      TryUpdatePrevious(next, true, &value)
//#endif

    /// If input and this cursor support batches, then process a batch and store it in CurrentBatch
    abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>> -> bool  
    override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>>) : bool =
      false

    member this.Reset() = 
      hasValidState <- false
      cursor.Reset()
    abstract Dispose: unit -> unit
    default this.Dispose() = 
      hasValidState <- false
      cursor.Dispose()

    abstract Clone: unit -> ICursor<'K,'V2>
      // TODO review + profile. for value types we could just return this
    /// This will work only on cursors that take only factory in ctor
    override this.Clone(): ICursor<'K,'V2> =
      // run-time type of the instance, could be derived type
      let ty = this.GetType()
      let args = [|cursorFactory :> obj|]
      // TODO using Activator is a very bad sign, are we doing something wrong here?
      let clone = Activator.CreateInstance(ty, args) :?> ICursor<'K,'V2> // should not be called too often
      if hasValidState then clone.MoveAt(this.CurrentKey, Lookup.EQ) |> ignore
      //Trace.Assert(movedOk) // if current key is set then we could move to it
      clone


    member this.MoveNext(): bool =
      if hasValidState then
        let mutable value = Unchecked.defaultof<'V2>
        let mutable found = false
        while not found && this.InputCursor.MoveNext() do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
#if PRERELEASE
          let before = this.InputCursor.CurrentKey
          let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
          let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
#endif
          if ok then
            found <- true
            this.CurrentKey <- this.InputCursor.CurrentKey
            this.CurrentValue <- value
        if found then 
          //hasInitializedValue <- true
          true 
        else false
      else this.MoveFirst()

    member this.MoveNext(ct:Threading.CancellationToken): Task<bool> =
      async {
        if hasValidState then
          let mutable value = Unchecked.defaultof<'V2>
          let mutable found = false
          let! moved' = this.InputCursor.MoveNext(ct) |> Async.AwaitTask
          let mutable moved = moved'
          while not found && moved do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
  #if PRERELEASE
            let before = this.InputCursor.CurrentKey
            let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
  #else
            let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
  #endif
            if ok then
              found <- true
              this.CurrentKey <- this.InputCursor.CurrentKey
              this.CurrentValue <- value
            else
              let! moved' = this.InputCursor.MoveNext(ct) |> Async.AwaitTask
              moved <- moved'
          if found then 
            //hasInitializedValue <- true
            return true 
          else return false
        else return this.MoveFirst()
      } |> Async.StartAsTask // fun x -> Async.StartAsTask(x, TaskCreationOptions.None, ct)


//    member this.MoveNext2(ct:Threading.CancellationToken): Task<bool> =
//      let mutable value = Unchecked.defaultof<'V2>
//      let mutable found = false
//      let rec loop(cond:bool) =
//        if cond then
//          let before = this.InputCursor.CurrentKey
//          let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
//          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//          let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
//          if ok then
//            found <- true
//            this.CurrentKey <- this.InputCursor.CurrentKey
//            this.CurrentValue <- value
//          else
//            this.InputCursor.MoveNext(ct).ContinueWith(fun (t:Task<bool>) -> 
//              loop(t.Result).Result
//            )
//            //moved <- moved'
//        else
//          false
//        
//      this.InputCursor.MoveNext(ct).ContinueWith(fun (t:Task<bool>) ->
//        let mutable moved = t.Result
//        while not found && moved do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
//  #if PRERELEASE
//          let before = this.InputCursor.CurrentKey
//          let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
//          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
//  #else
//          let ok = this.TryUpdateNext(this.InputCursor.Current, &value)
//  #endif
//          if ok then
//            found <- true
//            this.CurrentKey <- this.InputCursor.CurrentKey
//            this.CurrentValue <- value
//          else
//            let! moved' = this.InputCursor.MoveNext(ct) |> Async.AwaitTask
//            moved <- moved'
//        if found then 
//          //hasInitializedValue <- true
//          true 
//        else false
//      )


    member this.MoveAt(index: 'K, direction: Lookup): bool = 
      if this.InputCursor.MoveAt(index, direction) then
        let mutable value = Unchecked.defaultof<'V2>
#if PRERELEASE
        let before = this.InputCursor.CurrentKey
        let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
        let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
        if ok then
          this.CurrentKey <- this.InputCursor.CurrentKey
          this.CurrentValue <- value
          hasValidState <- true
          true
        else
          match direction with
          | Lookup.EQ -> false
          | Lookup.GE | Lookup.GT ->
            let mutable found = false
            while not found && this.InputCursor.MoveNext() do
#if PRERELEASE
              let before = this.InputCursor.CurrentKey
              let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
              if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
              let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
              if ok then 
                found <- true
                this.CurrentKey <- this.InputCursor.CurrentKey
                this.CurrentValue <- value
            if found then 
              hasValidState <- true
              true 
            else false
          | Lookup.LE | Lookup.LT ->
            let mutable found = false
            while not found && this.InputCursor.MovePrevious() do
#if PRERELEASE
              let before = this.InputCursor.CurrentKey
              let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
              if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
              let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
              if ok then
                found <- true
                this.CurrentKey <- this.InputCursor.CurrentKey
                this.CurrentValue <- value
            if found then 
              hasValidState <- true
              true 
            else false
          | _ -> failwith "wrong lookup value"
      else false
      
    
    member this.MoveFirst(): bool = 
      if this.InputCursor.MoveFirst() then
        let mutable value = Unchecked.defaultof<'V2>
#if PRERELEASE
        let before = this.InputCursor.CurrentKey
        let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
        let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
        if ok then
          this.CurrentKey <- this.InputCursor.CurrentKey
          this.CurrentValue <- value
          hasValidState <- true
          true
        else
          let mutable found = false
          while not found && this.InputCursor.MoveNext() do
#if PRERELEASE
            let before = this.InputCursor.CurrentKey
            let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
            let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
            if ok then 
              found <- true
              this.CurrentKey <- this.InputCursor.CurrentKey
              this.CurrentValue <- value
          if found then 
            hasValidState <- true
            true 
          else false
      else false
    
    member this.MoveLast(): bool = 
      if this.InputCursor.MoveLast() then
        let mutable value = Unchecked.defaultof<'V2>
#if PRERELEASE
        let before = this.InputCursor.CurrentKey
        let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
        let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
        if ok then
          this.CurrentKey <- this.InputCursor.CurrentKey
          this.CurrentValue <- value
          hasValidState <- true
          true
        else
          let mutable found = false
          while not found && this.InputCursor.MovePrevious() do
#if PRERELEASE
            let before = this.InputCursor.CurrentKey
            let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
            let ok = this.TryGetValue(this.InputCursor.CurrentKey, true, &value)
#endif
            if ok then
              found <- true
              this.CurrentKey <- this.InputCursor.CurrentKey
              this.CurrentValue <- value
          if found then 
            hasValidState <- true
            true
          else false
      else false

    member this.MovePrevious(): bool = 
      if hasValidState then
        let mutable value = Unchecked.defaultof<'V2>
        let mutable found = false
        while not found && this.InputCursor.MovePrevious() do
#if PRERELEASE
          let before = this.InputCursor.CurrentKey
          let ok = this.TryUpdatePrevious(this.InputCursor.Current, &value)
          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
          let ok = this.TryUpdatePrevious(this.InputCursor.Current, &value)
#endif
          if ok then 
            found <- true
            this.CurrentKey <- this.InputCursor.CurrentKey
            this.CurrentValue <- value
        if found then 
          hasValidState <- true
          true 
        else false
      else (this :> ICursor<'K,'V2>).MoveLast()


    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
        let mutable v = Unchecked.defaultof<'V2>
#if PRERELEASE
        let before = this.InputCursor.CurrentKey
        let ok = this.TryGetValue(key, false, &v)
        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
        let ok = this.TryGetValue(key, false, &v)
#endif
        value <- v
        ok


    interface IEnumerator<KVP<'K,'V2>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = this.MoveNext()
      member this.Current with get(): KVP<'K, 'V2> = KVP(this.CurrentKey, this.CurrentValue)
      member this.Current with get(): obj = KVP(this.CurrentKey, this.CurrentValue) :> obj 
      member x.Dispose(): unit = x.Dispose()

    interface ICursor<'K,'V2> with
      member this.Comparer with get() = cursor.Comparer
      member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
      member this.CurrentKey: 'K = this.CurrentKey
      member this.CurrentValue: 'V2 = this.CurrentValue
      member this.IsContinuous: bool = this.IsContinuous
      member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index, direction) 
      member this.MoveFirst(): bool = this.MoveFirst()
      member this.MoveLast(): bool = this.MoveLast()
      member this.MovePrevious(): bool = this.MovePrevious()
    
      member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNext(cancellationToken)
      member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = failwith "Not implemented yet"
    
      //member this.IsBatch with get() = this.IsBatch
      member this.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>(Func<ICursor<'K,'V2>>((this :> ICursor<'K,'V2>).Clone)) :> ISeries<'K,'V2>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  this.TryGetValue(key, &value)
      member this.Clone() = this.Clone()
    
      



/// A cursor that could perform map, filter, fold, scan or any other projection operations on input cursors.
[<AbstractClassAttribute>]
type internal BindCursor<'K,'V,'State,'V2>(cursorFactory:Func<ICursor<'K,'V>>) =
  let cursor = cursorFactory.Invoke()

  let mutable state = Unchecked.defaultof<'State>
  let mutable hasValidState = false

  /// True after any successful move and when CurrentKey is defined
  member this.HasValidState with get() = hasValidState and set (v) = hasValidState <- v
  //member val IsIndexed = false with get, set //source.IsIndexed
  /// By default, could move everywhere the source moves
  member val IsContinuous = cursor.IsContinuous with get, set

  member this.InputCursor with get() : ICursor<'K,'V> = cursor
    
  member val CurrentKey = Unchecked.defaultof<'K> with get, set
  member this.CurrentValue 
    with get() =
      if hasValidState then this.EvaluateState state
      else Unchecked.defaultof<'V2>

  member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)
  /// Stores current batch for a succesful batch move
  //abstract CurrentBatch : IReadOnlyOrderedMap<'K,'V2> with get
  member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'V2>> with get, set

  member this.State with get() = state

  /// Map state to value at current InputCursor position
  abstract EvaluateState: state:'State -> 'V2
  /// Creates new state at a key.
  abstract TryCreateState: key:'K * [<Out>] state: byref<'State> -> bool
  /// Updates state with next value of input
  abstract TryUpdateStateNext: next:KVP<'K,'V> * value: byref<'State> -> bool
  /// Updates state with next value of input
  //abstract TryUpdateStateNextAsync: next:KVP<'K,'V> * value: byref<'State> -> Task<bool>
  /// Updates state with previous value of input
  abstract TryUpdateStatePrevious: next:KVP<'K,'V> * value: byref<'State> -> bool
  override this.TryUpdateStatePrevious(next, [<Out>] value) = this.TryCreateState(next.Key, &value)

  abstract TryUpdateNextBatch: nextBatch: IReadOnlyOrderedMap<'K,'V> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>> -> bool  
  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>>) : bool =
//      let mutable batchState = Unchecked.defaultof<'State>
//      use bc = nextBatch.GetCursor()
//      if bc.MoveFirst() && this.TryCreateState(bc.CurrentKey, &batchState) then
//        // TODO (peft) check if nextBatch is sorted map to set size and if it is immutable to reuse keys
//        let sm = SortedMap()
//        sm.
//      else 
      // TODO this could be done lazily via fold, if we could move bind below fold
      false

  abstract TryGetValue: key:'K * [<Out>] value: byref<'V2> -> bool 
//  override this.TryGetValue(key, [<Out>] value: byref<'V2>) : bool = 
//    if hasValidState && this.InputCursor.Comparer.Compare(key, this.InputCursor.CurrentKey) = 0 then
//      value <- this.EvaluateState(state)
//      true
//    else
//      let ok, state' = this.TryCreateState(key)
//      if ok then
//        value <- this.EvaluateState(state')
//        true
//      else false

  member this.Reset() = 
    hasValidState <- false
    cursor.Reset()
  abstract Dispose: unit -> unit
  override this.Dispose() = 
    hasValidState <- false
    cursor.Dispose()

  /// Derived class must create a copy of self and, if hasValidState, move the new copy at the current key with MoveAt
  abstract Clone: unit -> ICursor<'K,'V2>


  member this.MoveNext(): bool =
      if hasValidState then
        let mutable found = false
        while not found && this.InputCursor.MoveNext() do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
#if PRERELEASE
          let before = this.InputCursor.CurrentKey
          let ok = this.TryUpdateStateNext(this.InputCursor.Current, &state)
          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
          let ok = this.TryUpdateStateNext(this.InputCursor.Current, &state)
#endif
          if ok then
            found <- true
            this.CurrentKey <- this.InputCursor.CurrentKey
            //this.CurrentValue <- this.EvaluateState(state)
        if found then 
          //hasInitializedValue <- true
          true 
        else false
      else this.MoveFirst()

  member this.MoveAt(index: 'K, direction: Lookup): bool = 
    if this.InputCursor.MoveAt(index, direction) then
#if PRERELEASE
      let before = this.InputCursor.CurrentKey
      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
      if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
#endif
      if ok then
        this.CurrentKey <- this.InputCursor.CurrentKey
        //this.CurrentValue <- this.EvaluateState(state)
        hasValidState <- true
        true
      else
        match direction with
        | Lookup.EQ -> false
        | Lookup.GE | Lookup.GT ->
          let mutable found = false
          while not found && this.InputCursor.MoveNext() do
#if PRERELEASE
            let before = this.InputCursor.CurrentKey
            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
#endif
            if ok then 
              found <- true
              this.CurrentKey <- this.InputCursor.CurrentKey
              //this.CurrentValue <- this.EvaluateState(state)
          if found then 
            hasValidState <- true
            true 
          else false
        | Lookup.LE | Lookup.LT ->
          let mutable found = false
          while not found && this.InputCursor.MovePrevious() do
#if PRERELEASE
            let before = this.InputCursor.CurrentKey
            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
            if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
            let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
#endif
            if ok then
              found <- true
              this.CurrentKey <- this.InputCursor.CurrentKey
              //this.CurrentValue <- this.EvaluateState(state)
          if found then 
            hasValidState <- true
            true 
          else false
        | _ -> failwith "wrong lookup value"
    else false
      
    
  member this.MoveFirst(): bool = 
    if this.InputCursor.MoveFirst() then
#if PRERELEASE
      let before = this.InputCursor.CurrentKey
      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
      if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
#endif
      if ok then
        this.CurrentKey <- this.InputCursor.CurrentKey
        //this.CurrentValue <- this.EvaluateState(state)
        hasValidState <- true
        true
      else
        let mutable found = false
        while not found && this.InputCursor.MoveNext() do
#if PRERELEASE
          let before = this.InputCursor.CurrentKey
          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
#endif
          if ok then 
            found <- true
            this.CurrentKey <- this.InputCursor.CurrentKey
            //this.CurrentValue <- this.EvaluateState(state)
        if found then 
          hasValidState <- true
          true 
        else false
    else false
    
  member this.MoveLast(): bool = 
    if this.InputCursor.MoveLast() then
#if PRERELEASE
      let before = this.InputCursor.CurrentKey
      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
      if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
      let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
#endif
      if ok then
        this.CurrentKey <- this.InputCursor.CurrentKey
        //this.CurrentValue <- this.EvaluateState(state)
        hasValidState <- true
        true
      else
        let mutable found = false
        while not found && this.InputCursor.MovePrevious() do
#if PRERELEASE
          let before = this.InputCursor.CurrentKey
          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
          if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
#endif
          if ok then
            found <- true
            this.CurrentKey <- this.InputCursor.CurrentKey
            //this.CurrentValue <- this.EvaluateState(state)
        if found then 
          hasValidState <- true
          true
        else false
    else false

  member this.MovePrevious(): bool = 
    if hasValidState then
      let mutable found = false
      while not found && this.InputCursor.MovePrevious() do
#if PRERELEASE
        let before = this.InputCursor.CurrentKey
        let ok = this.TryUpdateStatePrevious(this.InputCursor.Current, &state)
        if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
#else
        let ok = this.TryUpdateStatePrevious(this.InputCursor.Current, &state)
#endif
        if ok then 
          found <- true
          this.CurrentKey <- this.InputCursor.CurrentKey
          //this.CurrentValue <- this.EvaluateState(state)
      if found then 
        hasValidState <- true
        true 
      else false
    else (this :> ICursor<'K,'V2>).MoveLast()

  // TODO! this is first draft. At least do via rec function, later implement all binds in C#
  member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> =
    task {
      if hasValidState then
        let mutable found = false
        let mutable moved = false
        if this.InputCursor.MoveNext() then moved <- true
        else
          let! moved' = this.InputCursor.MoveNext(cancellationToken)
          moved <- moved'
        while not found && moved do
          let ok = this.TryUpdateStateNext(this.InputCursor.Current, &state)
          if ok then
            found <- true
            this.CurrentKey <- this.InputCursor.CurrentKey
          else
            if this.InputCursor.MoveNext() then moved <- true
            else
              let! moved' = this.InputCursor.MoveNext(cancellationToken)
              moved <- moved'
        if found then 
          return true 
        else return false
      else
        let mutable found = false
        let mutable moved = false
        if this.InputCursor.MoveNext() then moved <- true
        else
          let! moved' = this.InputCursor.MoveNext(cancellationToken)
          moved <- moved'
        while not found && moved do
          let ok = this.TryCreateState(this.InputCursor.CurrentKey, &state)
          if ok then
            found <- true
            this.CurrentKey <- this.InputCursor.CurrentKey
            //this.CurrentValue <- this.EvaluateState(state)
            hasValidState <- true
          else
            if this.InputCursor.MoveNext() then moved <- true
            else
              let! moved' = this.InputCursor.MoveNext(cancellationToken)
              moved <- moved'
        if found then 
          //hasInitializedValue <- true
          return true 
        else return false
    }


  member this.TryGetValueChecked(key: 'K, [<Out>] value: byref<'V2>): bool = 
    let mutable v = Unchecked.defaultof<'V2>
    let before = this.InputCursor.CurrentKey
    let ok = this.TryGetValue(key, &v)
    if cursor.Comparer.Compare(before, this.InputCursor.CurrentKey) <> 0 then 
      raise (InvalidOperationException("CursorBind's TryGetValue implementation must not move InputCursor"))
    value <- v
    ok


  interface IEnumerator<KVP<'K,'V2>> with
    member this.Reset() = this.Reset()
    member this.MoveNext(): bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V2> = KVP(this.CurrentKey, this.CurrentValue)
    member this.Current with get(): obj = KVP(this.CurrentKey, this.CurrentValue) :> obj 
    member x.Dispose(): unit = x.Dispose()

  interface ICursor<'K,'V2> with
    member this.Comparer with get() = cursor.Comparer
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
    member this.CurrentKey: 'K = this.CurrentKey
    member this.CurrentValue: 'V2 = this.CurrentValue
    member this.IsContinuous: bool = this.IsContinuous
    member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index, direction) 
    member this.MoveFirst(): bool = this.MoveFirst()
    member this.MoveLast(): bool = this.MoveLast()
    member this.MovePrevious(): bool = this.MovePrevious()
    
    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = this.MoveNext(cancellationToken)
    member this.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = failwith "TODO Not implemented yet"
    
    //member this.IsBatch with get() = this.IsBatch
    member this.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>(Func<ICursor<'K,'V2>>((this :> ICursor<'K,'V2>).Clone)) :> ISeries<'K,'V2>
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =  
#if PRERELEASE
      this.TryGetValueChecked(key, &value)
#else
      this.TryGetValue(key, &value)
#endif
    member this.Clone() = this.Clone()