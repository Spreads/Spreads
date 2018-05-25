// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    internal static class BlittableArrayConverterFactory
    {
        public static IBinaryConverter<TElement[]> GenericCreate<TElement>()
        {
            return new BlittableArrayBinaryConverter<TElement>();
        }

        public static object Create(Type type)
        {
            var method = typeof(BlittableArrayConverterFactory).GetTypeInfo().GetMethod("GenericCreate");
            var generic = method?.MakeGenericMethod(type);
            return generic?.Invoke(null, null);
        }
    }

    internal class BlittableArrayBinaryConverter<TElement> : IBinaryConverter<TElement[]>
    {
        public bool IsFixedSize => false;
        public int Size => -1;
        public byte Version => 0;
        private static readonly int ItemSize = TypeHelper<TElement>.Size;

        public int SizeOf(in TElement[] value, out MemoryStream temporaryStream, SerializationFormat format = SerializationFormat.Binary)
        {
            if (ItemSize > 0)
            {
                if (format == SerializationFormat.Binary)
                {
                    temporaryStream = null;
                    return 8 + ItemSize * value.Length;
                }
                if (format == SerializationFormat.BinaryLz4 || format == SerializationFormat.BinaryZstd)
                {
                    return CompressedBlittableArrayBinaryConverter<TElement>.Instance.SizeOf(value, 0, value.Length, out temporaryStream, format);
                }
                ThrowHelper.ThrowInvalidOperationException("BlittableArrayBinaryConverter must be called only with one of binary serialization format");
            }
            ThrowHelper.ThrowInvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
            temporaryStream = default;
            return default;
        }

        public unsafe int Write(in TElement[] value, IntPtr destination,
            MemoryStream temporaryStream = null, SerializationFormat format = SerializationFormat.Binary)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ItemSize > 0)
            {
                if (format == SerializationFormat.Binary)
                {
                    if (temporaryStream != null) throw new NotSupportedException("Uncompressed BlittableArrayBinaryConverter does not work with temp streams.");

                    var totalSize = 8 + ItemSize * value.Length;

                    ref var srcRef = ref As<TElement, byte>(ref value[0]);
                    // size
                    WriteUnaligned((void*)destination, totalSize);

                    var header = new DataTypeHeader
                    {
                        VersionAndFlags = { IsBinary = true },
                        TypeEnum = TypeEnum.Array,
                        TypeSize = (byte)ItemSize,
                        ElementTypeEnum = VariantHelper<TElement>.TypeEnum
                    };
                    WriteUnaligned((void*)(destination + 4), header);

                    if (value.Length > 0)
                    {
                        ref var dstRef = ref AsRef<byte>((void*)(destination + 8));
                        ByteUtil.VectorizedCopy(ref dstRef, ref srcRef, checked((uint)(ItemSize * value.Length)));
                    }
                    return totalSize;
                }

                if (format == SerializationFormat.BinaryLz4 || format == SerializationFormat.BinaryZstd)
                {
                    return CompressedBlittableArrayBinaryConverter<TElement>.Instance.Write(in value, 0, value.Length,
                        destination, temporaryStream, format);
                }
                ThrowHelper.ThrowInvalidOperationException("BlittableArrayBinaryConverter must be called only with one of binary serialization format");
            }
            ThrowHelper.ThrowInvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
            return default;
        }

        public unsafe int Read(IntPtr ptr, out TElement[] value)
        {
            var totalSize = ReadUnaligned<int>((void*)ptr);
            var header = ReadUnaligned<DataTypeHeader>((void*)(ptr + 4));
            //var version = (byte)(header >> 4);
            //var isCompressed = (header & 0b0000_0001) != 0;
            if (header.VersionAndFlags.Version != 0)
            {
                ThrowHelper.ThrowInvalidOperationException("ByteArrayBinaryConverter work only with version 0");
            }
            if (!header.VersionAndFlags.IsBinary)
            {
                ThrowHelper.ThrowInvalidOperationException("ByteArrayBinaryConverter work only with binary data");
            }

            if (ItemSize > 0)
            {
                if (!header.VersionAndFlags.IsCompressed)
                {
                    if (header.VersionAndFlags.IsDelta)
                    {
                        ThrowHelper.ThrowNotSupportedException("Raw ByteArrayBinaryConverter does not support deltas");
                    }

                    var arraySize = (totalSize - 8) / ItemSize;
                    if (arraySize > 0)
                    {
                        if (header.TypeEnum != TypeEnum.Array)
                        {
                            ThrowHelper.ThrowInvalidOperationException("Wrong TypeEnum: expecting array");
                        }
                        if (header.TypeSize != ItemSize)
                        {
                            ThrowHelper.ThrowInvalidOperationException("Wrong item size");
                        }
                        if (header.ElementTypeEnum != VariantHelper<TElement>.TypeEnum)
                        {
                            ThrowHelper.ThrowInvalidOperationException("Wrong SubTypeEnum");
                        }
                        var array = new TElement[arraySize];
                        ref var dstRef = ref As<TElement, byte>(ref array[0]);
                        ref var srcRef = ref AsRef<byte>((void*)(ptr + 8));

                        ByteUtil.VectorizedCopy(ref dstRef, ref srcRef, checked((uint)(totalSize - 8)));

                        value = array;
                    }
                    else
                    {
                        value = EmptyArray<TElement>.Instance;
                    }
                    return totalSize;
                }
                else
                {
                    var len = CompressedBlittableArrayBinaryConverter<TElement>.Instance.Read(ptr, out var tmp, out var count, true);
                    Debug.Assert(tmp.Length == count);
                    value = tmp;
                    return len;
                }
            }
            ThrowHelper.ThrowInvalidOperationException("BlittableArrayBinaryConverter must be called only on blittable types");
            value = default;
            return default;
        }
    }
}