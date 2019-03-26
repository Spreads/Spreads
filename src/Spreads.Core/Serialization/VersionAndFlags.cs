using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// Version and flags
    /// </summary>
    /// <remarks>
    /// Format:
    /// ```
    /// 0 1 2 3 4 5 6 7 8
    /// +-+-+-+-+-+-+-+-+
    /// |V|E|S|H|T|CMP|B|
    /// +---------------+
    ///
    /// B - Binary format (read as "Not JSON"). If not set then the payload is JSON,
    /// if set then payload is blittable or custom binary.
    ///
    /// CMP - Compression method:
    ///     00 - not compressed
    ///     01 - GZip
    ///     10 - Lz4
    ///     11 - Zstd
    ///
    /// V - Version. **Must be zero**. Otherwise there is a different header layout
    /// version that we hopefully will never have to implement. But if that sad
    /// day comes the existing data could be read with just a single if branch
    /// until it is totally converted to a new format (and yes, the format change
    /// must be so important that converting all data is justified, e.g.
    /// "Weissman score 5.2 vs 2.89" in some new compression codec or similar stuff :P ).
    ///
    /// The following flags are not used by BinarySerializer, which only uses
    /// three <see cref="SerializationFormat"/> bits.
    /// These flags indicate that input payload has additional parts,
    /// such as 8 bytes timestamp, 16 bytes authentication tag,
    /// 32 bytes hash and/or 64 bytes signature. BinarySerializer
    /// ignores these flags but its input data must be limited
    /// to the type described in <see cref="DataTypeHeader"/>,
    /// of which <see cref="VersionAndFlags"/> is the first byte.
    ///
    /// If all flags are set then the pipeline is:
    /// 1. Decrypt. Will fail on tampered payload due to *AE* in AEAD.
    /// 2. Deserialize payload. *AD* should have <see cref="DataTypeHeader"/>.
    /// 3. Calculate hash and compare with the present one. If they do not match throw.
    ///    This helps to find data corruption before encryption (which is optional).
    /// 4. Verify signature from the hash. Public key is in application context.
    /// 5. Timestamp is part of AD and must be valid if step 1 succeeds.
    ///    Message is now verified for tampering in transit, corruption due to bad
    ///    algorithm or storage
    ///
    /// T - Has timestamp
    ///
    /// H - Has BLAKE2B hash
    ///
    /// S - Has Ed25519 signature
    ///
    /// E - Is encrypted with AES256-GCM
    ///
    /// R - reserved bits
    ///
    /// ```
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    public struct VersionAndFlags
    {
        internal const int CompressionBitsOffset = 1;

        // 0 1 2 3 4 5 6 7 8
        // +-+-+-+-+-+-+-+-+
        // |V|E|S|H|T|CMP|B|
        // +---------------+

        internal const byte SerializationFormatMask = 0b_0000_0111;

        internal const byte IsBinaryMask = 0b_0000_0001;

        internal const byte CompressionMethodMask = 0b_0000_0110;

        internal const byte HasTimestampMask = 0b_0000_1000;
        internal const byte HasHashMask = 0b_0001_0000;
        internal const byte HasSignatureMask = 0b_0010_0000;
        internal const byte IsEncryptedMask = 0b_0100_0000;
        internal const byte VersionMask = 0b_1000_0000;

        private byte _value;

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
        /// Not JSON fallback but some custom layout (blittable or manual pack).
        /// </summary>
        public unsafe bool IsBinary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & IsBinaryMask) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~IsBinaryMask) | *(int*)&value);
        }

        public unsafe bool HasTimestamp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & HasTimestampMask) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~HasTimestampMask) | *(int*)&value);
        }

        public unsafe bool HasHash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & HasHashMask) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~HasHashMask) | *(int*)&value);
        }

        public unsafe bool HasSignature
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & HasSignatureMask) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~HasSignatureMask) | *(int*)&value);
        }

        public unsafe bool IsEncrypted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & IsEncryptedMask) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _value = (byte)((_value & ~IsEncryptedMask) | *(int*)&value);
        }

        public bool IsVersionZero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & VersionMask) == 0;
        }
    }
}
