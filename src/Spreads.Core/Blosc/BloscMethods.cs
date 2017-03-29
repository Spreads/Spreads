// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using Bootstrap;
using Spreads.Serialization;

namespace Spreads.Blosc
{
    // TODO Move to global Settings

    /// <summary>
    /// Blosc settings.
    /// </summary>
    public static class BloscSettings
    {
        internal static string defaultCompressionMethod = "lz4";

        /// <summary>
        /// Get or set default compression method (LZ4 or Zstd). Initially the value is LZ4.
        /// </summary>
        public static CompressionMethod CompressionMethod
        {
            get => defaultCompressionMethod == "lz4" ? CompressionMethod.LZ4 : CompressionMethod.Zstd;
            set
            {
                if (value == CompressionMethod.Zstd)
                {
                    defaultCompressionMethod = "zstd";
                }
                else
                {
                    defaultCompressionMethod = "lz4";
                }
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
                Bootstrapper.Instance.Bootstrap<BloscMethods>(
                    BloscLibraryName,
                    null,
                    () => { },
                    (library) =>
                    {
                        var handle = library.handle;
                        native_blosc_compress_ctx = Marshal.GetDelegateForFunctionPointer<BloscCompressCtxDelegate>
                            (library.loader.FindFunction(handle, "blosc_compress_ctx"));
                        native_blosc_decompress_ctx = Marshal.GetDelegateForFunctionPointer<BloscDecompressCtxDelegate>
                            (library.loader.FindFunction(handle, "blosc_decompress_ctx"));
                        native_blosc_cbuffer_sizes = Marshal.GetDelegateForFunctionPointer<BloscCbufferSizes>
                            (library.loader.FindFunction(handle, "blosc_cbuffer_sizes"));
                    },
                    () => { });
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error in BloscMethods Init: {ex.ToString()}");
                throw;
            }
        }

        public static readonly int ProcessorCount = Environment.ProcessorCount;

        #region Blosc

        public static int blosc_compress_ctx(IntPtr clevel, IntPtr doshuffle,
            UIntPtr typesize, UIntPtr nbytes, IntPtr src, IntPtr dest,
            UIntPtr destsize, [MarshalAs(UnmanagedType.LPStr)] string compressor,
            UIntPtr blocksize, int numinternalthreads)
        {
            try
            {
                return native_blosc_compress_ctx(clevel, doshuffle, typesize, nbytes, src, dest,
                    destsize, compressor, blocksize, numinternalthreads);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error calling native delegate blosc_compress_ctx: {ex.ToString()}");
                throw;
            }
        }

        public static int blosc_decompress_ctx(IntPtr src, IntPtr dest, UIntPtr destsize, int numinternalthreads)
        {
            try
            {
                return native_blosc_decompress_ctx(src, dest, destsize, numinternalthreads);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error calling native delegate blosc_decompress_ctx: {ex.ToString()}");
                throw;
            }
        }

        public static int blosc_cbuffer_sizes(IntPtr cbuffer, ref UIntPtr nbytes,
            ref UIntPtr cbytes, ref UIntPtr blocksize)
        {
            try
            {
                return native_blosc_cbuffer_sizes(cbuffer, ref nbytes, ref cbytes, ref blocksize);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Error calling native delegate blosc_cbuffer_sizes: {ex.ToString()}");
                throw;
            }
        }

        #endregion Blosc
    }
}