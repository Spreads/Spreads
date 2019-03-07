// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.DataTypes;
#if NETCOREAPP2_1
using System.IO.Compression;
#endif

namespace Spreads.Serialization.Experimental
{
    public static unsafe partial class BinarySerializerEx
    {
        /// By default compression sets CMP flag, compresses payload, prepends
        /// compressed bytes with original length and then writes compressed
        /// payload size + 4 bytes (original size).
        /// Compression format:
        ///
        ///
        ///
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |     TEOFS     |     TEOFS1    |     TEOFS2    |
        /// +---------------------------------------------------------------+
        /// |                 Compressed Payload Length + 4                 |
        /// +---------------------------------------------------------------+
        /// |                 Uncompressed Payload Length                   |
        /// +---------------------------------------------------------------+
        /// |                       Compressed Payload                      |
        /// |                              ...                              |

        private static readonly int[] CompressionMethodLevels = { 0, Settings.ZlibCompressionLevel, Settings.LZ4CompressionLevel, Settings.ZstdCompressionLevel };

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

            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (destination.Length < source.Length)
                {
                    ThrowHelper.FailFast($"destination.Length < source.Length. Must allocate enough space for unkown size.");
                }

                if (header.VersionAndFlags.IsBinary && header.IsTypeFixedSize)
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
            source.Slice(0, len).CopyTo(destination);
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
                source.CopyTo(destination);
                return srcLen;
            }

            var compressedLength = source.ReadInt32(DataTypeHeader.Size) - 4 - tsSize;
            var expectedUncompressedLength = source.ReadInt32(DataTypeHeader.Size + 4 + tsSize);
            if (AdditionalCorrectnessChecks.Enabled)
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
            //if (AdditionalCorrectnessChecks.Enabled)
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
