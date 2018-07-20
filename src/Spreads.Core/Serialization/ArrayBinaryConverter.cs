// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        // This is special, TypeHelper is aware of it (for others version must be > 0)
        public byte Version => 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(in TElement[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary)
        {
            if ((uint)valueOffset + (uint)valueCount > (uint)value.Length)
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
                        return CompressedBlittableArrayBinaryConverter<TElement>.Instance.SizeOf(value, valueOffset,
                            valueCount, out temporaryStream, format);
                    }
                }
            }

            return BinarySerializer.SizeOf(new ArraySegment<TElement>(value, valueOffset, valueCount), out temporaryStream,
                format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

                        // header
                        var header = new DataTypeHeader
                        {
                            VersionAndFlags = { IsBinary = true },
                            TypeEnum = TypeEnum.Array,
                            TypeSize = (byte)ItemSize,
                            ElementTypeEnum = VariantHelper<TElement>.TypeEnum
                        };
                        WriteUnaligned((void*)pinnedDestination, header);

                        // payload size
                        WriteUnaligned((void*)(pinnedDestination + DataTypeHeader.Size), totalSize - 8);

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

            // NB this converter should not be used directly and BinarySerializer check if stream is null and gets it when needed
            //SizeOf(value, valueOffset, valueCount, out var stream, format);
            //return Write(in value, pinnedDestination, stream, format);

            ThrowHelper.ThrowInvalidOperationException("SizeOf must have returned a temporary stream for cases when Size < 0 or JSON was requested");
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Read(IntPtr ptr, out TElement[] value, out int count, bool exactSize = true)
        {
            var header = ReadUnaligned<DataTypeHeader>((void*)ptr);
            var payloadSize = ReadUnaligned<int>((void*)(ptr + DataTypeHeader.Size));
            if (header.VersionAndFlags.Version != Version)
            {
                ThrowHelper.ThrowInvalidOperationException("ByteArrayBinaryConverter work only with version 0");
            }
            
            if (header.VersionAndFlags.IsBinary && ItemSize > 0)
            {
                if (!header.VersionAndFlags.IsCompressed)
                {
                    if (header.VersionAndFlags.IsDelta) { ThrowHelper.ThrowNotSupportedException("Raw ByteArrayBinaryConverter does not support deltas"); }

                    var arraySize = payloadSize / ItemSize;
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

                        CopyBlockUnaligned(ref dstRef, ref srcRef, checked((uint)payloadSize));

                        value = array;
                    }
                    else
                    {
                        value = EmptyArray<TElement>.Instance;
                    }

                    count = arraySize;
                    return payloadSize;
                }
                else
                {
                    var len = CompressedBlittableArrayBinaryConverter<TElement>.Instance.Read(ptr, out var tmp,
                        out count, exactSize);
                    Debug.Assert(len == payloadSize + 8);
                    value = tmp;
                    return len;
                }
            }


            var readLen = BinarySerializer.Read<TElement[]>(ptr, out var arr);
            if (readLen > 0 && arr != null)
            {
                value = arr;
                count = arr.Length;
                return readLen;
            }

            ThrowHelper.ThrowInvalidOperationException(
                "ArrayBinaryConverter cannot read array");
            value = default;
            count = 0;
            return default;
        }

        private static readonly int ItemSize = TypeHelper<TElement>.Size;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(in TElement[] value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            return SizeOf(in value, 0, value.Length, out temporaryStream, format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(in TElement[] value, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            return Write(in value, 0, value.Length, pinnedDestination, temporaryStream, format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(IntPtr ptr, out TElement[] value)
        {
            return Read(ptr, out value, out _, true);
        }
    }
}