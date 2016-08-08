/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.Serialization;

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

            temporaryStream = RecyclableMemoryManager.MemoryStreams.GetStream("ArrayBasedMapConverter.SizeOf",
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
                if (destination.Length < offset + len) return (int)BinaryConverterErrorCode.NotEnoughCapacity; ;
                temporaryStream.WriteToPtr(destination.Data + (int)offset);
                temporaryStream.Dispose();
                return checked((int)len);
            }

            // all headers: serializer + map properties + 2 * Blosc
            if (destination.Length < offset + 8 + 14) return (int)BinaryConverterErrorCode.NotEnoughCapacity; ;

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