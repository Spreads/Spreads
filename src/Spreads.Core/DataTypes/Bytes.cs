// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BinarySerialization(TypeEnum.FixedBinary, Size)]
    internal unsafe struct Bytes16
    {
        private const int Size = 16;
        private fixed byte Bytes[Size];
    }

    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BinarySerialization(TypeEnum.FixedBinary, Size)]
    internal unsafe struct Bytes32
    {
        private const int Size = 32;
        private fixed byte Bytes[Size];
    }

    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BinarySerialization(TypeEnum.FixedBinary, Size)]
    internal unsafe struct Bytes64
    {
        private const int Size = 32;
        private fixed byte Bytes[Size];
    }
}
