using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Spreads.Serialization.Microsoft.IO;

namespace Spreads.Serialization {

    /// <summary>
    /// Convert a generic object T to a pointer prefixed with version and length.
    /// 
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |            Version + Flags (currently only version)           |
    /// +---------------------------------------------------------------+
    /// |                        Length                                 |
    /// +---------------------------------------------------------------+
    /// |                       Serialized Payload                      |
    /// |                                                               |
    /// </summary>
    public interface IBinaryConverter<T> {
        /// <summary>
        /// Equivalent to check Size > 0
        /// </summary>
        bool IsFixedSize { get; }

        /// <summary>
        /// Zero for variable-length types, positive value for fixed-size types.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Version of the converter.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Returns the size of serialized bytes including the version+lenght header.
        /// For types with non-fixed size this method could serialize value into the memoryStream if it is not 
        /// possible to calculate serialized bytes length without actually performing serialization.
        /// If provided memory stream is not null, it is appended.
        /// </summary>
        int SizeOf(T value, ref MemoryStream memoryStream);

        /// <summary>
        /// For types with non-fixed size this method assumes that the pointer has enough capacity.
        /// Use SizeOf method to determine the bytes size of the value before writing. If SizeOf
        /// sets memoryStream then write its content directly, otherwise ToPtr will do the same serialization job twice.
        /// </summary>
        void ToPtr(T value, IntPtr ptr, MemoryStream memoryStream = null);

        /// <summary>
        /// Deserialize an object from a pointer.
        /// if not IsFixedSize, checks that version from the pointer equals the Version property.
        /// </summary>
        T FromPtr(IntPtr ptr);

    }


    public static class BinaryConvertorExtensions {
        public const int MaxBufferSize = 8 * 1024;
        [ThreadStatic]
        private static byte[] _threadStaticBuffer;

        internal static byte[] ThreadStaticBuffer
        {
            get
            {
                if (_threadStaticBuffer == null || _threadStaticBuffer.Length < MaxBufferSize) {
                    _threadStaticBuffer = new byte[MaxBufferSize];
                }
                return _threadStaticBuffer;
            }
        }

        /// <summary>
        /// Writes to a stream as if it was a pointer using IBinaryConverter<T>.ToPtr method
        /// </summary>
        public static unsafe int WriteAsPtr<T>(this MemoryStream stream, T value) {
            var size = TypeHelper<T>.Size;
            if (size <= 0) throw new InvalidOperationException("This method should only be used for writing fixed-size types to a stream");
            if (stream is RecyclableMemoryStream) throw new NotImplementedException("TODO");
            // NB do not use a buffer pool here but instead use a thread-static buffer
            // that will grow to maximum size of a type. Fixed-size types are usually small.
            // Take/return is more expensive than the work we do with the pool here.
            if (_threadStaticBuffer == null || _threadStaticBuffer.Length < size) {
                _threadStaticBuffer = new byte[size];
                if (size > MaxBufferSize) {
                    // NB 8 kb is arbitrary
                    Trace.TraceWarning("Thread-static buffer in BinaryConvertorExtensions is above 8kb");
                }
            }
            fixed (byte* ptr = &_threadStaticBuffer[0])
            {
                TypeHelper<T>.StructureToPtr(value, (IntPtr)ptr);
            }
            stream.Write(_threadStaticBuffer, 0, size);

            // NB this is not needed as long as convertor.ToPtr guarantees overwriting all Size bytes.
            // //Array.Clear(_buffer, 0, size);
            return size;
        }

        public static unsafe T ReadAsPtr<T>(this MemoryStream stream) {
            var size = TypeHelper<T>.Size;
            if (size <= 0) throw new InvalidOperationException("This method should only be used for writing fixed-size types to a stream");
            if (stream is RecyclableMemoryStream) throw new NotImplementedException("TODO");
            if (_threadStaticBuffer == null || _threadStaticBuffer.Length < size) {
                _threadStaticBuffer = new byte[size];
                if (size > MaxBufferSize) {
                    Trace.TraceWarning("Thread-static buffer in BinaryConvertorExtensions is above 8kb");
                }
            }
            T value;
            var read = 0;
            while ((read += stream.Read(_threadStaticBuffer, read, size - read)) < size) { }

            fixed (byte* ptr = &_threadStaticBuffer[0])
            {
                value = TypeHelper<T>.PtrToStructure((IntPtr)ptr);
            }
            return value;
        }

        /// <summary>
        /// Write entire stream to a pointer
        /// </summary>
        public static void WriteToPtr(this MemoryStream stream, IntPtr ptr) {
            stream.Position = 0;
            if (_threadStaticBuffer == null || _threadStaticBuffer.Length < MaxBufferSize) {
                _threadStaticBuffer = new byte[MaxBufferSize];
            }
            var rms = stream as RecyclableMemoryStream;
            if (rms != null) {
                throw new NotImplementedException("TODO use RecyclableMemoryStream internally");
            }
            int length = 0;
            int position = 0;
            while ((length = stream.Read(_threadStaticBuffer, 0, _threadStaticBuffer.Length)) > 0) {
                Marshal.Copy(_threadStaticBuffer, 0, ptr + position, length);
                position += length;
            }
        }
    }
}
