using Bootstrap;
using System;
using System.Runtime.InteropServices;

namespace Spreads.Native {
#if NET451
    [System.Security.SuppressUnmanagedCodeSecurity]
#endif
    public class BloscMethods {
        public const string BloscLibraryName = "libblosc";
        static BloscMethods() {
            // Ensure Bootstrapper is initialized and native libraries are loaded
            Bootstrapper.Instance.Bootstrap<BloscMethods>(
                new[] { BloscLibraryName },
                null,
                null,
                null,
                () => { },
                () => { },
                () => {
                });
        }

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

        #endregion

    }
}