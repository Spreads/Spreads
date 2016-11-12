// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using Spreads.Buffers;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable 0618

namespace Spreads.Serialization {

    public static class BinarySerializer {
        //private class JsonNetArrayPoolImpl : Newtonsoft.Json.IArrayPool<char> {
        //    public static readonly JsonNetArrayPoolImpl Instance = new JsonNetArrayPoolImpl();

        //    public char[] Rent(int minimumLength) {
        //        return BufferPool<char>.Shared.Rent(minimumLength);
        //    }

        //    public void Return(char[] array) {
        //        BufferPool<char>.Shared.Return(array, true);
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
            return size >= 0 ? size : Json.SizeOfJson<T>(value, out temporaryStream);
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

            var jsonStream = Json.Serialize<T>(value);
            size = checked((int)jsonStream.Length);
            if (destination.Length < offset + size) throw new ArgumentException("Value size is too big for destination");
            jsonStream.WriteToPtr(destination.Data + (int)offset);
            jsonStream.Dispose();
            return size;
        }

        public static unsafe int Write<T>(T value, byte[] destination, uint offset = 0u, MemoryStream temporaryStream = null) {
            fixed (byte* ptr = &destination[0]) {
                var buffer = new DirectBuffer(destination.Length, (IntPtr)ptr);
                return Write(value, ref buffer, offset, temporaryStream);
            }
        }

        public unsafe static int Write<T>(T value, ref PreservedMemory<byte> destination, uint offset = 0u,
            MemoryStream temporaryStream = null) {
            var tmpArraySegment = default(ArraySegment<byte>);
            var fixedMemory = default(FixedMemory<byte>);
            try {
                void* pointer;
                if (!destination.Memory.TryGetPointer(out pointer)) {
                    fixedMemory = destination.Fix();
                    if (fixedMemory.Memory.TryGetArray(out tmpArraySegment)) {
                        pointer = (void*)Marshal.UnsafeAddrOfPinnedArrayElement(tmpArraySegment.Array, tmpArraySegment.Offset);
                    }
                }
                var db = new DirectBuffer(tmpArraySegment.Count, pointer);
                return Write(value, ref db, offset, temporaryStream);
            } finally {
                if (!fixedMemory.Equals(default(FixedMemory<byte>))) {
                    fixedMemory.Dispose();
                }
            }
        }

        public static unsafe int Read<T>(IntPtr ptr, ref T value) {
            var size = TypeHelper<T>.Size;
            if (size >= 0) {
                return TypeHelper<T>.Read(ptr, ref value);
            }
            size = *(int*)ptr;
            var stream = new UnmanagedMemoryStream((byte*)ptr, size);
            value = Json.Deserialize<T>(stream);
            return size;
        }

        public static unsafe int Read<T>(byte[] buffer, int offset, ref T value) {
            fixed (byte* ptr = &buffer[offset]) {
                var size = *(int*)ptr;
                if ((uint)offset + size > buffer.Length) throw new ArgumentException("Buffer is too small");
                return Read((IntPtr)ptr, ref value);
            }
        }

        public static unsafe int Read<T>(PreservedMemory<byte> source, uint offset, ref T value) {
            var tmpArraySegment = default(ArraySegment<byte>);
            var fixedMemory = default(FixedMemory<byte>);
            try {
                void* pointer;
                if (!source.Memory.TryGetPointer(out pointer)) {
                    fixedMemory = source.Fix();
                    if (fixedMemory.Memory.TryGetArray(out tmpArraySegment)) {
                        pointer = (void*)Marshal.UnsafeAddrOfPinnedArrayElement(tmpArraySegment.Array, tmpArraySegment.Offset + (int)offset);
                    }
                }
                return Read((IntPtr)pointer, ref value);
            } finally {
                if (!fixedMemory.Equals(default(FixedMemory<byte>))) {
                    fixedMemory.Dispose();
                }
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

        internal static JsonSerializer Json => JsonSerializer.Instance;

        internal sealed class JsonSerializer {
            private readonly Newtonsoft.Json.JsonSerializer _serializer;
            internal static JsonSerializer Instance = new JsonSerializer();

            private JsonSerializer() {
                _serializer = new Newtonsoft.Json.JsonSerializer();
            }

            public int SizeOfJson<T>(T value, out MemoryStream memoryStream) {
                memoryStream = Serialize<T>(value);
                memoryStream.Position = 0;
                return checked((int)memoryStream.Length);
            }

            public MemoryStream Serialize<T>(T value) {
                var ms = RecyclableMemoryManager.MemoryStreams.GetStream();
                ms.WriteAsPtr<long>(0L);
                using (var writer = new StreamWriter(ms, Encoding.UTF8, 4096, true)) {
                    _serializer.Serialize(writer, value);
                    //writer.CloseOutput = false;
                }
                ms.Position = 0;
                ms.WriteAsPtr<int>(checked((int)ms.Length));
                ms.Position = 0;
                return ms;
            }

            public T Deserialize<T>(Stream stream) {
                // skip header
                stream.Position = 8;
                using (var reader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8, true, 4096, true))) {
                    return _serializer.Deserialize<T>(reader);
                }
            }
        }
    }

#pragma warning restore 0618
}
