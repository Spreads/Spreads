// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
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
            SerializationFormat format = SerializationFormat.Binary)
        {
            return SizeOf(in value, out temporaryStream, format);
        }

        /// <summary>
        /// Binary size of value T after serialization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(in T value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            if ((int)format < 100)
            {
                var size = TypeHelper<T>.SizeOf(value, out temporaryStream, format);
                if (size >= 0)
                {
                    return size;
                }
            }
            var ms = Json.SerializeWithHeader(value, format);
            temporaryStream = ms;
            return (checked((int)ms.Length));
        }

        /// <summary>
        /// Destination must be pinned and have enough size.
        /// Unless writing to an "endless" buffer SizeOf must be called
        /// first to determine the size and prepare destination buffer.
        /// For blittable types (Size >= 0) this method add 8 bytes header.
        /// Use Unsafe.WriteUnaligned() to write blittable types directly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteUnsafe<T>(in T value, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary)
        {
            if (TypeHelper<T>.Size >= 0 && (int)format < 100)
            {
                Debug.Assert(temporaryStream == null, "For primitive types MemoryStream should not be used");
                return TypeHelper<T>.Write(value, pinnedDestination, null, format);
            }
            return WriteSlow(in value, pinnedDestination, temporaryStream, format);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe int WriteSlow<T>(in T value, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary)
        {
            if (value == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(value));
            }

            int size;
            if (temporaryStream != null)
            {
                Debug.Assert(temporaryStream.Position == 0);
#if DEBUG
                    var checkSize = SizeOf(value, out MemoryStream tmp, format);
                    Debug.Assert(checkSize == temporaryStream.Length, "Memory stream length must be equal to the SizeOf");
                    tmp?.Dispose();
#endif
                size = checked((int)temporaryStream.Length);
                temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
                temporaryStream.Dispose();
                return size;
            }

            size = TypeHelper<T>.Size; //TypeHelper<T>.SizeOf(value, out tempStream);

            if ((int)format < 100)
            {
                if (TypeHelper<T>.Size > 0 || TypeHelper<T>.HasBinaryConverter)
                {
                    return TypeHelper<T>.Write(value, pinnedDestination, null, format);
                }
            }

            var jsonStream = Json.SerializeWithHeader(value, format);
            size = checked((int)jsonStream.Length);
            jsonStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
            jsonStream.Dispose();
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Write<T>(T value, ref byte[] destination,
            MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            var asMemory = (Memory<byte>)destination;
            return Write(in value, ref asMemory, temporaryStream, format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Write<T>(T value, ref Memory<byte> destination,
            MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            return Write(in value, ref destination, temporaryStream, format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Write<T>(in T value, ref Memory<byte> destination,
            MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            var capacity = destination.Length;
            int size;
            if (temporaryStream == null)
            {
                size = SizeOf(in value, out temporaryStream, format);
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
                return WriteUnsafe(value, (IntPtr)ptr, null, format);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(IntPtr ptr, out T value)
        {
            var size = ReadUnaligned<int>((void*)ptr);
            var header = ReadUnaligned<DataTypeHeader>((void*)(ptr + 4));

            if (header.VersionAndFlags.IsBinary)
            {
                Debug.Assert(TypeHelper<T>.Size >= 0 || TypeHelper<T>.HasBinaryConverter);
                return TypeHelper<T>.Read(ptr, out value);
            }

            if (header.VersionAndFlags.Version != 0)
            {
                ThrowHelper.ThrowNotImplementedException("Only version 0 is supported for unknown types that are serialized as JSON");
                value = default;
                return -1;
            }

            if (!header.VersionAndFlags.IsCompressed)
            {
                var stream = new UnmanagedMemoryStream((byte*)(ptr + 8), size - 8);
                value = Json.Deserialize<T>(stream);
                return size;
            }
            else
            {
                var comrpessedStream = new UnmanagedMemoryStream((byte*)(ptr + 8), size - 8);
                RecyclableMemoryStream decompressedStream =
                    RecyclableMemoryStreamManager.Default.GetStream();

                using (var decompressor = new DeflateStream(comrpessedStream, CompressionMode.Decompress, true))
                {
                    decompressor.CopyTo(decompressedStream);
                    decompressor.Close();
                }
                comrpessedStream.Dispose();
                decompressedStream.Position = 0;
                value = Json.Deserialize<T>(decompressedStream);
                return size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(ReadOnlyMemory<byte> buffer, out T value)
        {
            return Read(in buffer, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(in ReadOnlyMemory<byte> buffer, out T value)
        {
            using (var handle = buffer.Pin())
            {
                return Read((IntPtr)handle.Pointer, out value);
            }
        }

        public static JsonSerializer Json => JsonSerializer.Instance;

        public sealed class JsonSerializer
        {
            private class JsonNetArrayPool : IArrayPool<char>
            {
                public static readonly JsonNetArrayPool Pool = new JsonNetArrayPool();

                public char[] Rent(int minimumLength)
                {
                    return BufferPool<char>.Rent(minimumLength);
                }

                public void Return(char[] array)
                {
                    BufferPool<char>.Return(array, true);
                }
            }

            private readonly Newtonsoft.Json.JsonSerializer _serializer;
            internal static JsonSerializer Instance = new JsonSerializer();

            private JsonSerializer()
            {
                _serializer = new Newtonsoft.Json.JsonSerializer();
            }

            public MemoryStream Serialize<T>(T value)
            {
                var ms = RecyclableMemoryStreamManager.Default.GetStream();
                using (var writer = new StreamWriter(ms, Encoding.UTF8, 4096, true))
                using (var jsonwriter = new JsonTextWriter(writer))
                {
                    jsonwriter.ArrayPool = JsonNetArrayPool.Pool;
                    _serializer.Serialize(writer, value);
                }

                // we created the stream with initial positoin 0, return to that position
                ms.Position = 0;
                return ms;
            }

            internal MemoryStream SerializeWithHeader<T>(T value, SerializationFormat format)
            {
                // NB We may wated binary format but falled back here,
                // then we still prefer compressed version
                // Return uncompressed JSON only when explicitly asked for.
                if (format == SerializationFormat.Json)
                {
                    RecyclableMemoryStream ms = RecyclableMemoryStreamManager.Default.GetStream();
                    ms.WriteAsPtr(0L);
                    using (var writer = new StreamWriter(ms, Encoding.UTF8, 4096, true))
                    using (var jsonwriter = new JsonTextWriter(writer))
                    {
                        jsonwriter.ArrayPool = JsonNetArrayPool.Pool;
                        _serializer.Serialize(writer, value);
                    }

                    // we created the stream with initial positoin 0, return to that position

                    ms.Position = 0;

                    ms.WriteAsPtr(checked((int)ms.Length));
                    var header = new DataTypeHeader
                    {
                        VersionAndFlags = {
                            Version = 0,
                            IsBinary = false,
                            IsDelta = false,
                            IsCompressed = false },
                        TypeEnum = VariantHelper<T>.TypeEnum
                    };
                    ms.WriteAsPtr(header);
                    ms.Position = 0;
                    return ms;
                }
                else
                {
                    RecyclableMemoryStream ms =
                        RecyclableMemoryStreamManager.Default.GetStream("JSON.SerializeWithHeader", 4096, true);
                    // no header before compression ms.WriteAsPtr(0L);
                    using (var writer = new StreamWriter(ms, Encoding.UTF8, 4096, true))
                    using (var jsonwriter = new JsonTextWriter(writer))
                    {
                        jsonwriter.ArrayPool = JsonNetArrayPool.Pool;
                        _serializer.Serialize(writer, value);
                    }
                    var compressedStream =
                        RecyclableMemoryStreamManager.Default.GetStream(null, checked((int)ms.Length));
                    compressedStream.WriteAsPtr(0L);
                    using (var compressor = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
                    {
                        ms.Position = 0;
                        ms.CopyTo(compressor);
                        compressor.Close();
                    }
                    ms.Dispose();

                    compressedStream.Position = 0;
                    compressedStream.WriteAsPtr(checked((int)compressedStream.Length));
                    var header = new DataTypeHeader
                    {
                        VersionAndFlags = {
                            Version = 0,
                            IsBinary = false,
                            IsDelta = false,
                            IsCompressed = true },
                        TypeEnum = VariantHelper<T>.TypeEnum
                    };
                    compressedStream.WriteAsPtr(header);
                    compressedStream.Position = 0;
                    return compressedStream;
                }
            }

            public T Deserialize<T>(Stream stream)
            {
                using (var reader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8, true, 4096, true)))
                {
                    reader.ArrayPool = JsonNetArrayPool.Pool;
                    return _serializer.Deserialize<T>(reader);
                }
            }

            public MemoryStream Serialize(object value)
            {
                var ms = RecyclableMemoryStreamManager.Default.GetStream();
                using (var writer = new StreamWriter(ms, Encoding.UTF8, 4096, true))
                using (var jsonwriter = new JsonTextWriter(writer))
                {
                    jsonwriter.ArrayPool = JsonNetArrayPool.Pool;
                    _serializer.Serialize(writer, value);
                }
                // we created the stream with initial positoin 0, return to that position
                ms.Position = 0;
                return ms;
            }

            public object Deserialize(Stream stream, Type ty)
            {
                using (var reader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8, true, 4096, true)))
                {
                    reader.ArrayPool = JsonNetArrayPool.Pool;
                    _serializer.Deserialize(reader);
                    return _serializer.Deserialize(reader, ty);
                }
            }
        }
    }

#pragma warning restore 0618
}