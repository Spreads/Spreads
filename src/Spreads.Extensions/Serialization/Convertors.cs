using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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

        public int SizeOf(TElement[] value, out MemoryStream memoryStream) {
            if (_itemSize > 0) {
                memoryStream = null;
                return _itemSize * value.Length;
            }
            throw new NotImplementedException();
        }

        public void ToPtr(TElement[] value, IntPtr ptr, MemoryStream memoryStream = null) {
            throw new NotImplementedException();
        }

        public int FromPtr(IntPtr ptr, ref TElement[] value) {
            throw new NotImplementedException();
        }
    }


    internal class ByteArrayBinaryConverter : IBinaryConverter<byte[]> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(byte[] value, out MemoryStream memoryStream) {
            memoryStream = null;
            return value.Length;
        }

        public void ToPtr(byte[] value, IntPtr ptr, MemoryStream memoryStream = null) {
            // version
            Marshal.WriteInt32(ptr, Version);
            // size
            Marshal.WriteInt32(ptr + 4, value.Length);
            // payload
            Marshal.Copy(value, 0, ptr + 8, value.Length);
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
        public int SizeOf(string value, out MemoryStream memoryStream) {
            var maxLength = value.Length * 2;
            var needReturnToBufferPool = false;
            byte[] buffer;
            if (maxLength < BinaryConvertorExtensions.MaxBufferSize) {
                buffer = BinaryConvertorExtensions.ThreadStaticBuffer;
            } else {
                needReturnToBufferPool = true;
                buffer = OptimizationSettings.ArrayPool.Take<byte>(maxLength);
            }
            var len = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

            memoryStream = TypeHelper.MsManager.GetStream("StringBinaryConverter.SizeOf");
            Debug.Assert(memoryStream.Position == 0);

            memoryStream.WriteAsPtr<int>(Version);
            // placeholder for length
            memoryStream.WriteAsPtr<int>(0);
            Debug.Assert(memoryStream.Position == 8);

            memoryStream.Write(buffer, 0, len);
            memoryStream.Position = 4;
            memoryStream.WriteAsPtr<int>(len);
            memoryStream.Position = 0;

            if (needReturnToBufferPool) OptimizationSettings.ArrayPool.Return(buffer);
            return len + 8;
        }

        public unsafe void ToPtr(string value, IntPtr ptr, MemoryStream memoryStream = null) {
            if (memoryStream == null) {
                // version
                Marshal.WriteInt32(ptr, Version);
                // payload
                var maxLength = value.Length * 2;
                fixed (char* charPtr = value)
                {
                    var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)ptr + 8, maxLength);
                    // size
                    Marshal.WriteInt32(ptr + 4, len);
                }

            } else {
                memoryStream.WriteToPtr(ptr);
            }

        }

        public int FromPtr(IntPtr ptr, ref string value) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            bool needReturn = false;
            byte[] buffer;
            if (length < BinaryConvertorExtensions.MaxBufferSize) {
                buffer = BinaryConvertorExtensions.ThreadStaticBuffer;
            } else {
                needReturn = true;
                buffer = OptimizationSettings.ArrayPool.Take<byte>(length);
            }
            Marshal.Copy(ptr + 8, buffer, 0, length);
            value = Encoding.UTF8.GetString(buffer, 0, length);
            if (needReturn) OptimizationSettings.ArrayPool.Return(buffer);
            return length + 8;
        }

        public int Version => 0;
    }


    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(MemoryStream value, out MemoryStream memoryStream) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException("Memory stream is too large");
            memoryStream = null;
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
