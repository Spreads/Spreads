namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks

open Spreads
open Spreads.Collections


// Repeat, fill and other (TODO linear interpolate, cubic splines, trend, .... limited by fantasy and actuals needs: arima forecast, kalman filter)

type internal LaggedState<'K,'V> =
  struct
    val mutable Current : KVP<'K,'V>
    val mutable Previous : KVP<'K,'V>
    new(c,p) = {Current = c; Previous = p}
  end

/// Repeat previous value for all missing keys
type internal RepeatCursor<'K,'V>(cursorFactory:Func<ICursor<'K,'V>>) as this =
  inherit BindCursor<'K,'V,LaggedState<'K,'V>,'V>(cursorFactory)
  do
    this.IsContinuous <- true
  // reused when current state is not enough to find a value
  let mutable lookupCursor = Unchecked.defaultof<ICursor<'K,'V>>

  // perf counters
  static let mutable stateHit = 0
  static let mutable stateMiss  = 0
  static let mutable stateCreation = 0

  static member StateHit = stateHit
  static member StateMiss = stateMiss
  static member StateCreation = stateCreation

  override this.TryCreateState(key, [<Out>] value: byref<LaggedState<'K,'V>>) : bool =
    Interlocked.Increment(&stateCreation) |> ignore
    let current = this.InputCursor.Current
    if lookupCursor = Unchecked.defaultof<ICursor<'K,'V>> then lookupCursor <- this.InputCursor.Clone()
    if lookupCursor.MoveAt(key, Lookup.LE) then
      let previous = lookupCursor.Current
      value <- LaggedState(current, previous)
      true
    else false
    
  override this.TryUpdateStateNext(nextKvp, [<Out>] value: byref<LaggedState<'K,'V>>) : bool =
    value.Previous <- value.Current
    value.Current <- nextKvp
    true

  override this.TryUpdateStatePrevious(previousKvp, [<Out>] value: byref<LaggedState<'K,'V>>) : bool =
    value.Current <- value.Previous
    value.Previous <- previousKvp
    true

  override this.EvaluateState(state) = state.Current.Value

  override this.TryUpdateNextBatch(nextBatch: IReadOnlyOrderedMap<'K,'V>, [<Out>] value: byref<IReadOnlyOrderedMap<'K,'V>>) : bool =
    let repeatedBatch = CursorSeries(fun _ -> new RepeatCursor<'K,'V>(Func<ICursor<'K,'V>>(nextBatch.GetCursor)) :> ICursor<'K,'V>) :> Series<'K,'V>
    value <- repeatedBatch
    true

  override this.TryGetValue(key:'K, [<Out>] value: byref<'V>): bool =
    let current = this.State.Current
    let previous = this.State.Previous
    let c = this.InputCursor.Comparer.Compare(key, current.Key)
    if c = 0 && this.HasValidState then
      Interlocked.Increment(&stateHit) |> ignore
      value <- current.Value
      true
    elif c < 0 && this.InputCursor.Comparer.Compare(key, previous.Key) >= 0 then
      Interlocked.Increment(&stateHit) |> ignore
      value <- previous.Value
      true
    else
      Interlocked.Increment(&stateMiss) |> ignore
      if lookupCursor = Unchecked.defaultof<ICursor<'K,'V>> then lookupCursor <- this.InputCursor.Clone()
      if lookupCursor.MoveAt(key, Lookup.LE) then
        value <- lookupCursor.CurrentValue
        true
      else 
        false

  override this.Clone() = 
    let clone = new RepeatCursor<'K,'V>(cursorFactory) :> ICursor<'K,'V>
    if base.HasValidState then clone.MoveAt(base.CurrentKey, Lookup.EQ) |> ignore
    clone

  override this.Dispose() = 
    if lookupCursor <> Unchecked.defaultof<ICursor<'K,'V>> then lookupCursor.Dispose()
    base.Dispose()