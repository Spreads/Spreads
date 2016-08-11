/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Spreads.Buffers;
#pragma warning disable 0618
namespace Spreads.Serialization {

    public static class BinarySerializer {

        //private class JsonNetArrayPoolImpl : Newtonsoft.Json.IArrayPool<char> {
        //    public static readonly JsonNetArrayPoolImpl Instance = new JsonNetArrayPoolImpl();

        //    public char[] Rent(int minimumLength) {
        //        return ArrayPool<char>.Shared.Rent(minimumLength);
        //    }

        //    public void Return(char[] array) {
        //        ArrayPool<char>.Shared.Return(array, true);
        //    }
        //}

        //[Obsolete("Consider using an overload with memory stream")]
        //public static int SizeOf<T>(T value) {
        //    MemoryStream temp;
        //    var size = SizeOf<T>(value, out temp);
        //    // NB we could use CWT if T is reference type, but that defeats the purpose of the overload with ms
        //    temp?.Dispose();
        //    return size;
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>() {
            return TypeHelper<T>.Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T value, out MemoryStream temporaryStream) {
            var size = TypeHelper<T>.SizeOf(value, out temporaryStream);
            return size >= 0 ? size : Bson.SizeOfBson<T>(value, out temporaryStream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, ref DirectBuffer destination, uint offset = 0u, MemoryStream temporaryStream = null) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            int size;
            if (temporaryStream != null) {
                Debug.Assert(temporaryStream.Position == 0);
#if DEBUG
                MemoryStream tmp;
                var checkSize = SizeOf(value, out tmp);
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
            if (size > 0) {
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                var size2 = TypeHelper<T>.Write(value, ref destination, offset);
                Debug.Assert(size == size2, "Size and SizeOf must be equal for fixed-sized types.");
                return size;
            }
            if (size == 0) {
                MemoryStream tempStream;
                size = TypeHelper<T>.SizeOf(value, out tempStream);
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                // SizeOf returned a temp memory stream, just call this method recursively
                if (tempStream == null) return TypeHelper<T>.Write(value, ref destination, offset);
                Debug.Assert(size == checked((int)tempStream.Length));
                tempStream.WriteToPtr(destination.Data + (int)offset);
                // NB tempStream is owned here, dispose it
                tempStream.Dispose();
                return size;
            }

            var bsonStream = Bson.Serialize<T>(value);
            size = checked((int)bsonStream.Length);
            if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
            bsonStream.WriteToPtr(destination.Data + (int)offset);
            bsonStream.Dispose();
            return size;
        }

        public static unsafe int Write<T>(T value, byte[] destination, uint offset = 0u, MemoryStream temporaryStream = null) {
            fixed (byte* ptr = &destination[0]) {
                var buffer = new DirectBuffer(destination.Length, (IntPtr)ptr);
                return Write(value, ref buffer, offset, temporaryStream);
            }
        }


        public static unsafe int Read<T>(IntPtr ptr, ref T value) {
            var size = TypeHelper<T>.Size;
            if (size >= 0) {
                return TypeHelper<T>.Read(ptr, ref value);
            }
            size = *(int*)ptr;
            var stream = new UnmanagedMemoryStream((byte*)ptr, size);
            value = Bson.Deserialize<T>(stream);
            return size;
        }

        public static unsafe int Read<T>(byte[] buffer, int offset, ref T value) {
            fixed (byte* ptr = &buffer[offset]) {
                var size = *(int*)ptr;
                if ((uint)offset + size > buffer.Length) throw new ArgumentException("Buffer is too small");
                return Read((IntPtr)ptr, ref value);
            }
        }

        public static unsafe int Read<T>(byte[] buffer, ref T value) {
            return Read<T>(buffer, 0, ref value);
        }

        public static unsafe int Read<T>(DirectBuffer buffer, int offset, ref T value) {
            var len = *(int*)buffer.Data;
            if ((uint)offset + len > buffer.Length) throw new ArgumentException("Buffer is too small");
            return Read<T>(buffer.Data, ref value);
        }

        public static int Read<T>(DirectBuffer buffer, ref T value) {
            return Read<T>(buffer, 0, ref value);
        }

        internal static BsonSerializer Bson => BsonSerializer.Instance;

        internal sealed class BsonSerializer {
            readonly JsonSerializer _serializer;
            internal static BsonSerializer Instance = new BsonSerializer();
            private BsonSerializer() {
                _serializer = new JsonSerializer();
            }

            public int SizeOfBson<T>(T value, out MemoryStream memoryStream) {
                memoryStream = Serialize<T>(value);
                memoryStream.Position = 0;
                return checked((int)memoryStream.Length);
            }

            public MemoryStream Serialize<T>(T value) {
                var ms = RecyclableMemoryManager.MemoryStreams.GetStream();
                ms.WriteAsPtr<long>(0L);
                using (var writer = new BsonWriter(ms)) {
                    _serializer.Serialize(writer, value);
                    writer.CloseOutput = false;
                }
                ms.Position = 0;
                ms.WriteAsPtr<int>(checked((int)ms.Length));
                ms.Position = 0;
                return ms;
            }

            public T Deserialize<T>(Stream stream) {
                // skip header
                stream.Position = 8;
                using (var reader = new BsonReader(stream, typeof(T).IsArray, DateTimeKind.Unspecified)) {
                    return _serializer.Deserialize<T>(reader);
                }
            }
        }
    }
#pragma warning restore 0618
}
