// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads
{
    /// <summary>
    /// Direction to lookup data or move from a starting point
    /// </summary>
    public enum Lookup : byte
    {
        /// <summary>
        /// Less than, excludes a strating point even if it is present
        /// </summary>
        LT = 0,

        /// <summary>
        /// Less or equal
        /// </summary>
        LE = 1,

        /// <summary>
        /// Equal
        /// </summary>
        EQ = 2,

        /// <summary>
        /// Greater or equal
        /// </summary>
        GE = 3,

        /// <summary>
        /// Greater than, excludes a strating point even if it is present
        /// </summary>
        GT = 4
    }
}