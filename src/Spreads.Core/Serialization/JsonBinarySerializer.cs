// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Serialization.Utf8Json;

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

        public byte KnownTypeId => 0;

        public short FixedSize => -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(in T value, out RetainedMemory<byte> temporaryBuffer)
        {
            temporaryBuffer = JsonSerializer.SerializeToRetainedMemory(value);
            return temporaryBuffer.Length;
        }

        int IBinarySerializer<T>.SizeOf(in T value, out RetainedMemory<byte> temporaryBuffer)
        {
            return SizeOf(in value, out temporaryBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(in T value, in DirectBuffer destination)
        {
            var size = SizeOf(value, out var retainedMemory);
            try
            {
                // in general buffer could be empty/default if size is known, but not with Json
                ThrowHelper.AssertFailFast(size == retainedMemory.Length, "size == buffer.Count");

                if (size > destination.Length)
                {
                    return (int)BinarySerializerErrorCode.NotEnoughCapacity;
                }

                retainedMemory.Span.CopyTo(destination.Span);

                return size;
            }
            finally
            {
                retainedMemory.Dispose();
            }
        }

        int IBinarySerializer<T>.Write(in T value, DirectBuffer destination)
        {
            return Write(in value, in destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(in DirectBuffer source, out T value)
        {
            var reader = new JsonReader(source);
            value = JsonSerializer.Deserialize<T>(ref reader);
            return reader.GetCurrentOffsetUnsafe();
        }

        int IBinarySerializer<T>.Read(DirectBuffer source, out T value)
        {
            return Read(in source, out value);
        }
    }
}