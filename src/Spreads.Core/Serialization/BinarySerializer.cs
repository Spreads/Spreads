// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using Spreads.Buffers;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 0618

namespace Spreads.Serialization
{
    // TODO handle.Free() in finally blocks in converters

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

        /// <summary>
        /// Binary size of value T after serialization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T value, out MemoryStream temporaryStream, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            var size = TypeHelper<T>.SizeOf(value, out temporaryStream, compression);
            if (size >= 0)
            {
                return size;
            }
            var ms = Json.SerializeWithHeader(value, compression);
            temporaryStream = ms;
            return (checked((int)ms.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Write<T>(T value, ref Memory<byte> destination, uint offset = 0u,
            MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (TypeHelper<T>.IsPinnable)
            {
                var size = TypeHelper<T>.Size;
                Debug.Assert(temporaryStream == null, "For primitive types MemoryStream should not be populated");
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                // TODO this simple thing could be probably done without pinning
                var handle = destination.Retain(true);

                var ptr = (void*)((IntPtr)handle.PinnedPointer + (int)offset);

                Unsafe.Write(ptr, value);

                handle.Dispose();

                return size;
            }
            return WriteSlow(value, ref destination, offset, temporaryStream, compression);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe int WriteSlow<T>(T value, ref Memory<byte> destination, uint offset = 0u, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var handle = destination.Retain(true);
            try
            {
                var ptr = (IntPtr)handle.PinnedPointer + (int)offset;

                int size;
                if (temporaryStream != null)
                {
                    Debug.Assert(temporaryStream.Position == 0);
#if DEBUG
                    var checkSize = SizeOf(value, out MemoryStream tmp, compression);
                    Debug.Assert(checkSize == temporaryStream.Length, "Memory stream length must be equal to the SizeOf");
                    tmp?.Dispose();
#endif
                    size = checked((int)temporaryStream.Length);
                    if (destination.Length < offset + size)
                        throw new ArgumentException("Value size is too big for destination");
                    temporaryStream.WriteToPtr(ptr);
                    // NB temporaryStream is owned outside, do not dispose here
                    return size;
                }

                size = TypeHelper<T>.Size; //TypeHelper<T>.SizeOf(value, out tempStream);

                if (size == 0)
                {
                    size = TypeHelper<T>.SizeOf(value, out MemoryStream tempStream, compression);
                    if (destination.Length < offset + size)
                    {
                        throw new ArgumentException("Value size is too big for destination");
                    }
                    // SizeOf returned a temp memory stream, just call this method recursively
                    if (tempStream == null)
                    {
                        return TypeHelper<T>.Write(value, ref destination, offset, null, compression);
                    }
                    else
                    {
                        Debug.Assert(size == checked((int)tempStream.Length));
                        tempStream.WriteToPtr(ptr);
                        // NB tempStream is owned here, dispose it
                        tempStream.Dispose();
                        return size;
                    }
                }

                var jsonStream = Json.SerializeWithHeader(value, compression);
                size = checked((int)jsonStream.Length);
                if (destination.Length < offset + size)
                {
                    throw new ArgumentException("Value size is too big for destination");
                }
                jsonStream.WriteToPtr(ptr);
                jsonStream.Dispose();
                return size;
            }
            finally
            {
                handle.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, byte[] destination, uint offset = 0u, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            var buffer = (Memory<byte>)destination;
            return Write(value, ref buffer, offset, temporaryStream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, ref PreservedBuffer<byte> destination, uint offset = 0u,
            MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            var buffer = destination.Buffer;
            return Write(value, ref buffer, offset, temporaryStream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(IntPtr ptr, out T value)
        {
            if (TypeHelper<T>.IsPinnable || typeof(T) == typeof(DateTime))
            {
                // NB this case is JIT-time constant
                return TypeHelper<T>.Read(ptr, out value);
            }
            if (TypeHelper<T>.HasBinaryConverter)
            {
                return TypeHelper<T>.Read(ptr, out value);
            }

            var size = *(int*)ptr;
            var versionFlags = *(byte*)(ptr + 4);
            var version = versionFlags >> 4;
            var isCompressed = (versionFlags & 0b0000_0001) != 0;
            if (version != 0)
            {
                ThrowHelper.ThrowNotImplementedException("Only version 0 is supported for unknown types that are serialized as JSON");
                value = default(T);
                return -1;
            }
            if (!isCompressed)
            {
                var stream = new UnmanagedMemoryStream((byte*)(ptr + 8), size);
                value = Json.Deserialize<T>(stream);
                return size;
            }
            ThrowHelper.ThrowNotImplementedException("TODO JSON compression");
            value = default(T);
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(byte[] buffer, int offset, out T value)
        {
            fixed (byte* ptr = &buffer[offset])
            {
                var size = *(int*)ptr;
                if ((uint)offset + size > buffer.Length) throw new ArgumentException("Buffer is too small");
                return Read((IntPtr)ptr, out value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(PreservedBuffer<byte> source, uint offset, out T value)
        {
            var handle = source.Buffer.Retain(true);
            try
            {
                return Read((IntPtr)handle.PinnedPointer, out value);
            }
            finally
            {
                handle.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(byte[] buffer, out T value)
        {
            return Read(buffer, 0, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(Memory<byte> buffer, int offset, out T value)
        {
            var handle = buffer.Retain(true);
            try
            {
                var ptr = (IntPtr)handle.PinnedPointer + offset;
                var len = *(int*)ptr;
                if ((uint)offset + len > buffer.Length) throw new ArgumentException("Buffer is too small");
                return Read(ptr, out value);
            }
            finally
            {
                handle.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(Memory<byte> buffer, out T value)
        {
            return Read(buffer, 0, out value);
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

            //public int SizeOfJson<T>(T value, out MemoryStream memoryStream) {
            //    memoryStream = Serialize<T>(value);
            //    memoryStream.Position = 0;
            //    return checked((int)memoryStream.Length);
            //}

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

            internal MemoryStream SerializeWithHeader<T>(T value, CompressionMethod compression)
            {
                RecyclableMemoryStream ms = (RecyclableMemoryStream)RecyclableMemoryStreamManager.Default.GetStream("JSON.SerializeWithHeader", 4096, true);
                ms.WriteAsPtr(0L);
                using (var writer = new StreamWriter(ms, Encoding.UTF8, 4096, true))
                using (var jsonwriter = new JsonTextWriter(writer))
                {
                    jsonwriter.ArrayPool = JsonNetArrayPool.Pool;
                    _serializer.Serialize(writer, value);
                }
                // we created the stream with initial positoin 0, return to that position
                if (compression == CompressionMethod.DefaultOrNone)
                {
                    ms.Position = 0;
                    ms.WriteAsPtr(checked((int)ms.Length));
                    ms.Position = 0;
                    return ms;
                }
                else
                {
                    //foreach (var chunk in ms.Chunks)
                    //{
                    //    if (chunk.Count != ms.Length)
                    //    {
                    //        throw new NotImplementedException("TODO JSON compression");
                    //    }
                    //    CompressedArrayBinaryConverter<byte>.Instance.Write(chunk,)

                    //}
                    throw new NotImplementedException("TODO JSON compression");
                    // max buffer
                    //var buffer = BufferPool<byte>.Rent(8 + 16 + (Environment.ProcessorCount * 4) + checked((int)ms.Length));
                    //var array = ms.ToArray();
                    //ms.Dispose();
                    //CompressedArrayBinaryConverter<byte>.Instance.Write(array,)
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