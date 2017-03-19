// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Bootstrap;
using Spreads.Serialization;

namespace Spreads.Blosc
{
    public static class BloscSettings
    {
        internal static string defaultCompressionMethod = "lz4";

        public static CompressionMethod CompressionMethod
        {
            get => defaultCompressionMethod == "lz4" ? CompressionMethod.LZ4 : CompressionMethod.Zstd;
            set
            {
                if (value == CompressionMethod.Zstd)
                {
                    Trace.TraceWarning("Zstd support is experimental");
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

        static BloscMethods()
        {
            try
            {
                // Ensure Bootstrapper is initialized and native libraries are loaded
                Bootstrapper.Instance.Bootstrap<BloscMethods>(
                    new[] { BloscLibraryName },
                    null,
                    null,
                    null,
                    () => { },
                    () => { },
                    () =>
                    {
                    });
            }
            catch (Exception ex)
            {
                Environment.FailFast(ex.Message);
            }
        }

        public static readonly int ProcessorCount = Environment.ProcessorCount;

        #region Blosc

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int blosc_compress_ctx(IntPtr clevel, IntPtr doshuffle,
            UIntPtr typesize, UIntPtr nbytes, IntPtr src, IntPtr dest,
            UIntPtr destsize, [MarshalAs(UnmanagedType.LPStr)]string compressor,
            UIntPtr blocksize, int numinternalthreads);

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int blosc_decompress_ctx(IntPtr src, IntPtr dest,
            UIntPtr destsize, int numinternalthreads);

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int blosc_cbuffer_sizes(IntPtr cbuffer, ref UIntPtr nbytes,
            ref UIntPtr cbytes, ref UIntPtr blocksize);

        #endregion Blosc
    }
}