namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading.Tasks

/// <summary>
/// IComparer<'T> with additional methods for regular keys
/// </summary>
// TODO rename it to key comparer
[<AllowNullLiteral>]
type ISpreadsComparer<'K>= // when 'K : comparison

  // 

  inherit IComparer<'K>
  /// Returns int32 distance between two values when they are stored in 
  /// a regular sorted map. Regular means continuous integers or days or seonds, etc.
  /// This method could be used for IComparer<'K>.Compare implementation.
  abstract Diff : a:'K * b:'K -> int
  /// If Diff(A,B) = X, then Add(A,X) = B, this is a mirrow method for Diff
  abstract Add : 'K * diff:int -> 'K
  /// Generates an order-preserving hash.
  /// The hashes are used as bucket keys and should be a 
  /// http://en.wikipedia.org/wiki/Monotonic_function
  abstract Hash: k:'K -> 'K
  /// <summary>
  /// Get UInt64 representation of a key.
  /// In general, (a.AsUInt64 - b.AsUInt64) is not equal to ISpreadsComparer.Diff(a,b), e.g. for non-tick TimePeriod.
  /// </summary>
  abstract AsUInt64: k:'K -> uint64
  /// <summary>
  /// Get a key from its UInt64 representation.
  /// In general, (a.AsUInt64 - b.AsUInt64) is not equal to ISpreadsComparer.Diff(a,b), e.g. for non-tick TimePeriod.
  /// </summary>
  abstract FromUInt64: uint64 -> 'K


//type IKeyedValue<'K, 'V when 'K : comparison>=
//  inherit IComparer<IKeyedValue<'K, 'V>>
//  abstract Key : 'K with get
//  abstract Value : 'V with get


[<Obsolete("Not sure this abstraction needed")>]
[<AllowNullLiteral>]
type IPointer<'K when 'K : comparison> =
    inherit IEnumerator<'K>
    abstract MoveAt: index:'K * direction:Lookup -> bool
    abstract MoveFirst: unit -> bool
    abstract MoveLast: unit -> bool
    abstract MovePrevious: unit -> bool
    abstract CurrentKey:'K with get
    abstract MoveGetNextAsync: unit -> Task<'K>
    abstract Source : IReadOnlySortedKeys<'K> with get
and
  [<Obsolete("Not sure this abstraction needed, rename to SortedSet")>]
  [<AllowNullLiteral>]
  IReadOnlySortedKeys<'K when 'K : comparison> =
    inherit IEnumerable<'K>
    inherit IEnumerable
    abstract IsEmpty: bool with get
    [<Obsolete("IReadOnlySortedMap should not have this, the interface is endless")>]
    abstract Size: int64 with get
    abstract IsIndexed : bool with get
    abstract First : 'K with get
    abstract Last : unit -> 'K with get
    abstract Item : 'K -> 'K with get
    abstract Keys : IEnumerable<'K> with get
    abstract TryFind: key:'K * direction:Lookup * [<Out>] value: byref<'K> -> bool
    abstract TryGetFirst: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetLast: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool
    abstract GetPointer : unit -> IPointer<'K>
    abstract SyncRoot : obj with get

/// IPointer is an advanced enumerator supporting moves to first, last, previous, next, exact 
/// positions and relative LT/LE/GT/GE moves.
/// Pointer is resilient to changes in an underlying sequence during movements, e.g. the
///  sequence could grow during move next.
/// DefaultPointer class uses TryFind method of IReadOnlySortedMap interface for navigation with O(log n)
/// complexity of each movement (however optimizations are possible, e.g. in SortedMap class)
[<AllowNullLiteral>]
type IPointer<'K,'V when 'K : comparison> =
    inherit IEnumerator<KVP<'K, 'V>>
    /// Puts the pointer to the position according to LookupDirection
    abstract MoveAt: index:'K * direction:Lookup -> bool
    abstract MoveFirst: unit -> bool
    abstract MoveLast: unit -> bool
    abstract MovePrevious: unit -> bool
    abstract CurrentKey:'K with get
    abstract CurrentValue:'V with get
    /// Moves next and returns a pair at the next position.
    /// Returns a cancelled task if there are no more values.
    /// If the source map is IObservable and there are no more values
    /// then the pointer subscribes to the IObservable:
    /// After OnNext MoveGetNextAsync will return the new value;
    /// After OnCompleted MoveGetNextAsync will return a cancelled task;
    /// After OnError MoveGetNextAsync will return a faulted task with an exception from IObservable.
    abstract MoveGetNextAsync: unit -> Task<KVP<'K,'V>>
    abstract Source : IReadOnlySortedMap<'K,'V> with get
