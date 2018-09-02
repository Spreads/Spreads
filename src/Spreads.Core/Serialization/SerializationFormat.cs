namespace Spreads.Serialization
{
    /// <summary>
    /// Binary serialization format. Serialized data always has a header describing the method of
    /// binary serialization. The data could be Json, but because of the header it is not directly
    /// consumable by e.g. browsers. However, for JSON/JSONDeflate cases the only difference is
    /// the header and the reast payload could be directly sent to a browser expecting 'application/json'
    /// MIME type.
    /// </summary>
    /// <remarks>
    /// JsonDeflate:
    /// Objects are converted to JSON and compressed with raw deflate method
    /// if compression gives smaller size. This format is compatible with raw
    /// http payload with 'Accept-encoding: deflate' headers past the 8 bytes header.
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
    /// * Text trasport/protocol supports only JSON and the payload is plain or deflated JSON that
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
        Binary = 0,

        /// <summary>
        /// Use blittable reprezentation, byteshuffle with Blosc where possibe,
        /// fallback to JSON for non-blittable types and compress with BinaryLz4.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        BinaryLz4 = 1,

        /// <summary>
        /// Use blittable reprezentation, byteshuffle with Blosc where possibe,
        /// fallback to JSON for non-blittable types and compress with BinaryZstd.
        /// </summary>
        BinaryZstd = 2,

        // NB we use check < 100 in some places to detect binary

        /// <summary>
        /// Uncompressed JSON
        /// </summary>
        Json = 100,

        /// <summary>
        /// Serialize to JSON and compress with raw deflate method.
        /// </summary>
        JsonDeflate = 101,

//#if NETCOREAPP2_1
//        JsonBrotli = 102
//#endif
    }
}
