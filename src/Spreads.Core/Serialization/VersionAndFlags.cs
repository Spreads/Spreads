using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Native;

namespace Spreads.Serialization
{
    // Currently we have to support old uncompressed json with TS, i.e. 0000_1000 case, cannot move T and B.

    /// These are for arrays only and should be a part of payload
    /// D - Data is stored a delta vs previous data point (snapshot) without this flag or array data is diffed vs the first value in an array (if a type implements <see cref="IDelta{T}"/>).
    /// S - Data is shuffled before compression.

    /// <summary>
    /// Version and flags
    /// </summary>
    /// <remarks>
    /// Format:
    /// 0 1 2 3 4 5 6 7 8
    /// +-+-+-+-+-+-+-+-+
    /// |0|Ver|R|R|CMP|B|
    /// +---------------+
    /// B - Binary format (read as "Not JSON"). If not set then the payload is JSON, if set then payload is blittable or custom binary.
    /// CMP - compression method:
    ///     00 - not compressed
    ///     01 - GZip
    ///     10 - Lz4
    ///     11 - Zstd
    /// R - reserved.
    /// Ver - <see cref="IBinarySerializer{T}.SerializerVersion"/> version.
    /// 0 - will need completely new layout when this is not zero.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    public struct VersionAndFlags
    {
        internal const int VersionBitsOffset = 5;
        internal const int CompressionBitsOffset = 1;

        internal const byte SerializationFormatMask = 0b_0000_0111;

        internal const byte BinaryFlagMask = 0b_0000_0001;

        internal const byte CompressionMethodMask = 0b_0000_0110;

        internal const byte VersionMask = 0b_0110_0000;

        private byte _value;

        public byte ConverterVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(_value >> VersionBitsOffset);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~VersionMask) | ((value << VersionBitsOffset) & VersionMask));
        }

        public CompressionMethod CompressionMethod
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (CompressionMethod)((_value & CompressionMethodMask) >> CompressionBitsOffset);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~CompressionMethodMask) | (((int)value << CompressionBitsOffset) & CompressionMethodMask));
        }

        public SerializationFormat SerializationFormat
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (SerializationFormat)(_value & SerializationFormatMask);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~SerializationFormatMask) | ((int)value & SerializationFormatMask));
        }

        /// <summary>
        /// Compressed using raw deflate for JSON or using Blosc library with method encoded in Blosc header.
        /// </summary>
        [Obsolete("TODO Check all usages by renaming it")]
        public bool IsCompressed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CompressionMethod != CompressionMethod.None;
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
    }
}
