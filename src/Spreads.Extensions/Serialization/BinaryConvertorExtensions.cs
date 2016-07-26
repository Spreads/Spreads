using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;

namespace Spreads.Serialization {
    public static class BinaryConvertorExtensions {


        /// <summary>
        /// Writes to a stream as if it was a pointer using IBinaryConverter<T>.ToPtr method
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int WriteAsPtr<T>(this MemoryStream stream, T value) {
            var size = TypeHelper<T>.Size;
            if (size <= 0) throw new InvalidOperationException("This method should only be used for writing fixed-size types to a stream");
            // NB do not use a buffer pool here but instead use a thread-static buffer
            // that will grow to maximum size of a type. Fixed-size types are usually small.
            // Take/return is more expensive than the work we do with the pool here.
            using (var wrapper = RecyclableMemoryManager.GetBuffer(size)) {
                var threadStaticBuffer = wrapper.Buffer;
                fixed (byte* ptr = &threadStaticBuffer[0]) {
                    TypeHelper<T>.ToPtr(value, (IntPtr)ptr);
                }
                stream.Write(threadStaticBuffer, 0, size);
            }
            // NB this is not needed as long as convertor.ToPtr guarantees overwriting all Size bytes.
            // //Array.Clear(_buffer, 0, size);
            return size;
        }



        public static unsafe T ReadAsPtr<T>(this MemoryStream stream) {
            var size = TypeHelper<T>.Size;
            if (size <= 0) throw new InvalidOperationException("This method should only be used for writing fixed-size types to a stream");
            if (stream is RecyclableMemoryStream) throw new NotImplementedException("TODO");
            using (var wrapper = RecyclableMemoryManager.GetBuffer(size)) {
                var threadStaticBuffer = wrapper.Buffer;

                var value = default(T);
                var read = 0;
                while ((read += stream.Read(threadStaticBuffer, read, size - read)) < size) {
                }

                fixed (byte* ptr = &threadStaticBuffer[0]) {
                    TypeHelper<T>.FromPtr((IntPtr)ptr, ref value);
                }
                return value;
            }
        }

        /// <summary>
        /// Write entire stream to a pointer
        /// </summary>
        public static void WriteToPtr(this MemoryStream stream, IntPtr ptr) {
            stream.Position = 0;
            var threadStaticBuffer = RecyclableMemoryManager.ThreadStaticBuffer;

            var length = 0;
            var position = 0;
            while ((length = stream.Read(threadStaticBuffer, 0, threadStaticBuffer.Length)) > 0) {
                Marshal.Copy(threadStaticBuffer, 0, ptr + position, length);
                position += length;
            }
        }
    }
}