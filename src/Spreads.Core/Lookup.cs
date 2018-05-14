// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable InconsistentNaming
namespace Spreads
{
    /// <summary>
    /// Direction to lookup data or move from a starting point.
    /// </summary>
    public enum Lookup : byte
    {
        // TODO Review if binary representation could be useful. In general we should not depend on enum concrete values. 
        // equlity & direction: three bits
        // middle bit = equality is OK
        // left bit = less
        // right bit - greater
        
        /// <summary>
        /// Less than.
        /// </summary>
        LT = 0b_0000_0100, // 4

        /// <summary>
        /// Less or equal.
        /// </summary>
        LE = 0b_0000_0110, // 6

        /// <summary>
        /// Equal.
        /// </summary>
        EQ = 0b_0000_0010, // 2

        /// <summary>
        /// Greater or equal.
        /// </summary>
        GE = 0b_0000_0011, // 3

        /// <summary>
        /// Greater than.
        /// </summary>
        GT = 0b_0000_0001 // 1
        
        // we could continue: 111 - whatever, 000 - no value, 101 - not equal
    }
}