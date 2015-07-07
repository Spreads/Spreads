using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Spreads.Native {
    [System.Security.SuppressUnmanagedCodeSecurity]
    class NativeLibraryFacade : INativeLibraryFacade {
        public const string BloscLibraryName = "libblosc";
        
        #region Blosc

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int blosc_compress_ctx(IntPtr clevel, IntPtr doshuffle,
            UIntPtr typesize, UIntPtr nbytes, IntPtr src, IntPtr dest,
            UIntPtr destsize, [MarshalAs(UnmanagedType.LPStr)]string compressor,
            UIntPtr blocksize, int numinternalthreads);

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int blosc_decompress_ctx(IntPtr src, IntPtr dest,
            UIntPtr destsize, int numinternalthreads);

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int blosc_cbuffer_sizes(IntPtr cbuffer, ref UIntPtr nbytes,
            ref UIntPtr cbytes, ref UIntPtr blocksize);

        #endregion

        
        /////////////////////////////////////////////////////////////////////////
        ///////////  INativeLibraryFacade Interface              ////////////////
        /////////////////////////////////////////////////////////////////////////


        #region Blosc
        int INativeLibraryFacade.blosc_compress_ctx(IntPtr clevel, IntPtr doshuffle,
            UIntPtr typesize, UIntPtr nbytes, IntPtr src, IntPtr dest,
            UIntPtr destsize, string compressor,
            UIntPtr blocksize, int numinternalthreads) {
            return NativeLibraryFacade.blosc_compress_ctx(clevel, doshuffle,
                typesize, nbytes, src, dest, destsize, compressor, blocksize, numinternalthreads);
        }

        int INativeLibraryFacade.blosc_decompress_ctx(IntPtr src, IntPtr dest,
            UIntPtr destsize, int numinternalthreads) {
            return NativeLibraryFacade.blosc_decompress_ctx(src, dest, destsize, numinternalthreads);
        }

        int INativeLibraryFacade.blosc_cbuffer_sizes(IntPtr cbuffer, ref UIntPtr nbytes,
            ref UIntPtr cbytes, ref UIntPtr blocksize) {
            return NativeLibraryFacade.blosc_cbuffer_sizes(cbuffer, ref nbytes, ref cbytes, ref blocksize);
        }

        #endregion
                

    }
}