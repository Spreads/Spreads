// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads
{
    /// <summary>
    /// Base unit of a period
    /// </summary>
    public enum UnitPeriod : byte
    {
        Tick = 0,          //               100 nanosec
        Millisecond = 1,   //              10 000 ticks
        Second = 2,        //          10 000 000 ticks
        Minute = 3,        //         600 000 000 ticks
        Hour = 4,          //      36 000 000 000 ticks
        Day = 5,           //     864 000 000 000 ticks
        Month = 6,         //                  Variable

        /// <summary>
        /// Static or constant
        /// </summary>
        Eternity = 7,      //                  Infinity
    }

    public enum AppendOption
    {
        /// <summary>
        /// Throw if new keys overlap with existing keys.
        /// </summary>
        ThrowOnOverlap = 0,

        /// <summary>
        /// Ignore overlap if all new overlapped key are equal to existing, throw on unequal keys.
        /// </summary>
        IgnoreEqualOverlap = 1,

        /// <summary>
        /// Require that at least one (first) new key matches at least one (last) key of a map
        /// (foolproofing to avoid holes in the data). Existing values are not updated.
        /// </summary>
        RequireEqualOverlap = 2,

        /// <summary>
        /// Drop existing values starting from the first key of the new values and add all new values.
        /// </summary>
        DropOldOverlap = 3,

        /// <summary>
        /// Checks if all keys are equal in the overlap region and updates existing values with new ones.
        /// Throw if there are new keys in the overlap region or holes in new series.
        /// </summary>
        [Obsolete("TODO Not implemented in SM/SCM")]
        UpdateValuesIfAllOverlapKeysMatch = 4,

        /// <summary>
        /// Ignores equal overlap and updates the last value only.
        /// Throws if other values are different or keys do not match in the overlap region.
        /// </summary>
        //[Obsolete("TODO Not implemented in SM/SCM")]
        //UpdateLastValueOnly = 5
    }
}