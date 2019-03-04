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

            if (B3 == quote)
            {
                unescaped = B2 != bs;
                return 3;
            }

            if (B2 == quote)
            {
                unescaped = B1 != bs;
                return 2;
            }

            if (B1 == quote)
            {
                unescaped = B0 != bs;
                return 1;
            }

            unescaped = false;

            if (B0 == quote)
            {
                return 0;
            }

            return -1;
        }
    }
}