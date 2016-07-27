using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;

namespace Spreads.Serialization {

    internal static class ArrayConvertorFactory {
        public static IBinaryConverter<TElement[]> GenericCreate<TElement>() {
            return new ArrayBinaryConverter<TElement>();
        }
        public static object Create(Type type) {
            MethodInfo method = typeof(ArrayConvertorFactory).GetMethod("GenericCreate");
            MethodInfo generic = method.MakeGenericMethod(type);
            return generic.Invoke(null, null);
        }
    }

    internal class ArrayBinaryConverter<TElement> : IBinaryConverter<TElement[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int Version => TypeHelper<TElement>.Version;

        private static int _itemSize = TypeHelper<TElement>.Size;

        public int SizeOf(TElement[] value, out MemoryStream payloadStream) {
            if (_itemSize > 0) {
                payloadStream = null;
                return _itemSize * value.Length;
            }
            throw new NotImplementedException();
        }

        public int ToPtr(TElement[] value, IntPtr ptr, MemoryStream payloadStream = null) {
            throw new NotImplementedException();
        }

        public int FromPtr(IntPtr ptr, ref TElement[] value) {
            throw new NotImplementedException();
        }
    }


    internal class ByteArrayBinaryConverter : IBinaryConverter<byte[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(byte[] value, out MemoryStream payloadStream) {
            payloadStream = null;
            return value.Length + 8;
        }

        public int ToPtr(byte[] value, IntPtr ptr, MemoryStream payloadStream = null) {
            // version
            Marshal.WriteInt32(ptr, Version);
            // size
            Marshal.WriteInt32(ptr + 4, value.Length);
            // payload
            Marshal.Copy(value, 0, ptr + 8, value.Length);
            return value.Length + 8;
        }

        public int FromPtr(IntPtr ptr, ref byte[] value) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            var bytes = new byte[length];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            value = bytes;
            return length + 8;
        }

        public int Version => 0;
    }


    internal class StringBinaryConverter : IBinaryConverter<string> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(string value, out MemoryStream payloadStream) {
            var maxLength = value.Length * 2;
            var needReturnToBufferPool = false;
            // TODO (low) use BufferWrapper here and below - it just does exactly the same logic
            byte[] buffer;
            if (maxLength <= RecyclableMemoryManager.StaticBufferSize) {
                buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            } else {
                needReturnToBufferPool = true;
                buffer = ArrayPool<byte>.Shared.Rent(maxLength);
            }
            var len = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

            payloadStream = RecyclableMemoryManager.MemoryStreams.GetStream("StringBinaryConverter.SizeOf");
            Debug.Assert(payloadStream.Position == 0);

            payloadStream.WriteAsPtr<int>(Version);
            // placeholder for length
            payloadStream.WriteAsPtr<int>(0);
            Debug.Assert(payloadStream.Position == 8);

            payloadStream.Write(buffer, 0, len);
            payloadStream.Position = 4;
            payloadStream.WriteAsPtr<int>(len);
            payloadStream.Position = 0;

            if (needReturnToBufferPool) ArrayPool<byte>.Shared.Return(buffer, true);
            return len + 8;
        }

        public unsafe int ToPtr(string value, IntPtr ptr, MemoryStream payloadStream = null) {
            if (payloadStream == null) {
                // version
                Marshal.WriteInt32(ptr, Version);
                // payload
                var maxLength = value.Length * 2;
                fixed (char* charPtr = value) {
                    var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)ptr + 8, maxLength);
                    // size
                    Marshal.WriteInt32(ptr + 4, len);
                    return len + 8;
                }

            } else {
                payloadStream.WriteToPtr(ptr);
                return checked((int)payloadStream.Length);
            }

        }

        public int FromPtr(IntPtr ptr, ref string value) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            bool needReturn = false;
            byte[] buffer;
            if (length < RecyclableMemoryManager.StaticBufferSize) {
                buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            } else {
                needReturn = true;
                buffer = ArrayPool<byte>.Shared.Rent(length);
            }
            Marshal.Copy(ptr + 8, buffer, 0, length);
            value = Encoding.UTF8.GetString(buffer, 0, length);
            if (needReturn) ArrayPool<byte>.Shared.Return(buffer, true);
            return length + 8;
        }

        public int Version => 0;
    }


    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(MemoryStream value, out MemoryStream payloadStream) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(payloadStream), "Memory stream is too large");
            payloadStream = null;
            return checked((int)value.Length + 8);
        }


        public unsafe int ToPtr(MemoryStream value, IntPtr ptr, MemoryStream payloadStream = null) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(payloadStream), "Memory stream is too large");
            // version
            Marshal.WriteInt32(ptr, 0);
            // size
            Marshal.WriteInt32(ptr + 4, (int)value.Length);
            // payload
            ptr = ptr + 8;
            int b;
            while ((b = value.ReadByte()) >= 0) {
                *(byte*)ptr = (byte)b;
            }
            return checked((int)value.Length + 8);
        }

        public int FromPtr(IntPtr ptr, ref MemoryStream value) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            var bytes = new byte[length];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            value = new MemoryStream(bytes);
            return length + 8;
        }

        public int Version => 0;
    }
}
