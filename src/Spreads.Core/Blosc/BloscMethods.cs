// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Spreads.Blosc
{
    /// <summary>
    /// Blosc settings.
    /// </summary>
    internal static class BloscSettings
    {
        internal static string DefaultCompressionMethod = "lz4";

        /// <summary>
        /// Get or set default compression method: BinaryLz4 (default) or BinaryZstd).
        /// </summary>
        public static SerializationFormat SerializationFormat
        {
            get => DefaultCompressionMethod == "lz4" ? SerializationFormat.BinaryLz4 : SerializationFormat.BinaryZstd;
            set
            {
                if (value != SerializationFormat.BinaryZstd || value != SerializationFormat.BinaryZstd)
                {
                    ThrowHelper.ThrowNotSupportedException("Spreads.Blosc supports only lz4 or zstd");
                }
                DefaultCompressionMethod = value == SerializationFormat.BinaryZstd ? "zstd" : "lz4";
            }
        }
    }

#if NET451

    [SuppressUnmanagedCodeSecurity]
#endif

    internal class BloscMethods
    {
        public const string BloscLibraryName = "libblosc";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BloscCompressCtxDelegate(IntPtr clevel, IntPtr doshuffle,
            UIntPtr typesize, UIntPtr nbytes, IntPtr src, IntPtr dest,
            UIntPtr destsize, [MarshalAs(UnmanagedType.LPStr)]string compressor,
            UIntPtr blocksize, int numinternalthreads);

        private static BloscCompressCtxDelegate native_blosc_compress_ctx;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BloscDecompressCtxDelegate(IntPtr src, IntPtr dest, UIntPtr destsize, int numinternalthreads);

        private static BloscDecompressCtxDelegate native_blosc_decompress_ctx;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BloscCbufferSizes(IntPtr cbuffer, ref UIntPtr nbytes, ref UIntPtr cbytes, ref UIntPtr blocksize);

        private static BloscCbufferSizes native_blosc_cbuffer_sizes;

        static BloscMethods()
        {
            try
            {
                // Ensure Bootstrapper is initialized and native libraries are loaded
                Utils.Bootstrap.Bootstrapper.Instance.Bootstrap<BloscMethods>(
                    BloscLibraryName,
                    null,
                    () => { },
                    (library) =>
                    {
                        var handle = library._handle;
                        native_blosc_compress_ctx = Marshal.GetDelegateForFunctionPointer<BloscCompressCtxDelegate>
                            (library._loader.FindFunction(handle, "blosc_compress_ctx"));
                        native_blosc_decompress_ctx = Marshal.GetDelegateForFunctionPointer<BloscDecompressCtxDelegate>
                            (library._loader.FindFunction(handle, "blosc_decompress_ctx"));
                        native_blosc_cbuffer_sizes = Marshal.GetDelegateForFunctionPointer<BloscCbufferSizes>
                            (library._loader.FindFunction(handle, "blosc_cbuffer_sizes"));
                    },
                    () => { });
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error in BloscMethods Init: {ex}");
                throw;
            }
        }

        public static readonly int ProcessorCount = 1; // Environment.ProcessorCount;

        #region Blosc

        public static int blosc_compress_ctx(IntPtr clevel, IntPtr doshuffle,
            UIntPtr typesize, UIntPtr nbytes, IntPtr src, IntPtr dest,
            UIntPtr destsize, [MarshalAs(UnmanagedType.LPStr)] string compressor,
            UIntPtr blocksize, int numinternalthreads)
        {
            return native_blosc_compress_ctx(clevel, doshuffle, typesize, nbytes, src, dest,
                destsize, compressor, blocksize, numinternalthreads);
        }

        public static int blosc_decompress_ctx(IntPtr src, IntPtr dest, UIntPtr destsize, int numinternalthreads)
        {
            return native_blosc_decompress_ctx(src, dest, destsize, numinternalthreads);
        }

        public static int blosc_cbuffer_sizes(IntPtr cbuffer, ref UIntPtr nbytes,
            ref UIntPtr cbytes, ref UIntPtr blocksize)
        {
            return native_blosc_cbuffer_sizes(cbuffer, ref nbytes, ref cbytes, ref blocksize);
        }

        #endregion Blosc
    }
}