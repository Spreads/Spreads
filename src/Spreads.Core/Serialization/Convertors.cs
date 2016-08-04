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
        public unsafe int SizeOf(string value, out MemoryStream payloadStream) {
            fixed (char* charPtr = value) {
                var totalLength = 8 + Encoding.UTF8.GetByteCount(charPtr, value.Length);
                payloadStream = null;
                return totalLength;
            }
            //var maxLength = value.Length * 2;
            //var needReturnToBufferPool = false;
            //// TODO (low) use BufferWrapper here and below - it just does exactly the same logic
            //byte[] buffer;
            //if (maxLength <= RecyclableMemoryManager.StaticBufferSize) {
            //    buffer = RecyclableMemoryManager.ThreadStaticBuffer;
            //} else {
            //    needReturnToBufferPool = true;
            //    buffer = ArrayPool<byte>.Shared.Rent(maxLength);
            //}
            //var len = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

            //payloadStream = RecyclableMemoryManager.MemoryStreams.GetStream("StringBinaryConverter.SizeOf");
            //Debug.Assert(payloadStream.Position == 0);

            //payloadStream.WriteAsPtr<int>(Version);
            //// placeholder for length
            //payloadStream.WriteAsPtr<int>(0);
            //Debug.Assert(payloadStream.Position == 8);

            //payloadStream.Write(buffer, 0, len);
            //payloadStream.Position = 4;
            //payloadStream.WriteAsPtr<int>(len);
            //payloadStream.Position = 0;

            //if (needReturnToBufferPool) ArrayPool<byte>.Shared.Return(buffer, true);
            //return len + 8;
        }

        public unsafe int Write(string value, ref DirectBuffer destination, uint offset = 0u, MemoryStream payloadStream = null) {
            if (payloadStream == null) {
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
                throw new NotImplementedException();
                //payloadStream.WriteToPtr(ptr);
                //return checked((int)payloadStream.Length);
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
        public int SizeOf(MemoryStream value, out MemoryStream payloadStream) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(payloadStream), "Memory stream is too large");
            payloadStream = null;
            return checked((int)value.Length + 8);
        }


        public unsafe int Write(MemoryStream value, ref DirectBuffer destination, uint offset, MemoryStream payloadStream = null) {
            if (value.Length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(payloadStream), "Memory stream is too large");

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
