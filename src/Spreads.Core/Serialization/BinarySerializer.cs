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

        [Obsolete("Consider using an overload with memory stream")]
        public static int SizeOf<T>(T value) {
            MemoryStream temp;
            var size = SizeOf<T>(value, out temp);
            // NB we could use CWT if T is reference type, but that defeats the purpose of the overload with ms
            temp?.Dispose();
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>() {
            return TypeHelper<T>.Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T value, out MemoryStream payloadStream) {
            var size = TypeHelper<T>.SizeOf(value, out payloadStream);
            return size >= 0 ? size : Bson.SizeOfBson<T>(value, out payloadStream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, ref DirectBuffer destination, uint offset = 0u, MemoryStream payloadStream = null) {
            int size;
            if (payloadStream != null) {
#if DEBUG
                MemoryStream tmp;
                var checkSize = SizeOf(value, out tmp);
                Debug.Assert(checkSize == payloadStream.Length, "Memory stream length must ve equal to the SizeOf");
#endif
                size = 8 + checked((int)payloadStream.Length);

                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                destination.WriteInt32(0, size);
                destination.WriteByte(5, 0);

                payloadStream.WriteToPtr(destination.Data + (int)offset + 8);
                // NB memoryStream is owned outside, do not dispose here
                return size;
            }
            MemoryStream tempStream;
            size = TypeHelper<T>.SizeOf(value, out tempStream);
            if (size > 0) {
                Debug.Assert(tempStream == null, "Fixed-size values should not produce temp MemoryStream");
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                var size2 = TypeHelper<T>.Write(value, ref destination, offset);
                Debug.Assert(size == size2, "Size and SizeOf must be equal for fixed-sized types.");
                return size;
            }
            if (size == 0) {
                // SizeOf returned a temp memory stream, just call this method recursively
                if (tempStream == null) return TypeHelper<T>.Write(value, ref destination, offset);
                size = checked((int)tempStream.Length);
                if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
                tempStream.WriteToPtr(destination.Data + (int)offset);
                // NB tempStream is owned here, dispose it
                tempStream.Dispose();
                return size;
            }

            var bsonStream = Bson.Serialize<T>(value);
            size = checked((int)bsonStream.Length);
            if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
            bsonStream.WriteToPtr(destination.Data + (int)offset);
            return size;
        }

        public static unsafe int Write<T>(T value, byte[] destination, uint offset, MemoryStream memoryStream = null) {
            fixed (byte* ptr = &destination[0]) {
                var buffer = new DirectBuffer(destination.Length, (IntPtr)ptr);
                return Write(value, ref buffer, offset, memoryStream);
            }
        }

        //public static int Serialize<T>(T value, Stream destination, MemoryStream memoryStream = null) {
        //    // TODO length check
        //    throw new NotImplementedException();
        //}

        public static unsafe int Read<T>(IntPtr ptr, ref T value) {
            var size = TypeHelper<T>.Size;
            if (size >= 0) {
                return TypeHelper<T>.Read(ptr, ref value);
            }
            size = *(int*)ptr;
            var stream = new UnmanagedMemoryStream((byte*)ptr + 8, size - 8);
            value = Bson.Deserialize<T>(stream);
            return size;
        }

        public static unsafe int Read<T>(byte[] buffer, ref T value) {
            fixed (byte* ptr = &buffer[0]) {
                return Read((IntPtr)ptr, ref value);
            }
        }

        internal static BsonSerializer Bson => BsonSerializer.Instance;

        internal sealed class BsonSerializer {
            readonly JsonSerializer _serializer;
            internal static BsonSerializer Instance = new BsonSerializer();
            private BsonSerializer() {
                _serializer = new JsonSerializer();
            }

            public int SizeOfBson<T>(T value, out MemoryStream memoryStream) {
                memoryStream = RecyclableMemoryManager.MemoryStreams.GetStream();
                using (var writer = new BsonWriter(memoryStream)) {
                    _serializer.Serialize(writer, value);
                }
                memoryStream.Position = 0;
                return 8 + checked((int)memoryStream.Length);
            }

            public MemoryStream Serialize<T>(T value) {
                var ms = RecyclableMemoryManager.MemoryStreams.GetStream();
                using (var writer = new BsonWriter(ms)) {
                    _serializer.Serialize(writer, value);
                }
                ms.Position = 0;
                return ms;
            }

            public T Deserialize<T>(Stream stream) {
                using (var reader = new BsonReader(stream, typeof(T).IsArray, DateTimeKind.Unspecified)) {
                    return _serializer.Deserialize<T>(reader);
                }
            }
        }
    }
#pragma warning restore 0618
}
