namespace Spreads

type internal LaggedState<'K,'V> =
  struct
    val mutable Current : KVP<'K,'V>
    val mutable Previous : KVP<'K,'V>
    new(c,p) = {Current = c; Previous = p}
  end