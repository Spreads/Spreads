using System.Linq.Expressions;
using Spreads.Blosc;
using Spreads.Serialization;

namespace Spreads
{
    /// <summary>
    /// Global settings.
    /// </summary>
    public static class Settings
    {
        // TODO find best values
        /// <summary>
        /// 
        /// </summary>
        public const int SliceMemoryAlignment = 8;

        // NB at least 85k for LOH
        internal const int ThreadStaticPinnedBufferSize = 128 * 1024;

        internal class AdditionalCorrectnessChecks
        {
            // Unless _doAdditionalCorrectnessChecks is changed from default before this internal
            // class is ever referenced from any code path it will have default 
            // value and this field will be JIT compile-time constant. f set to false checks such as 
            // if(Settings.AdditionalCorrectnessChecks.DoChecks) will be 
            // completely eliminated by JIT.
            public static readonly bool DoChecks = _doAdditionalCorrectnessChecks;
        }

        /// <summary>
        /// Call Trace.TraceWarning when a finalizer of IDisposable objects is called.
        /// </summary>
        public static bool TraceFinalizationOfIDisposables { get; set; }

        ///// <summary>
        ///// Throw <see cref="OutOfOrderKeyException{TKey}"/> if Zip input values arrive out of order.
        ///// </summary>
        //public static bool ZipThrowOnOunOfOrderInputs { get; set; }

        /// <summary>
        /// Get or set default compression method: BinaryLz4 (default) or BinaryZstd).
        /// </summary>
        public static SerializationFormat DefaultSerializationFormat { get; set; } = SerializationFormat.JsonDeflate;

        // TODO when/if used often benchmark its effect and if significant then set default to false
        private static bool _doAdditionalCorrectnessChecks = true;

        /// <summary>
        /// When this property is set to true at the very beginning of 
        /// a program execution that 
        /// </summary>
        public static bool DoAdditionalCorrectnessChecks
        {
            get => AdditionalCorrectnessChecks.DoChecks;
            set => _doAdditionalCorrectnessChecks = value;
        }

        internal static int SCMDefaultChunkLength = 4096;

        internal static int _lz4CompressionLevel = 5;
        internal static int _zstdCompressionLevel = 5;

        public static int LZ4CompressionLevel
        {
            get => _lz4CompressionLevel;
            set
            {
                if (value < 0 || value >= 10)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                }
                _lz4CompressionLevel = value;
            }
        }

        public static int ZstdCompressionLevel
        {
            get => _zstdCompressionLevel;
            set
            {
                if (value < 0 || value >= 10)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                }
                _zstdCompressionLevel = value;
            }
        }
    }
}