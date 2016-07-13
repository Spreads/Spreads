using Bootstrap;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Spreads.Native {
#if NET451
    [System.Security.SuppressUnmanagedCodeSecurity]
#endif
    internal class NativeMethods {
        static NativeMethods()
        {
            // Ensure Bootstrapper is initialized and native libraries are loaded
            ABI = Bootstrapper.ABI;
        }
        public const string BloscLibraryName = "libblosc";

        internal static ABI ABI { get; set; }

#region Blosc

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int blosc_compress_ctx(IntPtr clevel, IntPtr doshuffle,
            UIntPtr typesize, UIntPtr nbytes, IntPtr src, IntPtr dest,
            UIntPtr destsize, [MarshalAs(UnmanagedType.LPStr)]string compressor,
            UIntPtr blocksize, int numinternalthreads);

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int blosc_decompress_ctx(IntPtr src, IntPtr dest,
            UIntPtr destsize, int numinternalthreads);

        [DllImport(BloscLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int blosc_cbuffer_sizes(IntPtr cbuffer, ref UIntPtr nbytes,
            ref UIntPtr cbytes, ref UIntPtr blocksize);

#endregion

        
        

    }
}