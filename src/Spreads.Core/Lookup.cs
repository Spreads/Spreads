// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable InconsistentNaming

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.Native;

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

    internal static unsafe class LookupHelpers
    {
        // public static readonly ulong* LookupAdjustments = InitAdjustments();
        // public static readonly int* DirectionNotFoundAdj = InitDirectionNotFoundAdj();
        // public static readonly int* DirectionFoundAdjXnoEq = InitDirectionFoundAdjXnoEq();
        //
        // private static ulong* InitAdjustments()
        // {
        //     unchecked
        //     {
        //         var lookups = new Lookup[] {0, (Lookup) 1, (Lookup) 2, (Lookup) 3, (Lookup) 4, (Lookup) 5, (Lookup) 6};
        //
        //         var ptr = (ulong*) Mem.ZallocAligned((UIntPtr) 64, (UIntPtr) 64);
        //         for (int i = 0; i < lookups.Length; i++)
        //         {
        //             var lookup = lookups[i];
        //             // -1 if direction is less, +1 if direction is greater than
        //             var directionFoundAdj = (3 - ((int) lookup & 0b_101)) >> 1;
        //             // 1 if direction is less, 0 otherwise
        //             var directionNotFoundAdj = (((int) lookup & 0b_100) >> 2);
        //             // 1 if equality is not OK
        //             var noEq = (~((int) lookup) & 0b_010) >> 1;
        //
        //             var directionFoundAdjXnoEq = directionFoundAdj * noEq;
        //
        //             var storage = (((ulong) (uint) directionNotFoundAdj) << 32) | (uint) directionFoundAdjXnoEq;
        //             ptr[i] = storage;
        //         }
        //
        //         return ptr;
        //     }
        // }
        //
        // private static int* InitDirectionNotFoundAdj()
        // {
        //     {
        //         var lookups = new Lookup[] {0, (Lookup) 1, (Lookup) 2, (Lookup) 3, (Lookup) 4, (Lookup) 5, (Lookup) 6};
        //
        //         var ptr = (int*) Mem.ZallocAligned((UIntPtr) 64, (UIntPtr) 64);
        //         for (int i = 0; i < lookups.Length; i++)
        //         {
        //             var lookup = lookups[i];
        //             // 1 if direction is less, 0 otherwise
        //             var directionNotFoundAdj = (((int) lookup & 0b_100) >> 2);
        //             ptr[i] = directionNotFoundAdj;
        //         }
        //
        //         return ptr;
        //     }
        // }
        //
        // private static int* InitDirectionFoundAdjXnoEq()
        // {
        //     unchecked
        //     {
        //         var lookups = new Lookup[] {0, (Lookup) 1, (Lookup) 2, (Lookup) 3, (Lookup) 4, (Lookup) 5, (Lookup) 6};
        //
        //         var ptr = (int*) Mem.ZallocAligned((UIntPtr) 64, (UIntPtr) 64);
        //         for (int i = 0; i < lookups.Length; i++)
        //         {
        //             var lookup = lookups[i];
        //             // -1 if direction is less, +1 if direction is greater than
        //             var directionFoundAdj = (3 - ((int) lookup & 0b_101)) >> 1;
        //             // 1 if equality is not OK
        //             var noEq = (~((int) lookup) & 0b_010) >> 1;
        //
        //             var directionFoundAdjXnoEq = directionFoundAdj * noEq;
        //
        //             ptr[i] = directionFoundAdjXnoEq;
        //         }
        //
        //         return ptr;
        //     }
        // }

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