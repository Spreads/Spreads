using Spreads.Blosc;
using Spreads.Serialization;

namespace Spreads
{
    /// <summary>
    /// Global settings.
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// Call Trace.TraceWarning when a finalizer of IDisposable objects is called.
        /// </summary>
        public static bool TraceFinalizationOfIDisposables { get; set; }

        /// <summary>
        /// Get or set default compression method: LZ4 (default) or Zstd).
        /// </summary>
        public static CompressionMethod CompressionMethod
        {
            get => BloscSettings.CompressionMethod;
            set => BloscSettings.CompressionMethod = value;
        }
    }
}