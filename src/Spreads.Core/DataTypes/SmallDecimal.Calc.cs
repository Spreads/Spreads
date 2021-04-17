// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    public readonly unsafe partial struct SmallDecimal
    {
        [StructLayout(LayoutKind.Explicit)]
        internal ref struct DecCalc
        {
            [FieldOffset(0)]
            internal uint uflags;

            [FieldOffset(4)]
            internal uint uhi;

            [FieldOffset(8)]
            internal uint ulo;

            [FieldOffset(12)]
            internal uint umid;

            /// <summary>
            /// The low and mid fields combined in little-endian order
            /// </summary>
            [FieldOffset(8)]
            internal ulong ulomidLE;

            // Sign mask for the flags field. A value of zero in this bit indicates a
            // positive Decimal value, and a value of one in this bit indicates a
            // negative Decimal value.
            internal const int SignShiftDc = 31;

            internal const uint SignMaskDc = 1u << 31;

            // Scale mask for the flags field. This byte in the flags field contains
            // the power of 10 to divide the Decimal value by. The scale byte must
            // contain a value between 0 and 28 inclusive.
            internal const int ScaleMaskDc = 0x00FF0000;

            // Number of bits scale is shifted by.
            internal const int ScaleShiftDc = 16;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DecCalc FromDecimal(decimal value)
            {
                return *(DecCalc*) &value;
            }

            internal int Scale
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (byte) ((uflags & ScaleMaskDc) >> ScaleShiftDc);
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => uflags = (uint) ((value << ScaleShiftDc) & ScaleMaskDc);
            }

            internal uint Negative
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (uflags & SignMaskDc) >> 31;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => uflags |= value << 31;
            }

            // for debug
            public override string ToString()
            {
                return $"{Convert.ToString(uflags, 2)} - scale {Scale} - hi {uhi} - lo {ulo} - mid {umid} - ulomidLE {ulomidLE}";
            }

            public void Truncate()
            {
                // if cannot truncate throw
                ThrowValueTooBigOrTooSmall();
            }
        }
    }
}