and
  /// Important! 'Read-only' doesn't mean that the object is immutable or not changing. It only means
  /// that there is no methods to change the map *from* this interface, without any assumptions about 
  /// the implementation. Underlying sequence could be mutable and rapidly changing; to prevent any 
  /// changes use lock (Monitor.Enter) on the SyncRoot property. Doing so will block any changes for 
  /// mutable implementations and won't affect immutable implementations.
  [<AllowNullLiteral>]
  IReadOnlySortedMap<'K,'V when 'K : comparison> =
    inherit IEnumerable<KVP<'K,'V>>
    inherit IEnumerable
    /// True if this.size = 0
    abstract IsEmpty: bool with get
    [<Obsolete("IReadOnlySortedMap should not have this, the interface is endless")>]
    abstract Size: int64 with get
    /// If true then elements are sorted by order of addition (index) and not by keys
    abstract IsIndexed : bool with get

    /// First element, throws InvalidOperationException if empty
    abstract First : KVP<'K, 'V> with get

    /// Last element, throws InvalidOperationException if empty
    abstract Last : unit -> KVP<'K, 'V> with get

    /// Values at key, throws KeyNotFoundException if key is not found
    abstract Item : 'K -> 'V with get

    abstract Keys : IEnumerable<'K> with get

    abstract Values : IEnumerable<'V> with get

    /// The method finds value according to direction, returns false if it could not find such a value
    /// For indexed series LE/GE directions are invalid (throws InvalidOperationException), while
    /// LT/GT search is done by index rather than by key and possible only when a key exists.
    abstract TryFind: key:'K * direction:Lookup * [<Out>] value: byref<KVP<'K, 'V>> -> bool
    
    abstract TryGetFirst: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetLast: [<Out>] value: byref<KVP<'K, 'V>> -> bool
    abstract TryGetValue: key:'K * [<Out>] value: byref<'V> -> bool

    /// Get pointer, which is an advanced enumerator supporting moves to first, last, previous, next, exact 
    /// positions and relative LT/LE/GT/GE moves
    abstract GetPointer : unit -> IPointer<'K,'V>

    abstract SyncRoot : obj with get


/// Main differences between immutable and mutable sorted maps:
/// * An immutable map returns a new version of itself on each mutation,
///   while mutable map changes data in-place;
/// * An immutable map doesn't have an Item setter (impossible to use a return 
///   type of the map) - instead use Add method that will return a new map if a 
///   key exists and new/old values is different or the same map if new value is the same;
/// * Mutable map will throw on addition of a key that already exists, use Item setter 
///   to set value for existing key.

/// Mutable sorted map
[<AllowNullLiteral>]
type ISortedMap<'K,'V when 'K : comparison> =
  inherit IEnumerable<KeyValuePair<'K,'V>>
  inherit IReadOnlySortedMap<'K,'V>

  abstract Item : 'K -> 'V with get,set

  // size is related to physical representation, could not be a part of ISeries
  // because of ephemeral Series, e.g. Windows that do not have a size but are navigable
  //abstract Size: int64 with get

  /// Adds new key and value to map, throws if the key already exists
  abstract Add : k:'K*v:'V -> unit

  /// Checked addition, checks that new element's key is larger/later than the Last element's key
  /// and adds element to this map
  /// throws ArgumentOutOfRangeException if new key is smaller than the last
  abstract AddLast : k:'K*v:'V -> unit

  /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
  /// and adds element to this map
  /// throws ArgumentOutOfRangeException if new key is larger than the first
  abstract AddFirst : k:'K*v:'V -> unit

  /// Remove a key and its value in-place.
  abstract Remove : k:'K -> bool
  abstract RemoveLast: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> unit
  abstract RemoveFirst: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> unit
  abstract RemoveMany: k:'K * direction:Lookup -> unit
  //abstract TryFindWithIndex: key:'K*direction:Lookup * [<Out>]result: byref<KeyValuePair<'K, 'V>> -> int


