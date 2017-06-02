// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Blosc;
using Spreads.Buffers;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    // TODO check effect of [MethodImpl(MethodImplOptions.AggressiveInlining)] in a real context (and wether the methods are actually inlined).
    // The methods are huge here and deal with P/Invoke and do a lot of work, probably method calls
    // are irrelevant. Yet still to find the case where such attribute hurts.

    internal interface ICompressedArrayBinaryConverter<TArray>
    {
        bool IsFixedSize { get; }
        int Size { get; }
        byte Version { get; }

        int SizeOf(TArray value, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            CompressionMethod compression = CompressionMethod.DefaultOrNone);

        int Write(TArray value, int valueOffset, int valueCount, ref Buffer<byte> destination,
            uint destinationOffset = 0u, MemoryStream temporaryStream = null,
            CompressionMethod compression = CompressionMethod.DefaultOrNone);

        int Read(IntPtr ptr, out TArray array, out int count, bool exactSize = false);
    }

    /// <summary>
    /// Used for IArrayBasedMap serialization and for arrays serialization with forced compression
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    internal class CompressedArrayBinaryConverter<TElement> : ICompressedArrayBinaryConverter<TElement[]>
    {
        internal static CompressedArrayBinaryConverter<TElement> Instance =
            new CompressedArrayBinaryConverter<TElement>();

        private CompressedArrayBinaryConverter()
        {
        }

        public bool IsFixedSize => false;
        public int Size => 0;
#pragma warning disable 618
        public byte Version => 0;

        private static readonly int ItemSize = TypeHelper<TElement>.Size;
#pragma warning restore 618

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(TElement[] value, int valueOffset, int valueCount, out MemoryStream temporaryStream,
            CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            if (ItemSize > 0)
            {
                var maxSize = 8 + (16 + BloscMethods.ProcessorCount * 4) + ItemSize * valueCount;
                var ownedBuffer = BufferPool<byte>.RentOwnedBuffer(maxSize);
                ownedBuffer.AddReference();
                var buffer = ownedBuffer.Buffer;

                var totalSize = Write(value, valueOffset, valueCount, ref buffer, 0, null, compression);
                temporaryStream = new RentedBufferStream(ownedBuffer, totalSize);
                return totalSize;
            }
            else if (Buffers.BufferPool.IsPreservedBuffer<TElement>())
            {
                throw new NotImplementedException();
            }
            else
            {
                // compress bytes array
                MemoryStream tempStream;
                var segment = new ArraySegment<TElement>(value, valueOffset, valueCount);
                var bytesSize = BinarySerializer.SizeOf(segment, out tempStream, compression);
                var buffer = BufferPool<byte>.Rent(bytesSize);
                var writtenBytes = BinarySerializer.Write(segment, buffer, 0, tempStream);
                tempStream?.Dispose();
                Debug.Assert(bytesSize == writtenBytes);
                var size = CompressedArrayBinaryConverter<byte>.Instance.SizeOf(buffer, 0, writtenBytes, out temporaryStream, compression);
                BufferPool<byte>.Return(buffer);
                return size;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(ArraySegment<TElement> segment, ref Buffer<byte> destination,
            uint destinationOffset = 0u, MemoryStream temporaryStream = null, CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            return Write(segment.Array, segment.Offset, segment.Count, ref destination, destinationOffset,
                temporaryStream, compression);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Write(TElement[] value, int valueOffset, int valueCount, ref Buffer<byte> destination,
            uint destinationOffset = 0u, MemoryStream temporaryStream = null,
            CompressionMethod compression = CompressionMethod.DefaultOrNone)
        {
            // NB Blosc calls below are visually large - many LOCs with comments, but this is only a single method call
            if (value == null) throw new ArgumentNullException(nameof(value));

            var handle = destination.Pin();
            try
            {
                var ptr = (IntPtr)handle.PinnedPointer + (int)destinationOffset;

                if (temporaryStream != null)
                {
                    var len = temporaryStream.Length;
                    if (destination.Length < destinationOffset + len)
                    {
                        return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                    }
                    temporaryStream.WriteToPtr(ptr);
                    temporaryStream.Dispose();
                    return checked((int)len);
                }
                bool isDiffable = false;
                var compressionMethod = compression == CompressionMethod.DefaultOrNone
                    ? BloscSettings.defaultCompressionMethod
                    : (compression == CompressionMethod.LZ4 ? "lz4" : "zstd");

                var position = 8;
                if (valueCount > 0)
                {
                    int compressedSize;
                    if (ItemSize > 0)
                    {
                        if (typeof(TElement) == typeof(DateTime))
                        {
                            var buffer = BufferPool<byte>.Rent(valueCount * 8);
                            var dtArray = (DateTime[])(object)value;
                            fixed (byte* srcPtr = &buffer[0])
                            {
                                for (var i = 0; i < valueCount; i++)
                                {
                                    *(DateTime*)(srcPtr + i * 8) = dtArray[i + valueOffset];
                                }
                                compressedSize = BloscMethods.blosc_compress_ctx(
                                    new IntPtr(9), // max compression 9
                                    new IntPtr(1), // do byte shuffle 1
                                    new UIntPtr((uint)ItemSize), // type size
                                    new UIntPtr((uint)(valueCount * ItemSize)), // number of input bytes
                                    (IntPtr)srcPtr,
                                    ptr + position, // destination
                                    new UIntPtr((uint)(destination.Length - position)), // destination length
                                    compressionMethod,
                                    new UIntPtr((uint)0), // default block size
                                    BloscMethods.ProcessorCount //
                                );
                            }
                            BufferPool<byte>.Return(buffer);
                        }
                        else if (value[0] is IDiffable<TElement> diffableFirst)
                        {
                            isDiffable = true;
                            // TODO (!) this is inefficient because of interface calls, should use Spreads.Unsafe
                            // however, there is only a single pattern match with IDiffable boxing so allocations are not an issue here
                            var first = value[0];
                            var buffer = BufferPool<byte>.Rent(valueCount * ItemSize);

                            fixed (byte* srcPtr = &buffer[0])
                            {
                                Unsafe.Write(srcPtr, first);
                                for (var i = 1; i < valueCount; i++)
                                {
                                    var current = value[i];
                                    var diff = diffableFirst.GetDelta(current);
                                    Unsafe.Write(srcPtr + i * ItemSize, diff);
                                }
                                compressedSize = BloscMethods.blosc_compress_ctx(
                                    new IntPtr(9), // max compression 9
                                    new IntPtr(1), // do byte shuffle 1
                                    new UIntPtr((uint)ItemSize), // type size
                                    new UIntPtr((uint)(valueCount * ItemSize)), // number of input bytes
                                    (IntPtr)srcPtr,
                                    ptr + position, // destination
                                    new UIntPtr((uint)(destination.Length - position)), // destination length
                                    compressionMethod,
                                    new UIntPtr((uint)0), // default block size
                                    BloscMethods.ProcessorCount //
                                );
                            }
                            BufferPool<byte>.Return(buffer);
                        }
                        else
                        {
                            var pinnedArray = GCHandle.Alloc(value, GCHandleType.Pinned);
                            var srcPtr = Marshal.UnsafeAddrOfPinnedArrayElement(value, valueOffset);
                            compressedSize = BloscMethods.blosc_compress_ctx(
                                new IntPtr(9), // max compression 9
                                new IntPtr(1), // do byte shuffle 1
                                new UIntPtr((uint)ItemSize), // type size
                                new UIntPtr((uint)(valueCount * ItemSize)), // number of input bytes
                                srcPtr,
                                ptr + position, // destination
                                new UIntPtr((uint)(destination.Length - position)), // destination length
                                compressionMethod,
                                new UIntPtr((uint)0), // default block size
                                BloscMethods.ProcessorCount //
                            );
                            pinnedArray.Free();
                        }
                    }
                    else if (Buffers.BufferPool.IsPreservedBuffer<TElement>())
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        MemoryStream tempStream;
                        var bytesSize =
                            BinarySerializer.SizeOf(new ArraySegment<TElement>(value, valueOffset, valueCount),
                                out tempStream, compression);
                        var buffer = BufferPool<byte>.Rent(bytesSize);
                        var writtenBytes =
                            BinarySerializer.Write(new ArraySegment<TElement>(value, valueOffset, valueCount), buffer, 0,
                                tempStream);
                        tempStream?.Dispose();
                        Debug.Assert(bytesSize == writtenBytes);
                        compressedSize = CompressedArrayBinaryConverter<byte>.Instance.Write(buffer, 0, writtenBytes,
                            ref destination, destinationOffset, null, compression);
                        BufferPool<byte>.Return(buffer);
                    }

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
                Marshal.WriteInt32(ptr, position);
                // version & flags
                Marshal.WriteByte(ptr + 4, (byte)((Version << 4) | (isDiffable ? 0b0000_0011 : 0b0000_0001)));
                return position;
            }
            finally
            {
                handle.Free();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Read(IntPtr ptr, out TElement[] value, out int length, bool exactSize = false)
        {
            var totalSize = Marshal.ReadInt32(ptr);
            var versionFlag = Marshal.ReadByte(ptr + 4);
            var version = (byte)(versionFlag >> 4);
            var isDiffable = (versionFlag & 0b0000_0010) != 0;
            var isCompressed = (versionFlag & 0b0000_0001) != 0;
            if (!isCompressed) throw new InvalidOperationException("Wrong compressed flag. CompressedArrayBinaryConverter.Read works only with compressed methods.");

            if (version != Version) throw new NotSupportedException($"CompressedBinaryConverter work only with version {Version}");
            if (ItemSize <= 0)
            {
                // first decompress bytes
                var size = CompressedArrayBinaryConverter<byte>.Instance.Read(ptr, out byte[] decompressedBytes, out length);

                // NB the length is encoded in the header and is returned as a part of ArraySegment
                Debug.Assert(length == BitConverter.ToInt32(decompressedBytes, 0));

                // then deserialize
                // NB the size of the array will be exact, BinarySerializer.Read does not support non-exact buffers
                BinarySerializer.Read(decompressedBytes, out value);

                BufferPool<byte>.Return(decompressedBytes);
                return size;
            }
            else if (Buffers.BufferPool.IsPreservedBuffer<TElement>())
            {
                throw new NotImplementedException();
            }
            else
            {
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
                // when caller provides an empty AS, it could require exact size, e.g. DateTimeArrayBinaryConverter
                var array = BufferPool<TElement>.Rent(arraySize, exactSize);
                length = arraySize;
                value = array;

                if (arraySize > 0)
                {
                    if (typeof(TElement) == typeof(DateTime))
                    {
                        var buffer = BufferPool<byte>.Rent(arraySize * 8);
                        var dtArray = new DateTime[arraySize];
                        fixed (byte* tgtPtr = &buffer[0])
                        {
                            var destination = (IntPtr)tgtPtr;
                            var decompSize = BloscMethods.blosc_decompress_ctx(
                                source, destination, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                            if (decompSize <= 0) throw new ArgumentException("Invalid compressed input");
                            Debug.Assert(decompSize == nbytes);
                            for (var i = 0; i < arraySize; i++)
                            {
                                dtArray[i] = *(DateTime*)(destination + i * 8);
                            }
                        }
                        value = (TElement[])(object)(dtArray);
                        BufferPool<byte>.Return(buffer);
                    }
                    else if (isDiffable && typeof(IDiffable<TElement>).GetTypeInfo().IsAssignableFrom(typeof(TElement).GetTypeInfo()))
                    {
                        var buffer = BufferPool<byte>.Rent(arraySize * ItemSize);
                        var targetArray = BufferPool<TElement>.Rent(arraySize);

                        fixed (byte* tgtPtr = &buffer[0])
                        {
                            var destination = tgtPtr;
                            var decompSize = BloscMethods.blosc_decompress_ctx(
                                source, (IntPtr)destination, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                            if (decompSize <= 0) throw new ArgumentException("Invalid compressed input");
                            Debug.Assert(decompSize == nbytes);

                            var first = Unsafe.Read<TElement>(destination);
                            var diffableFirst = (IDiffable<TElement>)first;
                            targetArray[0] = first;
                            for (var i = 1; i < arraySize; i++)
                            {
                                var currentDelta = Unsafe.Read<TElement>(destination + i * ItemSize);
                                var current = diffableFirst.AddDelta(currentDelta);
                                targetArray[i] = current;
                            }
                        }
                        value = targetArray; // (<TElement>)(object)(new ArraySegment<TElement>(targetArray, 0, arraySize));
                        BufferPool<byte>.Return(buffer);
                    }
                    else
                    {
                        var pinnedArray = GCHandle.Alloc(value, GCHandleType.Pinned);
                        var destination = pinnedArray.AddrOfPinnedObject();

                        // TODO remove this try/catch and debugger stuff, it was used to catch an eror that disappeared after adding
                        // try/catch. Probably some reordering, maybe add a memory barrier before the call
                        try
                        {
                            var decompSize = BloscMethods.blosc_decompress_ctx(
                                source, destination, new UIntPtr((uint)nbytes), BloscMethods.ProcessorCount);
                            if (decompSize <= 0) throw new ArgumentException("Invalid compressed input");
                            Debug.Assert(decompSize == nbytes);
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
                        finally
                        {
                            pinnedArray.Free();
                        }
                    }
                }
                else
                {
                    // BufferPool returns an empty array
                }
                return totalSize;
            }
        }
    }
}