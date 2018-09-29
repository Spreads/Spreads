// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Serialization.Utf8Json;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    // TODO Settings.ProtectedCopy to write first to a temp buffer for custom binary converters
    // Impl always checks SizeOf, then allocates buffer and then gives that buffer to the Write
    // method. That is fine for blittables and JSON, we know their impl is correct by construction.
    // Actually, all converters that return temp buffer are checked, if temp buffer size is wrong
    // the write will fail. We only need to protect writes that do not return temp buffer
    // (subject to Write just uses it and do not calls Converter's Write, this is true now and
    // should not change). This should be on by default and a separate setting, not a part of
    // AdditionalCorrectnessCheck, which protects from wrong usage of DirectBuffer API, but not
    // from direct pointer write overruns.

    public enum BinaryConverterErrorCode
    {
        NotEnoughCapacity = -1
    }

    /// <summary>
    /// Serialize a generic object T to a pointer, prefixed with version and length.
    /// </summary>
    /// <remarks>
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |  Ver  |T|D|C|B|    TypeEnum   |    TypeSize   | SubTypeEnum   |
    /// +---------------------------------------------------------------+
    /// |R|     Payload Length (if VarLen/TypeSize is negative )        |
    /// +---------------------------------------------------------------+
    /// |                     Serialized Payload                      ...
    /// C - compressed
    /// D - diffed (if a type implements <see cref="IDelta{T}"/>)
    /// B - binary format. If not set then the payload is JSON,
    /// if set then payload is custom binary (payload could have
    /// it's own headers e.g. Blosc)
    /// T - value has Timestamp as the first element of payload for binary case or Timestamp field on JSON object.
    /// </remarks>
    public interface IBinaryConverter<T>
    {
        /// <summary>
        /// Version of the converter. 15 (4 bits) max.
        /// </summary>
        byte ConverterVersion { get; }

        /// <summary>
        /// Returns the size of serialized bytes without the version+lenght header.
        /// For types with non-fixed size this method could serialize value into a temporary buffer if it is not
        /// possible to calculate serialized bytes length without actually performing serialization.
        /// The temporaryBuffer ArraySegment should use a buffer from <see cref="BufferPool{T}.Rent"/>
        /// and start with offset 8, otherwise BinarySerialized will copy (not implemented and likely won't) or throw.
        /// The buffer is owned by the caller, no other references to it should remain after the call.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="temporaryBuffer">A buffer where a value is serialized into if it is not possible to calculate serialized buffer size
        /// without actually performing serialization.</param>
        int SizeOf(T value, out ArraySegment<byte> temporaryBuffer);

        /// <summary>
        /// Write serialized value to the destination. Use SizeOf to prepare destination of required size.
        /// This method is called by <see cref="BinarySerializer"/> only when <see cref="SizeOf"/> returned
        /// positive length with default/empty temporaryBuffer.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="destination">A pinned pointer to a buffer to serialize the value into. It must have at least number of bytes returned by SizeOf().</param>
        /// <returns>Returns the number of bytes written to the destination buffer or a negative error code that corresponds to <see cref="BinaryConverterErrorCode"/>.</returns>
        int Write(T value, ref DirectBuffer destination);

        /// <summary>
        /// Reads new value or fill existing value with data from the pointer,
        /// returns number of bytes read including any header.
        /// If not IsFixedSize, checks that version from the pointer equals the Version property.
        /// </summary>
        int Read(ref DirectBuffer source, out T value);
    }

    public sealed class JsonBinaryConverter<T> : IBinaryConverter<T>
    {
        private JsonBinaryConverter()
        {
        }

        // This is not a "binary" converter, but a fallback with the same interface
        public static JsonBinaryConverter<T> Instance = new JsonBinaryConverter<T>();

        public byte ConverterVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }

        public static int SizeOf(T value, out ArraySegment<byte> temporaryBuffer)
        {
            // offset 16 to allow writing header + length + ts without copy
            temporaryBuffer = JsonSerializer.SerializeToRentedBuffer(value, DataTypeHeader.Size + 4 + 8);
            return temporaryBuffer.Count;
        }

        int IBinaryConverter<T>.SizeOf(T value, out ArraySegment<byte> temporaryBuffer)
        {
            return SizeOf(value, out temporaryBuffer);
        }

        public static int Write(T value, ref DirectBuffer destination)
        {
            var size = SizeOf(value, out var buffer);
            try
            {
                // in general buffer could be empty/default if size is known, but not with Json
                ThrowHelper.AssertFailFast(size == buffer.Count, "size == buffer.Count");

                if (size > destination.Length)
                {
                    return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                }

                ((Span<byte>)buffer).CopyTo(destination.Span);

                return size;
            }
            finally
            {
                BufferPool<byte>.Return(buffer.Array);
            }
        }

        int IBinaryConverter<T>.Write(T value, ref DirectBuffer destination)
        {
            return Write(value, ref destination);
        }

        public static int Read(ref DirectBuffer source, out T value)
        {
            //if (MemoryMarshal.TryGetArray(source, out var segment))
            //{
            //    var reader = new JsonReader(segment.Array, segment.Offset);
            //    value = JsonSerializer.Deserialize<T>(ref reader);
            //    return reader.GetCurrentOffsetUnsafe();
            //}

            // var buffer = BufferPool<byte>.Rent(checked((int)(uint)source.Length));
            //try
            //{
            // source.Span.CopyTo(((Span<byte>)buffer));
            var reader = new JsonReader(source);
            value = JsonSerializer.Deserialize<T>(ref reader);
            return reader.GetCurrentOffsetUnsafe();
            //}
            //finally
            //{
            //    BufferPool<byte>.Return(buffer);
            //}
        }

        int IBinaryConverter<T>.Read(ref DirectBuffer source, out T value)
        {
            return Read(ref source, out value);
        }
    }
}
