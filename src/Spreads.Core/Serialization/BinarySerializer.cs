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
    public static class BinarySerializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>()
        {
            return TypeHelper<T>.Size;
        }

        /// <summary>
        /// Binary size of value T after serialization. When i
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
        public unsafe static int Write<T>(T value, ref DirectBuffer destination, uint offset = 0u,
            MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            var size = TypeHelper<T>.Size;
            if (size > 0)
            {
                Debug.Assert(temporaryStream == null, "For primitive types MemoryStream should not be populated");
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                var pointer = destination.Data + (int)offset;
                //Debug.Assert(pointer.ToInt64() % size == 0, "Unaligned unsafe write");
                Unsafe.Write<T>((void*)pointer, value);
                return size;
            }
            return WriteSlow(value, ref destination, offset, temporaryStream, compression);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteSlow<T>(T value, ref DirectBuffer destination, uint offset = 0u, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            int size;
            if (temporaryStream != null)
            {
                Debug.Assert(temporaryStream.Position == 0);
#if DEBUG
                MemoryStream tmp;
                var checkSize = SizeOf(value, out tmp, compression);
                Debug.Assert(checkSize == temporaryStream.Length, "Memory stream length must ve equal to the SizeOf");
                tmp?.Dispose();
#endif
                size = checked((int)temporaryStream.Length);
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                temporaryStream.WriteToPtr(destination.Data + (int)offset);
                // NB temporaryStream is owned outside, do not dispose here
                return size;
            }

            size = TypeHelper<T>.Size; //TypeHelper<T>.SizeOf(value, out tempStream);

            if (size == 0)
            {
                MemoryStream tempStream;
                size = TypeHelper<T>.SizeOf(value, out tempStream, compression);
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                // SizeOf returned a temp memory stream, just call this method recursively
                if (tempStream == null) return TypeHelper<T>.Write(value, ref destination, offset, null, compression);
                Debug.Assert(size == checked((int)tempStream.Length));
                tempStream.WriteToPtr(destination.Data + (int)offset);
                // NB tempStream is owned here, dispose it
                tempStream.Dispose();
                return size;
            }

            var jsonStream = Json.SerializeWithHeader(value, compression);
            size = checked((int)jsonStream.Length);
            if (destination.Length < offset + size)
                throw new ArgumentException("Value size is too big for destination");
            jsonStream.WriteToPtr(destination.Data + (int)offset);
            jsonStream.Dispose();
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Write<T>(T value, byte[] destination, uint offset = 0u, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            fixed (byte* ptr = &destination[0])
            {
                var buffer = new DirectBuffer(destination.Length, (IntPtr)ptr);
                return Write(value, ref buffer, offset, temporaryStream);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static int Write<T>(T value, ref PreservedBuffer<byte> destination, uint offset = 0u,
            MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            var tmpArraySegment = default(ArraySegment<byte>);
            var handle = default(BufferHandle);
            try
            {
                void* pointer;
                if (!destination.Buffer.TryGetPointer(out pointer))
                {
                    handle = destination.Buffer.Pin();
                    if (destination.Buffer.TryGetArray(out tmpArraySegment))
                    {
                        pointer = (void*)Marshal.UnsafeAddrOfPinnedArrayElement(tmpArraySegment.Array, tmpArraySegment.Offset);
                    }
                }
                var db = new DirectBuffer(tmpArraySegment.Count, pointer);
                return Write(value, ref db, offset, temporaryStream);
            }
            finally
            {
                handle.Free();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(IntPtr ptr, ref T value)
        {
            var size = TypeHelper<T>.Size;
            if (size >= 0)
            {
                return TypeHelper<T>.Read(ptr, ref value);
            }
            size = *(int*)ptr;
            var versionFlags = *(byte*)(ptr + 4);
            var version = versionFlags >> 4;
            var isCompressed = (versionFlags & 0b0000_0001) != 0;
            if (version != 0) throw new NotImplementedException("Only version 0 is supported for unknown types that are serialized as JSON");
            if (!isCompressed)
            {
                var stream = new UnmanagedMemoryStream((byte*)(ptr + 8), size);
                value = Json.Deserialize<T>(stream);
                return size;
            }
            else
            {
                throw new NotImplementedException("TODO Json compression");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(byte[] buffer, int offset, ref T value)
        {
            fixed (byte* ptr = &buffer[offset])
            {
                var size = *(int*)ptr;
                if ((uint)offset + size > buffer.Length) throw new ArgumentException("Buffer is too small");
                return Read((IntPtr)ptr, ref value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(PreservedBuffer<byte> source, uint offset, ref T value)
        {
            var handle = default(BufferHandle);
            try
            {
                void* pointer;
                if (!source.Buffer.TryGetPointer(out pointer))
                {
                    handle = source.Buffer.Pin();
                    ArraySegment<byte> tmpArraySegment;
                    if (source.Buffer.TryGetArray(out tmpArraySegment))
                    {
                        pointer = (void*)Marshal.UnsafeAddrOfPinnedArrayElement(tmpArraySegment.Array, tmpArraySegment.Offset + (int)offset);
                    }
                }
                return Read((IntPtr)pointer, ref value);
            }
            finally
            {
                handle.Free();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(byte[] buffer, ref T value)
        {
            return Read<T>(buffer, 0, ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int Read<T>(DirectBuffer buffer, int offset, ref T value)
        {
            var len = *(int*)buffer.Data;
            if ((uint)offset + len > buffer.Length) throw new ArgumentException("Buffer is too small");
            return Read<T>(buffer.Data, ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DirectBuffer buffer, ref T value)
        {
            return Read<T>(buffer, 0, ref value);
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
                ms.WriteAsPtr<long>(0L);
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
                    ms.WriteAsPtr<int>(checked((int)ms.Length));
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