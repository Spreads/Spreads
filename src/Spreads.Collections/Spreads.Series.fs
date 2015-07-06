namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Threading.Tasks

open Spreads

// TODO see benchmark for ReadOnly. Reads are very slow while iterations are not affected (GetCursor() returns original cursor) in release mode. Optimize 
// reads of this wrapper either here by type-checking the source of the cursor and using direct methods on the source
// or make cursor thread-static and initialize it only once (now it is called on each method)

// TODO duplicate IReadOnlyOrderedMap methods as an instance method to avoid casting in F#. That will require ovverrides in all children or conflict
// check how it is used from C# (do tests in C# in general)

// TODO check thread safety of the default series implementation. Should we use ThreadLocal for caching cursors that are called via the interfaces?


[<AllowNullLiteral>]
[<Serializable>]
//[<AbstractClassAttribute>]
type BaseSeries internal() =
  // this is ugly, but rewriting the whole structure is uglier // TODO "proper" methods DI
  //static member internal DoInit() =
  static do
    let moduleInfo = 
      Reflection.Assembly.GetExecutingAssembly().GetTypes()
      |> Seq.find (fun t -> t.Name = "Initializer")
    //let ty = typeof<BaseSeries>
    let mi = moduleInfo.GetMethod("init", (Reflection.BindingFlags.Static ||| Reflection.BindingFlags.NonPublic) )
    mi.Invoke(null, [||]) |> ignore


