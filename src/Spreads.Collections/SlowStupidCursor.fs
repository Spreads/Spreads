namespace Spreads.Collections.Experimental
//
//open System
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
///// Wrap IReadOnlyOrderedMap over ICursor
//[<AllowNullLiteral>]
//[<Serializable>]
//type CursorSeries<'K,'V when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) =
//  inherit Series<'K,'V>()
//  override this.GetCursor() = cursorFactory()
//
//
//
//// TODO check hypothesis that this helper will correctly inline struct and call its methods via call not callvirt
//module CursorHelper =
////  let inline C (c:ICursor<'K,'V>) = c.Current
////  let inline CK (c:ICursor<'K,'V>) = c.CurrentKey
////  let inline CV (c:ICursor<'K,'V>) = c.CurrentValue
////  let inline CB (c:ICursor<'K,'V>) = c.CurrentBatch
////  let inline MF (c:ICursor<'K,'V>) = c.MoveFirst()
////  let inline ML (c:ICursor<'K,'V>) = c.MoveLast()
////  let inline MN (c:ICursor<'K,'V>) = c.MoveNext()
////  let inline MNA (ct) (c:ICursor<'K,'V>) = c.MoveNext(ct)
////  let inline MNB (ct) (c:ICursor<'K,'V>) = c.MoveNextBatchAsync(ct)
////  let inline MP (c:ICursor<'K,'V>) = c.MovePrevious()
////  let inline MA (key) (direction) (c:ICursor<'K,'V>) = c.MoveAt(key, direction)
////  let inline TGV (key) (c:ICursor<'K,'V>) = c.TryGetValue(key)
////  let inline Clone (c:ICursor<'K,'V>) = c.Clone()
////  let inline Reset (c:ICursor<'K,'V>) = c.Reset()
////  let inline Dispose (c:ICursor<'K,'V>) = c.Dispose()
////  let inline Source (c:ICursor<'K,'V>) = c.Source
////  let inline IsB (c:ICursor<'K,'V>) = c.IsBatch
////  let inline IsC (c:ICursor<'K,'V>) = c.IsContinuous
//  let inline FilterF (filterFunc:'K->'V->bool) = filterFunc
//  let inline FolderF (folderFunc:'S->'K->'V->'S) = folderFunc
//  let inline ValueF (valueFunc:'S ->'V2) = valueFunc
//  let inline AnyF k v : bool = true
//  let inline IdF x = x
//  let inline batchFolder(createStateFunc:'C->'K->'S opt,valueFunc:'S ->'V2, // TODO check valid state and recreate it is invalid
//        //filterFunc:'K->'V->bool, 
//        foldNextFunc:'S->'K->'V->'S opt,
//        batchFunc:('S->IReadOnlyOrderedMap<'K,'V>->IReadOnlyOrderedMap<'K,'V2>) option) = 
//          if batchFunc.IsSome then batchFunc.Value
//          else (fun (st:'S) (batch:IReadOnlyOrderedMap<'K,'V>) -> 
//                  let sm = SortedMap() // init the batch // TODO IReadOnlyOrderedMap must have Count property
//                  for kvp in batch do // iterate through the batch
//                    let optValue = foldNextFunc st kvp.Key kvp.Value
//                    if optValue.IsPresent then // filter it if neccessary
//                      let newState = optValue.Present // update state
//                      let newValue = valueFunc newState // get value from state
//                      sm.AddLast(kvp.Key,newValue) // build return batch
//                  sm :> IReadOnlyOrderedMap<'K,'V2> // return the batch
//               )
//
//open CursorHelper
//
//// cursor factory is like a thunk for Vagabond, could exetute remotely?
//
//[<ObsoleteAttribute("Slow as shit in a sink")>]
//type internal CursorBind<'K,'V,'V2,'S when 'K : comparison> =
//  //struct
//    // source cursor
//  val mutable public cursor : ICursor<'K,'V> // the main reason for struct is a hope that when nested this is not boxed
//  // state of this cursor is valid
//  val mutable public hasValidState : bool // state is valid for folderFunc update, otherwise always recreate state
//  val mutable public isContinuous : bool // the value of this cursor exists for any key, not just the keys of the input cursor
//
//  val mutable public state : 'S // folder state for move next
//  val mutable public batch : IReadOnlyOrderedMap<'K,'V2> // folder state for move next
//  // these two functions are enough for map
//  val public createStateFunc:ICursor<'K,'V>->'K->'S opt // create state at any 'K
//  val public valueFunc : 'S ->'V2 // get output from state
//
//  // this function adds filter ability
//  //val public filterFunc : 'K->'V->bool // filter source key and value
//    
//  // these two functions are for fast update of state without recreating it on each move
//  val public foldNextFunc :'S->'K->'V->'S opt // update state on next value
//  val public foldPreviousFunc :'S->'K->'V->'S opt // update state on previous value
//    
//  // optimized batch processing of input
//  val public batchFunc : 'S->IReadOnlyOrderedMap<'K,'V>->IReadOnlyOrderedMap<'K,'V2> // get output from state
//
//  new(cursor:ICursor<'K,'V>, isContinuous:bool,createStateFunc:ICursor<'K,'V>->'K->'S opt,valueFunc:'S ->'V2,
//      //?filterFunc:'K->'V->bool,
//      ?foldNextFunc:'S->'K->'V->'S opt, ?foldPreviousFunc:'S->'K->'V->'S opt,
//      ?batchFunc:'S->IReadOnlyOrderedMap<'K,'V>->IReadOnlyOrderedMap<'K,'V2>) =
//    // rewrite optional function to their default implementation
//    //let filter = if filterFunc.IsSome then filterFunc.Value else AnyF
//    let nextFolder = 
//      if foldNextFunc.IsSome then foldNextFunc.Value
//      else (fun _ k _ -> createStateFunc cursor k)
//    let previousFolder = 
//      if foldPreviousFunc.IsSome then foldPreviousFunc.Value
//      else (fun _ k _ -> createStateFunc cursor k)
//
//    { cursor = cursor;
//      hasValidState = false;
//      isContinuous = isContinuous;
//      state = Unchecked.defaultof<'S>;
//      batch = Unchecked.defaultof<_>;
//      createStateFunc=createStateFunc;
//      valueFunc = valueFunc;
//      //filterFunc = filter;
//      foldNextFunc = nextFolder;
//      foldPreviousFunc = previousFolder;
//      batchFunc = batchFolder(createStateFunc, valueFunc,  nextFolder, batchFunc) //filter,
//      }
//  //end
//
//  member this.Clone() = // NB source position is cloned with the cursor, 
//    let c = CursorBind(this.cursor.Clone() :?> 'C,this.isContinuous,this.createStateFunc,this.valueFunc, this.foldNextFunc,this.foldPreviousFunc,this.batchFunc) //this.filterFunc,
//    c
//  
//  member this.Reset() = 
//    this.hasValidState <- false
//    this.cursor.Reset()
//  member this.Dispose() = 
//    this.hasValidState <- false
//    this.cursor.Dispose()
//
//  member this.Current with get() : KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)
//  member this.CurrentBatch with get() = this.batch
//  member this.CurrentKey with get() : 'K = if not this.hasValidState then invalidOp "invalid cursor state" else this.cursor.CurrentKey
//  member this.CurrentValue with get():'V2 = 
//    if not this.hasValidState then invalidOp "invalid cursor state" 
//    else this.valueFunc this.state
//
//  // TODO hasValidState plays a second role "isStarted", rethink logic, read how IEnumerator behaves wrt reset and false moves
//
//  member this.MoveNext(): bool =
//    if this.hasValidState then
//      let mutable found = false
//      while this.cursor.MoveNext() && not found do
//        let optValue = this.foldNextFunc this.state this.cursor.CurrentKey this.cursor.CurrentValue
//        if optValue.IsPresent then
//          found <- true
//          this.state <- optValue.Present
//      if found then 
//        this.hasValidState <- true
//        true
//      else 
//        false
//    else this.MoveFirst()
//
//  member this.MovePrevious(): bool = 
//    if this.hasValidState then
//      let mutable found = false
//      while this.cursor.MovePrevious() && not found do
//        let optValue = this.foldPreviousFunc this.state this.cursor.CurrentKey this.cursor.CurrentValue
//        if optValue.IsPresent then
//          found <- true
//          this.state <- optValue.Present
//      if found then 
//        this.hasValidState <- true
//        true 
//      else false
//    else this.MoveLast()
//
//  member this.MoveAt(index: 'K, direction: Lookup): bool = 
//    if this.cursor.MoveAt(index, direction) then
//      let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//      if optValue.IsPresent then
//        this.state <- optValue.Present
//        this.hasValidState <- true
//        true
//      else
//        match direction with
//        | Lookup.EQ -> false
//        | Lookup.GE | Lookup.GT ->
//          let mutable found = false
//          while this.cursor.MoveNext() && not found do
//            let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//            if optValue.IsPresent then
//              found <- true
//              this.state <- optValue.Present
//          if found then 
//            this.hasValidState <- true
//            true 
//          else false
//        | Lookup.LE | Lookup.LT ->
//          let mutable found = false
//          while this.cursor.MovePrevious() && not found do
//            let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//            if optValue.IsPresent then
//              found <- true
//              this.state <- optValue.Present
//          if found then 
//            this.hasValidState <- true
//            true 
//          else false
//        | _ -> failwith "wrong lookup value"
//    else false
//
//  member this.MoveFirst(): bool = 
//    if this.cursor.MoveFirst() then
//      let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//      if optValue.IsPresent then 
//        this.state <- optValue.Present
//        this.hasValidState <- true
//        true
//      else
//        let mutable found = false
//        while this.cursor.MoveNext() && not found do
//          let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//          if optValue.IsPresent then
//            found <- true
//            this.state <- optValue.Present
//        if found then 
//          this.hasValidState <- true
//          true 
//        else false
//    else false
//
//  member this.MoveLast(): bool = 
//    if this.cursor.MoveLast() then
//      let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//      if optValue.IsPresent then
//        this.state <- optValue.Present
//        this.hasValidState <- true
//        true
//      else
//        let mutable found = false
//        while this.cursor.MovePrevious() && not found do
//          let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//          if optValue.IsPresent then
//            found <- true
//            this.state <- optValue.Present
//        if found then 
//          this.hasValidState <- true
//          true 
//        else false
//    else false
//
//  member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool =
//    let optValue = this.createStateFunc this.cursor this.cursor.CurrentKey
//    if optValue.IsPresent then
//      let v = optValue.Present |> this.valueFunc
//      value <- v
//      true
//    else false
//
//  interface IEnumerator<KVP<'K,'V2>> with
//    member this.Current with get(): KVP<'K, 'V2> = this.Current
//    member this.Current with get(): obj = this.Current :> obj 
//    member this.Dispose(): unit = this.Dispose()  
//    member this.Reset() = this.Reset()
//    member this.MoveNext(): bool = this.MoveNext()
//    
//  interface ICursor<'K,'V2> with
//    member this.Current: KVP<'K,'V2> = KVP(this.CurrentKey, this.CurrentValue)
//    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V2> = this.CurrentBatch
//    member this.CurrentKey: 'K = this.CurrentKey
//    member this.CurrentValue: 'V2 = this.CurrentValue
//    member this.IsContinuous: bool = this.isContinuous
//    
//    member this.MoveAt(index: 'K, direction: Lookup): bool = this.MoveAt(index,direction)
//    member this.MoveFirst(): bool = this.MoveFirst()
//    member this.MoveLast(): bool = this.MoveLast()
//    member this.MovePrevious(): bool = this.MovePrevious()
//
//    member this.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    member this.MoveNextBatchAsync(cancellationToken: Threading.CancellationToken): Task<bool> = 
//      failwith "Not implemented yet"
//    
//    member this.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = this.TryGetValue(key, &value)
//
//    //member this.IsBatch with get() = this.cursor.IsBatch
//    member this.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>((this :> ICursor<'K,'V2>).Clone) :> ISeries<'K,'V2>
//
//    member this.Clone(): ICursor<'K,'V2> = this.Clone() :> ICursor<'K,'V2>
//
//
//  // TODO kind of loop fusion and what nessos does with streams, but to opposite direction
//  // TODO test must show that this improves performance
//
//[<Extension>]
//type SeriesExtensions () =
//  [<Extension>]
//  static member  AddWithBind(source: Series<'K,int64>, addition:int64) : Series<'K,int64> = //inline
//    let createState (c:ICursor<'K,int64>) (k:'K) : int64 opt =
//      let ok, value2 = c.TryGetValue(k)
//      if ok then OptionalValue(value2 + addition)
//      else OptionalValue.Missing
//
//    // val public foldNextFunc :'S->'K->'V->'S opt // update state on next value
//    let updateNextState (st:int64) (k:'K) (v:int64) = OptionalValue(v + addition)
//
//    let valueF (x:int64) = x
//
//    let cursorBind() = CursorBind(source.GetCursor(),false,createState, valueF,updateNextState) :> ICursor<'K,int64>
//
//    CursorSeries(cursorBind) :> Series<'K,int64>