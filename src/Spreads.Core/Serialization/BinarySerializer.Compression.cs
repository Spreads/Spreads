// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using Spreads.Buffers;

#if NETCOREAPP2_1
using System.IO.Compression;
#endif

namespace Spreads.Serialization
{
    public static unsafe partial class BinarySerializer
    {
        private static readonly int[] CompressionMethodLevels = { 0, Settings.ZlibCompressionLevel, Settings.LZ4CompressionLevel, Settings.ZstdCompressionLevel };

        internal static void UpdateLevels()
        {
            CompressionMethodLevels[1] = Settings.ZlibCompressionLevel;
            CompressionMethodLevels[2] = Settings.LZ4CompressionLevel;
            CompressionMethodLevels[3] = Settings.ZstdCompressionLevel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Compress(in DirectBuffer source, in DirectBuffer destination, CompressionMethod method)
        {
            if (method == CompressionMethod.GZip)
            {
                return WriteGZip(in source, in destination);
            }

            if (method == CompressionMethod.Lz4)
            {
                return WriteLz4(in source, in destination);
            }

            if (method == CompressionMethod.Zstd)
            {
                return WriteZstd(in source, in destination);
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

        /// <summary>
        /// Source must be of exact compressed payload size, which must be stored somewhere (e.g. in custom header). Use Slice() to trim source to the exact size.
        /// Destination must have enough size for uncompressed payload.
        /// This method returns number of uncompressed bytes written to the destination buffer.
        /// Non-positive return value means an error, exact value if opaque as of now (impl. detail: it returns native error code for GZip/LZ4, but that could change).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Decompress(in DirectBuffer source, in DirectBuffer destination, CompressionMethod method)
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
        internal static int WriteLz4(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.compress_lz4(source._pointer, (IntPtr)source._length, destination._pointer,
                (IntPtr)destination._length, Settings.LZ4CompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadLz4(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.decompress_lz4(source._pointer, (IntPtr)source._length, destination._pointer, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZstd(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.compress_zstd(source._pointer, (IntPtr)source._length, destination._pointer,
                (IntPtr)destination._length, Settings.ZstdCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZstd(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.decompress_zstd(source._pointer, (IntPtr)source._length, destination._pointer, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZlib(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.compress_zlib(source._pointer, (IntPtr)source._length, destination._pointer,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZlib(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.decompress_zlib(source._pointer, (IntPtr)source._length, destination._pointer, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteDeflate(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.compress_deflate(source._pointer, (IntPtr)source._length, destination._pointer,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadDeflate(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.decompress_deflate(source._pointer, (IntPtr)source._length, destination._pointer, (IntPtr)destination._length);
        }

        internal static int WriteGZip(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.compress_gzip(source._pointer, (IntPtr)source._length, destination._pointer,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadGZip(in DirectBuffer source, in DirectBuffer destination)
        {
            return Native.Compression.decompress_gzip(source._pointer, (IntPtr)source._length, destination._pointer, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Shuffle(in DirectBuffer source, in DirectBuffer destination, byte typeSize)
        {
            Native.Compression.shuffle((IntPtr)typeSize, (IntPtr)source._length, source._pointer, destination._pointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Unshuffle(in DirectBuffer source, in DirectBuffer destination, byte typeSize)
        {
            Native.Compression.unshuffle((IntPtr)typeSize, (IntPtr)source._length, source._pointer, destination._pointer);
        }
    }
}
