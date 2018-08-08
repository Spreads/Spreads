namespace Spreads.Serialization
{
    /// <summary>
    /// JsonDeflate:
    /// Objects are converted to JSON and compressed with raw deflate method
    /// if compression gives smaller size. This format is compatible with raw
    /// http payload with 'Accept-encoding: deflate' headers.
    ///
    /// BinaryLz4/Zstd:
    /// Binary is stored as blittable representation with Blosc byteshuffle for arrays with fixed-size elements.
    /// Then serialized (and byteshuffled) buffer is compressed with LZ4 (super fast) or Zstd (high compression)
    /// if compression gives smaller size. Actual buffer layout is type dependent.
    ///
    /// Serialized buffer should have a header with a flag indicating the format and
    /// wether or not the buffer is compressed. Some data types are stored as deltas
    /// with diffing performed before byteshuffling and the header must have such flag as well.
    /// </summary>
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

#if NETCOREAPP2_1
        JsonBrotli = 102
#endif
    }
}