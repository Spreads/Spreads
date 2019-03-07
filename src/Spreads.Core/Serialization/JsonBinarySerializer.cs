// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Serialization.Utf8Json;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Serialization.Experimental;

namespace Spreads.Serialization
{
    /// <summary>
    /// Fallback serializer that serializes data as JSON but pretends to be a binary one.
    /// </summary>
    public sealed class JsonBinarySerializer<T> : IBinarySerializer<T>
    {
        private JsonBinarySerializer()
        {
        }

        // This is not a "binary" converter, but a fallback with the same interface
        public static JsonBinarySerializer<T> Instance = new JsonBinarySerializer<T>();

        public byte SerializerVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }

        public static int SizeOf(T value, out RetainedMemory<byte> temporaryBuffer, out bool withPadding)
        {
            // offset 16 to allow writing header + length + ts without copy
            Debug.Assert(DataTypeHeaderEx.Size + 4 + 8 == 16);
            Debug.Assert(BinarySerializer.HEADER_PADDING == 16);
            temporaryBuffer = JsonSerializer.SerializeToRetainedMemory(value, BinarySerializer.HEADER_PADDING);
            withPadding = true;
            return temporaryBuffer.Length - BinarySerializer.HEADER_PADDING;
        }

        int IBinarySerializer<T>.SizeOf(T value, out RetainedMemory<byte> temporaryBuffer, out bool withPadding)
        {
            return SizeOf(value, out temporaryBuffer, out withPadding);
        }

        public static int Write(T value, ref DirectBuffer destination)
        {
            var size = SizeOf(value, out var retainedMemory, out var withPadding);
            Debug.Assert(withPadding);
            try
            {
                // in general buffer could be empty/default if size is known, but not with Json
                ThrowHelper.AssertFailFast(size == retainedMemory.Length - BinarySerializer.HEADER_PADDING, "size == buffer.Count");

                if (size > destination.Length)
                {
                    return (int)BinarySerializerErrorCode.NotEnoughCapacity;
                }

                retainedMemory.Span.Slice(BinarySerializer.HEADER_PADDING).CopyTo(destination.Span);

                return size;
            }
            finally
            {
                retainedMemory.Dispose();
            }
        }

        int IBinarySerializer<T>.Write(T value, ref DirectBuffer destination)
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

        int IBinarySerializer<T>.Read(ref DirectBuffer source, out T value)
        {
            return Read(ref source, out value);
        }
    }
}
