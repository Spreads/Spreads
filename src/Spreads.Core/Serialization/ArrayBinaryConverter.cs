// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    internal static class ArrayConverterFactory
    {
        public static IBinaryConverter<TElement[]> GenericCreate<TElement>()
        {
            return new ArrayBinaryConverter<TElement>();
        }

        public static object Create(Type type)
        {
            var method = typeof(ArrayConverterFactory).GetTypeInfo().GetMethod("GenericCreate");
            var generic = method?.MakeGenericMethod(type);
            return generic?.Invoke(null, null);
        }
    }

    internal class ArrayBinaryConverter<TElement> : IBinaryConverter<TElement[]>, IArrayBinaryConverter<TElement>
    {
        internal static ArrayBinaryConverter<TElement> Instance =
            new ArrayBinaryConverter<TElement>();

        public bool IsFixedSize => false;
        public int Size => -1;
        public byte Version => 0;

        public int SizeOf(in TElement[] map, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary)
        {
            if ((uint)valueOffset + (uint)valueCount > (uint)map.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }

            if ((int)format < 100)
            {
                if (ItemSize > 0)
                {
                    if (format == SerializationFormat.Binary)
                    {
                        temporaryStream = null;
                        return 8 + ItemSize * valueCount;
                    }

                    if (format == SerializationFormat.BinaryLz4 || format == SerializationFormat.BinaryZstd)
                    {
                        return CompressedBlittableArrayBinaryConverter<TElement>.Instance.SizeOf(map, valueOffset,
                            valueCount, out temporaryStream, format);
                    }
                }
            }
            temporaryStream = BinarySerializer.Json.SerializeWithHeader(map.Skip(valueOffset).Take(valueCount).ToArray(), format);
            return checked((int)temporaryStream.Length);
        }

        public unsafe int Write(in TElement[] value,
            int valueOffset,
            int valueCount,
            IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (temporaryStream != null)
            {
                var len = temporaryStream.Length;
                temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
                temporaryStream.Dispose();
                return checked((int)len);
            }

            if ((int)format < 100)
            {
                if (ItemSize > 0)
                {
                    if (format == SerializationFormat.Binary)
                    {
                        // if (temporaryStream != null) throw new NotSupportedException("Uncompressed ArrayBinaryConverter does not work with temp streams.");

                        var totalSize = 8 + ItemSize * valueCount;

                        ref var srcRef = ref As<TElement, byte>(ref value[valueOffset]);
                        // size
                        WriteUnaligned((void*)pinnedDestination, totalSize);

                        var header = new DataTypeHeader
                        {
                            VersionAndFlags = { IsBinary = true },
                            TypeEnum = TypeEnum.Array,
                            TypeSize = (byte)ItemSize,
                            ElementTypeEnum = VariantHelper<TElement>.TypeEnum
                        };
                        WriteUnaligned((void*)(pinnedDestination + 4), header);

                        if (valueCount > 0)
                        {
                            ref var dstRef = ref AsRef<byte>((void*)(pinnedDestination + 8));

                            CopyBlockUnaligned(ref dstRef, ref srcRef, checked((uint)(ItemSize * valueCount)));
                        }

                        return totalSize;
                    }

                    if (format == SerializationFormat.BinaryLz4 || format == SerializationFormat.BinaryZstd)
                    {
                        return CompressedBlittableArrayBinaryConverter<TElement>.Instance.Write(in value, valueOffset,
                            valueCount,
                            pinnedDestination, null, format);
                    }

                    // ThrowHelper.ThrowInvalidOperationException("ArrayBinaryConverter must be called only with one of binary serialization format");
                }
            }

            ThrowHelper.ThrowInvalidOperationException("SizeOf must have returned a temporary stream for cases when Size < 0 or JSON was requested");
            return default;
        }

        public unsafe int Read(IntPtr ptr, out TElement[] value, out int count, bool exactSize = true)
        {
            var totalSize = ReadUnaligned<int>((void*)ptr);
            var header = ReadUnaligned<DataTypeHeader>((void*)(ptr + 4));
            ////var version = (byte)(header >> 4);
            ////var isCompressed = (header & 0b0000_0001) != 0;
            //if (header.VersionAndFlags.Version != 0)
            //{
            //    ThrowHelper.ThrowInvalidOperationException("ByteArrayBinaryConverter work only with version 0");
            //}
            //if (!header.VersionAndFlags.IsBinary)
            //{
            //    ThrowHelper.ThrowInvalidOperationException("ByteArrayBinaryConverter work only with binary data");
            //}

            if (header.VersionAndFlags.IsBinary)
            {
                if (ItemSize > 0)
                {
                    if (!header.VersionAndFlags.IsCompressed)
                    {
                        if (header.VersionAndFlags.IsDelta) { ThrowHelper.ThrowNotSupportedException("Raw ByteArrayBinaryConverter does not support deltas"); }

                        var arraySize = (totalSize - 8) / ItemSize;
                        if (arraySize > 0)
                        {
                            if (header.TypeEnum != TypeEnum.Array) { ThrowHelper.ThrowInvalidOperationException("Wrong TypeEnum: expecting array"); }
                            if (header.TypeSize != ItemSize) { ThrowHelper.ThrowInvalidOperationException("Wrong item size"); }
                            if (header.ElementTypeEnum != VariantHelper<TElement>.TypeEnum) { ThrowHelper.ThrowInvalidOperationException("Wrong SubTypeEnum"); }

                            TElement[] array;
                            if (BitUtil.IsPowerOfTwo(arraySize) || !exactSize)
                            {
                                array = BufferPool<TElement>.Rent(arraySize);
                                if (exactSize && array.Length != arraySize)
                                {
                                    BufferPool<TElement>.Return(array);
                                    array = new TElement[arraySize];
                                }
                            }
                            else
                            {
                                array = new TElement[arraySize];
                            }

                            ref var dstRef = ref As<TElement, byte>(ref array[0]);
                            ref var srcRef = ref AsRef<byte>((void*)(ptr + 8));

                            CopyBlockUnaligned(ref dstRef, ref srcRef, checked((uint)(totalSize - 8)));

                            value = array;
                        }
                        else
                        {
                            value = EmptyArray<TElement>.Instance;
                        }

                        count = arraySize;
                        return totalSize;
                    }
                    else
                    {
                        var len = CompressedBlittableArrayBinaryConverter<TElement>.Instance.Read(ptr, out var tmp,
                            out count, exactSize);
                        Debug.Assert(len == totalSize);
                        value = tmp;
                        return len;
                    }
                }

                ThrowHelper.ThrowInvalidOperationException(
                    "ArrayBinaryConverter must be called only on blittable types");
                value = default;
                count = 0;
                return default;
            }
            else
            {
                if (!header.VersionAndFlags.IsCompressed)
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    var stream = new UnmanagedMemoryStream((byte*)(ptr + 8), totalSize - 8);
                    value = BinarySerializer.Json.Deserialize<TElement[]>(stream);
                    count = value.Length;
                    return totalSize;
                }
                else
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    var comrpessedStream = new UnmanagedMemoryStream((byte*)(ptr + 8), totalSize - 8);
                    RecyclableMemoryStream decompressedStream =
                        RecyclableMemoryStreamManager.Default.GetStream();

                    using (var decompressor = new DeflateStream(comrpessedStream, CompressionMode.Decompress, true))
                    {
                        decompressor.CopyTo(decompressedStream);
                        decompressor.Close();
                    }
                    comrpessedStream.Dispose();
                    decompressedStream.Position = 0;
                    value = BinarySerializer.Json.Deserialize<TElement[]>(decompressedStream);
                    count = value.Length;
                    return totalSize;
                }
            }
        }

        private static readonly int ItemSize = TypeHelper<TElement>.Size;

        public int SizeOf(in TElement[] map, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            return SizeOf(in map, 0, map.Length, out temporaryStream, format);
        }

        public int Write(in TElement[] value, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            return Write(in value, 0, value.Length, pinnedDestination, temporaryStream, format);
        }

        public int Read(IntPtr ptr, out TElement[] value)
        {
            return Read(ptr, out value, out _, true);
        }
    }
}