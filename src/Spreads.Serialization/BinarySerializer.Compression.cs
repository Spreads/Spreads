// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    public static unsafe partial class BinarySerializer
    {
        internal static int Compress(ReadOnlySpan<byte> source, Span<byte> destination, CompressionMethod method)
        {
            fixed (byte* pSource = source)
            fixed (byte* pDestination = destination)
            {
                switch (method)
                {
                    case CompressionMethod.None:
                    {
                        var len = source.Length;
                        if (len > destination.Length)
                            return -1;

                        Buffer.MemoryCopy((void*)pSource, (void*)pDestination, destination.Length, len);
                        return len;
                    }
                    case CompressionMethod.GZip:
                    {
                        return Native.Compression.compress_gzip(pSource, (IntPtr)source.Length, pDestination,
                            (IntPtr)destination.Length, ZlibCompressionLevel);
                    }
                    case CompressionMethod.Lz4:
                    {
                        return Native.Compression.compress_lz4(pSource, (IntPtr)source.Length, pDestination,
                            (IntPtr)destination.Length, LZ4CompressionLevel);
                    }
                    case CompressionMethod.Zstd:
                    {
                        return Native.Compression.compress_zstd(pSource, (IntPtr)source.Length, pDestination,
                            (IntPtr)destination.Length, ZstdCompressionLevel);
                    }

                    case CompressionMethod.Deflate:
                    {
                        return Native.Compression.compress_deflate(pSource, (IntPtr)source.Length,
                            pDestination,
                            (IntPtr)destination.Length, ZlibCompressionLevel);
                    }
                    case CompressionMethod.ZLib:
                    {
                        return Native.Compression.compress_zlib(pSource, (IntPtr)source.Length, pDestination,
                            (IntPtr)destination.Length, ZlibCompressionLevel);

                    }
                    default:
                        ThrowHelper.ThrowNotSupportedException();
                        return -1;
                }
            }
        }

        /// <summary>
        /// Source must be of exact compressed payload size, which must be stored somewhere (e.g. in custom header). Use Slice() to trim source to the exact size.
        /// Destination must have enough size for uncompressed payload.
        /// This method returns number of uncompressed bytes written to the destination buffer.
        /// Non-positive return value means an error, exact value if opaque as of now (impl. detail: it returns native error code for GZip/LZ4, but that could change).
        /// </summary>
        internal static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination, CompressionMethod method)
        {
            if (method == CompressionMethod.GZip)
            {
                return ReadGZip(source, destination);
            }

            if (method == CompressionMethod.Lz4)
            {
                return ReadLz4(source, destination);
            }

            if (method == CompressionMethod.Zstd)
            {
                return ReadZstd(source, destination);
            }

            if (method == CompressionMethod.None)
            {
                var len = source.Length;
                if (len > destination.Length)
                {
                    return -1;
                }
                source.CopyTo(destination);
                return len;
            }

            ThrowHelper.ThrowNotSupportedException();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadLz4(in ReadOnlySpan<byte> source, in Span<byte> destination)
        {
            fixed (byte* s = source)
            fixed (byte* d = destination)
            {
                return Native.Compression.decompress_lz4(s, (IntPtr)source.Length, d,
                    (IntPtr)destination.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZstd(in ReadOnlySpan<byte> source, in Span<byte> destination)
        {
            fixed (byte* s = source)
            fixed (byte* d = destination)
            {
                return Native.Compression.decompress_zstd(s, (IntPtr)source.Length,
                    d, (IntPtr)destination.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZlib(in ReadOnlySpan<byte> source, in Span<byte> destination)
        {
            fixed (byte* s = source)
            fixed (byte* d = destination)
            {
                return Native.Compression.decompress_zlib(s, (IntPtr)source.Length,
                    d, (IntPtr)destination.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadDeflate(in ReadOnlySpan<byte> source, in Span<byte> destination)
        {
            fixed (byte* s = source)
            fixed (byte* d = destination)
            {
                return Native.Compression.decompress_deflate(s, (IntPtr)source.Length,
                   d, (IntPtr)destination.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadGZip(in ReadOnlySpan<byte> source, in Span<byte> destination)
        {
            fixed (byte* s = source)
            fixed (byte* d = destination)
            {
                return Native.Compression.decompress_gzip(s, (IntPtr)source.Length, d, (IntPtr)destination.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination, byte typeSize)
        {
            fixed (byte* s = source)
            fixed (byte* d = destination)
            {
                Native.Compression.shuffle((IntPtr)typeSize, (IntPtr)source.Length, s, d);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unshuffle(ReadOnlySpan<byte> source, Span<byte> destination, byte typeSize)
        {
            fixed (byte* s = source)
            fixed (byte* d = destination)
            {
                Native.Compression.unshuffle((IntPtr)typeSize, (IntPtr)source.Length, s, d);
            }
        }
    }
}
