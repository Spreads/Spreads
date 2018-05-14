using Spreads.Serialization;
using System.Runtime.InteropServices;

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
