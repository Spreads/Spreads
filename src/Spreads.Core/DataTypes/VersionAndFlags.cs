using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// Version and flags
    /// 0
    /// 0 1 2 3 4 5 6 7 8
    /// +-+-+-+-+-+-+-+-+
    /// |  Ver  |R|D|C|B|
    /// +---------------+
    /// C - compressed
    /// D - diffed (if a type implements <see cref="IDelta{T}"/>)
    /// B - binary format. If not set then the payload is JSON,
    /// if set then payload is custom binary (payload could have
    /// it's own headers e.g. Blosc)
    /// R - reserved
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    public struct VersionAndFlags
    {
        internal const int VersionBitsOffset = 4;

        internal const int CompressedBitOffset = 0;

        internal const byte VersionMask = 0b_1111_0000;
        internal const byte BinaryFlagMask = 0b_0000_0001;
        internal const byte CompressedFlagMask = 0b_0000_0010;
        internal const byte DeltaFlagMask = 0b_0000_0100;

        private byte _value;

        public byte Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (byte)(_value >> VersionBitsOffset); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _value = (byte)((_value & ~VersionMask) | ((value << VersionBitsOffset) & VersionMask)); }
        }

        public bool IsCompressed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (_value & CompressedFlagMask) != 0; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value)
                {
                    _value |= CompressedFlagMask;
                }
                else
                {
                    _value = (byte)(_value & ~CompressedFlagMask);
                }
            }
        }

        public bool IsDelta
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (_value & DeltaFlagMask) != 0; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value)
                {
                    _value |= DeltaFlagMask;
                }
                else
                {
                    _value = (byte)(_value & ~DeltaFlagMask);
                }
            }
        }

        public bool IsBinary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (_value & BinaryFlagMask) != 0; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value)
                {
                    _value |= BinaryFlagMask;
                }
                else
                {
                    _value = (byte)(_value & ~BinaryFlagMask);
                }
            }
        }
    }
}