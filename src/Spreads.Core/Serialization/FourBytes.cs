// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    [StructLayout(LayoutKind.Explicit, Size = 4, Pack = 1)]
    internal struct FourBytes
    {
        [FieldOffset(0)]
        public byte B0;

        [FieldOffset(1)]
        public byte B1;

        [FieldOffset(2)]
        public byte B2;

        [FieldOffset(3)]
        public byte B3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int QuoteIndex(out bool unescaped)
        {
            const byte quote = (byte)'\"';
            const byte bs = (byte)'\\';

            if (B0 == quote)
            {
                unescaped = false;
                return 0;
            }

            if (B1 == quote)
            {
                unescaped = B0 != bs;
                return 1;
            }

            if (B2 == quote)
            {
                unescaped = B1 != bs;
                return 2;
            }

            if (B3 == quote)
            {
                unescaped = B2 != bs;
                return 3;
            }

            unescaped = false;
            return -1;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 8, Pack = 1)]
    internal struct EightBytes
    {
        [FieldOffset(0)]
        public byte B0;

        [FieldOffset(1)]
        public byte B1;

        [FieldOffset(2)]
        public byte B2;

        [FieldOffset(3)]
        public byte B3;

        [FieldOffset(4)]
        public byte B4;

        [FieldOffset(5)]
        public byte B5;

        [FieldOffset(6)]
        public byte B6;

        [FieldOffset(7)]
        public byte B7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int QuoteIndex(out bool unescaped)
        {
            const byte quote = (byte)'\"';
            const byte bs = (byte)'\\';

            if (B0 == quote)
            {
                unescaped = false;
                return 0;
            }

            if (B1 == quote)
            {
                unescaped = B0 != bs;
                return 1;
            }

            if (B2 == quote)
            {
                unescaped = B1 != bs;
                return 2;
            }

            if (B3 == quote)
            {
                unescaped = B2 != bs;
                return 3;
            }

            if (B4 == quote)
            {
                unescaped = B3 != bs;
                return 4;
            }

            if (B5 == quote)
            {
                unescaped = B4 != bs;
                return 5;
            }

            if (B6 == quote)
            {
                unescaped = B5 != bs;
                return 6;
            }

            if (B7 == quote)
            {
                unescaped = B6 != bs;
                return 7;
            }

            unescaped = false;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int QuoteIndex3(out bool unescaped)
        {
            const byte quote = (byte)'\"';
            const byte bs = (byte)'\\';

            unescaped = false;

            if (B3 == quote || B2 == quote || B1 == quote || B0 == quote)
            {
                return 0;
            }

            // var x = &(B0 == quote)

            //if (B0 == quote)
            //{
            //    unescaped = false;
            //    return 0;
            //}

            //if (B1 == quote)
            //{
            //    unescaped = B0 != bs;
            //    return 1;
            //}

            //if (B2 == quote)
            //{
            //    unescaped = B1 != bs;
            //    return 2;
            //}

            //if (B3 == quote)
            //{
            //    unescaped = B2 != bs;
            //    return 3;
            //}

            return -1;
        }
    }
}