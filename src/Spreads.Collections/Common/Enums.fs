// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads
open System


/// Defines how IOrderedMap.Append behaves
type AppendOption =
  /// Throw if new values overlap with existing values.
  | ThrowOnOverlap = 0
  /// Ignore overlap if all new key and values are equal to existing, throw on unequal keys/values.
  | IgnoreEqualOverlap = 1
  /// Require that at least one (first) new value matches at least one (last) value of a map (foolproofing).
  | RequireEqualOverlap = 2
  /// Drop existing values starting from the first key of the new values and add all new values.
  | DropOldOverlap = 3
  /// Checks if all keys are equal in the overlap region and updates existing values with new ones. Throw if there are new keys in the overlap region or holes in new series.
  | [<Obsolete("TODO Not implemented in SM/SCM")>] UpdateValuesIfAllOverlapKeysMatch = 4
  /// Ignores equal overlap and updates the last value only. Throws if other values are different or keys do not match in the overlap region.
  | [<Obsolete("TODO Not implemented in SM/SCM")>] UpdateLastValueOnly = 5


/// Base unit of a period
type UnitPeriod =
  | Tick = 0uy          //               100 nanosec
  | Millisecond = 1uy   //              10 000 ticks
  | Second = 2uy        //          10 000 000 ticks
  | Minute = 3uy        //         600 000 000 ticks
  | Hour = 4uy          //      36 000 000 000 ticks
  | Day = 5uy           //     864 000 000 000 ticks
  | Month = 6uy         //                  Variable
  /// Static or constant
  | Eternity = 7uy      //                  Infinity

  // NB
  // (do not add this, keep this enum representable as 3 bits. 4-8 are probably also OK, but we already have data that use the 2,3,4,5 numbers)
  // ... MicroSecond = 8   //                  10 ticks 
  // (this is bad numbering. initial thought was that we do not need sub-millisecond periods
  // because they are all point of times, or just use ticks - they are just 10x of microseconds. Every subsecond (even sub-day) period is 
  // representable as a number of ticks, millis are mostly for convenience)
  // e.g. (2^15)*tick > 3 millisecs, so we could use short for period length and cover all common cases. And long covers all possible cases.