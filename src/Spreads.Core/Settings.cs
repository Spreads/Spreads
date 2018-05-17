using Spreads.Blosc;
using Spreads.Serialization;

namespace Spreads
{
    /// <summary>
    /// Global settings.
    /// </summary>
    public static class Settings
    {
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
        /// Get or set default compression method: LZ4 (default) or Zstd).
        /// </summary>
        public static CompressionMethod DefaultCompressionMethod { get; set; } = CompressionMethod.DeflateJson;

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
    }
}