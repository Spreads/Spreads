// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Spreads {

    internal interface IArrayBasedMap<TKey, TValue> {
        int Length { get; }
        long Version { get; }
        bool IsRegular { get; }
        bool IsReadOnly { get; }
        TKey[] Keys { get; }
        TValue[] Values { get; }
    }

    internal abstract class ArrayBasedMapConverter<TKey, TValue, T> : IBinaryConverter<T> where T : IArrayBasedMap<TKey, TValue> {
#pragma warning disable 0618

        //private static readonly int KeySize = TypeHelper<TKey>.Size;
        //private static readonly int ValueSize = TypeHelper<TValue>.Size;
        public bool IsFixedSize => false;

        public int Size => 0;
        public byte Version => 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(T value, out MemoryStream temporaryStream) {
            // headers
            var size = 8 + 14;
            MemoryStream keys;
            MemoryStream values;
            // NB for regular keys, use keys array length
            var keysSize = CompressedArrayBinaryConverter<TKey>.Instance.SizeOf(value.Keys, 0, value.IsRegular ? value.Keys.Length : value.Length, out keys);
            var valuesSize = CompressedArrayBinaryConverter<TValue>.Instance.SizeOf(value.Values, 0, value.Length, out values);
            Debug.Assert(keys != null && values != null);
            size += keysSize;
            size += valuesSize;

            temporaryStream = RecyclableMemoryStreamManager.Default.GetStream("ArrayBasedMapConverter.SizeOf",
                size);
            // binary size
            temporaryStream.WriteAsPtr<int>(size);
            // binary version
            temporaryStream.WriteAsPtr<byte>(Version);
            // flags + reserved
            temporaryStream.WriteAsPtr<byte>(0);
            temporaryStream.WriteAsPtr<short>(0);
            // map size
            temporaryStream.WriteAsPtr<int>(value.Length);
            // map version
            temporaryStream.WriteAsPtr<long>(value.Version);
            temporaryStream.WriteAsPtr<byte>((byte)(value.IsRegular ? 1 : 0));
            temporaryStream.WriteAsPtr<byte>((byte)(value.IsReadOnly ? 1 : 0));
            keys.CopyTo(temporaryStream);
            values.CopyTo(temporaryStream);
            keys.Dispose();
            values.Dispose();
            temporaryStream.Position = 0;
            Debug.Assert(size == temporaryStream.Length);
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(T value, ref DirectBuffer destination, uint offset = 0, MemoryStream temporaryStream = null) {
            if (temporaryStream != null) {
                var len = temporaryStream.Length;
                if (destination.Length < offset + len) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                temporaryStream.WriteToPtr(destination.Data + (int)offset);
                temporaryStream.Dispose();
                return checked((int)len);
            }

            // all headers: serializer + map properties + 2 * Blosc
            if (destination.Length < offset + 8 + 14) return (int)BinaryConverterErrorCode.NotEnoughCapacity;

            var position = (int)offset + 8;
            // 14 - map header
            destination.WriteInt32(position, value.Length);
            destination.WriteInt64(position + 4, value.Version);
            destination.WriteByte(position + 4 + 8, (byte)(value.IsRegular ? 1 : 0));
            destination.WriteByte(position + 4 + 8 + 1, (byte)(value.IsReadOnly ? 1 : 0));

            position = position + 14;

            if (value.Length == 0) return position; // empty map

            // assume that we have enough capacity, the check (via SizeOf call) is done inside BinarySerializer
            // TODO instead of special treatment of regular keys, think about skipping compression for small arrays
            //if (value.IsRegular) {
            //    keysSize = BinarySerializer.Write<TKey[]>(value.Keys, ref destination, (uint)position);
            //}
            var keysSize = CompressedArrayBinaryConverter<TKey>.Instance.Write(
                        value.Keys, 0, value.Length, ref destination, (uint)position);
            if (keysSize > 0) {
                position += keysSize;
            } else {
                return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            }

            var valuesSize = CompressedArrayBinaryConverter<TValue>.Instance.Write(
                    value.Values, 0, value.Length, ref destination, (uint)position);
            if (valuesSize > 0) {
                position += valuesSize;
            } else {
                return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            }

            // length (include all headers)
            destination.WriteInt32(0, position);
            // version
            destination.WriteByte(4, Version);
            return position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract int Read(IntPtr ptr, ref T value);

#pragma warning restore 0618
    }
}