using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Spreads.Buffers;

namespace Spreads.Serialization {




    internal class StringBinaryConverter : IBinaryConverter<string> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public unsafe int SizeOf(string value, out MemoryStream temporaryStream) {
            fixed (char* charPtr = value) {
                var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                temporaryStream = null;
                return totalLength;
            }
        }

        public unsafe int Write(string value, ref DirectBuffer destination, uint offset = 0u, MemoryStream temporaryStream = null) {
            if (temporaryStream == null) {
                fixed (char* charPtr = value) {
                    var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                    if (!destination.HasCapacity(offset, totalLength)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                    var ptr = destination.Data + (int)offset;
                    // size
                    Marshal.WriteInt32(ptr, totalLength);
                    // version
                    Marshal.WriteByte(ptr, Version);
                    // payload
                    var len = Encoding.UTF8.GetBytes(charPtr, value.Length, (byte*)ptr + 8, totalLength);
                    Debug.Assert(totalLength == len + 8);
                    return len + 8;
                }

            } else {
                throw new NotSupportedException("StringBinaryConverter does not work with temp streams.");
                //throw new NotImplementedException();
                //temporaryStream.WriteToPtr(ptr);
                //return checked((int)temporaryStream.Length);
            }

        }

        public int Read(IntPtr ptr, ref string value) {
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

        public byte Version => 1;
    }


    internal class MemoryStreamBinaryConverter : IBinaryConverter<MemoryStream> {
        public bool IsFixedSize => false;
        public int Size => 0;
        public int SizeOf(MemoryStream value, out MemoryStream temporaryStream) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");
            temporaryStream = null;
            return checked((int)value.Length + 8);
        }

        public unsafe int Write(MemoryStream value, ref DirectBuffer destination, uint offset, MemoryStream temporaryStream = null) {
            if (temporaryStream != null) throw new NotSupportedException("MemoryStreamBinaryConverter does not work with temp streams.");

            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(temporaryStream), "Memory stream is too large");

            var totalLength = checked((int)value.Length + 8);
            if (!destination.HasCapacity(offset, totalLength)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            var ptr = destination.Data + (int)offset;
            // size
            Marshal.WriteInt32(ptr, totalLength);
            // version
            Marshal.WriteInt32(ptr + 4, 0);

            // payload
            ptr = ptr + 8;
            int b;
            // TODO (perf) this looks silly
            while ((b = value.ReadByte()) >= 0) {
                *(byte*)ptr = (byte)b;
                ptr = ptr + 1;
            }
            return totalLength;
        }

        public int Read(IntPtr ptr, ref MemoryStream value) {
            var version = Marshal.ReadInt32(ptr);
            if (version != 0) throw new NotSupportedException();
            var length = Marshal.ReadInt32(ptr + 4);
            var bytes = new byte[length];
            Marshal.Copy(ptr + 8, bytes, 0, length);
            value = new MemoryStream(bytes);
            return length + 8;
        }

        public byte Version => 1;
    }
}
