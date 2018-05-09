namespace Spreads.Serialization
{
    /// <summary>
    /// LZ4 for arrays of blittable types, None for other types.
    /// Choosing any compression method other than DefaultOrNone will force
    /// compression for non-blittable types.
    /// </summary>
    public enum CompressionMethod : byte
    {
        /// <summary>
        /// LZ4 for arrays of blittable types, None for other types.
        /// </summary>
        DefaultOrNone = 0,

        /// <summary>
        /// Use LZ4.
        /// </summary>
        LZ4 = 1,

        /// <summary>
        /// Use Zstandard.
        /// </summary>
        Zstd = 2,

        // TODO Gziped JSON (and no MsgPack!)
    }
}