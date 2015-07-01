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


// TODO check hypothesis that this helper will correctly inline struct and call its methods via call not callvirt
module CursorHelper =
//  let inline C (c:ICursor<'K,'V>) = c.Current
//  let inline CK (c:ICursor<'K,'V>) = c.CurrentKey
//  let inline CV (c:ICursor<'K,'V>) = c.CurrentValue
//  let inline CB (c:ICursor<'K,'V>) = c.CurrentBatch
//  let inline MF (c:ICursor<'K,'V>) = c.MoveFirst()
//  let inline ML (c:ICursor<'K,'V>) = c.MoveLast()
//  let inline MN (c:ICursor<'K,'V>) = c.MoveNext()
//  let inline MNA (ct) (c:ICursor<'K,'V>) = c.MoveNext(ct)
//  let inline MNB (ct) (c:ICursor<'K,'V>) = c.MoveNextBatchAsync(ct)
//  let inline MP (c:ICursor<'K,'V>) = c.MovePrevious()
//  let inline MA (key) (direction) (c:ICursor<'K,'V>) = c.MoveAt(key, direction)
//  let inline TGV (key) (c:ICursor<'K,'V>) = c.TryGetValue(key)
//  let inline Clone (c:ICursor<'K,'V>) = c.Clone()
//  let inline Reset (c:ICursor<'K,'V>) = c.Reset()
//  let inline Dispose (c:ICursor<'K,'V>) = c.Dispose()
//  let inline Source (c:ICursor<'K,'V>) = c.Source
//  let inline IsB (c:ICursor<'K,'V>) = c.IsBatch
//  let inline IsC (c:ICursor<'K,'V>) = c.IsContinuous
  let inline FilterF (filterFunc:'K->'V->bool) = filterFunc
  let inline FolderF (folderFunc:'S->'K->'V->'S) = folderFunc
  let inline ValueF (valueFunc:'S ->'V2) = valueFunc
  let inline AnyF k v : bool = true
  let inline IdF x = x
  let inline batchFolder(createStateFunc:'C->'K->'S,valueFunc:'S ->'V2,
        filterFunc:'K->'V->bool, 
        foldNextFunc:'S->'K->'V->'S,
        batchFunc:('S->IReadOnlyOrderedMap<'K,'V>->IReadOnlyOrderedMap<'K,'V2>) option) = 
          if batchFunc.IsSome then batchFunc.Value
          else (fun (st:'S) (batch:IReadOnlyOrderedMap<'K,'V>) -> 
                  let sm = SortedMap() // init the batch // TODO IReadOnlyOrderedMap must have Count property
                  for kvp in batch do // iterate through the batch
                    if filterFunc kvp.Key kvp.Value then // filter it if neccessary
                      let newState = foldNextFunc st kvp.Key kvp.Value // update state
                      let newValue = valueFunc newState // get value from state
                      sm.AddLast(kvp.Key,newValue) // build return batch
                  sm :> IReadOnlyOrderedMap<'K,'V2> // return the batch
               )

open CursorHelper

// cursor factory is like a thunk for Vagabond, could exetute remotely?

// could do both map and filter
// need cursor factory and not a cursor to be able to clone, Series.GetCursor is the factory. NB! But Cursor.Clone() is also cursor factory
type internal CursorBind<'K,'V,'V2,'S when 'K : comparison> =
  //struct
    // source cursor
  val mutable public cursor : ICursor<'K,'V> // the main reason for struct is a hope that when nested this is not boxed
  // state of this cursor is valid
  val mutable public hasValidState : bool // state is valid for folderFunc update, otherwise always recreate state
  val mutable public state : 'S // folder state for move next
  val mutable public batch : IReadOnlyOrderedMap<'K,'V2> // folder state for move next
  // these two functions are enough for map
  val public createStateFunc:ICursor<'K,'V>->'K->'S // create state at any 'K
  val public valueFunc : 'S ->'V2 // get output from state

  // this function adds filter ability
  val public filterFunc : 'K->'V->bool // filter source key and value
    
  // these two functions are for fast update of state without recreating it on each move
  val public foldNextFunc :'S->'K->'V->'S // update state on next value
  val public foldPreviousFunc :'S->'K->'V->'S // update state on previous value
    
  // optimized batch processing of input
  val public batchFunc : 'S->IReadOnlyOrderedMap<'K,'V>->IReadOnlyOrderedMap<'K,'V2> // get output from state

  new(cursor:ICursor<'K,'V>, createStateFunc:ICursor<'K,'V>->'K->'S,valueFunc:'S ->'V2,
      ?filterFunc:'K->'V->bool, 
      ?foldNextFunc:'S->'K->'V->'S, ?foldPreviousFunc:'S->'K->'V->'S,
      ?batchFunc:'S->IReadOnlyOrderedMap<'K,'V>->IReadOnlyOrderedMap<'K,'V2>) =
    // rewrite optional function to their default implementation
    let filter = if filterFunc.IsSome then filterFunc.Value else AnyF
    let nextFolder = 
      if foldNextFunc.IsSome then foldNextFunc.Value
      else (fun _ k _ -> createStateFunc cursor k)
    let previousFolder = 
      if foldPreviousFunc.IsSome then foldPreviousFunc.Value
      else (fun _ k _ -> createStateFunc cursor k)

    { cursor = cursor;
      hasValidState = false;
      state = Unchecked.defaultof<'S>;
      batch = Unchecked.defaultof<_>;
      createStateFunc=createStateFunc;
      valueFunc = valueFunc;
      filterFunc = filter;
      foldNextFunc = nextFolder;
      foldPreviousFunc = previousFolder;
      batchFunc = batchFolder(createStateFunc, valueFunc, filter, nextFolder, batchFunc)
      }
  //end

  member this.Clone() = // NB source position is cloned with the cursor, 
    let c = CursorBind(this.cursor.Clone() :?> 'C,this.createStateFunc,this.valueFunc,this.filterFunc, this.foldNextFunc,this.foldPreviousFunc,this.batchFunc)
    c
  
  member this.Reset() = 
    this.hasValidState <- false
    this.cursor.Reset()
  member this.Dispose() = 
    this.hasValidState <- false
    this.cursor.Dispose()

  member this.Current with get() : KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)
  member this.CurrentBatch with get() = this.batch
  member this.CurrentKey with get() : 'K = if not this.hasValidState then invalidOp "invalid cursor state" else this.cursor.CurrentKey
  member this.CurrentValue with get():'V2 = 
    if not this.hasValidState then invalidOp "invalid cursor state" 
    else this.valueFunc this.state

  // TODO hasValidState plays a second role "isStarted", rethink logic, read how IEnumerator behaves wrt reset and false moves

  member this.MoveNext(): bool =
    if this.hasValidState then
      let mutable found = false
      while this.cursor.MoveNext() && not found do
        if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
          found <- true
          this.state <- this.foldNextFunc this.state this.cursor.CurrentKey this.cursor.CurrentValue
      if found then 
        this.hasValidState <- true
        true
      else 
        false
    else this.MoveFirst()

  member this.MovePrevious(): bool = 
    if this.hasValidState then
      let mutable found = false
      while this.cursor.MovePrevious() && not found do
        if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
          found <- true
          this.state <- this.foldPreviousFunc this.state this.cursor.CurrentKey this.cursor.CurrentValue
      if found then 
        this.hasValidState <- true
        true 
      else false
    else this.MoveLast()

  member this.MoveAt(index: 'K, direction: Lookup): bool = 
    if this.cursor.MoveAt(index, direction) then
      if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
        this.state <- this.createStateFunc this.cursor this.cursor.CurrentKey
        this.hasValidState <- true
        true
      else
        match direction with
        | Lookup.EQ -> false
        | Lookup.GE | Lookup.GT ->
          let mutable found = false
          while this.cursor.MoveNext() && not found do
            if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
              found <- true
              this.state <- this.createStateFunc this.cursor this.cursor.CurrentKey
          if found then 
            this.hasValidState <- true
            true 
          else false
        | Lookup.LE | Lookup.LT ->
          let mutable found = false
          while this.cursor.MovePrevious() && not found do
            if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
              found <- true
              this.state <- this.createStateFunc this.cursor this.cursor.CurrentKey
          if found then 
            this.hasValidState <- true
            true 
          else false
        | _ -> failwith "wrong lookup value"
    else false

  member this.MoveFirst(): bool = 
    if this.cursor.MoveFirst() then
      if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
        this.state <- this.createStateFunc this.cursor this.cursor.CurrentKey
        this.hasValidState <- true
        true
      else
        let mutable found = false
        while this.cursor.MoveNext() && not found do
          if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
            found <- true
            this.state <- this.createStateFunc this.cursor this.cursor.CurrentKey
        if found then 
          this.hasValidState <- true
          true 
        else false
    else false

  member this.MoveLast(): bool = 
    if this.cursor.MoveLast() then
      if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
        this.state <- this.createStateFunc this.cursor this.cursor.CurrentKey
        this.hasValidState <- true
        true
      else
        let mutable found = false
        while this.cursor.MovePrevious() && not found do
          if this.filterFunc this.cursor.CurrentKey this.cursor.CurrentValue then
            found <- true
            this.state <- this.createStateFunc this.cursor this.cursor.CurrentKey
        if found then 
          this.hasValidState <- true
          true 
        else false
    else false

  member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
    let ok, value2 = this.TryGetValue(key)
    value <- value2.Value
    ok

  interface IEnumerator<KVP<'K,'V2>> with
    member this.Current with get(): KVP<'K, 'V2> = this.Current
    member this.Current with get(): obj = this.Current :> obj 
    member this.Dispose(): unit = this.Dispose()  
    member this.Reset() = this.Reset()
    member this.MoveNext(): bool = this.MoveNext()
    
  interface ICursor<'K,'V2> with
    member this.Current: KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
    member this.CurrentKey: 'K = this.CurrentKey
    member this.CurrentValue: 'V2 = this.CurrentValue
    member this.IsContinuous: bool = this.IsContinuous
    
    member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index,direction)
    member this.MoveFirst(): bool = this.MoveFirst()
    member this.MoveLast(): bool = this.MoveLast()
    member this.MovePrevious(): bool = this.MovePrevious()

    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    member this.MoveNextBatchAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    
    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = this.TryGetValue(key, &value)

    member this.IsBatch with get() = this.cursor.IsBatch
    member this.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>((this :> ICursor<'K,'V2>).Clone) :> ISeries<'K,'V2>

    member this.Clone(): ICursor<'K,'V2> = this.Clone() :> ICursor<'K,'V2>


  // TODO kind of loop fusion and what nessos does with streams, but to opposite direction
  // TODO test must show that this improves performance





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

  //member val IsIndexed = false with get, set //source.IsIndexed
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

  //member this.Clone() = this.MemberwiseClone() :?> ICursor<'K,'V2>

  member this.Reset() = 
    hasInitializedValue <- false
    cursor.Reset()
  member this.Dispose() = 
    hasInitializedValue <- false
    cursor.Dispose()

  interface IEnumerator<KVP<'K,'V2>> with    
    member this.Reset() = this.Reset()
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
    member this.Current with get(): KVP<'K, 'V2> = this.Current
    member this.Current with get(): obj = this.Current :> obj 
    member x.Dispose(): unit = x.Dispose()

  interface ICursor<'K,'V2> with
    member x.Current: KVP<'K,'V2> = KVP(x.CurrentKey, x.CurrentValue)
    member x.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = x.CurrentBatch
    member x.CurrentKey: 'K = x.CurrentKey
    member x.CurrentValue: 'V2 = x.CurrentValue
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
    
    member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    member x.MoveNextBatchAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
      failwith "Not implemented yet"
    
    member x.IsBatch with get() = x.IsBatch
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
    
    // TODO review. for value types we could just return this
    member x.Clone(): ICursor<'K,'V2> =
      // run-time type of the instance
      let ty = x.GetType()
      let args = [|cursorFactory :> obj|]
      // TODO very bad sign, we are doing something wrong here
      let clone = Activator.CreateInstance(ty, args) :?> ICursor<'K,'V2> // should not be called too often
      if hasInitializedValue then clone.MoveAt(x.CurrentKey, Lookup.EQ) |> ignore
      //Debug.Assert(movedOk) // if current key is set then we could move to it
      clone
      //x.Clone()

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