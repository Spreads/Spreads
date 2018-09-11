// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Blosc;
using Spreads.Buffers;
using System;
#if NETCOREAPP2_1
using System.IO.Compression;
#endif
using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    public enum CompressionMethod
    {
        None = 0, // 00
        GZip = 1, // 01
        Lz4 = 2,  // 10
        Zstd = 3, // 11
    }

    public static unsafe partial class BinarySerializer
    {
        private static readonly bool UseCalli = Settings.PreferCalli && IntPtr.Size == 8;

#if NETCOREAPP2_1
        internal static readonly bool IsBrotliSupported = true;
#else
        internal static readonly bool IsBrotliSupported = false;
#endif

#if NETCOREAPP2_1

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteBrotli(DirectBuffer source, DirectBuffer destination)
        {
            if (BrotliEncoder.TryCompress(source.Span, destination.Span, out var bytesWritten,
                Settings.BrotliCompressionLevel, 10))
            {
                return bytesWritten;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadBrotli(DirectBuffer source, DirectBuffer destination)
        {
            if (BrotliDecoder.TryDecompress(source.Span, destination.Span, out var bytesWritten))
            {
                return bytesWritten;
            }

            return 0;
        }
#endif

        private static readonly IntPtr[] CompressionMethodPointers = { BloscMethods.compress_copy_ptr, BloscMethods.compress_gzip_ptr, BloscMethods.compress_lz4_ptr, BloscMethods.compress_zstd_ptr };
        private static readonly int[] CompressionMethodLevels = { 0, Settings.ZlibCompressionLevel, Settings.LZ4CompressionLevel, Settings.ZstdCompressionLevel };
        private static readonly IntPtr[] DecompressionMethodPointers = { BloscMethods.decompress_copy_ptr, BloscMethods.decompress_gzip_ptr, BloscMethods.decompress_lz4_ptr, BloscMethods.decompress_zstd_ptr };

        internal static void UpdateLevels()
        {
            CompressionMethodLevels[1] = Settings.ZlibCompressionLevel;
            CompressionMethodLevels[2] = Settings.LZ4CompressionLevel;
            CompressionMethodLevels[3] = Settings.ZstdCompressionLevel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Write(DirectBuffer source, DirectBuffer destination, CompressionMethod method)
        {
            if (UseCalli)
            {
                var methodIdx = (int)method;
                var ptr = CompressionMethodPointers[methodIdx];
                var level = CompressionMethodLevels[methodIdx];

                return UnsafeEx.CalliCompress(source._data, source._length, destination._data, destination._length,
                    level, ptr);
            }

            if (method == CompressionMethod.GZip)
            {
                return WriteGZip(source, destination);
            }

            if (method == CompressionMethod.Lz4)
            {
                return WriteLz4(source, destination);
            }

            if (method == CompressionMethod.Zstd)
            {
                return WriteZstd(source, destination);
            }

            ThrowHelper.ThrowNotSupportedException();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Read(DirectBuffer source, DirectBuffer destination, CompressionMethod method)
        {
            if (UseCalli)
            {
                var methodIdx = (int)method;
                var ptr = DecompressionMethodPointers[methodIdx];

                return UnsafeEx.CalliDecompress(source._data, source._length, destination._data, destination._length,
                    ptr);
            }

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

            ThrowHelper.ThrowNotSupportedException();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteLz4(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompress(source._data, source._length, destination._data, destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_lz4_ptr);
            }

            return BloscMethods.compress_lz4(source._data, source._length, destination._data,
                destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadLz4(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompress(source._data, source._length, destination._data, destination._length,
                    BloscMethods.decompress_lz4_ptr);
            }

            return BloscMethods.decompress_lz4(source._data, source._length, destination._data, destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZstd(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompress(source._data, source._length, destination._data, destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_zstd_ptr);
            }

            return BloscMethods.compress_zstd(source._data, source._length, destination._data,
                destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZstd(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompress(source._data, source._length, destination._data, destination._length,
                    BloscMethods.decompress_zstd_ptr);
            }

            return BloscMethods.decompress_zstd(source._data, source._length, destination._data, destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZlib(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompress(source._data, source._length, destination._data, destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_zlib_ptr);
            }

            return BloscMethods.compress_zlib(source._data, source._length, destination._data,
                destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZlib(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompress(source._data, source._length, destination._data, destination._length,
                    BloscMethods.decompress_zlib_ptr);
            }

            return BloscMethods.decompress_zlib(source._data, source._length, destination._data, destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteDeflate(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompress(source._data, source._length, destination._data, destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_deflate_ptr);
            }

            return BloscMethods.compress_deflate(source._data, source._length, destination._data,
                destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadDeflate(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompress(source._data, source._length, destination._data, destination._length,
                    BloscMethods.decompress_deflate_ptr);
            }

            return BloscMethods.decompress_deflate(source._data, source._length, destination._data, destination._length);
        }

        internal static int WriteGZip(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompress(source._data, source._length, destination._data, destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_gzip_ptr);
            }

            return BloscMethods.compress_gzip(source._data, source._length, destination._data,
                destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadGZip(DirectBuffer source, DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompress(source._data, source._length, destination._data, destination._length,
                    BloscMethods.decompress_gzip_ptr);
            }

            return BloscMethods.decompress_gzip(source._data, source._length, destination._data, destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Shuffle(DirectBuffer source, DirectBuffer destination, byte typeSize)
        {
            if (UseCalli)
            {
                UnsafeEx.CalliShuffleUnshuffle((IntPtr)typeSize, source._length, source._data, destination._data, BloscMethods.shuffle_ptr);
            }
            else
            {
                BloscMethods.shuffle((IntPtr)typeSize, source._length, source._data, destination._data);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Unshuffle(DirectBuffer source, DirectBuffer destination, byte typeSize)
        {
            if (UseCalli)
            {
                UnsafeEx.CalliShuffleUnshuffle((IntPtr)typeSize, source._length, source._data, destination._data, BloscMethods.unshuffle_ptr);
            }
            else
            {
                BloscMethods.unshuffle((IntPtr)typeSize, source._length, source._data, destination._data);
            }
        }

        [Obsolete("TODO remove")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ShuffleXX(DirectBuffer source, DirectBuffer destination, byte typeSize)
        {
            UnsafeEx.CalliShuffleUnshuffle((IntPtr)typeSize, source._length, source._data, destination._data, BloscMethods.shuffle_ptr);
        }

        [Obsolete("TODO remove")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UnshuffleXX(DirectBuffer source, DirectBuffer destination, byte typeSize)
        {
            UnsafeEx.CalliShuffleUnshuffle((IntPtr)typeSize, source._length, source._data, destination._data, BloscMethods.unshuffle_ptr);
        }

        [Obsolete("TODO remove")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZlibXX(DirectBuffer source, DirectBuffer destination)
        {
            return UnsafeEx.CalliCompress(source._data, source._length, destination._data, destination._length,
                Settings.ZlibCompressionLevel, BloscMethods.compress_zlib_ptr);
        }

        [Obsolete("TODO remove")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZlibXX(DirectBuffer source, DirectBuffer destination)
        {
            return UnsafeEx.CalliDecompress(source._data, source._length, destination._data, destination._length, BloscMethods.decompress_zlib_ptr);
        }
    }
}