and
  [<AllowNullLiteral>]
  [<Serializable>]
  [<AbstractClassAttribute>]
  Series<'K,'V when 'K : comparison>() as this =
    inherit BaseSeries()
    
    abstract GetCursor : unit -> ICursor<'K,'V>


    interface IEnumerable<KeyValuePair<'K, 'V>> with
      member this.GetEnumerator() = this.GetCursor() :> IEnumerator<KeyValuePair<'K, 'V>>
    interface System.Collections.IEnumerable with
      member this.GetEnumerator() = (this.GetCursor() :> System.Collections.IEnumerator)
    interface ISeries<'K, 'V> with
      member this.GetCursor() = this.GetCursor()
      member this.GetEnumerator() = this.GetCursor() :> IAsyncEnumerator<KVP<'K, 'V>>
      member this.IsIndexed with get() = this.GetCursor().Source.IsIndexed
      member this.SyncRoot with get() = this.GetCursor().Source.SyncRoot

    interface IReadOnlyOrderedMap<'K,'V> with
      member this.IsEmpty = not (this.GetCursor().MoveFirst())
      //member this.Count with get() = map.Count
      member this.First with get() = 
        let c = this.GetCursor()
        if c.MoveFirst() then c.Current else failwith "Series is empty"

      member this.Last with get() =
        let c = this.GetCursor()
        if c.MoveLast() then c.Current else failwith "Series is empty"

      member this.TryFind(k:'K, direction:Lookup, [<Out>] result: byref<KeyValuePair<'K, 'V>>) = 
        let c = this.GetCursor()
        if c.MoveAt(k, direction) then 
          result <- c.Current 
          true
        else failwith "Series is empty"

      member this.TryGetFirst([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
        try
          res <- (this :> IReadOnlyOrderedMap<'K,'V>).First
          true
        with
        | _ -> 
          res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
          false

      member this.TryGetLast([<Out>] res: byref<KeyValuePair<'K, 'V>>) = 
        try
          res <- (this :> IReadOnlyOrderedMap<'K,'V>).Last
          true
        with
        | _ -> 
          res <- Unchecked.defaultof<KeyValuePair<'K, 'V>>
          false

      member this.TryGetValue(k, [<Out>] value:byref<'V>) = 
        let c = this.GetCursor()
        if c.IsContinuous then
          c.TryGetValue(k, &value)
        else
          let v = ref Unchecked.defaultof<KVP<'K,'V>>
          let ok = c.MoveAt(k, Lookup.EQ)
          if ok then value <- c.CurrentValue else value <- Unchecked.defaultof<'V>
          ok

      member this.Item 
        with get k = 
          let ok, v = (this :> IReadOnlyOrderedMap<'K,'V>).TryGetValue(k)
          if ok then v else raise (KeyNotFoundException())

      member this.Keys 
        with get() =
          let c = this.GetCursor()
          seq {
            while c.MoveNext() do
              yield c.CurrentKey
          }

      member this.Values
        with get() =
          let c = this.GetCursor()
          seq {
            while c.MoveNext() do
              yield c.CurrentValue
          }


    /// Used for implement scalar operators which are essentially a map application
    static member inline private ScalarOperatorMap<'K,'V,'V2 when 'K : comparison>(source:Series<'K,'V>, mapFunc:Func<'V,'V2>) = 
      let mapF = ref mapFunc.Invoke
      let mapCursor = 
        {new CursorBind<'K,'V,'V2>(source.GetCursor) with
          override this.TryGetValue(key:'K, [<Out>] value: byref<KVP<'K,'V2>>): bool =
            let ok, value2 = this.InputCursor.TryGetValue(key)
            if ok then
              value <- KVP(key, mapF.Value(value2))
              true
            else false
          override this.TryUpdateNext(next:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
            value <- KVP(next.Key, mapF.Value(next.Value))
            true
          override this.TryUpdatePrevious(previous:KVP<'K,'V>, [<Out>] value: byref<KVP<'K,'V2>>) : bool =
            value <- KVP(previous.Key, mapF.Value(previous.Value))
            true
          override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V2>>) : bool =
            VectorMathProvider.Default.MapBatch(mapFunc.Invoke, nextBatch, &value)
        }
      CursorSeries(fun _ -> mapCursor :> ICursor<'K,'V2>) :> Series<'K,'V2>

    static member (+) (source:Series<'K,int64>, addition:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x + addition)
    static member (~+) (source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x)
    static member (-) (source:Series<'K,int64>, subtraction:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x - subtraction)
    static member (~-) (source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> -x)
    static member (*) (source:Series<'K,int64>, multiplicator:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x * multiplicator)
    static member (*) (multiplicator:int64,source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> multiplicator * x)
    static member (/) (source:Series<'K,int64>, divisor:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x / divisor)
    static member (/) (numerator:int64,source:Series<'K,int64>) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> numerator / x)
    static member (%) (source:Series<'K,int64>, modulo:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> x % modulo)
    static member ( ** ) (source:Series<'K,int64>, power:int64) : Series<'K,int64> = Series.ScalarOperatorMap(source, fun x -> raise (NotImplementedException("TODO Implement with fold, checked?")))
    static member (=) (source:Series<'K,int64>, other:int64) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x = other)
    static member (>) (source:Series<'K,int64>, other:int64) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x > other)
    static member (>) (other:int64, source:Series<'K,int64>) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> other > x)


    // TODO zip operators

    // TODO implement for other numeric types by Ctrl+H types
    static member (+) (source:Series<'K,float>, addition:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x + addition)
    static member (~+) (source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x)
    static member (-) (source:Series<'K,float>, subtraction:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x - subtraction)
    static member (~-) (source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> -x)
    static member (*) (source:Series<'K,float>, multiplicator:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x * multiplicator)
    static member (*) (multiplicator:float,source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> multiplicator * x)
    static member (/) (source:Series<'K,float>, divisor:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x / divisor)
    static member (/) (numerator:float,source:Series<'K,float>) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> numerator / x)
    static member (%) (source:Series<'K,float>, modulo:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x % modulo)
    static member ( ** ) (source:Series<'K,float>, power:float) : Series<'K,float> = Series.ScalarOperatorMap(source, fun x -> x ** power)
    static member (>) (source:Series<'K,float>, other:float) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x > other)
    //static member op_GreaterThan(source:Series<'K,float>, comparand:float) : Series<'K,bool> = Series.ScalarOperatorMap(source, fun x -> x > comparand)



and
  // TODO (perf) base Series() implements IROOM ineficiently, see comments in above type Series() implementation
  
  /// Wrap Series over ICursor
  [<AllowNullLiteral>]
  [<Serializable>]
  CursorSeries<'K,'V when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) =
      inherit Series<'K,'V>()
      override this.GetCursor() = cursorFactory()


// Attempts to manually optimize callvirt and object allocation failed badly
// They are not needed, however, in most of the cases, e.g. iterations.
//
// see https://msdn.microsoft.com/en-us/library/ms973852.aspx
// ...the virtual and interface method call sites are monomorphic (e.g. per call site, the target method does not change over time), 
// so the combination of caching the virtual method and interface method dispatch mechanisms (the method table and interface map 
// pointers and entries) and spectacularly provident branch prediction enables the processor to do an unrealistically effective 
// job calling through these otherwise difficult-to-predict, data-dependent branches. In practice, a data cache miss on any of the 
// dispatch mechanism data, or a branch misprediction (be it a compulsory capacity miss or a polymorphic call site), can and will
// slow down virtual and interface calls by dozens of cycles.
//
// Our benchmark confirms that the slowdown of .Repeat(), .ReadOnly(), .Map(...) and .Filter(...) is quite small 

and // TODO internal
  /// A cursor that could perform map, filter, fold, scan operations on input cursors.
  [<AbstractClassAttribute>]
  CursorBind<'K,'V,'V2 when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) =
    
    // TODO TryGetValue should not change state of the cursor, it looks like now it does, e.g. for repeat

    let cursor = cursorFactory()

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

    /// For every successful move of the inut coursor creates an output value. If direction is not EQ, continues moves to the direction 
    /// until the state is created.
    /// NB: For continuous cases it should be optimized for cases when the key is between current
    /// and previous, e.g. Repeat() should keep the previous key and do comparison (2 times) instead of 
    /// searching the source, which if O(log n) for SortedMap or near 20 comparisons for binary search.
    /// Such lookup between the current and previous is heavilty used in CursorZip.
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
      false

    member this.Reset() = 
      hasValidState <- false
      cursor.Reset()
    member this.Dispose() = 
      hasValidState <- false
      cursor.Dispose()

    interface IEnumerator<KVP<'K,'V2>> with    
      member this.Reset() = this.Reset()
      member x.MoveNext(): bool =
        if hasValidState then
          let mutable found = false
          while not found && x.InputCursor.MoveNext() do // NB! x.InputCursor.MoveNext() && not found // was stupid serious bug, order matters
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
            hasValidState <- true
            true
          else
            match direction with
            | Lookup.EQ -> false
            | Lookup.GE | Lookup.GT ->
              let mutable found = false
              while not found && x.InputCursor.MoveNext() do
                let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
                if ok then 
                  found <- true
                  x.CurrentKey <- value.Key
                  x.CurrentValue <- value.Value
              if found then 
                hasValidState <- true
                true 
              else false
            | Lookup.LE | Lookup.LT ->
              let mutable found = false
              while not found && x.InputCursor.MovePrevious() do
                let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
                if ok then
                  found <- true
                  x.CurrentKey <- value.Key
                  x.CurrentValue <- value.Value
              if found then 
                hasValidState <- true
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
            hasValidState <- true
            true
          else
            let mutable found = false
            while not found && x.InputCursor.MoveNext() do
              let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
              if ok then 
                found <- true
                x.CurrentKey <- value.Key
                x.CurrentValue <- value.Value
            if found then 
              hasValidState <- true
              true 
            else false
        else false
    
      member x.MoveLast(): bool = 
        if x.InputCursor.MoveLast() then
          let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
          if ok then
            x.CurrentKey <- value.Key
            x.CurrentValue <- value.Value
            hasValidState <- true
            true
          else
            let mutable found = false
            while not found && x.InputCursor.MovePrevious() do
              let ok, value = x.TryGetValue(x.InputCursor.CurrentKey)
              if ok then
                found <- true
                x.CurrentKey <- value.Key
                x.CurrentValue <- value.Value
            if found then 
              hasValidState <- true
              true 
            else false
        else false

      member x.MovePrevious(): bool = 
        if hasValidState then
          let mutable found = false
          while not found && x.InputCursor.MovePrevious() do
            let ok, value = x.TryUpdatePrevious(x.InputCursor.Current)
            if ok then 
              found <- true
              x.CurrentKey <- value.Key
              x.CurrentValue <- value.Value
          if found then 
            hasValidState <- true
            true 
          else false
        else (x :> ICursor<'K,'V2>).MoveLast()
    
      member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        failwith "Not implemented yet"
      member x.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        failwith "Not implemented yet"
    
      //member x.IsBatch with get() = x.IsBatch
      member x.Source: ISeries<'K,'V2> = CursorSeries<'K,'V2>((x :> ICursor<'K,'V2>).Clone) :> ISeries<'K,'V2>
      member x.TryGetValue(key: 'K, [<Out>] value: byref<'V2>): bool = 
        let ok, v = x.TryGetValue(key)
        value <- v.Value
        ok
    
      // TODO review + profile. for value types we could just return this
      member x.Clone(): ICursor<'K,'V2> =
        // run-time type of the instance, could be derived type
        let ty = x.GetType()
        let args = [|cursorFactory :> obj|]
        // TODO using Activator is a very bad sign, are we doing something wrong here?
        let clone = Activator.CreateInstance(ty, args) :?> ICursor<'K,'V2> // should not be called too often
        if hasValidState then clone.MoveAt(x.CurrentKey, Lookup.EQ) |> ignore
        //Debug.Assert(movedOk) // if current key is set then we could move to it
        clone


and // TODO internal
  /// A cursor that joins to cursors. 
  [<AbstractClassAttribute>]
  CursorZip<'K,'V1,'V2,'R when 'K : comparison>(cursorFactoryL:unit->ICursor<'K,'V1>, cursorFactoryR:unit->ICursor<'K,'V2>) =
  
    let cursorL = cursorFactoryL()
    let cursorR = cursorFactoryR()
    //let lIsAhead() = CursorHelper.lIsAhead cursorL cursorR
    // TODO comparer as a part of ICursor interface
    // if comparers are not equal then throw invaliOp
    let cmp = Comparer<'K>.Default 

    // if continuous, do not move but treat as if cursor is positioned at other (will move next and the next step)
    // TryGetValue must have optimizations for forward enumeration e.g. for Repeat - keep previous and move one step ahead
    // if not continuous, then move after k
    let moveOrGetNextAtK (cursor:ICursor<'K,'V>) (k:'K) : 'V opt =
      if cursor.IsContinuous then 
        let ok, v = cursor.TryGetValue(k)
        if ok then OptionalValue(v)
        else
          // weird case when continuous has holes
          let mutable found = false
          while not found && cursor.MoveNext() do
            let c = cmp.Compare(cursor.CurrentKey, k)
            if c >= 0 then found <- true
          OptionalValue.Missing
      else
        let mutable found = false
        let mutable ok = false
        while not found && cursor.MoveNext() do
          let c = cmp.Compare(cursor.CurrentKey, k)
          if c > 0 then found <- true
          if c = 0 then 
            found <- true
            ok <- true
        if ok then
          OptionalValue(cursor.CurrentValue)
        else OptionalValue.Missing

    let moveOrGetPrevAtK (cursor:ICursor<'K,'V>) (k:'K) : 'V opt =
      if cursor.IsContinuous then 
        let ok, v = cursor.TryGetValue(k)
        if ok then OptionalValue(v)
        else
          // weird case when continuous has holes
          let mutable found = false
          while not found && cursor.MovePrevious() do
            let c = cmp.Compare(cursor.CurrentKey, k)
            if c >= 0 then found <- true
          OptionalValue.Missing
      else
        let mutable found = false
        let mutable ok = false
        while not found && cursor.MovePrevious() do
          let c = cmp.Compare(cursor.CurrentKey, k)
          if c > 0 then found <- true
          if c = 0 then 
            found <- true
            ok <- true
        if ok then
          OptionalValue(cursor.CurrentValue)
        else OptionalValue.Missing
    

    let mutable hasValidState = false

    member this.Comparer with get() = cmp
    member this.HasValidState with get() = hasValidState and set (v) = hasValidState <- v

    member val IsContinuous = cursorL.IsContinuous && cursorR.IsContinuous with get, set

    /// Source series
    member this.InputCursorL with get() : ICursor<'K,'V1> = cursorL
    member this.InputCursorR with get() : ICursor<'K,'V2> = cursorR

    member val CurrentKey = Unchecked.defaultof<'K> with get, set
    member val CurrentValue = Unchecked.defaultof<'R> with get, set
    member this.Current with get () = KVP(this.CurrentKey, this.CurrentValue)

    /// Stores current batch for a succesful batch move. Value is defined only after successful MoveNextBatch
    member val CurrentBatch = Unchecked.defaultof<IReadOnlyOrderedMap<'K,'R>> with get, set

    /// Update state with a new value. Should be optimized for incremental update of the current state in custom implementations.
    abstract TryZip: key:'K * v1:'V1 * v2:'V2 * [<Out>] value: byref<'R> -> bool
//      raise (NotImplementedException("must implement in derived cursor"))

    /// If input and this cursor support batches, then process a batch and store it in CurrentBatch
    abstract TryZipNextBatches: nextBatchL: IReadOnlyOrderedMap<'K,'V1> * nextBatchR: IReadOnlyOrderedMap<'K,'V2> * [<Out>] value: byref<IReadOnlyOrderedMap<'K,'R>> -> bool  
    override this.TryZipNextBatches(nextBatchL: IReadOnlyOrderedMap<'K,'V1>, nextBatchR: IReadOnlyOrderedMap<'K,'V2>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'R>>) : bool =
      // should work only when keys are equal
      false

    member this.Reset() = 
      hasValidState <- false
      cursorL.Reset()
    member this.Dispose() = 
      hasValidState <- false
      cursorL.Dispose()

    // https://en.wikipedia.org/wiki/Sort-merge_join
    member this.MoveNext(skipMove:bool): bool =
      let mutable skipMove = skipMove
      // TODO movefirst must try batch. if we are calling MoveNext and the input is in the batch state, then iterate over the batch

      // MoveNext is called after previous MoveNext or when MoveFirst() cannot get value after moving both cursors to first positions
      // we must make a step: if positions are equal, step both and check if they are equal
      // if positions are not equal, then try to get continuous values or move the laggard forward and repeat the checks

      // there are three possible STARTING STATES before MN
      // 1. cl.k = cr.k - on the previous move cursors ended up at equal key, but these keys are already consumed.
      //    must move both keys
      //    other cases are possible if one or both cursors are continuous
      // 2. cl.k < cr.k, cl should took cr's value at cl.k, implies that cr IsCont
      //    must move cl since previous (before this move) cl.k is already consumed
      // 3. reverse of 2
      // MN MUST leave cursors in one of these states as well

      let cl = this.InputCursorL
      let cr = this.InputCursorR

      let mutable ret = false // could have a situation when should stop but the state is valid, e.g. for async move next
      let mutable shouldStop = false

      let mutable c = cmp.Compare(cl.CurrentKey, cr.CurrentKey)
      let mutable move = c // - 1 - move left, 0 - move both, 1 - move right
      let mutable shouldMoveL = if skipMove then false else c <= 0 // left is equal or behind
      let mutable shouldMoveR = if skipMove then false else c >= 0 // right is equal or behind

      while not shouldStop do // Check that all paths set shouldStop to true
        //if shouldMove = false, lmoved & rmoved = true without moves via short-circuit eval of ||
        let lmoved, rmoved = 
          if skipMove then
            skipMove <- false
            true, true
          else
            shouldMoveL && cl.MoveNext(), shouldMoveR && cr.MoveNext()
        c <- cmp.Compare(cl.CurrentKey, cr.CurrentKey) 
        match lmoved, rmoved with
        | false, false ->
          Debug.Assert(shouldMoveL || shouldMoveR)
          shouldStop <- true // definetly cannot move forward in any way
          //this.HasValidState <- // leave it as it was, e.g. move first was ok, but no new values. async could pick up later
          ret <- false
        | false, true ->
          Debug.Assert(shouldMoveR)
          if c = 0 then
            let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, cr.CurrentValue)
            if valid then
              shouldStop <- true
              ret <- true
              this.CurrentKey <- cl.CurrentKey
              this.CurrentValue <- v
              this.HasValidState <- true
          else
            // the new key is defined by R
            // regardless of the relative position, try get value from cl if it IsCont
            if cl.IsContinuous then
              let ok, lv = cl.TryGetValue(cr.CurrentKey) // trying to get a value before cl's current key if cl IsCont
              if ok then
                let valid, v = this.TryZip(cr.CurrentKey, lv, cr.CurrentValue)
                if valid then
                  shouldStop <- true
                  ret <- true
                  this.CurrentKey <- cr.CurrentKey
                  this.CurrentValue <- v
                  this.HasValidState <- true
          if not shouldStop then
            // if shouldMoveL was true but we couldn't, it is over - move only right
            // else move it if it is <= r
            shouldMoveL <- if shouldMoveL then false else c <= 0
            shouldMoveR <- if shouldMoveL then true else c >= 0
        | true, false ->
          Debug.Assert(shouldMoveL)
          if c = 0 then
            let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, cr.CurrentValue)
            if valid then
              shouldStop <- true
              ret <- true
              this.CurrentKey <- cl.CurrentKey
              this.CurrentValue <- v
              this.HasValidState <- true
          else
            // the new key is defined by R
            // regardless of the relative position, try get value from cl if it IsCont
            if cr.IsContinuous then
              let ok, rv = cr.TryGetValue(cl.CurrentKey) // trying to get a value before cl's current key if cl IsCont
              if ok then
                let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, rv)
                if valid then
                  shouldStop <- true
                  ret <- true
                  this.CurrentKey <- cl.CurrentKey
                  this.CurrentValue <- v
                  this.HasValidState <- true
          if not shouldStop then
            shouldMoveL <- if shouldMoveR then true else c <= 0
            shouldMoveR <- if shouldMoveR then false else c >= 0
        | true, true -> //
          // now we have potentially valid positioning and should try to get values from it
          // new c after moves, c will remain if at the updated positions we cannot get result. save this comparison
          //c <- cmp.Compare(cl.CurrentKey, cr.CurrentKey)
          match c with
          | l_vs_r when l_vs_r = 0 -> // new keys are equal, try get result from them and iterate if cannot do so
            let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, cr.CurrentValue)
            if valid then
              shouldStop <- true
              ret <- true
              this.CurrentKey <- cl.CurrentKey
              this.CurrentValue <- v
              this.HasValidState <- true
            if not shouldStop then // keys were equal and consumed, move both next
              shouldMoveL <- true
              shouldMoveR <- true
          | l_vs_r when l_vs_r > 0 ->
            // cl is ahead, cr must check if cl IsCont and try to get cl's value at cr's key
            // smaller key defines current position
            if cl.IsContinuous then
              let ok, lv = cl.TryGetValue(cr.CurrentKey) // trying to get a value before cl's current key if cl IsCont
              if ok then
                let valid, v = this.TryZip(cr.CurrentKey, lv, cr.CurrentValue)
                if valid then
                  shouldStop <- true
                  ret <- true
                  this.CurrentKey <- cr.CurrentKey
                  this.CurrentValue <- v
                  this.HasValidState <- true
            // cr will move on next iteration, cl must stay
            if not shouldStop then
              shouldMoveL <- false
              shouldMoveR <- true
          | _ -> //l_vs_r when l_vs_r < 0
            // flip side of the second case
            if cr.IsContinuous then
              let ok, rv = cr.TryGetValue(cl.CurrentKey) // trying to get a value before cl's current key if cl IsCont
              if ok then
                let valid, v = this.TryZip(cl.CurrentKey, cl.CurrentValue, rv)
                if valid then
                  shouldStop <- true
                  ret <- true
                  this.CurrentKey <- cl.CurrentKey
                  this.CurrentValue <- v
                  this.HasValidState <- true
            // cl will move on next iteration, cr must stay
            if not shouldStop then
              shouldMoveL <- true
              shouldMoveR <- false
      ret


    interface IEnumerator<KVP<'K,'R>> with    
      member this.Reset() = this.Reset()
      member this.MoveNext(): bool = 
        if this.HasValidState then this.MoveNext(false)
        else (this :> ICursor<'K,'R>).MoveFirst()

      member this.Current with get(): KVP<'K, 'R> = this.Current
      member this.Current with get(): obj = this.Current :> obj 
      member x.Dispose(): unit = x.Dispose()

    interface ICursor<'K,'R> with
      member x.Current: KVP<'K,'R> = KVP(x.CurrentKey, x.CurrentValue)
      member x.CurrentBatch: IReadOnlyOrderedMap<'K,'R> = x.CurrentBatch
      member x.CurrentKey: 'K = x.CurrentKey
      member x.CurrentValue: 'R = x.CurrentValue
      member x.IsContinuous: bool = x.IsContinuous
      member x.MoveAt(index: 'K, direction: Lookup): bool = failwith "not implemented"

      member this.MoveFirst(): bool =
        let cl = this.InputCursorL
        let cr = this.InputCursorR
        if cl.MoveFirst() && cr.MoveFirst() then this.MoveNext(true)
        else false
    
      member this.MoveLast(): bool = 
        let cl = this.InputCursorL
        let cr = this.InputCursorR
        let mutable ok = false
        let mutable k = Unchecked.defaultof<'K>
        let mutable vl = Unchecked.defaultof<'V1>
        let mutable vr = Unchecked.defaultof<'V2>

        if cl.MoveLast() && cr.MoveLast() then
          // minus, reverse direction
          let c = -cmp.Compare(cl.CurrentKey, cr.CurrentKey)
          match c with
          | l_vs_r when l_vs_r = 0 ->
            k <- cl.CurrentKey
            vl <- cl.CurrentValue
            vr <- cr.CurrentValue
            ok <- true
          | l_vs_r when l_vs_r > 0 -> // left is ahead
            // need to get right's value at left's key
            let vrOpt = moveOrGetPrevAtK cr cl.CurrentKey
            if vrOpt.IsPresent then
              k <- cl.CurrentKey
              vl <- cl.CurrentValue
              vr <- vrOpt.Present
              ok <- true
          | _ -> // right is ahead
            // need to get left's value at right's key
            let vrOpt = moveOrGetPrevAtK cl cr.CurrentKey
            if vrOpt.IsPresent then
              k <- cl.CurrentKey
              vl <- vrOpt.Present
              vr <- cr.CurrentValue
              ok <- true
          if ok then
            let valid, v = this.TryZip(k, vl, vr)
            if valid then
              this.CurrentKey <- k
              this.CurrentValue <- v
              true
            else false
          else false
        else false

      member x.MovePrevious(): bool = failwith "not implemented"
//        let cl = x.InputCursorL
//        let cr = x.InputCursorR
//        if hasValidState then
//          let mutable found = false
//          while not found && x.InputCursorL.MovePrevious() do
//            let ok, value = x.TryUpdatePrevious(x.InputCursorL.Current)
//            if ok then 
//              found <- true
//              x.CurrentKey <- value.Key
//              x.CurrentValue <- value.Value
//          if found then 
//            hasValidState <- true
//            true 
//          else false
//        else (x :> ICursor<'K,'R>).MoveLast()
    
      member x.MoveNext(cancellationToken: Threading.CancellationToken): Task<bool> = 
        failwith "Not implemented yet"
      member x.MoveNextBatch(cancellationToken: Threading.CancellationToken): Task<bool> = 
        failwith "Not implemented yet"
    
      //member x.IsBatch with get() = x.IsBatch
      member x.Source: ISeries<'K,'R> = CursorSeries<'K,'R>((x :> ICursor<'K,'R>).Clone) :> ISeries<'K,'R>
      member this.TryGetValue(key: 'K, [<Out>] value: byref<'R>): bool =
        // this method must not move cursors or position of the current cursor
        let cl = this.InputCursorL
        let cr = this.InputCursorR
        let ok1, v1 = cl.TryGetValue(key)
        let ok2, v2 = cr.TryGetValue(key)
        if ok1 && ok2 then
          this.TryZip(key, v1, v2, &value)
        else false
    
      member x.Clone(): ICursor<'K,'R> =
        // run-time type of the instance, could be derived type
        let ty = x.GetType()
        let args = [|cursorFactoryL :> obj;cursorFactoryR :> obj|]
        // TODO using Activator is a very bad sign, are we doing something wrong here?
        let clone = Activator.CreateInstance(ty, args) :?> ICursor<'K,'R> // should not be called too often
        if hasValidState then clone.MoveAt(x.CurrentKey, Lookup.EQ) |> ignore
        //Debug.Assert(movedOk) // if current key is set then we could move to it
        clone