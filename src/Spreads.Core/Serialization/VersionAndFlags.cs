using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    // Currently we have to support old uncompressed json with TS, i.e. 0000_1000 case
    // With new flag set we could only change this once, so the R flag will mean that 
    // additional params are stored in the next bytes.

    /// <summary>
    /// Version and flags
    /// 0
    /// 0 1 2 3 4 5 6 7 8
    /// +-+-+-+-+-+-+-+-+
    /// |CNV|S|D|T|ZLD|B|
    /// +---------------+
    /// B - Binary format (read as "Not JSON"). If not set then the payload is JSON, if set then payload is blittable or custom binary.
    /// ZLD - compression method (Zstd/Lz4/Deflate/None):
    ///     00 - not compressed
    ///     01 - Raw Deflate
    ///     10 - Lz4
    ///     11 - Zstd
    /// T - Timestamped. A value has Timestamp (8 bytes) as the first element of payload. It is included in payload length for varsized types.
    /// D - Data is stored a delta vs previous data point (snapshot) without this flag or array data is diffed vs the first value in an array (if a type implements <see cref="IDelta{T}"/>).
    /// S - Data is shuffled before compression.
    /// CNV - Converter version.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    public struct VersionAndFlags
    {
        internal const int VersionBitsOffset = 7;

        internal const byte BinaryFlagMask = 0b_0000_0001;
        internal const byte CompressedFlagMask = 0b_0000_0010;
        internal const byte DeltaOrDeflateFlagMask = 0b_0000_0100;
        internal const byte TimestampFlagMask = 0b_0000_1000;
        internal const byte VersionMask = 0b_1000_0000;
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
            get => (_value & DeltaOrDeflateFlagMask) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value)
                {
                    _value |= DeltaOrDeflateFlagMask;
                }
                else
                {
                    _value = (byte)(_value & ~DeltaOrDeflateFlagMask);
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