[<AllowNullLiteral>]
type IVirtualSortedMap<'K,'V when 'K : comparison>  =
  inherit ISortedMap<'K,'V>
  inherit IDisposable

[<AllowNullLiteral>]
type IImmutableSortedMap<'K,'V when 'K : comparison> =
  inherit IEnumerable<KeyValuePair<'K,'V>>
  inherit IReadOnlySortedMap<'K,'V>

  // size is related to physical representation, could not be a part of ISeries
  // because of ephemeral Series, e.g. Windows that do not have a size but are navigable
  //abstract Size: int64 with get

  /// Unchecked addition, returns a new version of map with new element added
  abstract Add : k:'K*v:'V -> IImmutableSortedMap<'K,'V>

  /// Checked addition, checks that new element's key is larger/later than the Last element's key
  /// and returns a new version of map with new element added
  abstract AddLast : k:'K*v:'V -> IImmutableSortedMap<'K,'V>

  /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
  /// and returns a new version of map with new element added
  abstract AddFirst : k:'K*v:'V -> IImmutableSortedMap<'K,'V>

  /// Remove a key and its value from the map. When the key is not a member of the map, the original map is returned.
  abstract Remove : k:'K -> IImmutableSortedMap<'K,'V>
  abstract RemoveLast: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> IImmutableSortedMap<'K,'V>
  abstract RemoveFirst: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> IImmutableSortedMap<'K,'V>
  abstract RemoveMany: k:'K * direction:Lookup -> IImmutableSortedMap<'K,'V>


// marker interfaces, 'K is the minimum type we must know
[<AllowNullLiteral; Obsolete("Rethink it")>]
type IPanel<'K when 'K : comparison> = interface end
[<AllowNullLiteral; Obsolete("Rethink it")>]
type ISeries<'K when 'K : comparison> = 
    interface
        inherit IPanel<'K>
    end


// ISeries doesn't have any mutable properties or mutating methods, but implementation could 
// be either mutable or immutable

[<AllowNullLiteral>]
type ISeries<'K,'V when 'K : comparison> =   
    inherit IEnumerable<KVP<'K,'V>>
    inherit IEnumerable
    inherit IReadOnlySortedMap<'K,'V>
    inherit ISeries<'K>
    inherit IPanel<'K,'V> // TODO Series is always a column-untyped panel of one series
        
    /// How to substitute missing values and resolve conflicts in zip/join operations for series.
    abstract MissingBehavior : MissingBehavior with get // TODO? if it is default, why method? delete it and just set Inner as default?? or remove the word 'Default' if e.g. Window could have 'Continous'

/// Panel without column types. Conceptually panel is a sequence of series.
and [<AllowNullLiteral; Obsolete("Rethink it")>] IPanel<'K,'V when 'K : comparison> =
    abstract Series: seq<ISeries<'K,'V>> with get
    
[<AllowNullLiteral; Obsolete("Rethink it")>]
type IPanel<'TRowKey,'TColumnKey, 'TValue when 'TRowKey: comparison and 'TColumnKey : comparison> =
    inherit IPanel<'TRowKey>
    inherit IPanel<'TRowKey, 'TValue>
    abstract Columns: ISeries<'TColumnKey, ISeries<'TRowKey,'TValue>> with get
    abstract Rows: ISeries<'TRowKey, ISeries<'TColumnKey,'TValue>> with get
    /// Apply a function for each column and returns a panel backed by ISeries after function application
    /// e.g.  in C#: var newPanel = oldPanel.Columnwise(c => c.Repeat())
    abstract ColumnWise : (Func<ISeries<'TRowKey,'TValue>,ISeries<'TRowKey,'TValue>>) -> IPanel<'TRowKey,'TColumnKey, 'TValue>
    abstract RowWise : (Func<ISeries<'TColumnKey,'TValue>,ISeries<'TColumnKey,'TValue>>) -> IPanel<'TRowKey,'TColumnKey, 'TValue>
    /// Apply a function to each value, alias to map
    abstract PointWise : (Func<ISeries<'TColumnKey,'TValue>,ISeries<'TColumnKey,'TValue>>) -> IPanel<'TRowKey,'TColumnKey, 'TValue>


