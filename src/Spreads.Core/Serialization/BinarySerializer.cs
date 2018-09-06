// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// using Newtonsoft.Json;
using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Serialization.Utf8Json;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using static System.Runtime.CompilerServices.Unsafe;

#pragma warning disable 0618

namespace Spreads.Serialization
{
    /// <summary>
    /// Binary Serializer that tries to serialize objects to their blittable representation whenever possible
    /// and falls back to JSON.NET for non-blittable types. It supports versioning and custom binary converters.
    /// </summary>
    public static class BinarySerializer
    {
        /// <summary>
        /// Positive number for fixed-size types, zero for types with a custom binary converters, negative for all other types.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>()
        {
            return TypeHelper<T>.Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T value, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            return SizeOf(in value, out temporaryStream, format, timestamp);
        }

        /// <summary>
        /// Binary size of value T required for serialization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SizeOf<T>(in T value, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            if ((int)format < 100)
            {
                var size = TypeHelper<T>.SizeOf(value, out temporaryStream, format, timestamp);
                if (size >= 0)
                {
                    return size;
                }
            }

            return SizeOfSlow(in value, out temporaryStream, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int SizeOfSlow<T>(in T value, out MemoryStream temporaryStream,
            SerializationFormat format, Timestamp timestamp)
        {
            var tsSize = timestamp == default ? 0 : Timestamp.Size;

            // NB when we request binary uncompressed but items are not fixed size we use uncompressed
            // JSON so that we could iterate over values as byte Spans/DirectBuffers without uncompressing
            if (format == SerializationFormat.Json || format == SerializationFormat.Binary)
            {
                var rms = JsonSerializer.SerializeWithOffset(value, DataTypeHeader.Size + 4 + tsSize);
                rms.Position = 0;
                var header = new DataTypeHeader
                {
                    VersionAndFlags =
                    {
                         Version = 0,
                         IsBinary = false,
                         IsDelta = false,
                         IsCompressed = false,
                        IsTimestamped = tsSize > 0
                    },
                    TypeEnum = VariantHelper<T>.TypeEnum
                };
                rms.WriteAsPtr(header);

                rms.WriteAsPtr(checked((int)rms.Length - 8));

                if (tsSize > 0)
                {
                    rms.WriteAsPtr(timestamp);
                }

                rms.Position = 0;
                temporaryStream = rms;
                return checked((int)rms.Length);
            }
            else
            {
                // NB: fallback for failed binary uses Json.Deflate
                // uncompressed
                var rms = JsonSerializer.SerializeWithOffset(value, 0);
                var compressedStream =
                    RecyclableMemoryStreamManager.Default.GetStream(null, checked((int)rms.Length));

                compressedStream.WriteAsPtr(0L);

                if (tsSize > 0)
                {
                    compressedStream.WriteAsPtr(timestamp);
                }

                using (var compressor = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
                {
                    rms.Position = 0;
                    rms.CopyTo(compressor);
                    compressor.Dispose();
                }

                rms.Dispose();

                var header = new DataTypeHeader
                {
                    VersionAndFlags =
                    {
                        // NB Do not assign defaults
                        // Version = 0,
                        // IsBinary = false,
                        // IsDelta = false,
                        IsCompressed = true,
                        IsTimestamped = tsSize > 0
                    },
                    TypeEnum = VariantHelper<T>.TypeEnum
                };

                compressedStream.Position = 0;
                compressedStream.WriteAsPtr(header);

                compressedStream.WriteAsPtr(checked((int)compressedStream.Length - 8));

                compressedStream.Position = 0;

                temporaryStream = compressedStream;
                return (checked((int)compressedStream.Length));
            }
        }

        /// <summary>
        /// Destination must be pinned and have enough size.
        /// Unless writing to an "endless" buffer SizeOf must be called
        /// first to determine the size and prepare destination buffer.
        /// For blittable types (Size >= 0) this method add 8 bytes header.
        /// Use Unsafe.WriteUnaligned() to write blittable types directly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteUnsafe<T>(in T value, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            if (TypeHelper<T>.Size >= 0 && (int)format < 100)
            {
                Debug.Assert(temporaryStream == null, "For primitive types MemoryStream should not be used");
                return TypeHelper<T>.Write(value, pinnedDestination, null, format, timestamp);
            }
            return WriteSlow(in value, pinnedDestination, temporaryStream, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe int WriteSlow<T>(in T value, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            if (value == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(value));
            }

            if (temporaryStream != null)
            {
                Debug.Assert(temporaryStream.Position == 0);
#if DEBUG
                var checkSize = SizeOf(value, out MemoryStream tmp, format);
                Debug.Assert(checkSize == temporaryStream.Length, "Memory stream length must be equal to the SizeOf");
                tmp?.Dispose();
#endif
                var size = checked((int)temporaryStream.Length);
                temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
                temporaryStream.Dispose();
                return size;
            }

            if ((int)format < 100)
            {
                if (TypeHelper<T>.Size > 0 || TypeHelper<T>.HasBinaryConverter)
                {
                    return TypeHelper<T>.Write(value, pinnedDestination, null, format, timestamp);
                }
            }

            SizeOf(in value, out temporaryStream, format, timestamp);
            if (temporaryStream == null)
            {
                ThrowHelper.ThrowInvalidOperationException("Tempstream for Json or binary fallback must be returned from SizeOf");
            }

            return WriteSlow(in value, pinnedDestination, temporaryStream, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, ref byte[] destination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            var asMemory = (Memory<byte>)destination;
            return Write(in value, ref asMemory, temporaryStream, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, ref Memory<byte> destination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            return Write(in value, ref destination, temporaryStream, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int Write<T>(in T value, ref Memory<byte> destination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            var capacity = destination.Length;

            int size;
            if (temporaryStream == null)
            {
                size = SizeOf(in value, out temporaryStream, format, timestamp);
            }
            else
            {
                size = checked((int)temporaryStream.Length);
                if (temporaryStream.Length > capacity)
                {
                    ThrowHelper.ThrowInvalidOperationException("desctination doesn't have enough size");
                }

                temporaryStream.WriteToRef(ref destination.Span[0]);
            }

            if (size > capacity)
            {
                ThrowHelper.ThrowInvalidOperationException("desctination doesn't have enough size");
            }

            if (temporaryStream != null)
            {
                Debug.Assert(temporaryStream.Length == size);
                temporaryStream.WriteToRef(ref destination.Span[0]);
                temporaryStream.Dispose();
                return size;
            }

            fixed (void* ptr = &destination.Span[0])
            {
                return WriteUnsafe(value, (IntPtr)ptr, null, format, timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(IntPtr ptr, out T value)
        {
            return Read(ptr, out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(IntPtr ptr, out T value, out Timestamp timestamp)
        {
            var header = ReadUnaligned<DataTypeHeader>((void*)ptr);

            if (header.VersionAndFlags.IsBinary || TypeHelper<T>.HasBinaryConverter)
            {
                Debug.Assert(TypeHelper<T>.Size >= 0 || TypeHelper<T>.HasBinaryConverter);
                return TypeHelper<T>.Read(ptr, out value, out timestamp);
            }

            var payloadSize = ReadUnaligned<int>((void*)(ptr + DataTypeHeader.Size));
            return ReadSlow(ptr, out value, header, payloadSize, out timestamp);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe int ReadSlow<T>(IntPtr ptr, out T value, DataTypeHeader header, int payloadSize, out Timestamp timestamp)
        {
            if (header.VersionAndFlags.Version != 0)
            {
                ThrowHelper.ThrowNotImplementedException(
                    "Only version 0 is supported for unknown types that are serialized as JSON");
                value = default;
                timestamp = default;
                return -1;
            }

            if (!header.VersionAndFlags.IsCompressed)
            {
                var tsSize = header.VersionAndFlags.IsTimestamped ? Timestamp.Size : 0;

                var buffer = BufferPool<byte>.Rent(payloadSize);

                CopyBlockUnaligned(ref buffer[0], ref *(byte*)(ptr + DataTypeHeader.Size + 4 + tsSize), (uint)(payloadSize - tsSize));

                var rms = RecyclableMemoryStream.Create(RecyclableMemoryStreamManager.Default, null,
                    payloadSize, buffer, payloadSize);

                timestamp = tsSize > 0 ? ReadUnaligned<Timestamp>((void*)(ptr + DataTypeHeader.Size + 4)) : default;

                value = JsonSerializer.Deserialize<T>(rms);
                rms.Close();
                return payloadSize + DataTypeHeader.Size + 4;
            }
            else
            {
                return ReadJsonCompressed(ptr, out value, header, payloadSize, out timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe int ReadJsonCompressed<T>(IntPtr ptr, out T value, DataTypeHeader header, int payloadSize, out Timestamp timestamp)
        {
            var tsSize = header.VersionAndFlags.IsTimestamped ? Timestamp.Size : 0;

            var buffer = BufferPool<byte>.Rent(payloadSize - tsSize);

            CopyBlockUnaligned(ref buffer[0], ref *(byte*)(ptr + DataTypeHeader.Size + 4 + tsSize), (uint)(payloadSize - tsSize));
            var comrpessedStream = RecyclableMemoryStream.Create(RecyclableMemoryStreamManager.Default, null,
                payloadSize, buffer, payloadSize);

            RecyclableMemoryStream decompressedStream =
                RecyclableMemoryStreamManager.Default.GetStream();

            using (var decompressor = new DeflateStream(comrpessedStream, CompressionMode.Decompress, true))
            {
                decompressor.CopyTo(decompressedStream);
                decompressor.Dispose();
            }

            comrpessedStream.Dispose();
            decompressedStream.Position = 0;

            timestamp = tsSize > 0 ? ReadUnaligned<Timestamp>((void*)(ptr + DataTypeHeader.Size + 4)) : default;

            value = JsonSerializer.Deserialize<T>(decompressedStream);

            return payloadSize + DataTypeHeader.Size + 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ReadOnlyMemory<byte> buffer, out T value)
        {
            return Read(buffer, out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(ReadOnlyMemory<byte> buffer, out T value, out Timestamp timestamp)
        {
            using (var handle = buffer.Pin())
            {
                return Read((IntPtr)handle.Pointer, out value, out timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(Stream stream, out T value, out Timestamp timestamp)
        {
            if (stream is RecyclableMemoryStream rms && rms.IsSingleChunk)
            {
                return Read(rms.SingleChunk, out value, out timestamp);
            }

            try
            {
                var len = checked((int)stream.Length);
                rms = RecyclableMemoryStreamManager.Default.GetStream(null, len, true);

                try
                {
                    if (!rms.IsSingleChunk)
                    {
                        ThrowHelper.ThrowInvalidOperationException(
                            "RMS GetStream(null, len, true) must return single chunk");
                    }

                    stream.CopyTo(rms);
                    return Read(rms.SingleChunk, out value, out timestamp);
                }
                finally
                {
                    rms.Dispose();
                }
            }
            catch (NotSupportedException)
            {
                rms = RecyclableMemoryStreamManager.Default.GetStream();
                try
                {
                    stream.CopyTo(rms);
                    return Read(rms, out value, out timestamp);
                }
                finally
                {
                    rms.Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe Timestamp ReadTimestamp(byte* ptr, out int payloadOffset)
        {
            var isVarSize = *(ptr + 2) == 0;
            var offset = DataTypeHeader.Size + (*(byte*)&isVarSize << 2); // 4 for varsize or 0 for fixed size
            long tsLen = VersionAndFlags.TimestampFlagMask & *ptr;

            var tsMask = ~((tsLen >> 3) - 1); // all 1s or 0s

            // the only requirment if ptr + offset + 8 not causing segfault.
            var timestamp = (Timestamp)(tsMask &  ReadUnaligned<long>(ptr + offset));

            payloadOffset = offset + (int)tsLen;
            return timestamp;
        }
    }

#pragma warning restore 0618
}
