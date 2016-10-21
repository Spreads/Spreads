// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace Spreads
{
    /// <summary>
    /// Direction to lookup data or move from a starting point
    /// </summary>
    public enum Lookup
    {
        /// <summary>
        /// Less than, excludes a strating point even if it is present
        /// </summary>
        LT,
        /// <summary>
        /// Less or equal
        /// </summary>
        LE,
        /// <summary>
        /// Equal
        /// </summary>
        EQ,
        /// <summary>
        /// Greater or equal
        /// </summary>
        GE,
        /// <summary>
        /// Greater than, excludes a strating point even if it is present
        /// </summary>
        GT
    }
}