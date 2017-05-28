// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    // TODO

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 4)]
    internal unsafe struct UnitOfMeasure
    {
        // note that if we put invalid byte instead of alphanumeric, e.g. '_', we have 2^16 options for other UoMs, probably more than enough
        [FieldOffset(0)]
        private fixed byte _isoCode[3];

        [FieldOffset(1)]
        private readonly byte _scale;        // byte

        // 2 bytes reserved
        [FieldOffset(2)]
        private readonly short _reserved;
    }
}