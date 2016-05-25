using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

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

        public int SizeOf(TElement[] value, ref MemoryStream memoryStream) {
            if (_itemSize > 0) {
                memoryStream = null;
                return _itemSize * value.Length;
            }
            throw new NotImplementedException();
        }

        public void ToPtr(TElement[] value, IntPtr ptr, MemoryStream memoryStream = null) {
            throw new NotImplementedException();
        }

        public TElement[] FromPtr(IntPtr ptr) {
            throw new NotImplementedException();
        }
    }


    internal class ByteArrayBinaryConverter : IBinaryConverter<byte[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(byte[] value, ref MemoryStream memoryStream) {
            return value.Length;
        }

        public void ToPtr(byte[] value, IntPtr ptr, MemoryStream memoryStream = null) {
            // version
            Marshal.WriteInt32(ptr, 0);
            // size
            Marshal.WriteInt32(ptr + 4, value.Length);
            // payload
            Marshal.Copy(value, 0, ptr + 8, value.Length);
        }

        public byte[] FromPtr(IntPtr ptr) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            var bytes = new byte[length];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            return bytes;
        }

        public int Version => 0;
    }



    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(MemoryStream value, ref MemoryStream memoryStream) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException("Memory stream is too large");
            return (int)value.Length;
        }


        public unsafe void ToPtr(MemoryStream value, IntPtr ptr, MemoryStream memoryStream = null) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException("Memory stream is too large");
            // version
            Marshal.WriteInt32(ptr, 0);
            // size
            Marshal.WriteInt32(ptr + 4, (int)value.Length);
            // payload
            var rms = value as Spreads.Serialization.Microsoft.IO.RecyclableMemoryStream;
            if (rms != null) {
                throw new NotImplementedException("TODO use RecyclableMemoryStream internally");
            }
            ptr = ptr + 8;
            int b;
            while ((b = value.ReadByte()) >= 0) {
                *(byte*)ptr = (byte)b;
            }
        }

        public MemoryStream FromPtr(IntPtr ptr) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            var bytes = new byte[length];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            return new MemoryStream(bytes);
        }

        public int Version => 0;
    }
}
