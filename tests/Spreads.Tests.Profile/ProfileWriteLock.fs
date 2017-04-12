namespace Spreads.Tests.Profile

open Spreads
open System.Runtime.CompilerServices

[<Sealed>]
type LockTestSeries()=
  inherit BaseSeries<int, int>()

  [<DefaultValueAttribute>]
  val mutable counter : int64

  member this.Counter = this.counter

  [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
  member this.Increment() : unit =
      let mutable v2 = 0L;
      try
        try
          ()
        finally
          v2 <- this.BeforWrite()
        this.counter <- this.counter + 1L
      finally
        this.AfterWrite(v2, true);
      

  override this.Comparer: System.Collections.Generic.IComparer<int> = 
    raise (System.NotImplementedException())
  override this.First: System.Collections.Generic.KeyValuePair<int,int> = 
    raise (System.NotImplementedException())
  override this.GetAt(idx: int): int = 
    raise (System.NotImplementedException())
  override this.GetCursor(): ICursor<int,int> = 
    raise (System.NotImplementedException())
  member this.GetEnumerator(): IAsyncEnumerator<System.Collections.Generic.KeyValuePair<int,int>> = 
    raise (System.NotImplementedException())
  override this.IsEmpty: bool = 
    raise (System.NotImplementedException())
  override this.IsIndexed: bool = 
    raise (System.NotImplementedException())
  override this.IsReadOnly: bool = 
    raise (System.NotImplementedException())
  override this.Item
    with get (key: int): int = raise (System.NotImplementedException())
  override this.Keys: System.Collections.Generic.IEnumerable<int> = 
    raise (System.NotImplementedException())
  override this.Last: System.Collections.Generic.KeyValuePair<int,int> = 
    raise (System.NotImplementedException())
  override this.TryFind(key: int, direction: Lookup, value: byref<System.Collections.Generic.KeyValuePair<int,int>>): bool = 
    raise (System.NotImplementedException())
  override this.TryGetFirst(value: byref<System.Collections.Generic.KeyValuePair<int,int>>): bool = 
    raise (System.NotImplementedException())
  override this.TryGetLast(value: byref<System.Collections.Generic.KeyValuePair<int,int>>): bool = 
    raise (System.NotImplementedException())
  override this.TryGetValue(key: int, value: byref<int>): bool = 
    raise (System.NotImplementedException())
  override this.Values: System.Collections.Generic.IEnumerable<int> = 
    raise (System.NotImplementedException())
