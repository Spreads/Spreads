// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using System.Buffers;
using Spreads.Blosc;

namespace Spreads.Serialization {

    // this is special case used only for IArrayBasedMap serialization (TODO and for array compression, but via an explicit method Compress)
    // arrays returned by Read are taken from shared pool
    internal class CompressedArrayBinaryConverter<TElement> {
        internal static CompressedArrayBinaryConverter<TElement> Instance =
            new CompressedArrayBinaryConverter<TElement>();

        private CompressedArrayBinaryConverter() { }

        public bool IsFixedSize => false;
        public int Size => 0;
#pragma warning disable 618
        public byte Version => 1;
        private static readonly int ItemSize = TypeHelper<TElement>.Size;
#pragma warning restore 618

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int SizeOf(TElement[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream) {
            if (ItemSize > 0) {
                var maxSize = 8 + 16 + ItemSize * valueCount;
                var buffer = ArrayPool<byte>.Shared.Rent(maxSize);
                fixed (byte* ptr = &buffer[0]) {
                    var directBuffer = new DirectBuffer(maxSize, ptr);
                    var totalSize = Write(value, valueOffset, valueCount, ref directBuffer);
                    temporaryStream = new RentedMemoryStream(buffer, 0, totalSize);
                    return totalSize;
                }
            } else {
                // compress bytes array
                MemoryStream tempStream;
                var bytesSize = BinarySerializer.SizeOf(new ArraySegment<TElement>(value, valueOffset, valueCount), out tempStream);
                var buffer = ArrayPool<byte>.Shared.Rent(bytesSize);
                var writtenBytes = BinarySerializer.Write(new ArraySegment<TElement>(value, valueOffset, valueCount), buffer, 0, tempStream);
                tempStream?.Dispose();
                Debug.Assert(bytesSize == writtenBytes);
                var size = CompressedArrayBinaryConverter<byte>.Instance.SizeOf(buffer, 0, writtenBytes, out temporaryStream);
                ArrayPool<byte>.Shared.Return(buffer);
                return size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(ArraySegment<TElement> segment, ref DirectBuffer destination,
            uint destinationOffset = 0u, MemoryStream temporaryStream = null) {
            return Write(segment.Array, segment.Offset, segment.Count, ref destination, destinationOffset,
                temporaryStream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Write(TElement[] value, int valueOffset, int valueCount, ref DirectBuffer destination, uint destinationOffset = 0u, MemoryStream temporaryStream = null) {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (temporaryStream != null) {
                var len = temporaryStream.Length;
                if (destination.Length < destinationOffset + len) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                temporaryStream.WriteToPtr(destination.Data + (int)destinationOffset);
                temporaryStream.Dispose();
                return checked((int)len);
            }

            var position = 8;
            if (valueCount > 0) {
                int compressedSize;
                if (ItemSize > 0) {
                    if (typeof(TElement) == typeof(DateTime)) {
                        var buffer = ArrayPool<byte>.Shared.Rent(valueCount * 8);
                        var dtArray = (DateTime[])(object)value;
                        fixed (byte* srcPtr = &buffer[0]) {
                            for (var i = 0; i < valueCount; i++) {
                                *(DateTime*)(srcPtr + i * 8) = dtArray[i + valueOffset];
                            }
                            compressedSize = BloscMethods.blosc_compress_ctx(
                            new IntPtr(9), // max compression
                            new IntPtr(1), // do shuffle
                            new UIntPtr((uint)ItemSize), // type size
                            new UIntPtr((uint)(valueCount * ItemSize)), // number of input bytes
                            (IntPtr)srcPtr,
                            destination.Data + position, // destination
                            new UIntPtr((uint)(destination.Length - position)), // destination length
                            "lz4", // hardcoded, do not change
                            new UIntPtr((uint)0), // default block size
                            BloscMethods.ProcessorCount // 
                            );
                        }
                        ArrayPool<byte>.Shared.Return(buffer);
                    } else {
                        var pinnedArray = GCHandle.Alloc(value, GCHandleType.Pinned);
                        var srcPtr = Marshal.UnsafeAddrOfPinnedArrayElement(value, valueOffset);
                        compressedSize = BloscMethods.blosc_compress_ctx(
                            new IntPtr(9), // max compression
                            new IntPtr(1), // do shuffle
                            new UIntPtr((uint)ItemSize), // type size
                            new UIntPtr((uint)(valueCount * ItemSize)), // number of input bytes
                            srcPtr,
                            destination.Data + position, // destination
                            new UIntPtr((uint)(destination.Length - position)), // destination length
                            "lz4", // hardcoded, do not change
                            new UIntPtr((uint)0), // default block size
                            BloscMethods.ProcessorCount // 
                            );
                        pinnedArray.Free();
                    }
                } else {
                    MemoryStream tempStream;
                    var bytesSize = BinarySerializer.SizeOf(new ArraySegment<TElement>(value, valueOffset, valueCount), out tempStream);
                    var buffer = ArrayPool<byte>.Shared.Rent(bytesSize);
                    var writtenBytes = BinarySerializer.Write(new ArraySegment<TElement>(value, valueOffset, valueCount), buffer, 0, tempStream);
                    tempStream?.Dispose();
                    Debug.Assert(bytesSize == writtenBytes);
                    compressedSize = CompressedArrayBinaryConverter<byte>.Instance.Write(buffer, 0, writtenBytes, ref destination, destinationOffset);
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (compressedSize > 0) {
                    position += compressedSize;
                } else {
                    return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                }
            }

            // length
            destination.WriteInt32(0, position); // include all headers
            // version
            destination.WriteByte(4, Version);
            return position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Read(IntPtr ptr, ref ArraySegment<TElement> value) {
            var totalSize = Marshal.ReadInt32(ptr);
            var version = Marshal.ReadByte(ptr + 4);
            if (version != Version) throw new NotSupportedException($"CompressedBinaryConverter work only with version {Version}");
            if (ItemSize <= 0) {
                // first decompress bytes
                var decompressedBytes = default(ArraySegment<byte>);
                var size = CompressedArrayBinaryConverter<byte>.Instance.Read(ptr, ref decompressedBytes);
                // NB the length is encoded in the header and is returned as a part of ArraySegment
                Debug.Assert(decompressedBytes.Count == BitConverter.ToInt32(decompressedBytes.Array, 0));
                // then deserialize
                TElement[] array = null;
                // NB the size of the array will be exact, the byte array has the correct length
                BinarySerializer.Read<TElement[]>(decompressedBytes.Array, ref array);
                value = new ArraySegment<TElement>(array, 0, array.Length);
                return size;
            } else {

                if (totalSize <= 8 + 16) {
                    value = new ArraySegment<TElement>(EmptyArray<TElement>.Instance, 0, 0);
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
                var array = ArrayPool<TElement>.Shared.Rent(arraySize);
                value = new ArraySegment<TElement>(array, 0, arraySize);
                if (arraySize > 0) {
                    if (typeof(TElement) == typeof(DateTime)) {
                        var buffer = ArrayPool<byte>.Shared.Rent(arraySize * 8);
                        var dtArray = new DateTime[arraySize];
                        fixed (byte* tgtPtr = &buffer[0]) {
                            var destination = (IntPtr)tgtPtr;
                            var decompSize = BloscMethods.blosc_decompress_ctx(
                                source, destination, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                            if (decompSize <= 0) throw new ArgumentException("Invalid compressed input");
                            Debug.Assert(decompSize == nbytes);
                            for (var i = 0; i < arraySize; i++) {
                                dtArray[i] = *(DateTime*)(destination + i * 8);
                            }
                        }
                        value = (ArraySegment<TElement>)(object)(new ArraySegment<DateTime>(dtArray, 0, arraySize));
                        ArrayPool<byte>.Shared.Return(buffer);
                    } else {
                        var pinnedArray = GCHandle.Alloc(value.Array, GCHandleType.Pinned);
                        var destination = pinnedArray.AddrOfPinnedObject();
                        int decompSize = 0;
                        try {
                            decompSize = BloscMethods.blosc_decompress_ctx(
                                source, destination, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                        } catch (Exception ex) {
                            Trace.WriteLine($"Blosc error: nbytes: {nbytes}, arr segment size: {value.Count}, \n\r exeption: {ex.Message + Environment.NewLine + ex.ToString()}");
                        }
                        if (decompSize <= 0) throw new ArgumentException("Invalid compressed input");
                        Debug.Assert(decompSize == nbytes);
                        pinnedArray.Free();
                    }
                } else {
                    // BufferPool returns an empty array
                }
                return totalSize;
            }
        }
    }
}