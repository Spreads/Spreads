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

        internal const byte VersionMask = 0b_1111_0000;
        internal const byte BinaryFlagMask = 0b_0000_0001;
        internal const byte CompressedFlagMask = 0b_0000_0010;
        internal const byte DeltaFlagMask = 0b_0000_0100;
        internal const byte TimestampFlagMask = 0b_0000_1000;

        private byte _value;

        public byte Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(_value >> VersionBitsOffset);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~VersionMask) | ((value << VersionBitsOffset) & VersionMask));
        }

        /// <summary>
        /// Compressed using raw deflate for JSON or using Blosc library with method encoded in Blosc header.
        /// </summary>
        public bool IsCompressed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & CompressedFlagMask) != 0;
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

        /// <summary>
        /// Values are stored as deltas.
        /// </summary>
        public bool IsDelta
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & DeltaFlagMask) != 0;
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

        /// <summary>
        /// Not JSON fallback but some custom layout (blittable or manual pack).
        /// </summary>
        public bool IsBinary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & BinaryFlagMask) != 0;
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

        public bool IsTimestamped
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & TimestampFlagMask) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value)
                {
                    _value |= TimestampFlagMask;
                }
                else
                {
                    _value = (byte)(_value & ~TimestampFlagMask);
                }
            }
        }
    }
}