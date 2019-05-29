// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads

open System.Collections.Generic
open System.Runtime.InteropServices

[<Interface>]
[<AllowNullLiteral>]
type IImmutableSeries<'K,'V> =
  inherit ISeries<'K,'V>
  abstract Size: int64 with get
  /// Unchecked addition, returns a new version of map with new element added
  abstract Add : k:'K*v:'V -> IImmutableSeries<'K,'V>
  /// Checked addition, checks that new element's key is larger/later than the Last element's key
  /// and returns a new version of map with new element added
  abstract AddLast : k:'K*v:'V -> IImmutableSeries<'K,'V>
  /// Checked addition, checks that new element's key is smaller/earlier than the First element's key
  /// and returns a new version of map with new element added
  abstract AddFirst : k:'K*v:'V -> IImmutableSeries<'K,'V>
  /// Remove a key and its value from the map. When the key is not a member of the map, the original map is returned.
  abstract Remove : k:'K -> IImmutableSeries<'K,'V>
  abstract RemoveLast: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> IImmutableSeries<'K,'V>
  abstract RemoveFirst: [<Out>]value: byref<KeyValuePair<'K, 'V>> -> IImmutableSeries<'K,'V>
  abstract RemoveMany: k:'K * direction:Lookup -> IImmutableSeries<'K,'V>

