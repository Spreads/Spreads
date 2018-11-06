// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

#pragma warning disable 0618

namespace Spreads.Serialization
{
    public static unsafe partial class BinarySerializer
    {
        // Binary converter padding
        // ReSharper disable once InconsistentNaming
        internal const int BC_PADDING = 16;

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
        public static int SizeOfRaw<T>(T value, out RetainedMemory<byte> temporaryBuffer, out bool withPadding,
            bool isBinary = false)
        {
            return SizeOfRaw(in value, out temporaryBuffer, out withPadding, isBinary);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SizeOfRaw<T>(in T value, out RetainedMemory<byte> temporaryBuffer, out bool withPadding, bool isBinary = false)
        {
            temporaryBuffer = default;
            withPadding = false;
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
                    return bc.SizeOf(value, out temporaryBuffer, out withPadding);
                }
            }

            return JsonBinaryConverter<T>.SizeOf(value, out temporaryBuffer, out withPadding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(T value, out RetainedMemory<byte> temporaryBuffer,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            return SizeOf(in value, out temporaryBuffer, format, timestamp);
        }

        /// <summary>
        /// With header + len + optional TS.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SizeOf<T>(in T value, out RetainedMemory<byte> temporaryBuffer,
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
        private static int SizeOfVarSize<T>(in T value, out RetainedMemory<byte> temporaryBuffer,
            SerializationFormat format, Timestamp timestamp)
        {
            temporaryBuffer = default;
            var isBinary = ((int)format & 1) == 1;
            var hasTs = (long)timestamp != default;
            var tsSize = *(int*)(&hasTs) << 3;

            var rawSize = SizeOfRaw(in value, out var rawTemporaryBuffer, out var withPadding, isBinary);

            if (rawSize <= 0)
            {
                return rawSize;
            }

            if (Settings.AdditionalCorrectnessChecks.Enabled)
            {
                if (!rawTemporaryBuffer.IsEmpty &&
                    rawSize != rawTemporaryBuffer.Length - (withPadding ? BC_PADDING : 0))
                {
                    ThrowHelper.FailFast("Wrong raw size");
                }
            }

            var compressionMethod = format.CompressionMethod();

            // first check because there is no header in the buffer from SizeOfRaw, only empty is OK
            if (rawTemporaryBuffer.IsEmpty && compressionMethod == CompressionMethod.None)
            {
                // we know size without performing serialization yet and do not need compression
                return DataTypeHeader.Size + 4 + tsSize + rawSize;
            }

            // now we need to form temporaryBuffer that is ready for copying to a final destination

            IBinaryConverter<T> bc = null;

            // reuse the raw buffer or create one in case it is empty or without padding.
            var rawOffset = BC_PADDING;
            RetainedMemory<byte> tmpArray;
            DirectBuffer tmpDestination;

            if (rawTemporaryBuffer.IsEmpty) // requested compression, empty is possible only when TypeHelper<T>.BinaryConverter != null
            {
                bc = TypeHelper<T>.BinaryConverter;
                ThrowHelper.AssertFailFast(bc != null, "TypeHelper<T>.BinaryConverter != null, in other cases raw temp buffer should be present");
                rawOffset = DataTypeHeader.Size + 4 + tsSize;
                tmpArray = BufferPool.RetainTemp(rawOffset + rawSize);
                tmpDestination = new DirectBuffer(tmpArray.Length, (byte*)tmpArray.Pointer);
                var slice = tmpDestination.Slice(rawOffset);
                // ReSharper disable once PossibleNullReferenceException
                bc.Write(value, ref slice);
                // now tmpArray is the same as if if was returned from SizeOfNoHeader
            }
            else
            {
                if (withPadding)
                {
                    Debug.Assert(rawOffset == BC_PADDING);
                    tmpArray = rawTemporaryBuffer;
                    tmpDestination = new DirectBuffer(tmpArray.Length, (byte*)tmpArray.Pointer);
                }
                else
                {
                    rawOffset = DataTypeHeader.Size + 4 + tsSize;
                    tmpArray = BufferPool.RetainTemp(rawOffset + rawSize);
                    tmpDestination = new DirectBuffer(tmpArray.Length, (byte*)tmpArray.Pointer);
                    var slice = tmpDestination.Slice(rawOffset);
                    rawTemporaryBuffer.Span.CopyTo(slice.Span);
                }
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

            if (compressionMethod == CompressionMethod.None)
            {
                tmpArray = tmpArray.Slice(firstOffset, tmpArray.Length - firstOffset);
                temporaryBuffer = tmpArray;
                return totalLength;
            }

            var uncompressedBufferWithHeader = tmpDestination.Slice(firstOffset, totalLength);

            var tmpArray2 = BufferPool.RetainTemp(checked((int)(uint)uncompressedBufferWithHeader.Length));
            var destination2 = new DirectBuffer(tmpArray2.Length, (byte*)tmpArray2.Pointer).Slice(0, uncompressedBufferWithHeader.Length);

            var compressedSize = CompressWithHeader(uncompressedBufferWithHeader, destination2, compressionMethod);

            tmpArray.Dispose();

            if (compressedSize > 0)
            {
                tmpArray2 = tmpArray2.Slice(0, compressedSize);
                temporaryBuffer = tmpArray2;
                return compressedSize;
            }

            tmpArray2.Dispose();
            return -1;
        }

#pragma warning disable EPS05 // Use in-modifier for a readonly struct: public method should support older C# versions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, ref DirectBuffer pinnedDestination,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
#pragma warning restore EPS05 // Use in-modifier for a readonly struct
        {
            return Write(in value, ref pinnedDestination, in temporaryBuffer, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Write<T>(in T value, ref DirectBuffer pinnedDestination,
            in RetainedMemory<byte> temporaryBuffer = default,
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
                    Debug.Assert(temporaryBuffer.IsEmpty);
                    return TypeHelper<T>.WriteWithHeader(in value, pinnedDestination, timestamp);
                }
                // Use custom TypeHelper<T>.BinaryConverter only when asked for isBinary
                bc = TypeHelper<T>.BinaryConverter ?? JsonBinaryConverter<T>.Instance;
            }

            return WriteVarSize(value, ref pinnedDestination, in temporaryBuffer, format, timestamp, bc);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteVarSize<T>(T value,
            ref DirectBuffer pinnedDestination,
            in RetainedMemory<byte> temporaryBuffer,
            SerializationFormat format,
            Timestamp timestamp, IBinaryConverter<T> bc)
        {
            RetainedMemory<byte> rm = temporaryBuffer;
            try
            {
                var hasTs = (long)timestamp != default;
                var sizeOf = 0;
                if (rm.IsEmpty)
                {
                    rm.Dispose();
                    sizeOf = SizeOf(in value, out rm, format, timestamp);
                    if (!rm.IsEmpty && sizeOf != rm.Length)
                    {
                        rm.Dispose();
                        return int.MinValue;
                    }
                }

                if (!rm.IsEmpty)
                {
                    sizeOf = rm.Length;
                    if (sizeOf > pinnedDestination.Length || sizeOf < DataTypeHeader.Size + 4)
                    {
                        // kind of error code with info
                        sizeOf = -sizeOf;
                    }
                    else
                    {
                        rm.Span.CopyTo(pinnedDestination.Span);
                        // in finally: rm.Dispose();
                    }
                    return sizeOf;
                }

                var compressionMethod = format.CompressionMethod();
                if (compressionMethod != CompressionMethod.None)
                {
                    ThrowHelper.FailFast("Compressed format must return non-empty temporaryBuffer");
                }

                if (sizeOf < DataTypeHeader.Size + 4)
                {
                    return int.MinValue + 2;
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
                header.VersionAndFlags.IsBinary =
                    bc != JsonBinaryConverter<T>.Instance; // not from format but what we are actually using now

                if (sizeOf > pinnedDestination.Length)
                {
                    return -sizeOf;
                }

                var pos = 0;

                pos += DataTypeHeader.Size + 4; // + payload length
                var tsSize = 0;
                if (hasTs)
                {
                    tsSize = 8;
                    header.VersionAndFlags.IsTimestamped = true;
                    pinnedDestination.Write(pos, timestamp);
                    pos += 8;
                }

                // NB: after header.VersionAndFlags.IsTimestamped = true;
                pinnedDestination.Write(0, header);
                var tmpDestinationSlice = pinnedDestination.Slice(pos);

                // TODO protect from bad BC impl by default, it could write through the end of destination
                var written = bc.Write(value, ref tmpDestinationSlice);

                if (pos + written != sizeOf)
                {
                    ThrowHelper.FailFast($"Wrong binary converter: sizeOf {sizeOf} != written {written}");
                }

                pinnedDestination.Write(DataTypeHeader.Size, tsSize + written);

                return DataTypeHeader.Size + 4 + tsSize + written;
            }
            finally
            {
                rm.Dispose();
            }
        }

#pragma warning disable EPS05 // Use in-modifier for a readonly struct

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value, Memory<byte> buffer,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
#pragma warning restore EPS05 // Use in-modifier for a readonly struct
        {
            return Write(in value, buffer, temporaryBuffer, format, timestamp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Write<T>(in T value, Memory<byte> buffer,
            in RetainedMemory<byte> temporaryBuffer,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            using (var handle = buffer.Pin())
            {
                var db = new DirectBuffer(buffer.Length, (byte*)handle.Pointer);
                return Write(in value, ref db, in temporaryBuffer, format, timestamp);
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

            if (header.VersionAndFlags.IsBinary && header.IsTypeFixedSize) // IsFixedSize not possible with compression
            {
                if (TypeHelper<T>.FixedSize < 0 || DataTypeHeader.Size + TypeHelper<T>.FixedSize > source.Length)
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

        //// branchless reads

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal static int TsSize(Timestamp ts)
        //{
        //    var isNonDefault = (long)ts != default;
        //    var size = (*(byte*)&isNonDefault << 3); // 1 << 3 = 8 or zero
        //    return size;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal static Timestamp ReadTimestamp(byte* ptr, out int payloadOffset)
        //{
        //    var isVarSize = *(ptr + 2) == 0;
        //    var offset = DataTypeHeader.Size + (*(byte*)&isVarSize << 2); // 4 for varsize or 0 for fixed size
        //    long tsLen = VersionAndFlags.TimestampFlagMask & *ptr;

        //    var tsMask = ~((tsLen >> 3) - 1); // all 1s or 0s

        //    // the only requirment if ptr + offset + 8 not causing segfault.
        //    var timestamp = (Timestamp)(tsMask & ReadUnaligned<long>(ptr + offset));

        //    payloadOffset = offset + (int)tsLen;
        //    return timestamp;
        //}

        //[ThreadStatic]
        //private static decimal placeHolder;

        //[Obsolete]
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal static Timestamp ReadTimestamp2(byte* ptr, out int payloadOffset)
        //{
        //    var x = AsPointer(ref placeHolder);
        //    // store pointers in the first 16 bytes of the shared thread-static buffer
        //    //var bptr = default(decimal);
        //    //var sharedPtr = (void*)&bptr;
        //    //if (sharedPtr == (void*) IntPtr.Zero)
        //    //{
        //    //    sharedPtr = BufferPool.StaticBufferMemory.Pointer;
        //    //}
        //    var ptrXX = (byte**)x; // (byte**)sharedPtr;
        //    long ts = default;
        //    // ()ptrXX = &ts

        //    // var ptrX = stackalloc Timestamp*[2];
        //    // we need ANY pointer that does not segfault on read
        //    *ptrXX = (byte*)(x) + 8; // (byte*)&ts;

        //    var isVarSize = *(ptr + 2) == 0;
        //    var offset = 4 << *(int*)(byte*)&isVarSize; // + (*(byte*)&isVarSize << 2); // 4 for varsize or 0 for fixed size
        //    long tsLen = VersionAndFlags.TimestampFlagMask & *ptr;

        //    *(ptrXX + 8) = (byte*)(ptr + offset);

        //    var hasTs = tsLen;
        //    var ptrToDeref = *(ptrXX + hasTs);
        //    ts = Volatile.Read(ref *(long*)(ptrToDeref));

        //    payloadOffset = offset + (int)tsLen;
        //    return (Timestamp)ts;
        //}

        #endregion Experiments
    }

#pragma warning restore 0618
}
