// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    public static partial class BinarySerializer
    {
        /// <summary>
        /// Get or set default compression method: BinaryLz4 (default) or BinaryZstd).
        /// </summary>
        public static SerializationFormat DefaultSerializationFormat { get; set; } = SerializationFormat.JsonGZip;

        // See e.g. https://gregoryszorc.com/blog/2017/03/07/better-compression-with-zstandard/
        internal static int _lz4CompressionLevel = 5;

        internal static int _zstdCompressionLevel = 1;

        internal static int _zlibCompressionLevel = 3;

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Default is 5.
        /// </summary>
        public static int LZ4CompressionLevel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        /// <summary>
        /// Default is 1. For many data types the default value
        /// increases IO throughput, i.e. time spent on both compression
        /// and writing smaller data is less than time spent on writing
        /// uncompressed data. Higher compression level only marginally
        /// improves compression ratio. Even Zstandard authors recommend
        /// using minimal compression level for most use cases.
        /// Please measure time and compression ratio with different
        /// compression levels before increasing the default level.
        /// </summary>
        public static int ZstdCompressionLevel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        /// <summary>
        /// Default is 3.
        /// </summary>
        public static int ZlibCompressionLevel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _zlibCompressionLevel;
            set
            {
                if (value < 0 || value >= 10)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                }

                _zlibCompressionLevel = value;
            }
        }

        internal static int _compressionStartFrom = 860;

        /// <summary>
        /// Minimum serialized payload size to apply compression.
        /// The value is inclusive.
        /// </summary>
        public static int CompressionStartFrom
        {
            // TODO (review) for small values with all numeric (esp. non-double) fields this method call could have visible impact, maybe do the same thing as with AdditionalCorrectness
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _compressionStartFrom;
            set
            {
                if (value < 500)
                {
                    value = 500;
                }

                _compressionStartFrom = value;
            }
        }

        private static Opt<bool> _safeBinarySerializerWrite = Opt<bool>.Missing;

        /// <summary>
        /// If <see cref="BinarySerializer{T}.SizeOf"/> does not return a temp buffer
        /// then force using a temp buffer for destination of <see cref="BinarySerializer{T}.Write"/>
        /// method. This is slower but useful to ensure correct implementation of
        /// a custom <see cref="BinarySerializer{T}"/>.
        ///
        /// <para>By default is set to <see cref="Settings.DoAdditionalCorrectnessChecks"/></para>
        ///
        /// </summary>
        public static bool DefensiveBinarySerializerWrite
        {
            get => _safeBinarySerializerWrite.PresentOrDefault(AdditionalCorrectnessChecks.Enabled);
            set => _safeBinarySerializerWrite = Opt.Present(value);
        }

        /// <summary>
        /// Set to true to use <see cref="StructLayoutAttribute.Size"/> as <see cref="BinarySerializationAttribute.BlittableSize"/>.
        /// </summary>
        public static bool UseStructLayoutSizeAsBlittableSize = false;

    }
}
