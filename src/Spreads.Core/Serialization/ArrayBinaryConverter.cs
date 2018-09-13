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

        public bool IsFixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        public int FixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => -1;
        }

        // This is special, TypeHelper is aware of it (for others version must be > 0)
        public byte ConverterVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }

        public int SizeOf(TElement[] value, out ArraySegment<byte> temporaryBuffer)
        {

            throw new NotImplementedException();
        }

        public int Write(TElement[] value, DirectBuffer destination)
        {
            throw new NotImplementedException();
        }

        public int Read(DirectBuffer source, out TElement[] value)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(TElement[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            throw new NotImplementedException();

            //if ((uint)valueOffset + (uint)valueCount > (uint)value.Length)
            //{
            //    ThrowHelper.ThrowArgumentOutOfRangeException();
            //}

            //var tsSize = (long)timestamp == default ? 0 : Timestamp.Size;

            //if ((int)format < 100)
            //{
            //    if (ItemSize > 0)
            //    {
            //        if (format == SerializationFormat.Binary)
            //        {
            //            temporaryStream = null;
            //            return 8 + tsSize + ItemSize * valueCount;
            //        }

            //        if (format == SerializationFormat.BinaryLz4 || format == SerializationFormat.BinaryZstd)
            //        {
            //            return CompressedBlittableArrayBinaryConverter<TElement>.Instance.SizeOf(value, valueOffset,
            //                valueCount, out temporaryStream, format, timestamp);
            //        }
            //    }
            //}

            //return BinarySerializer.SizeOf(new ArraySegment<TElement>(value, valueOffset, valueCount), out temporaryStream,
            //    format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Write(TElement[] value,
            int valueOffset,
            int valueCount,
            IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
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

                        var tsSize = (long)timestamp == default ? 0 : Timestamp.Size;

                        var payloadSize = tsSize + ItemSize * valueCount;

                        ref var srcRef = ref As<TElement, byte>(ref value[valueOffset]);

                        // header
                        var header = new DataTypeHeader
                        {
                            VersionAndFlags = { IsBinary = true, IsTimestamped = tsSize > 0 },
                            TypeEnum = TypeEnum.Array,
                            TypeSize = (byte)ItemSize,
                            ElementTypeEnum = VariantHelper<TElement>.TypeEnum
                        };
                        WriteUnaligned((void*)pinnedDestination, header);

                        // payload size
                        WriteUnaligned((void*)(pinnedDestination + DataTypeHeader.Size), payloadSize);

                        if (tsSize > 0)
                        {
                            WriteUnaligned((void*)(pinnedDestination + DataTypeHeader.Size + 4), timestamp);
                        }

                        if (valueCount > 0)
                        {
                            ref var dstRef = ref AsRef<byte>((void*)(pinnedDestination + 8 + tsSize));

                            CopyBlockUnaligned(ref dstRef, ref srcRef, checked((uint)(ItemSize * valueCount)));
                        }

                        return 8 + payloadSize;
                    }

                    if (format == SerializationFormat.BinaryLz4 || format == SerializationFormat.BinaryZstd)
                    {
                        return CompressedBlittableArrayBinaryConverter<TElement>.Instance.Write(value, valueOffset,
                            valueCount,
                            pinnedDestination, null, format, timestamp);
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
        public unsafe int Read(IntPtr ptr, out TElement[] value, out int count, out Timestamp timestamp, bool exactSize = true)
        {
            throw new NotImplementedException();
            //var header = ReadUnaligned<DataTypeHeader>((void*)ptr);
            //var payloadSize = ReadUnaligned<int>((void*)(ptr + DataTypeHeader.Size));
            //var position = 8;

            //if (header.VersionAndFlags.ConverterVersion != ConverterVersion)
            //{
            //    ThrowHelper.ThrowInvalidOperationException("ByteArrayBinaryConverter work only with version 0");
            //}

            //if (header.VersionAndFlags.IsBinary && ItemSize > 0)
            //{
            //    if (!header.VersionAndFlags.IsCompressed)
            //    {
            //        if (header.VersionAndFlags.IsDelta)
            //        {
            //            ThrowHelper.ThrowNotSupportedException("Raw ByteArrayBinaryConverter does not support deltas");
            //        }

            //        var tsSize = 0;
            //        if (header.VersionAndFlags.IsTimestamped)
            //        {
            //            tsSize = Timestamp.Size;
            //            timestamp = ReadUnaligned<Timestamp>((void*)(ptr + position));
            //            position += 8;
            //        }
            //        else
            //        {
            //            timestamp = default;
            //        }

            //        var arraySize = (payloadSize - tsSize) / ItemSize;
            //        if (arraySize > 0)
            //        {
            //            if (header.TypeEnum != TypeEnum.Array)
            //            {
            //                ThrowHelper.ThrowInvalidOperationException("Wrong TypeEnum: expecting array");
            //            }

            //            if (header.TypeSize != ItemSize)
            //            {
            //                ThrowHelper.ThrowInvalidOperationException("Wrong item size");
            //            }

            //            if (header.ElementTypeEnum != VariantHelper<TElement>.TypeEnum)
            //            {
            //                ThrowHelper.ThrowInvalidOperationException("Wrong SubTypeEnum");
            //            }

            //            TElement[] array;
            //            if (BitUtil.IsPowerOfTwo(arraySize) || !exactSize)
            //            {
            //                array = BufferPool<TElement>.Rent(arraySize);
            //                if (exactSize && array.Length != arraySize)
            //                {
            //                    BufferPool<TElement>.Return(array);
            //                    array = new TElement[arraySize];
            //                }
            //            }
            //            else
            //            {
            //                array = new TElement[arraySize];
            //            }

            //            ref var dstRef = ref As<TElement, byte>(ref array[0]);
            //            ref var srcRef = ref AsRef<byte>((void*)(ptr + position));

            //            CopyBlockUnaligned(ref dstRef, ref srcRef, checked((uint)payloadSize));

            //            value = array;
            //        }
            //        else
            //        {
            //            value = EmptyArray<TElement>.Instance;
            //        }

            //        count = arraySize;
            //        return 8 + payloadSize;
            //    }

            //    {
            //        var len = CompressedBlittableArrayBinaryConverter<TElement>.Instance.Read(ptr, out var tmp,
            //            out count, out timestamp, exactSize);
            //        if (Settings.AdditionalCorrectnessChecks.Enabled)
            //        {
            //            ThrowHelper.AssertFailFast(len == 8 + payloadSize,
            //                $"len {len} == 8 + payloadSize {payloadSize}");
            //        }

            //        Debug.Assert(len == 8 + payloadSize);
            //        value = tmp;
            //        return len;
            //    }
            //}

            //var readLen = BinarySerializer.Read<TElement[]>(ptr, out var arr, out timestamp);
            //if (readLen > 0 && arr != null)
            //{
            //    value = arr;
            //    count = arr.Length;
            //    return readLen;
            //}

            //ThrowHelper.ThrowInvalidOperationException("ArrayBinaryConverter cannot read array");
            //value = default;
            //count = 0;
            //return default;
        }

        private static readonly int ItemSize = TypeHelper<TElement>.FixedSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(TElement[] value, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary,
            Timestamp timestamp = default)
        {
            return SizeOf(value, 0, value.Length, out temporaryStream, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(TElement[] value, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary, Timestamp timestamp = default)
        {
            return Write(value, 0, value.Length, pinnedDestination, temporaryStream, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(IntPtr ptr, out TElement[] value, out Timestamp timestamp)
        {
            return Read(ptr, out value, out _, out timestamp, true);
        }
    }
}