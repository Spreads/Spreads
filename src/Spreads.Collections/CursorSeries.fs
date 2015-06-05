namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices

open Spreads


/// Wrap IReadOnlyOrderedMap over ICursor
[<AllowNullLiteral>]
[<Serializable>]
type CursorSeries<'K,'V when 'K : comparison>(cursorFactory:unit->ICursor<'K,'V>) =
  inherit Series<'K,'V>()
  override this.GetCursor() = cursorFactory()



/// This is similar to .Single in extensions
/// Generators are Create

// 

[<AbstractClassAttribute>]
type TransformerCursor<'K,'V, 'K2, 'V2 when 'K : comparison and 'K2 : comparison>(source:ISeries<'K,'V>) =
  
  let c = source.GetCursor()
  let state = ref Unchecked.defaultof<'R>
  

  // TODO add key type for the most general case
  // check if key types are not equal, in that case check if new values are sorted. On first 
  // unsorted value change output to Indexed
  member val IsIndexed = source.IsIndexed with get, set

  member this.Source with get() = source

  abstract State : KVP<'K2,'V2> with get
  abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool
  abstract TryCreateState: kvp:KVP<'K,'V> * [<Out>] value: byref<KVP<'K2,'V2> > -> bool
  abstract TryUpdateNext: next:KVP<'K,'V> -> 'R
  
  abstract TryUpdatePrevious: next:KVP<'K,'V> -> 'R
  // default create state at c.MoveAt(c.Current, )

  abstract UpdateBatch: batch:IReadOnlyOrderedMap<'K,'V> -> IReadOnlyOrderedMap<'K,'R>
  
  /// By default, could move everywhere the source moves
  abstract IsContinuous: bool with get
  override this.IsContinuous with get() = c.IsContinuous


  interface ICursor<'K,'R> with
    member x.Current: KVP<'K,'R> = KVP( !state
    
    member x.Current: obj = 
      failwith "Not implemented yet"
    
    member x.CurrentBatch: IReadOnlyOrderedMap<'K,'R> = 
      failwith "Not implemented yet"
    
    member x.CurrentKey: 'K = 
      failwith "Not implemented yet"
    
    member x.CurrentValue: 'R = 
      failwith "Not implemented yet"
    
    member x.Dispose(): unit = 
      failwith "Not implemented yet"
    
    member x.IsContinuous: bool = 
      failwith "Not implemented yet"
    
    member x.MoveAt(index: 'K, direction: Lookup): bool = 
      failwith "Not implemented yet"
    
    member x.MoveFirst(): bool = 
      failwith "Not implemented yet"
    
    member x.MoveLast(): bool = 
      failwith "Not implemented yet"
    
    member x.MoveNext(): bool = 
      failwith "Not implemented yet"
    
    member x.MoveNextAsync(cancellationToken: Threading.CancellationToken): Threading.Tasks.Task<bool> = 
      failwith "Not implemented yet"
    
    member x.MoveNextBatchAsync(cancellationToken: Threading.CancellationToken): Threading.Tasks.Task<bool> = 
      failwith "Not implemented yet"
    
    member x.MovePrevious(): bool = 
      failwith "Not implemented yet"
    
    member x.Reset(): unit = 
      failwith "Not implemented yet"
    
    member x.Source: ISeries<'K,'R> = 
      failwith "Not implemented yet"
    
    member x.TryGetValue(key: 'K, value: byref<'R>): bool = 
      failwith "Not implemented yet"
    



// TODO generators