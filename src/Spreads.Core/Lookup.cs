// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable InconsistentNaming

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads
{
    /// <summary>
    /// Direction to lookup data or move from a starting point.
    /// </summary>
    [Flags]
    public enum Lookup : byte
    {
        // NB: do not change binary, we depend on it not only in the helper
        // TODO move all methods that depend on binary layout to the helper

        // equality & direction: three bits
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

    internal static class LookupHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEqualityOK(this Lookup lookup)
        {
            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            return (lookup & Lookup.EQ) != 0;
        }

        /// <summary>
        /// Returns 1 if Lookup is LT or LE.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComplementAdjustment(this Lookup lookup)
        {
            return (int) (((uint) lookup & (uint) Lookup.LT) >> 2);
        }

        /// <summary>
        /// Returns offset to add to found index when equality is not valid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NeqOffset(this Lookup lookup)
        {
            // TODO review if branchless possible

            if (((int) lookup & (int) (Lookup.GT)) != 0)
            {
                return 1;
            }

            Debug.Assert(((int) lookup & (int) (Lookup.LT)) != 0);
            return -1;
        }
    }
}