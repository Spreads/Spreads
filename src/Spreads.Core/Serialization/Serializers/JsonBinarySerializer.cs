// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Serialization.Utf8Json;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization.Serializers
{
    /// <summary>
    /// Fallback serializer that serializes data as JSON but pretends to be a binary one.
    /// </summary>
    public sealed class JsonBinarySerializer<T> : BinarySerializer<T>
    {
        private JsonBinarySerializer()
        {
        }

        // This is not a "binary" converter, but a fallback with the same interface
        public static JsonBinarySerializer<T> Instance = new JsonBinarySerializer<T>();

        public override byte SerializerVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }

        public override byte KnownTypeId => 0;

        public override short FixedSize => -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfStatic(in T value, out RetainedMemory<byte> temporaryBuffer)
        {
            temporaryBuffer = JsonSerializer.SerializeToRetainedMemory(value);
            return temporaryBuffer.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfStatic(in T value, BufferWriter bufferWriter)
        {
            Debug.Assert(bufferWriter != null);

            // TODO Json should use the same BufferWriter
            var segment = JsonSerializer.SerializeToRentedBuffer(value);
            var size = segment.Count;

            bufferWriter.Write<int>(size);
            bufferWriter.Write(segment.AsSpan());

            BufferPool<byte>.Return(segment.Array, false);

            return size;
        }

        public override int SizeOf(in T value, BufferWriter bufferWriter)
        {
            return SizeOfStatic(in value, bufferWriter);
        }

        public override int SizeOf(in T value, out RetainedMemory<byte> temporaryBuffer)
        {
            return SizeOfStatic(in value, out temporaryBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteStatic(in T value, in DirectBuffer destination)
        {
            var size = SizeOfStatic(value, out var retainedMemory);
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

        public override int Write(in T value, DirectBuffer destination)
        {
            return WriteStatic(in value, in destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(in DirectBuffer source, out T value)
        {
            var reader = new JsonReader(source);
            value = JsonSerializer.Deserialize<T>(ref reader);
            return reader.GetCurrentOffsetUnsafe();
        }

        public override int Read(DirectBuffer source, out T value)
        {
            return Read(in source, out value);
        }
    }
}