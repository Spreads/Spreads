using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.DataTypes
{

    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Bytes16
    {
        private const int Size = 16;
        private fixed byte Bytes[Size];
    }

    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Bytes32
    {
        private const int Size = 32;
        private fixed byte Bytes[Size];
    }

    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = Size)]
    public unsafe struct Bytes64
    {
        private const int Size = 32;
        private fixed byte Bytes[Size];
    }

}
