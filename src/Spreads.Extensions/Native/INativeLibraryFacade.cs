using System;

namespace Spreads.Native {
    interface INativeLibraryFacade {
        
        int blosc_compress_ctx(IntPtr clevel, IntPtr doshuffle, UIntPtr typesize,
                                UIntPtr nbytes, IntPtr src, IntPtr dest,
                                UIntPtr destsize, string compressor, UIntPtr blocksize,
                                int numinternalthreads);

        int blosc_decompress_ctx(IntPtr src, IntPtr dest, UIntPtr destsize, int numinternalthreads);

        int blosc_cbuffer_sizes(IntPtr cbuffer, ref UIntPtr nbytes, ref UIntPtr cbytes, ref UIntPtr blocksize);

    }
}
