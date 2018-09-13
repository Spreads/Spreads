using System;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// Preferred serialization method. Binary is used only for blittable types or when IBinaryConverter is present.
    /// Compression is done only when compressed size is not larger than the original size (usually it works, but it is
    /// possible that data will not be compressed and providing this enum to serializer means best-effort, not a guarantee).
    ///
    /// Binary serialization format. Serialized data always has a header describing the method of
    /// binary serialization. The data could be Json, but because of the header it is not directly
    /// consumable by e.g. browsers. However, for Json/JsonGZip cases the only difference is
    /// the header and the rest payload could be directly sent to a browser expecting 'application/json'
    /// MIME type.
    /// </summary>
    /// <remarks>
    /// JsonGZip:
    /// Objects are converted to Json and compressed with JsonGZip method. This format is compatible with raw
    /// http payload with 'Accept-encoding: gzip' headers past the 8 bytes header.
    ///
    /// BinaryLz4/Zstd:
    /// Binary is stored as blittable representation with Blosc byteshuffle for arrays with fixed-size elements.
    /// Then serialized (and byteshuffled) buffer is compressed with LZ4 (super fast but lower compression) or
    /// Zstd (still fast and high compression, preferred method) if compression gives smaller size.
    /// Actual buffer layout is type dependent.
    ///
    /// Serialized buffer has a header with a flag indicating the format and
    /// wether or not the buffer is compressed. Some data types are stored as deltas
    /// with diffing performed before byteshuffling and the header must have such flag as well.
    ///
    /// SerializationFormat should not be confused with Transport/Protocol format:
    /// * Text trasport/protocol supports only JSON and the payload is plain or gzipped JSON that
    ///   could be consumed by browsers or http servers (with headers indicating the actual format - compressed or not).
    /// * Binary trasport/protocol cannot be consumed directly by browsers or http servers. The exact data layout of payload
    ///   depends on data type and is encoded in a 4 (+ optional 4 bytes length for variable sized data) bytes header.
    ///   Data could be serialized as JSON (mostly for convenience if a type is not used a lot and not blittable)
    ///   but it is still a binary format, though one that could be trivially converted to JSON.
    /// </remarks>
    public enum SerializationFormat : byte
    {
        /// <summary>
        /// Custom binary format without compression.
        /// </summary>
        Binary = (CompressionMethod.None << 1) | VersionAndFlags.BinaryFlagMask,

        /// <summary>
        /// Well-balanced compression with good x-plat support.
        /// </summary>
        [Obsolete("Hide from intellisense. Prefer Zstd/Lz4 for binary.")]
        BinaryGZip = (CompressionMethod.GZip << 1) | VersionAndFlags.BinaryFlagMask,

        /// <summary>
        /// Fast compression, larger size.
        /// </summary>
        /// <remarks>
        /// Use blittable reprezentation, byteshuffle with Blosc where possibe,
        /// fallback to JSON for non-blittable types and compress with BinaryLz4.
        /// </remarks>
        BinaryLz4 = (CompressionMethod.Lz4 << 1) | VersionAndFlags.BinaryFlagMask,

        /// <summary>
        /// Good compression ratio, slower speed.
        /// </summary>
        /// <remarks>
        /// Use blittable reprezentation, byteshuffle with Blosc where possibe,
        /// fallback to JSON for non-blittable types and compress with BinaryZstd.
        /// </remarks>
        BinaryZstd = (CompressionMethod.Zstd << 1) | VersionAndFlags.BinaryFlagMask,

        // NB we use check < 100 in some places to detect binary

        /// <summary>
        /// Uncompressed JSON
        /// </summary>
        Json = CompressionMethod.None << 1,

        /// <summary>
        /// Serialize to JSON and compress with GZip method.
        /// </summary>
        JsonGZip = CompressionMethod.GZip << 1,

        [Obsolete("Hide from intellisense. Prefer GZip for Json.")]
        JsonLz4 = CompressionMethod.Lz4 << 1,

        [Obsolete("Hide from intellisense. Prefer GZip for Json.")]
        JsonZstd = CompressionMethod.Zstd << 1,
    }

    internal static class SerializationFormatExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompressionMethod CompressionMethod(this SerializationFormat format)
        {
            return (CompressionMethod)((int)format >> 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBinary(this SerializationFormat format)
        {
            return ((int)format & 1) != 0;
        }
    }
}
