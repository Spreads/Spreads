namespace Spreads.Serialization
{
    /// <summary>
    /// LZ4 for blittable arrays inside IArrayBasedMap, None for other types.
    /// Choosing any compression method other than DefaultOrNone will force
    /// compression for non-primitive types.
    /// </summary>
    public enum CompressionMethod : byte
    {
        /// <summary>
        /// LZ4 for blittable arrays inside IArrayBasedMap, None for other types.
        /// </summary>
        DefaultOrNone = 0,

        /// <summary>
        /// Use LZ4
        /// </summary>
        LZ4 = 1,

        /// <summary>
        /// Use Zstandard
        /// </summary>
        Zstd = 2
    }
}