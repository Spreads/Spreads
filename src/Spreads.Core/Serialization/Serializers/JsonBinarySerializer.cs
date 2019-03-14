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

        public override byte KnownTypeId => 0;

        public override short FixedSize => -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SizeOfStatic(in T value, BufferWriter bufferWriter)
        {
            Debug.Assert(bufferWriter != null);

            // TODO Json should use the same BufferWriter
            var segment = JsonSerializer.SerializeToRentedBuffer(value);
            var size = segment.Count;

            bufferWriter.WriteSpan(segment.AsSpan());

            BufferPool<byte>.Return(segment.Array, false);

            return size;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public override int SizeOf(in T value, BufferWriter payload)
        {
            return SizeOfStatic(in value, payload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteStatic(in T value, in DirectBuffer destination)
        {
            var bufferWriter = BufferWriter.Create();
            var size = SizeOfStatic(value, bufferWriter);
            try
            {
                // in general buffer could be empty/default if size is known, but not with Json
                ThrowHelper.AssertFailFast(size == bufferWriter.WrittenLength, "size == buffer.Count");

                if (size > destination.Length)
                {
                    return (int)BinarySerializerErrorCode.NotEnoughCapacity;
                }

                bufferWriter.WrittenSpan.CopyTo(destination.Span);

                return size;
            }
            finally
            {
                bufferWriter.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public override int Write(in T value, DirectBuffer destination)
        {
            return WriteStatic(in value, in destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadStatic(in DirectBuffer source, out T value)
        {
            var reader = new JsonReader(source);
            value = JsonSerializer.Deserialize<T>(ref reader);
            return reader.GetCurrentOffsetUnsafe();
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public override int Read(DirectBuffer source, out T value)
        {
            return ReadStatic(in source, out value);
        }
    }
}