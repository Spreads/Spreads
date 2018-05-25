// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.IO;
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

        public int Size => 0;
        public byte Version => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int SizeOf(in T value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            // headers
            var size = 8 + 14;
            // NB for regular keys, use keys array length
            var keysSize = CompressedBlittableArrayBinaryConverter<TKey>.Instance.SizeOf(value.Keys, 0, value.IsRegular ? value.Keys.Length : value.Length, out var keys, format);
            var valuesSize = CompressedBlittableArrayBinaryConverter<TValue>.Instance.SizeOf(value.Values, 0, value.Length, out var values, format);
            Debug.Assert(keys != null && values != null);
            size += keysSize;
            size += valuesSize;

            if (value.Length == 0)
            {
                temporaryStream = default;
                return size; // empty map
            }

            var buffer = RecyclableMemoryStreamManager.Default.GetLargeBuffer(size, String.Empty);

            ref var destination = ref buffer[0];
            // relative to ptr + offset
            var position = 8;

            // 14 - map header
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position), value.Length);
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position + 4), value.Version);
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position + 4 + 8), (byte)(value.IsRegular ? 1 : 0));
            WriteUnaligned(ref AddByteOffset(ref destination, (IntPtr)position + 4 + 8 + 1), (byte)(value.IsCompleted ? 1 : 0));

            position = position + 14;

            keys.WriteToRef(ref AddByteOffset(ref destination, (IntPtr)position));
            position += keysSize;

            values.WriteToRef(ref AddByteOffset(ref destination, (IntPtr)position));
            position += valuesSize;

            // length (include all headers)
            WriteUnaligned((void*)destination, position);
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
            WriteUnaligned((void*)(destination + 4), header);
            temporaryStream = RecyclableMemoryStream.Create(RecyclableMemoryStreamManager.Default,
                null,
                position,
                buffer,
                position);
            return position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Write(in T value, IntPtr destination, MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            if (temporaryStream == null)
            {
                SizeOf(in value, out temporaryStream, format);
            }

            var len = temporaryStream.Length;
            temporaryStream.WriteToRef(ref AsRef<byte>((void*)destination));
            temporaryStream.Dispose();
            return checked((int)len);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract int Read(IntPtr ptr, out T value);

#pragma warning restore 0618
    }
}