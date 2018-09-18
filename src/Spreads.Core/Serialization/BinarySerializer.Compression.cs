// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Blosc;
using Spreads.Buffers;
using Spreads.DataTypes;
using System;

#if NETCOREAPP2_1
using System.IO.Compression;
#endif

using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    public static unsafe partial class BinarySerializer
    {
        /// By default compressoin sets ZLD flag, compresses payload, prepends
        /// compressed bytes with original length and then writes compressed
        /// payload size + 4 bytes (original size).
        /// Compression format:
        ///
        ///
        ///
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |    TypeEnum   |    TypeSize   | SubTypeEnum   |
        /// +---------------------------------------------------------------+
        /// |                 Compressed Payload Length + 4                 |
        /// +---------------------------------------------------------------+
        /// |                 Uncompressed Payload Length                   |
        /// +---------------------------------------------------------------+
        /// |                       Compressed Payload                      |
        /// |                              ...                              |

        // Calli for compression is almost meaningless since compression costs >>> P/Invoke costs.
        // But nice to have an example of the technique. Gains are visible for shuffle/unshuffle small buffers.
        private static readonly bool UseCalli = Settings.PreferCalli && IntPtr.Size == 8 && AppDomain.CurrentDomain.IsFullyTrusted;

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

        /// <summary>
        /// This is a compression step for any serializer. The serializer job is to prepare data for best
        /// compression, e.g. delta/shuffle and then use this method on the entire serialized payload.
        /// Serializer could shuffle different parts of payload separately, but compressing a bigger payload
        /// should be more efficient. This is one of the main reasons why we separated shuffle from compression
        /// and do not use Blosc's high-level functions e.g. for Span-based maps.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CompressWithHeader(in DirectBuffer source, in DirectBuffer destination, CompressionMethod method)
        {
            var srcLen = checked((int)(uint)source.Length);
            var header = source.Read<DataTypeHeader>(0);
            var uncompressedLength = source.ReadInt32(DataTypeHeader.Size);

            var hasTs = header.VersionAndFlags.IsTimestamped;
            var tsSize = *(int*)(&hasTs) << 3;

            if (Settings.AdditionalCorrectnessChecks.Enabled)
            {
                if (destination.Length < source.Length)
                {
                    ThrowHelper.FailFast($"destination.Length < source.Length. Must allocate enough space for unkown size.");
                }

                if (header.VersionAndFlags.IsBinary && header.IsFixedSize)
                {
                    ThrowHelper.FailFast($"Should not compress individual atomic types.");
                }
            }

            if (uncompressedLength < Settings.CompressionLimit || method == CompressionMethod.None)
            {
                goto COPY;
            }

            {
                var uncompressedPayload = source.Slice(DataTypeHeader.Size + 4 + tsSize);

                var compressedHeaderSize = DataTypeHeader.Size + 4 + tsSize + 4;

                // compressors must compress! it will return > 0 if total size is less than original, otherwise just copy
                // should be very rare case that compression is useless given the Settings.CompressionLimit above
                var compressedDestination = destination.Slice(compressedHeaderSize, srcLen - compressedHeaderSize);

                var compressedLength = Compress(uncompressedPayload, compressedDestination, method);

                if (compressedLength <= 0)
                {
#if DEBUG
                    System.Diagnostics.Trace.TraceWarning("Compression is not effective.");
#endif
                    goto COPY;
                }

                header.VersionAndFlags.CompressionMethod = method;
                destination.Write(0, header);
                destination.Write(DataTypeHeader.Size, 4 + tsSize + compressedLength);
                if (hasTs)
                {
                    destination.Write(DataTypeHeader.Size + 4, source.Read<Timestamp>(DataTypeHeader.Size + 4));
                }
                destination.Write(DataTypeHeader.Size + 4 + tsSize, uncompressedLength);

                return DataTypeHeader.Size + 4 + tsSize + 4 + compressedLength;
            }

            COPY:
            var len = DataTypeHeader.Size + 4 + uncompressedLength;
            source.CopyTo(0, destination, 0, len);
            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int DecompressWithHeader(in DirectBuffer source, in DirectBuffer destination)
        {
            var header = source.Read<DataTypeHeader>(0);
            var method = header.VersionAndFlags.CompressionMethod;

            var hasTs = header.VersionAndFlags.IsTimestamped;
            var tsSize = *(int*)(&hasTs) << 3;

            if (method == CompressionMethod.None)
            {
                var srcLen = checked((int)(uint)source.Length);
                source.CopyTo(0, destination, 0, srcLen);
                return srcLen;
            }

            var compressedLength = source.ReadInt32(DataTypeHeader.Size) - 4 - tsSize;
            var expectedUncompressedLength = source.ReadInt32(DataTypeHeader.Size + 4 + tsSize);
            if (Settings.AdditionalCorrectnessChecks.Enabled)
            {
                if (destination.Length < DataTypeHeader.Size + 4 + tsSize + expectedUncompressedLength)
                {
                    ThrowHelper.FailFast(
                        $"destination.Length {destination.Length} < DataTypeHeader.Size + 4 + uncompressedLength {DataTypeHeader.Size + 4 + expectedUncompressedLength}");
                }
            }

            var compressedPayload = source.Slice(DataTypeHeader.Size + 4 + tsSize + 4, compressedLength);
            var decompressedDestination = destination.Slice(DataTypeHeader.Size + 4 + tsSize);
            var uncompressedLength = Decompress(in compressedPayload, in decompressedDestination, method);
            if (uncompressedLength <= 0)
            {
                ThrowHelper.ThrowArgumentException("Corrupted compressed data");
            }

            header.VersionAndFlags.CompressionMethod = CompressionMethod.None;
            destination.Write(0, header);
            destination.Write(DataTypeHeader.Size, tsSize + uncompressedLength);
            return DataTypeHeader.Size + 4 + tsSize + uncompressedLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Compress(in DirectBuffer source, in DirectBuffer destination, CompressionMethod method)
        {
            //if (Settings.AdditionalCorrectnessChecks.Enabled)
            //{
            //    if (destination.Length < source.Length)
            //    {
            //        ThrowHelper.FailFast($"BinarySerializer.Compress: " +
            //                             $"destination.Length {destination.Length} < source.Length {source.Length}." +
            //                             $"This method expects buffers are prepared with enough size.");
            //    }

            //    //if (source.Length < 255)
            //    //{
            //    //    ThrowHelper.FailFast($"BinarySerializer.Compress: source.Length {source.Length} < 255");
            //    //}
            //}

            if (UseCalli)
            {
                var methodIdx = (int)method;
                var ptr = CompressionMethodPointers[methodIdx];
                var level = CompressionMethodLevels[methodIdx];

                return UnsafeEx.CalliCompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    level, ptr);
            }

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
                    return 0;
                }
                source.CopyTo(0, destination, 0, len);
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
            if (UseCalli)
            {
                var methodIdx = (int)method;
                var ptr = DecompressionMethodPointers[methodIdx];

                return UnsafeEx.CalliDecompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
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

            if (method == CompressionMethod.None)
            {
                var len = source.Length;
                if (len > destination.Length)
                {
                    return 0;
                }
                source.CopyTo(0, destination, 0, len);
                return len;
            }

            ThrowHelper.ThrowNotSupportedException();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteLz4(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_lz4_ptr);
            }

            return BloscMethods.compress_lz4(source._data, (IntPtr)source._length, destination._data,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadLz4(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    BloscMethods.decompress_lz4_ptr);
            }

            return BloscMethods.decompress_lz4(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZstd(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_zstd_ptr);
            }

            return BloscMethods.compress_zstd(source._data, (IntPtr)source._length, destination._data,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZstd(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    BloscMethods.decompress_zstd_ptr);
            }

            return BloscMethods.decompress_zstd(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteZlib(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_zlib_ptr);
            }

            return BloscMethods.compress_zlib(source._data, (IntPtr)source._length, destination._data,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadZlib(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    BloscMethods.decompress_zlib_ptr);
            }

            return BloscMethods.decompress_zlib(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteDeflate(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_deflate_ptr);
            }

            return BloscMethods.compress_deflate(source._data, (IntPtr)source._length, destination._data,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadDeflate(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    BloscMethods.decompress_deflate_ptr);
            }

            return BloscMethods.decompress_deflate(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length);
        }

        internal static int WriteGZip(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliCompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    Settings.ZlibCompressionLevel,
                    BloscMethods.compress_gzip_ptr);
            }

            return BloscMethods.compress_gzip(source._data, (IntPtr)source._length, destination._data,
                (IntPtr)destination._length, Settings.ZlibCompressionLevel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadGZip(in DirectBuffer source, in DirectBuffer destination)
        {
            if (UseCalli)
            {
                return UnsafeEx.CalliDecompressUnmanagedCdecl(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length,
                    BloscMethods.decompress_gzip_ptr);
            }

            return BloscMethods.decompress_gzip(source._data, (IntPtr)source._length, destination._data, (IntPtr)destination._length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Shuffle(in DirectBuffer source, in DirectBuffer destination, byte typeSize)
        {
            if (UseCalli)
            {
                UnsafeEx.CalliShuffleUnshuffle((IntPtr)typeSize, (IntPtr)source._length, source._data, destination._data, BloscMethods.shuffle_ptr);
            }
            else
            {
                BloscMethods.shuffle((IntPtr)typeSize, (IntPtr)source._length, source._data, destination._data);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Unshuffle(in DirectBuffer source, in DirectBuffer destination, byte typeSize)
        {
            if (UseCalli)
            {
                UnsafeEx.CalliShuffleUnshuffle((IntPtr)typeSize, (IntPtr)source._length, source._data, destination._data, BloscMethods.unshuffle_ptr);
            }
            else
            {
                BloscMethods.unshuffle((IntPtr)typeSize, (IntPtr)source._length, source._data, destination._data);
            }
        }
    }
}
