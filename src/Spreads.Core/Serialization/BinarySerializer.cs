// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// using Newtonsoft.Json;
using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using static System.Runtime.CompilerServices.Unsafe;

#pragma warning disable 0618

namespace Spreads.Serialization
{
    // 3 serialization cases: fixed binary, custom converter and json
    // compression is applied to the result of the later two

    /// <summary>
    /// Binary Serializer that tries to serialize objects to their blittable representation whenever possible
    /// and falls back to JSON.NET for non-blittable types. It supports versioning and custom binary converters.
    /// </summary>
    public static unsafe partial class BinarySerializer
    {
        /// <summary>
        /// Positive number for fixed-size types, negative for all other types. Zero is invalid.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FixedSize<T>()
        {
            return TypeHelper<T>.FixedSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfRaw<T>(T value, out ArraySegment<byte> temporaryBuffer,
            bool isBinary = false)
        {
            return SizeOfRaw(in value, out temporaryBuffer, isBinary);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SizeOfRaw<T>(in T value, out ArraySegment<byte> temporaryBuffer, bool isBinary = false)
        {
            temporaryBuffer = default;

            if (isBinary)
            {
                // fixed size binary is never compressed
                if (TypeHelper<T>.IsFixedSize)
                {
                    return TypeHelper<T>.FixedSize;
                }

                var bc = TypeHelper<T>.BinaryConverter;
                if (bc != null)
                {
                    return bc.SizeOf(value, out temporaryBuffer);
                }
            }

            return JsonBinaryConverter<T>.SizeOf(value, out temporaryBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T value, out ArraySegment<byte> temporaryBuffer,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            return SizeOf(in value, out temporaryBuffer, format, timestamp);
        }

        /// <summary>
        /// With header + len + optional TS.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SizeOf<T>(in T value, out ArraySegment<byte> temporaryBuffer,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            var hasTs = (long)timestamp != default;
            var tsSize = *(int*)(&hasTs) << 3;

            var isBinary = ((int)format & 1) == 1;

            // fixed size binary is never compressed
            if (TypeHelper<T>.IsFixedSize)
            {
                if (isBinary)
                {
                    temporaryBuffer = default;
                    return DataTypeHeader.Size + tsSize + TypeHelper<T>.FixedSize; // no len
                }
            }

            return SizeOfVarSize(in value, out temporaryBuffer, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int SizeOfVarSize<T>(in T value, out ArraySegment<byte> temporaryBuffer,
            SerializationFormat format, Timestamp timestamp)
        {
            temporaryBuffer = default;
            var isBinary = ((int)format & 1) == 1;
            var hasTs = (long)timestamp != default;
            var tsSize = *(int*)(&hasTs) << 3;

            var rawSize = SizeOfRaw(in value, out var rawTemporaryBuffer, isBinary);

            if (rawSize <= 0)
            {
                return rawSize;
            }

            var compressionMethod = format.CompressionMethod();

            if (rawTemporaryBuffer.Count == 0 && compressionMethod == CompressionMethod.None)
            {
                // we know size without performing serialization yet and do not need compression
                return DataTypeHeader.Size + 4 + tsSize + rawSize;
            }

            // now we need to form temporaryBuffer that is ready for copying to a final destination

            IBinaryConverter<T> bc = null;

            // first reuse the raw buffer or create one in rare case it is empty
            var rawOffset = 16;
            byte[] tmpArray;
            MemoryHandle pin;
            DirectBuffer tmpDestination;

            if (rawTemporaryBuffer.Count == 0)
            {
                bc = TypeHelper<T>.BinaryConverter;
                ThrowHelper.AssertFailFast(bc != null, "TypeHelper<T>.BinaryConverter != null, in other cases raw temp buffer should be present");
                rawOffset = DataTypeHeader.Size + 4 + tsSize;
                tmpArray = BufferPool<byte>.Rent(rawOffset + rawSize);
                pin = ((Memory<byte>)tmpArray).Pin();
                tmpDestination = new DirectBuffer(tmpArray.Length, (byte*)pin.Pointer);
                var sl = tmpDestination.Slice(rawOffset);
                // ReSharper disable once PossibleNullReferenceException
                bc.Write(value, ref sl);
                // now tmpArray is the same as if if was returned from SizeOfNoHeader
            }
            else
            {
                ThrowHelper.AssertFailFast(rawTemporaryBuffer.Offset == rawOffset, "rawTemporaryBuffer.Offset == 16");
                rawOffset = rawTemporaryBuffer.Offset;
                tmpArray = rawTemporaryBuffer.Array;
                pin = ((Memory<byte>)tmpArray).Pin();
                // ReSharper disable once PossibleNullReferenceException
                tmpDestination = new DirectBuffer(tmpArray.Length, (byte*)pin.Pointer);
            }

            if (bc == null)
            {
                bc = JsonBinaryConverter<T>.Instance;
            }

            var header = TypeHelper<T>.DefaultBinaryHeader;
            header.VersionAndFlags.ConverterVersion = bc.ConverterVersion;

            // NB first step is to serialize, compressor could chose to copy (if small data or compressed is larger)
            // not! header.VersionAndFlags.CompressionMethod = compressionMethod;

            // NB binary is best effort
            header.VersionAndFlags.IsBinary = bc != JsonBinaryConverter<T>.Instance; // not from format but what we are actually using now

            var firstOffset = rawOffset - tsSize - 4 - DataTypeHeader.Size;

            if (hasTs)
            {
                header.VersionAndFlags.IsTimestamped = hasTs;
                tmpDestination.Write(firstOffset + DataTypeHeader.Size + 4, timestamp);
            }

            // NB: after header.VersionAndFlags.IsTimestamped = hasTs;
            tmpDestination.Write(firstOffset, header);

            var payloadLength = tsSize + rawSize;

            tmpDestination.Write(firstOffset + DataTypeHeader.Size, payloadLength);

            var totalLength = DataTypeHeader.Size + 4 + payloadLength;

            var uncompressedBufferWithHeader =
                tmpDestination.Slice(firstOffset, totalLength);
            if (compressionMethod == CompressionMethod.None)
            {
                pin.Dispose();
                temporaryBuffer = new ArraySegment<byte>(tmpArray, firstOffset, totalLength);
                return totalLength;
            }

            byte[] tmpArray2;
            MemoryHandle pin2;
            tmpArray2 = BufferPool<byte>.Rent(checked((int)(uint)uncompressedBufferWithHeader.Length));
            pin2 = ((Memory<byte>)tmpArray2).Pin();
            var destination2 = new DirectBuffer(tmpArray2.Length, (byte*)pin2.Pointer).Slice(0, uncompressedBufferWithHeader.Length);

            var compressedSize = CompressWithHeader(uncompressedBufferWithHeader, destination2, compressionMethod);

            pin.Dispose();
            pin2.Dispose();

            BufferPool<byte>.Return(tmpArray);

            if (compressedSize > 0)
            {
                temporaryBuffer = new ArraySegment<byte>(tmpArray2, 0, compressedSize);
                return compressedSize;
            }

            BufferPool<byte>.Return(tmpArray2);
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Write<T>(in T value, ref DirectBuffer pinnedDestination,
            ArraySegment<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            var isBinary = ((int)format & 1) == 1;

            IBinaryConverter<T> bc = null;

            // if fixed & binary just write
            if (isBinary)
            {
                // fixed size binary is never compressed
                if (TypeHelper<T>.IsFixedSize)
                {
                    return TypeHelper<T>.WriteWithHeader(in value, pinnedDestination, timestamp);
                }
                // Use custom TypeHelper<T>.BinaryConverter only when asked for isBinary
                bc = TypeHelper<T>.BinaryConverter ?? JsonBinaryConverter<T>.Instance;
            }

            return WriteVarSize(value, ref pinnedDestination, temporaryBuffer, format, timestamp, bc);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteVarSize<T>(T value, ref DirectBuffer pinnedDestination, ArraySegment<byte> temporaryBuffer, SerializationFormat format,
            Timestamp timestamp, IBinaryConverter<T> bc)
        {
            var hasTs = (long)timestamp != default;
            var len = temporaryBuffer.Count;
            if (len > 0)
            {
                // temporaryBuffer must be already fully packed according to format & timestamp,
                // it is what SizeOf with format & timestamp overload returns.
                if (Settings.AdditionalCorrectnessChecks.Enabled)
                {
                    // these are non-recoverable errors indicating wrong code using the methods

                    // ReSharper disable once PossibleNullReferenceException
                    // var firstByte = temporaryBuffer.Array[temporaryBuffer.Offset];
                    //var versionFlags = *(VersionAndFlags*)(&firstByte);
                    //ThrowHelper.AssertFailFast(format == versionFlags.SerializationFormat
                    //                           && !(versionFlags.IsTimestamped ^ hasTs), "Wrong SerializationFormat in temporaryBuffer");

                    ThrowHelper.AssertFailFast(len <= pinnedDestination.Length, "len > pinnedDestination.Length");

                    ThrowHelper.AssertFailFast(len > DataTypeHeader.Size + 4, "Small temporaryBuffer");

                    // TODO payload size check
                }

                ((Span<byte>)temporaryBuffer).CopyTo(pinnedDestination.Span);
                BufferPool<byte>.Return(temporaryBuffer.Array);
                return len;
            }

            if (bc == null)
            {
                bc = JsonBinaryConverter<T>.Instance;
            }

            var compressionMethod = format.CompressionMethod();

            var header = TypeHelper<T>.DefaultBinaryHeader;
            header.VersionAndFlags.ConverterVersion = bc.ConverterVersion;

            // NB first step is to serialize, compressor could chose to copy (if small data or compressed is larger)
            // not! header.VersionAndFlags.CompressionMethod = compressionMethod;

            // NB binary is best effort
            header.VersionAndFlags.IsBinary = bc != JsonBinaryConverter<T>.Instance; // not from format but what we are actually using now

            byte[] tmpArray = null;
            MemoryHandle pin = default;
            try
            {
                DirectBuffer destination2;
                if (compressionMethod != CompressionMethod.None)
                {
                    tmpArray = BufferPool<byte>.Rent(checked((int)(uint)pinnedDestination.Length));
                    pin = ((Memory<byte>)tmpArray).Pin();
                    destination2 = new DirectBuffer(tmpArray.Length, (byte*)pin.Pointer).Slice(0, pinnedDestination.Length);
                }
                else
                {
                    destination2 = pinnedDestination;
                }

                var pos = 0;

                // IBinaryConverter cannot compress and write timestamp, but avoid copy if we do not need this

                pos += DataTypeHeader.Size + 4; // + payload length
                var tsSize = 0;
                if (hasTs)
                {
                    tsSize = 8;
                    header.VersionAndFlags.IsTimestamped = true;
                    destination2.Write(pos, timestamp);
                    pos += 8;
                }

                // NB: after header.VersionAndFlags.IsTimestamped = true;
                destination2.Write(0, header);
                var sl = destination2.Slice(pos);
                var rawPayloadLength = bc.Write(value, ref sl);

                destination2.Write(DataTypeHeader.Size, tsSize + rawPayloadLength);

                if (compressionMethod == CompressionMethod.None)
                {
                    return DataTypeHeader.Size + 4 + tsSize + rawPayloadLength;
                }

                return CompressWithHeader(destination2, pinnedDestination, compressionMethod);
            }
            finally
            {
                if (tmpArray != null)
                {
                    BufferPool<byte>.Return(tmpArray);
                    pin.Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Write<T>(in T value, Memory<byte> buffer,
            ArraySegment<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            using (var handle = buffer.Pin())
            {
                var db = new DirectBuffer(buffer.Length, (byte*)handle.Pointer);
                return Write(in value, ref db, temporaryBuffer, format, timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ref DirectBuffer source, out T value)
        {
            return Read(ref source, out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ref DirectBuffer source, out T value, out Timestamp timestamp)
        {
            var header = source.Read<DataTypeHeader>(0);

            if (header.VersionAndFlags.IsBinary && header.IsFixedSize) // IsFixedSize not possible with compression
            {
                if (TypeHelper<T>.FixedSize < 0 || DataTypeHeader.Size + TypeHelper<T>.FixedSize < source.Length)
                {
                    value = default;
                    timestamp = default;
                    return -1;
                }
                Debug.Assert(TypeHelper<T>.FixedSize >= 0);
                return TypeHelper<T>.ReadWithHeader(source.Data, out value, out timestamp);
            }

            return ReadSlow<T>(ref source, out value, out timestamp);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadSlow<T>(ref DirectBuffer source, out T value, out Timestamp timestamp)
        {
            var header = source.Read<DataTypeHeader>(0);

            IBinaryConverter<T> bc = null;

            if (header.VersionAndFlags.IsBinary)
            {
                bc = TypeHelper<T>.BinaryConverter;
            }

            var tsSize = header.VersionAndFlags.IsTimestamped ? Timestamp.Size : 0;
            var offset = DataTypeHeader.Size + 4 + tsSize;

            timestamp = tsSize > 0 ? source.Read<Timestamp>(DataTypeHeader.Size + 4) : default;

            var payloadSize = source.Read<int>(DataTypeHeader.Size);

            var calculatedSourceSize = DataTypeHeader.Size + 4 + payloadSize;

            if (payloadSize < 0 || calculatedSourceSize > source.Length)
            {
                goto INVALID_RETURN;
            }

            // this is how much we read from source, we return this value
            var readSize = calculatedSourceSize;

            byte[] tmpArray = null;
            MemoryHandle pin = default;
            try
            {
                if (header.VersionAndFlags.CompressionMethod != CompressionMethod.None)
                {
                    var uncompressedBufferSize = offset + source.Read<int>(offset);
                    tmpArray = BufferPool<byte>.Rent(uncompressedBufferSize);
                    pin = ((Memory<byte>)tmpArray).Pin();
                    var sourceUncompressed = new DirectBuffer(tmpArray.Length, (byte*)pin.Pointer).Slice(0, uncompressedBufferSize);
                    // we must return readSize = this is how many bytes we have read from original source
                    readSize = DecompressWithHeader(source, sourceUncompressed);
                    // use sourceUncompressed as if there was no compression
                    source = sourceUncompressed;
                }

                if (bc == null)
                {
                    bc = JsonBinaryConverter<T>.Instance;
                }
                var slice = source.Slice(offset, readSize - offset);
                var readSize1 = offset + bc.Read(ref slice, out value);
                if (readSize > 0 && readSize1 != readSize)
                {
                    goto INVALID_RETURN;
                }
                return calculatedSourceSize;
            }
            finally
            {
                if (tmpArray != null)
                {
                    BufferPool<byte>.Return(tmpArray);
                    pin.Dispose();
                }
            }

            INVALID_RETURN:
            value = default;
            timestamp = default;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ReadOnlyMemory<byte> buffer, out T value)
        {
            return Read(buffer, out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ReadOnlyMemory<byte> buffer, out T value, out Timestamp timestamp)
        {
            using (var handle = buffer.Pin())
            {
                var db = new DirectBuffer(buffer.Length, (byte*)handle.Pointer);
                return Read(ref db, out value, out timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(Stream stream, out T value, out Timestamp timestamp)
        {
            if (stream is RecyclableMemoryStream rms && rms.IsSingleChunk)
            {
                return Read(rms.SingleChunk, out value, out timestamp);
            }

            try
            {
                var len = checked((int)stream.Length);
                rms = RecyclableMemoryStreamManager.Default.GetStream(null, len, true);

                try
                {
                    if (!rms.IsSingleChunk)
                    {
                        ThrowHelper.ThrowInvalidOperationException(
                            "RMS GetStream(null, len, true) must return single chunk");
                    }

                    stream.CopyTo(rms);
                    return Read(rms.SingleChunk, out value, out timestamp);
                }
                finally
                {
                    rms.Dispose();
                }
            }
            catch (NotSupportedException)
            {
                rms = RecyclableMemoryStreamManager.Default.GetStream();
                try
                {
                    stream.CopyTo(rms);
                    return Read(rms, out value, out timestamp);
                }
                finally
                {
                    rms.Dispose();
                }
            }
        }

        #region Experiments

        // branchless reads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TsSize(Timestamp ts)
        {
            var isNonDefault = (long)ts != default;
            var size = (*(byte*)&isNonDefault << 3); // 1 << 3 = 8 or zero
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Timestamp ReadTimestamp(byte* ptr, out int payloadOffset)
        {
            var isVarSize = *(ptr + 2) == 0;
            var offset = DataTypeHeader.Size + (*(byte*)&isVarSize << 2); // 4 for varsize or 0 for fixed size
            long tsLen = VersionAndFlags.TimestampFlagMask & *ptr;

            var tsMask = ~((tsLen >> 3) - 1); // all 1s or 0s

            // the only requirment if ptr + offset + 8 not causing segfault.
            var timestamp = (Timestamp)(tsMask & ReadUnaligned<long>(ptr + offset));

            payloadOffset = offset + (int)tsLen;
            return timestamp;
        }

        [ThreadStatic]
        private static decimal placeHolder;

        [ThreadStatic]
        private static void* sharedPtr = BufferPool.StaticBufferMemory.Pointer;

        [Obsolete]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Timestamp ReadTimestamp2(byte* ptr, out int payloadOffset)
        {
            var x = AsPointer(ref placeHolder);
            // store pointers in the first 16 bytes of the shared thread-static buffer
            //var bptr = default(decimal);
            //var sharedPtr = (void*)&bptr;
            //if (sharedPtr == (void*) IntPtr.Zero)
            //{
            //    sharedPtr = BufferPool.StaticBufferMemory.Pointer;
            //}
            var ptrXX = (byte**)x; // (byte**)sharedPtr;
            long ts = default;
            // ()ptrXX = &ts

            // var ptrX = stackalloc Timestamp*[2];
            // we need ANY pointer that does not segfault on read
            *ptrXX = (byte*)(x) + 8; // (byte*)&ts;

            var isVarSize = *(ptr + 2) == 0;
            var offset = 4 << *(int*)(byte*)&isVarSize; // + (*(byte*)&isVarSize << 2); // 4 for varsize or 0 for fixed size
            long tsLen = VersionAndFlags.TimestampFlagMask & *ptr;

            *(ptrXX + 8) = (byte*)(ptr + offset);

            var hasTs = tsLen;
            var ptrToDeref = *(ptrXX + hasTs);
            ts = Volatile.Read(ref *(long*)(ptrToDeref));

            payloadOffset = offset + (int)tsLen;
            return (Timestamp)ts;
        }

        #endregion Experiments
    }

#pragma warning restore 0618
}
