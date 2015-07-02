namespace Spreads

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open Spreads
//
//[<AbstractClassAttribute>]
//type Cursor<'K,'V when 'K : comparison>() as this =
//  [<DefaultValueAttribute>]
//  val mutable internal currentKey : 'K
//  [<DefaultValueAttribute>]
//  val mutable internal currentValue : 'V
//  //  these two are only interfaces, other
//  //  inherit System.IDisposable
//  //  inherit System.Collections.IEnumerator
//  member inline this.Current with get() = KVP(this.currentKey, this.currentValue)
//  /// Advances the enumerator to the next element in the sequence, returning the result asynchronously.
//  /// <returns>
//  /// Task containing the result of the operation: true if the enumerator was successfully advanced 
//  /// to the next element; false if the enumerator has passed the end of the sequence.
//  /// </returns>    
//  abstract MoveNextAsync: cancellationToken:CancellationToken  -> Task<bool>
//  /// Puts the cursor to the position according to LookupDirection
//  abstract MoveAt: index:'K * direction:Lookup -> bool
//  abstract MoveFirst: unit -> bool
//  abstract MoveLast: unit -> bool
//  abstract MovePrevious: unit -> bool
//  abstract CurrentKey:'K with get
//  abstract CurrentValue:'V with get
//  /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
//  /// Returns true when a batch is available immediately (async for IO, not for waiting for new values),
//  /// returns false when there is no more immediate values and a consumer should switch to MoveNextAsync().
//  /// NB: Btach processing is synchronous via IEnumerable interface of a batch, real-time is pull-based asynchronous.
//  abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
//  /// Optional (used for batch/SIMD optimization where gains are visible), could throw NotImplementedException()
//  /// The actual implementation of the batch could be mutable and could reference a part of the original series, therefore consumer
//  /// should never try to mutate the batch directly even if type check reveals that this is possible, e.g. it is a SortedMap
//  abstract CurrentBatch: IReadOnlyOrderedMap<'K,'V> with get
//  /// True if last successful move was MoveNextBatchAsync and CurrentBatch contains a valid value.
//  abstract IsBatch: bool with get
//  /// Original series. Note that .Source.GetCursor() is equivalent to .Clone() called on not started cursor
//  abstract Source : ISeries<'K,'V> with get
//  /// If true then TryGetValue could return values for any keys, not only for existing keys.
//  /// E.g. previous value, interpolated value, etc.
//  abstract IsContinuous: bool with get
//  /// Create a copy of cursor that is positioned at the same place as this cursor.
//  abstract Clone: unit -> ICursor<'K,'V>
//  /// Gets a calculated value for continuous series without moving the cursor position.
//  /// This method must be called only when IsContinuous is true, otherwise NotSupportedException will be thrown.
//  /// E.g. a continuous cursor for Repeat() will check if current state allows to get previous value,
//  /// and if not then .Source.GetCursor().MoveAt(key, LE). The TryGetValue method should be optimized
//  /// for sort join case using enumerator, e.g. for repeat it should keep previous value and check if 
//  /// the requested key is between the previous and the current keys, and then return the previous one.
//  abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool
//


