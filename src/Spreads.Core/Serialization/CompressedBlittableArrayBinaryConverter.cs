// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Blosc;
using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    /// <summary>
    /// Used for IArrayBasedMap serialization and for arrays serialization with forced format
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    internal class CompressedBlittableArrayBinaryConverter<TElement> : IArrayBinaryConverter<TElement>
    {
        private static readonly bool IsIDelta = typeof(IDelta<TElement>).GetTypeInfo().IsAssignableFrom(typeof(TElement));

        internal static CompressedBlittableArrayBinaryConverter<TElement> Instance =
            new CompressedBlittableArrayBinaryConverter<TElement>();

        private CompressedBlittableArrayBinaryConverter()
        {
        }

        public bool IsFixedSize => false;
        public int Size => -1;
#pragma warning disable 618
        public byte Version => 0;

        private static readonly int ItemSize = TypeHelper<TElement>.Size;
#pragma warning restore 618

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int SizeOf(in TElement[] map, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            SerializationFormat format = SerializationFormat.Binary)
        {
            if (ItemSize > 0)
            {
                var maxSize = 8 + (16 + BloscMethods.ProcessorCount * 4) + ItemSize * valueCount;
                var buffer = RecyclableMemoryStreamManager.Default.GetLargeBuffer(maxSize, String.Empty);
                int totalSize;
                fixed (byte* ptr = &buffer[0])
                {
                    totalSize = Write(map, valueOffset, valueCount, (IntPtr)ptr, null, format);
                }
                temporaryStream = RecyclableMemoryStream.Create(RecyclableMemoryStreamManager.Default,
                    null,
                    totalSize,
                    buffer,
                    totalSize);
                return totalSize;
            }
            ThrowHelper.ThrowInvalidOperationException("CompressedBlittableArrayBinaryConverter only supports blittable types");

            temporaryStream = default;
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(in ArraySegment<TElement> segment, IntPtr destination,
            MemoryStream temporaryStream = null, SerializationFormat compression = SerializationFormat.Binary)
        {
            return Write(segment.Array, segment.Offset, segment.Count, destination,
                temporaryStream, compression);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Write(in TElement[] value, int valueOffset, int valueCount, IntPtr pinnedDestination,
            MemoryStream temporaryStream = null,
            SerializationFormat format = SerializationFormat.Binary)
        {
            // NB Blosc calls below are visually large - many LOCs with comments, but this is only a single method call
            if (value == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(value));
                return default;
            }

            if (temporaryStream != null)
            {
                var len = temporaryStream.Length;
                temporaryStream.WriteToRef(ref AsRef<byte>((void*)pinnedDestination));
                temporaryStream.Dispose();
                return checked((int)len);
            }
            if (!(format == SerializationFormat.BinaryLz4 || format == SerializationFormat.BinaryZstd))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(format));
            }

            var compressionMethod = format == SerializationFormat.BinaryLz4 ? "lz4" : "zstd";
            var compressionLevel = format == SerializationFormat.BinaryLz4
                ? Settings._lz4CompressionLevel
                : Settings._zstdCompressionLevel;

            var isDelta = IsIDelta;

            var position = 8;
            if (valueCount > 0)
            {
                var inputSize = valueCount * ItemSize;
                var compressedSize = 0;
                if (ItemSize > 0)
                {
                    var maxSize = 8 + (16 + BloscMethods.ProcessorCount * 4) + ItemSize * valueCount;

                    if (typeof(TElement) == typeof(DateTime))
                    {
                        isDelta = true;

                        // NB thread-static buffer, no thread switches/async here!
                        var buffer = BufferPool.StaticBuffer.Array.Length >= inputSize
                            ? BufferPool.StaticBuffer.Array
                            : BufferPool<byte>.Rent(inputSize);
                        var dtArray = (DateTime[])(object)value;
                        var longArray = Unsafe.As<long[]>(dtArray);

                        // NB For DateTime we calculate delta not from the first but
                        // from the previous value. This is a special case for the
                        // fact that DT[] is usually increasing by a similar (regular) step
                        // and the deltas are always positive, small and close to each other.
                        // In contrast, Price/Decimal could fluctuate in a small range
                        // and delta from previous could often change its sign, which
                        // leads to a very different bits and significantly reduces
                        // the Blosc shuffling benefits. For stationary time series
                        // deltas from the first value are also stationary and their sign
                        // changes less frequently than the sign of deltas from previous.

                        // TODO Review, use Unsafe.Add
                        var previousLong = longArray[valueOffset];

                        Unsafe.WriteUnaligned(ref buffer[0], previousLong);
                        for (var i = 1; i < valueCount; i++)
                        {
                            var currentLong = longArray[i + valueOffset];
                            var diff = currentLong - previousLong;
                            Unsafe.WriteUnaligned(ref buffer[i * ItemSize], diff);
                            previousLong = currentLong;
                        }

                        fixed (byte* srcPtr = &buffer[0])
                        {
                            compressedSize = BloscMethods.blosc_compress_ctx(
                                new IntPtr(compressionLevel),
                                new IntPtr(1), // do byte shuffle 1
                                new UIntPtr((uint)ItemSize), // type size
                                new UIntPtr((uint)inputSize), // number of input bytes
                                (IntPtr)srcPtr,
                                pinnedDestination + position, // destination
                                new UIntPtr((uint)maxSize), // destination length
                                compressionMethod,
                                new UIntPtr(0), // default block size
                                BloscMethods.ProcessorCount //
                            );
                        }

                        if (BufferPool.StaticBuffer.Array.Length < inputSize)
                        {
                            BufferPool<byte>.Return(buffer);
                        }
                    }
                    else if (IsIDelta)
                    {
                        var first = value[valueOffset];
                        var buffer = BufferPool.StaticBuffer.Array.Length >= inputSize
                            ? BufferPool.StaticBuffer.Array
                            : BufferPool<byte>.Rent(inputSize);

                        Unsafe.WriteUnaligned(ref buffer[0], first);
                        for (var i = 1; i < valueCount; i++)
                        {
                            var diff = Unsafe.GetDeltaConstrained(ref first, ref value[valueOffset + i]);
                            Unsafe.WriteUnaligned(ref buffer[i * ItemSize], diff);
                        }
                        fixed (byte* srcPtr = &buffer[0])
                        {
                            compressedSize = BloscMethods.blosc_compress_ctx(
                                new IntPtr(compressionLevel), // max format 9
                                new IntPtr(1), // do byte shuffle 1
                                new UIntPtr((uint)ItemSize), // type size
                                new UIntPtr((uint)(valueCount * ItemSize)), // number of input bytes
                                (IntPtr)srcPtr,
                                pinnedDestination + position, // destination
                                new UIntPtr((uint)maxSize), // destination length
                                compressionMethod,
                                new UIntPtr(0), // default block size
                                BloscMethods.ProcessorCount //
                            );
                        }
                        if (BufferPool.StaticBuffer.Array.Length < inputSize)
                        {
                            BufferPool<byte>.Return(buffer);
                        }
                    }
                    else
                    {
                        var pinnedArray = GCHandle.Alloc(value, GCHandleType.Pinned);
                        var srcPtr = Marshal.UnsafeAddrOfPinnedArrayElement(value, valueOffset);
                        compressedSize = BloscMethods.blosc_compress_ctx(
                            new IntPtr(compressionLevel), // max format 9
                            new IntPtr(1), // do byte shuffle 1
                            new UIntPtr((uint)ItemSize), // type size
                            new UIntPtr((uint)(valueCount * ItemSize)), // number of input bytes
                            srcPtr,
                            pinnedDestination + position, // destination
                            new UIntPtr((uint)maxSize), // destination length
                            compressionMethod,
                            new UIntPtr(0), // default block size
                            BloscMethods.ProcessorCount //
                        );
                        pinnedArray.Free();
                    }
                }
                else if (BufferPoolRetainedMemoryHelper<TElement>.IsRetainedMemory)
                {
                    ThrowHelper.ThrowNotImplementedException();
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperationException(
                        "CompressedBlittableArrayBinaryConverter only supports blittable types");
                }

                //else if (BufferPoolRetainedMemoryHelper<TElement>.IsRetainedMemory)
                //{
                //    throw new NotImplementedException();
                //}
                //else
                //{
                //    MemoryStream tempStream;
                //    var bytesSize =
                //        BinarySerializer.SizeOf(new ArraySegment<TElement>(value, valueOffset, valueCount),
                //            out tempStream, format);
                //    var buffer = BufferPool<byte>.Rent(bytesSize);
                //    var writtenBytes =
                //        BinarySerializer.Write(new ArraySegment<TElement>(value, valueOffset, valueCount), buffer, 0,
                //            tempStream);
                //    tempStream?.Dispose();
                //    Debug.Assert(bytesSize == writtenBytes);
                //    compressedSize = CompressedBlittableArrayBinaryConverter<byte>.Instance.Write(buffer, 0, writtenBytes,
                //        ref destination, destinationOffset, null, format);
                //    BufferPool<byte>.Return(buffer);
                //}

                if (compressedSize > 0)
                {
                    position += compressedSize;
                }
                else
                {
                    return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                }
            }

            // length
            WriteUnaligned((void*)(pinnedDestination), position);
            // version & flags
            var header = new DataTypeHeader
            {
                VersionAndFlags = {
                    Version = 0,
                    IsBinary = true,
                    IsDelta = isDelta,
                    IsCompressed = true },
                TypeEnum = TypeEnum.Array,
                TypeSize = (byte)ItemSize,
                ElementTypeEnum = VariantHelper<TElement>.TypeEnum
            };
            WriteUnaligned((void*)(pinnedDestination + 4), header);
            return position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Read(IntPtr ptr, out TElement[] value, out int length, bool exactSize = false)
        {
            var totalSize = ReadUnaligned<int>((void*)ptr);
            var header = ReadUnaligned<DataTypeHeader>((void*)(ptr + 4));

            if (!header.VersionAndFlags.IsCompressed)
            {
                ThrowHelper.ThrowInvalidOperationException("Wrong compressed flag. CompressedBlittableArrayBinaryConverter.Read works only with compressed data.");
            }

            if (header.VersionAndFlags.Version != 0)
            {
                ThrowHelper.ThrowNotSupportedException($"CompressedBinaryConverter work only with version {Version}");
            }
            if (ItemSize <= 0)
            {
                ThrowHelper.ThrowInvalidOperationException("ItemSize <= 0");
            }

            if (totalSize <= 8 + 16)
            {
                value = EmptyArray<TElement>.Instance;
                length = 0;
                return totalSize;
            }

            var source = ptr + 8;

            // avoid additional P/Invoke call, read header directly
            // https://github.com/Blosc/c-blosc/blob/master/README_HEADER.rst
            var nbytes = *(int*)(source + 4);
#if DEBUG
                var blocksize = *(int*)(source + 8);
                var cbytes = *(int*)(source + 12);
                var nbytes2 = new UIntPtr();
                var cbytes2 = new UIntPtr();
                var blocksize2 = new UIntPtr();
                BloscMethods.blosc_cbuffer_sizes(source, ref nbytes2, ref cbytes2, ref blocksize2);
                Debug.Assert(nbytes == nbytes2.ToUInt32());
                Debug.Assert(cbytes == cbytes2.ToUInt32());
                Debug.Assert(blocksize == blocksize2.ToUInt32());
#endif
            var arraySize = nbytes / ItemSize;
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

            length = arraySize;
            value = array;

            if (arraySize > 0)
            {
                var inputSize = arraySize * ItemSize;
                if (typeof(TElement) == typeof(DateTime))
                {
                    var buffer = BufferPool.StaticBuffer.Array.Length >= inputSize
                        ? BufferPool.StaticBuffer.Array
                        : BufferPool<byte>.Rent(inputSize);
                    var dtArray = As<DateTime[]>(array);

                    fixed (byte* tgtPtr = &buffer[0])
                    {
                        var destination = (IntPtr)tgtPtr;
                        var decompSize = BloscMethods.blosc_decompress_ctx(
                            source, destination, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                        if (decompSize <= 0) throw new ArgumentException("Invalid compressed input");
                        Debug.Assert(decompSize == nbytes);
                    }
                    var longArray = Unsafe.As<long[]>(buffer);
                    // NB a lot of data was stored without diff for DateTime,
                    // should just check the flag
                    if (header.VersionAndFlags.IsDelta)
                    {
                        var previousLong = longArray[0];
                        var first = As<long, DateTime>(ref previousLong);
                        dtArray[0] = first;
                        for (var i = 1; i < arraySize; i++)
                        {
                            var deltaLong = longArray[i];
                            var currentLong = previousLong + deltaLong;
                            dtArray[i] = As<long, DateTime>(ref currentLong);
                            previousLong = currentLong;
                        }
                    }
                    else
                    {
                        for (var i = 0; i < arraySize; i++)
                        {
                            dtArray[i] = As<long, DateTime>(ref longArray[i]);
                        }
                    }

                    if (BufferPool.StaticBuffer.Array.Length < inputSize)
                    {
                        BufferPool<byte>.Return(buffer);
                    }
                }
                else if (header.VersionAndFlags.IsDelta)
                {
                    if (!IsIDelta)
                    {
                        ThrowHelper.ThrowInvalidOperationException("Delta flag is set for a type that does not implement IDelta interface.");
                    }

                    var buffer = BufferPool.StaticBuffer.Array.Length >= inputSize
                        ? BufferPool.StaticBuffer.Array
                        : BufferPool<byte>.Rent(inputSize);
                    var targetArray = array;

                    fixed (byte* tgtPtr = &buffer[0])
                    {
                        var destination = tgtPtr;
                        var decompSize = BloscMethods.blosc_decompress_ctx(
                            source, (IntPtr)destination, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                        if (decompSize <= 0) throw new ArgumentException("Invalid compressed input");
                        Debug.Assert(decompSize == nbytes);

                        var first = Unsafe.ReadUnaligned<TElement>(destination);
                        targetArray[0] = first;
                        for (var i = 1; i < arraySize; i++)
                        {
                            var currentDelta = Unsafe.Read<TElement>(destination + i * ItemSize);
                            var current = Unsafe.AddDeltaConstrained(ref first, ref currentDelta);
                            targetArray[i] = current;
                        }
                    }

                    if (BufferPool.StaticBuffer.Array.Length < inputSize)
                    {
                        BufferPool<byte>.Return(buffer);
                    }
                }
                else
                {
                    ref var asRefByte = ref Unsafe.As<TElement, byte>(ref array[0]);

                    // TODO remove this try/catch and debugger stuff, it was used to catch an eror that disappeared after adding
                    // try/catch.
                    try
                    {
                        fixed (byte* pointer = &asRefByte)
                        {
                            var decompSize = BloscMethods.blosc_decompress_ctx(
                                source, (IntPtr)pointer, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                            if (decompSize <= 0)
                            {
                                ThrowHelper.ThrowArgumentException("Invalid compressed input");
                            }
                            Debug.Assert(decompSize == nbytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debugger.Launch();
                        UIntPtr nb = UIntPtr.Zero;
                        UIntPtr cb = UIntPtr.Zero;
                        UIntPtr bl = UIntPtr.Zero;

                        BloscMethods.blosc_cbuffer_sizes(source, ref nb, ref cb, ref bl);
                        //}
                        Trace.WriteLine($"Blosc error: nbytes: {nbytes}, nbytes2: {nb}, cbytes: {cb} arr size: {value.Length}, \n\r exeption: {ex.Message + Environment.NewLine + ex}");
                        throw;
                    }
                }
            }
            return totalSize;
        }
    }
}