// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    internal interface IArrayBasedMap<TKey, TValue>
    {
        int Length { get; }
        long Version { get; }
        bool IsRegular { get; }
        bool IsCompleted { get; }
        TKey[] Keys { get; }
        TValue[] Values { get; }
    }

    internal abstract class ArrayBasedMapConverter<TKey, TValue, T> : IBinaryConverter<T> where T : IArrayBasedMap<TKey, TValue>
    {
#pragma warning disable 0618

        //private static readonly int KeySize = TypeHelper<TKey>.Size;
        //private static readonly int ValueSize = TypeHelper<TValue>.Size;
        public bool IsFixedSize => false;

        public int Size => -1;
        public byte Version => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int SizeOf(in T map, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            if ((int)format >= 100)
            {
                ThrowHelper.ThrowInvalidOperationException("Invalid non-binary format for ArrayBasedMapConverter");
            }
            if (format == SerializationFormat.Binary)
            {
                format = SerializationFormat.BinaryLz4;
            }

            // headers
            var size = 8 + 14;
            // NB for regular keys, use keys array length
            var keysSize = ArrayBinaryConverter<TKey>.Instance.SizeOf(map.Keys, 0, map.IsRegular ? map.Keys.Length : map.Length, out var keysStream, format);
            var valuesSize = ArrayBinaryConverter<TValue>.Instance.SizeOf(map.Values, 0, map.Length, out var valuesStream, format);

            Debug.Assert(keysStream != null && valuesStream != null);

            size += keysSize;
            size += valuesSize;

            if (map.Length == 0)
            {
                temporaryStream = default;
                return size; // empty map
            }

            var buffer = RecyclableMemoryStreamManager.Default.GetLargeBuffer(size, String.Empty);

            ref var destination = ref buffer[0];
            // relative to ptr + offset
            var position = 8;

            // 14 - map header
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position), map.Length);
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position + 4), map.Version);
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position + 4 + 8), (byte)(map.IsRegular ? 1 : 0));
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position + 4 + 8 + 1), (byte)(map.IsCompleted ? 1 : 0));

            position = position + 14;
            if (keysStream != null)
            {
                keysStream.WriteToRef(ref AddByteOffset(ref destination, (IntPtr)position));
            }
            else
            {
                fixed (byte* keysPtr = &buffer[position])
                {
                    ArrayBinaryConverter<TKey>.Instance.Write(map.Keys, 0, map.IsRegular ? map.Keys.Length : map.Length,
                        (IntPtr)keysPtr, null, format);
                }
            }
            position += keysSize;

            if (valuesStream != null)
            {
                valuesStream.WriteToRef(ref AddByteOffset(ref destination, (IntPtr)position));
            }
            else
            {
                fixed (byte* valuesPtr = &buffer[position])
                {
                    ArrayBinaryConverter<TValue>.Instance.Write(map.Values, 0, map.Length,
                        (IntPtr)valuesPtr, null, format);
                }
            }
            position += valuesSize;

            // length (include all headers)
            WriteUnaligned(ref destination, position);
            // version
            var header = new DataTypeHeader
            {
                VersionAndFlags = {
                    Version = 0,
                    IsBinary = true,
                    IsDelta = false,
                    IsCompressed = true },
                TypeEnum = TypeEnum.ArrayBasedMap
            };
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)4), header);
            temporaryStream = RecyclableMemoryStream.Create(RecyclableMemoryStreamManager.Default,
                null,
                position,
                buffer,
                position);
            return position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Write(in T value, IntPtr pinnedDestination, MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            if (temporaryStream == null)
            {
                SizeOf(in value, out temporaryStream, format);
            }

            var len = temporaryStream.Length;
            temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
            temporaryStream.Dispose();
            return checked((int)len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract int Read(IntPtr ptr, out T value);

#pragma warning restore 0618
    }

    internal class ArrayBasedMapJsonConverter<TKey, TValue, TMap> : JsonConverter<TMap>
        where TMap : IArrayBasedMap<TKey, TValue>
    {
        public override void WriteJson(JsonWriter writer, TMap value, JsonSerializer serializer)
        {
            var map = (IArrayBasedMap<TKey, TValue>)value;
            writer.WriteStartArray();
            writer.WriteValue(map.Length);
            // TODO long version
            writer.WriteValue(checked((int)map.Version));
            writer.WriteValue(map.IsRegular);
            writer.WriteValue(map.IsCompleted);
            serializer.Serialize(writer, map.Keys.Take(map.IsRegular ? 2 : map.Length));
            serializer.Serialize(writer, map.Values.Take(map.Length));
            writer.WriteEndArray();
        }

        public override TMap ReadJson(JsonReader reader, Type objectType, TMap existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return default;
            }
            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new Exception("Invalid JSON for Variant type");
            }

            var length = reader.ReadAsInt32();
            var versionAsInt32 = reader.ReadAsInt32();
            var isRegural = reader.ReadAsBoolean();
            var isCompleted = reader.ReadAsBoolean();

            var keys = serializer.Deserialize<TKey[]>(reader);
            var values = serializer.Deserialize<TValue[]>(reader);

            return default;
        }
    }
}