/// Uses IReadOnlyOrderedMap's TryFind method, doesn't know anything about underlying sequence
type MapCursor<'K,'V when 'K : comparison>
  (map:IReadOnlyOrderedMap<'K,'V>) as this =
  //inherit Cursor<'K,'V>()
  [<DefaultValue>] 
  val mutable private currentPosition : bool * KeyValuePair<'K,'V>

  let mutable isReset = true

  let isUpdateable = match map with | :? IUpdateable<'K,'V> -> true | _ -> false
  let observerStarted = ref false
  let tcs = ref (TaskCompletionSource<bool>())
  let ctr = ref (Unchecked.defaultof<CancellationTokenRegistration>)
  let isWaitingForTcs = ref false
  let sr = Object()

  let updateHandler : UpdateHandler<'K,'V> =
    let impl _ (kvp:KVP<'K,'V>) =
      lock(sr) (fun _ ->
        // right now a client is waiting for a task to complete, there is no more elements in the map
        if !isWaitingForTcs then
          this.currentPosition <- true, kvp
          (!ctr).Dispose()
          (!tcs).TrySetResult(true)  |> ignore
        else
          // do nothing, MoveNextAsync will try call MoveNext() and it will return the correct result
          ()
      )
    UpdateHandler(impl)
  let cancelHandler = fun () -> (!tcs).TrySetCanceled() |> ignore

  abstract MoveAt: index:'K * direction:Lookup -> bool
  override this.MoveAt(index:'K, lookup:Lookup) = 
    isReset <- false
    this.currentPosition <- map.TryFind(index, lookup)
    fst this.currentPosition

  abstract MoveFirst: unit -> bool
  override this.MoveFirst():bool = 
    try
      this.MoveAt(map.First.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  abstract MoveLast: unit -> bool
  override this.MoveLast():bool =
    try
      this.MoveAt(map.Last.Key, Lookup.EQ)
    with
      | :? InvalidOperationException -> false

  abstract member MoveNext : unit -> bool
  override this.MoveNext():bool = 
    if isReset then this.MoveFirst()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.GT)
      fst this.currentPosition
  
  abstract MovePrevious: unit -> bool
  override this.MovePrevious():bool = 
    if isReset then this.MoveLast()
    else
      this.currentPosition <- map.TryFind((snd this.currentPosition).Key, Lookup.LT)
      fst this.currentPosition

  abstract Current:KVP<'K,'V> with get
  override this.Current 
    with get(): KeyValuePair<'K, 'V> = 
      snd this.currentPosition

  abstract CurrentKey:'K with get
  override this.CurrentKey with get():'K = this.Current.Key

  abstract CurrentValue:'V with get
  override this.CurrentValue with get():'V = this.Current.Value

  abstract Dispose: unit -> unit
  override this.Dispose() =
    if !observerStarted then
      Debug.Assert(map :? IUpdateable<'K,'V>)
      (map :?> IUpdateable<'K,'V>).OnData.RemoveHandler(updateHandler)

  abstract member Reset : unit -> unit
  override this.Reset() = isReset <- true

// Not needed, non-async methods should never block, they return false if there are no immediate values available. Then called should call MoveNextAsync().
//  abstract MoveAtAsync: index:'K * direction:Lookup -> Task<bool>
//  abstract MoveFirstAsync: unit -> Task<bool>
//  abstract MoveLastAsync: unit -> Task<bool>
//  abstract MovePreviousAsync: unit -> Task<bool>
//  override this.MoveAtAsync(index:'K, lookup:Lookup) = Task.FromResult(this.MoveAt(index, lookup))
//  override this.MoveFirstAsync():Task<bool> = Task.FromResult(this.MoveFirst())
//  override this.MoveLastAsync():Task<bool> = Task.FromResult(this.MoveLast())
//  override this.MovePreviousAsync():Task<bool> = Task.FromResult(this.MovePrevious())

  abstract member MoveNextAsync : CancellationToken -> Task<bool>
  override this.MoveNextAsync(ct) =
    match this.MoveNext() with
    | true -> Task.FromResult(true)      
    | false -> 
      match isUpdateable with
      | true -> 
        let upd = map :?> IUpdateable<'K,'V>
        lock(sr) (fun _ ->
          if not !observerStarted then 
            upd.OnData.AddHandler updateHandler
            observerStarted := true
          tcs := TaskCompletionSource()
          ctr := ct.Register(Action(cancelHandler))
          isWaitingForTcs := true
          tcs.Value.Task
        )
      | _ -> Task.FromResult(false) // has no values and will never have because is not IUpdateable
  
  abstract MoveNextBatchAsync: cancellationToken:CancellationToken  -> Task<bool>
  abstract CurrentBatch: IReadOnlyOrderedMap<'K,'V> with get
  override this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))
  override this.MoveNextBatchAsync(cancellationToken: CancellationToken): Task<bool> = raise (NotSupportedException("IReadOnlyOrderedMap do not support batches, override the method in a map implementation"))

  abstract Source : ISeries<'K,'V> with get
  override this.Source with get() = map :> ISeries<'K,'V>

  abstract Clone: unit -> ICursor<'K,'V>
  override this.Clone() =
    let c = new MapCursor<'K,'V>(map)
    c.currentPosition <- this.currentPosition
    c :> ICursor<'K,'V>

  abstract IsBatch: bool with get
  override this.IsBatch with get() = false
  abstract IsContinuous: bool with get
  override this.IsContinuous with get() = false

  interface IDisposable with
    member this.Dispose() = this.Dispose()

  interface IEnumerator<KVP<'K,'V>> with    
    member this.Reset() = this.Reset()
    member this.MoveNext():bool = this.MoveNext()
    member this.Current with get(): KVP<'K, 'V> = this.Current
    member this.Current with get(): obj = this.Current :> obj

  interface IAsyncEnumerator<KVP<'K,'V>> with
    member x.Current: KVP<'K, 'V> = this.Current
    member this.MoveNext(cancellationToken:CancellationToken): Task<bool> = this.MoveNextAsync(cancellationToken) 

  interface ICursor<'K,'V> with
    // TODO need some implementation of ROOM to implement the batch
    member this.CurrentBatch: IReadOnlyOrderedMap<'K,'V> = this.CurrentBatch
    member this.MoveNextBatch(cancellationToken: CancellationToken): Task<bool> = this.MoveNextBatchAsync(cancellationToken)
    //member this.IsBatch with get() = this.IsBatch
    member this.MoveAt(index:'K, lookup:Lookup) = this.MoveAt(index, lookup)
    member this.MoveFirst():bool = this.MoveFirst()
    member this.MoveLast():bool =  this.MoveLast()
    member this.MovePrevious():bool = this.MovePrevious()
    member this.CurrentKey with get():'K = this.CurrentKey
    member this.CurrentValue with get():'V = this.CurrentValue
    member this.Source with get() = this.Source
    member this.Clone() = this.Clone()
    member this.IsContinuous with get() = this.IsContinuous
    member this.TryGetValue(key, [<Out>]value: byref<'V>) : bool = map.TryGetValue(key, &value)