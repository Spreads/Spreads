using System;
using System.Diagnostics;
using System.IO;
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
        [ThreadStatic]
        private static byte[] _buffer;

        /// <summary>
        /// Writes to a stream as if it was a pointer using IBinaryConverter<T>.ToPtr method
        /// </summary>
        public static unsafe int WriteToPtr<T>(this Stream stream, IBinaryConverter<T> convertor, T value) {
            var size = convertor.Size;
            if (size <= 0) throw new InvalidOperationException("This method should only be used for writing fixed-size types to a stream");
            if (stream is RecyclableMemoryStream) throw new NotImplementedException("TODO");
            // NB do not use a buffer pool here but instead use a thread-static buffer
            // that will grow to maximum size of a type. Fixed-size types are usually small.
            // Take/return is more expensive than the work we do with the pool here.
            if (_buffer == null || _buffer.Length < size) {
                _buffer = new byte[size];
                if (size > 10 * 1024) {
                    // NB 10 kb is arbitrary
                    Trace.TraceWarning("Thread-static buffer in BinaryConvertorExtensions is above 10kb");
                }
            }
            fixed (byte* ptr = &_buffer[0])
            {
                convertor.ToPtr(value, (IntPtr)ptr);
            }
            stream.Write(_buffer, 0, size);

            // NB this is not needed as long as convertor.ToPtr guarantees overwriting all Size bytes.
            // //Array.Clear(_buffer, 0, size);
            return size;
        }
    }
}
