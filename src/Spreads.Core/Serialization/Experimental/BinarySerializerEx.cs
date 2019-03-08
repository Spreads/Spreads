// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization.Experimental
{
    public static unsafe partial class BinarySerializerEx
    {
        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Binary serializer padding
        /// </summary>
        internal const int HEADER_PADDING = 16;

        /// <summary>
        /// Returns a positive value for fixed serialized size of the type <typeparamref name="T"/>
        /// or a meaningless negative value for variable size type. Zero value is invalid.
        /// </summary>
        [Obsolete("remove this attribute, just do not use internally")] // TODO
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FixedSize<T>()
        {
            return TypeEnumHelper<T>.FixedSize;
        }

        #region SizeOf

        /// <summary>
        /// Returns the size of serialized value payload.
        /// When serialized payload length is only known after serialization, which is often the case for non-fixed size type,
        /// this method must serialize the value into <paramref name="temporaryBuffer"/>.
        /// The <paramref name="temporaryBuffer"/> <see cref="RetainedMemory{T}"/> could be taken from
        /// <see cref="BufferPool.Retain"/>. The buffer is owned by the caller, no other references to it should remain after the call.
        /// When non-empty <paramref name="temporaryBuffer"/> is returned the buffer is written completely by
        /// <see cref="BinarySerializerEx"/> write method, which then disposes the buffer.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="temporaryBuffer">A buffer with serialized payload. (optional, for cases when the serialized size is not known without performing serialization)</param>
        /// <param name="format">Serialization format to use.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(in T value, out RetainedMemory<byte> temporaryBuffer, SerializationFormat format = default)
        {
            int rawLength;
            // TODO fixed size for tuples should be a sum of components, padding is possible
            if (format.IsBinary())
            {
                if (TypeEnumHelper<T>.IsFixedSize)
                {
                    temporaryBuffer = default;
                    return TypeEnumHelper<T>.FixedSize;
                }

                // Type binary serializer
                var tbs = TypeHelper<T>.BinarySerializerEx;
                if (tbs != null)
                {
                    rawLength = tbs.SizeOf(in value, out temporaryBuffer);
                    goto HAS_RAW_SIZE;
                }
            }

            rawLength = JsonBinarySerializerEx<T>.SizeOf(in value, out temporaryBuffer);

        HAS_RAW_SIZE:

            if (rawLength <= 0
                || format.CompressionMethod() == CompressionMethod.None
            )
            {
                return rawLength;
            }

            return SizeOfCompressed(rawLength, in value, ref temporaryBuffer, format);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static int SizeOfCompressed<T>(int rawLength, in T value, ref RetainedMemory<byte> temporaryBuffer, SerializationFormat format)
        {
            if (temporaryBuffer.IsEmpty)
            {
                PopulateTempBuffer(rawLength, value, ref temporaryBuffer, format);
            }

            // Now we have raw bytes in the buffer and need to compress them
            Debug.Assert(rawLength == temporaryBuffer.Length);

            var source = temporaryBuffer.ToDirectBuffer();

            // Compressed buffer will be prefixed with raw size.
            // ```
            // 0                   1                   2                   3
            // 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            // |    RawSize    |         Payload          ....
            // +---------------------------------------------------------------+
            // ```
            // If compression fails (returns non-positive number) or returns a
            // number larger than the raw size then we copy raw payload
            // and set RawSize = -rawSize.
            var compressedMemory = BufferPool.RetainTemp(4 + rawLength);
            var comrpessedDb = compressedMemory.ToDirectBuffer();

            var compressedSize = Compress(in source, comrpessedDb.Slice(4), format.CompressionMethod());

            Debug.Assert(compressedSize != 0);

            if (compressedSize > 0 && compressedSize < rawLength)
            {
                comrpessedDb.WriteInt32(0, rawLength);
            }
            else
            {
                compressedSize = rawLength;
                comrpessedDb.WriteInt32(0, -rawLength);
                source.CopyTo(comrpessedDb.Slice(4));
            }

            temporaryBuffer.Dispose();
            temporaryBuffer = default;

            if (compressedSize > 0)
            {
                compressedMemory = compressedMemory.Slice(0, compressedSize);
                temporaryBuffer = compressedMemory;
                return compressedSize;
            }

            compressedMemory.Dispose();
            return -1;
        }

        private static void PopulateTempBuffer<T>(int rawLength, T value, ref RetainedMemory<byte> temporaryBuffer, SerializationFormat format)
        {
            Debug.Assert(temporaryBuffer._manager == null);
            temporaryBuffer.Dispose(); // noop if assert is OK

            // Fixed size already returned and we do not compress it,
            // JSON always returns non-empty temp buffer.
            // The only option is that TBS returned size with empty buffer.
            // We must now populate it for compression.
            Debug.Assert(TypeHelper<T>.BinarySerializerEx != null && format.IsBinary());
            var tbs = TypeHelper<T>.BinarySerializerEx;
            temporaryBuffer = BufferPool.RetainTemp(rawLength).Slice(0, rawLength);
            Debug.Assert(temporaryBuffer.IsPinned, "BufferPool.RetainTemp must return already pinned buffers.");
            var written = tbs.Write(value, temporaryBuffer.ToDirectBuffer());
            ThrowHelper.AssertFailFast(rawLength == written, $"Wrong IBinarySerializer<{typeof(T).Name}> implementation");
        }

        #endregion SizeOf

        #region Write

        #region Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value,
            Span<byte> destination,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            fixed (byte* ptr = &destination.GetPinnableReference())
            {
                var db = new DirectBuffer(destination.Length, ptr);
                return Write(value, db, temporaryBuffer, format, timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value,
            ref DataTypeHeaderEx headerDestination,
            Span<byte> destination,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            fixed (byte* ptr = &destination.GetPinnableReference())
            {
                var db = new DirectBuffer(destination.Length, ptr);
                return Write(value, ref headerDestination, db, temporaryBuffer, format, timestamp);
            }
        }

        #endregion Span overloads

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="destination">First 4 bytes must be clear or match <see cref="DataTypeHeaderEx"/>.</param>
        /// <param name="temporaryBuffer"></param>
        /// <param name="format"></param>
        /// <param name="timestamp"></param>
        /// <returns>Number of written bytes, including <see cref="DataTypeHeaderEx"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(in T value,
            DirectBuffer destination,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            ref var headerDestination = ref *(DataTypeHeaderEx*)destination.Data;
            destination = destination.Slice(DataTypeHeaderEx.Size);
            var written = Write(value, ref headerDestination, destination, temporaryBuffer, format, timestamp);

            if (written > 0)
            {
                return DataTypeHeaderEx.Size + written;
            }

            return written; // error code
        }

        private static readonly byte* VoidWrite = (byte*)Marshal.AllocHGlobal(8);

        [StructLayout(LayoutKind.Explicit)]
        // ReSharper disable once UnusedMember.Local
        private struct PointersStruct
        {
            [FieldOffset(0)]
            // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
            private readonly byte* Void;

            [FieldOffset(8)]
            // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
            private readonly byte* Target;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PointersStruct(byte* target)
            {
                Void = VoidWrite;
                Target = target;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(in T value,
            ref DataTypeHeaderEx headerDestination,
            DirectBuffer destination,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            Timestamp timestamp = default)
        {
            IBinarySerializerEx<T> tbs = TypeHelper<T>.BinarySerializerEx;

            // if fixed & binary just write
            if (format.IsBinary())
            {
                // fixed size binary is never compressed
                if (TypeEnumHelper<T>.IsFixedSize)
                {
                    if (AdditionalCorrectnessChecks.Enabled && !temporaryBuffer.IsEmpty)
                    {
                        temporaryBuffer.Dispose();
                        ThrowTempBufferMustBeEmptyForFixedSize();
                    }
                    var hasTs = (long)timestamp != default;

                    DataTypeHeaderEx header = TypeEnumHelper<T>.DefaultBinaryHeader;
                    header.VersionAndFlags.IsTimestamped = hasTs;
                    var existingHeader = headerDestination;
                    if (existingHeader == default)
                    {
                        headerDestination = header;
                    }
                    else
                    {
                        if (existingHeader != header)
                        {
                            return (int)BinarySerializerErrorCode.HeaderMismatch;
                        }
                    }

#if BRANCHLESS
                    // 30 (branchless) vs 37 (100% predicted)
                    // TODO: if this does not hurt non-branching bench a lot keep it.
                    // It's hard to make a reliable benchmark with branch misprediction,
                    // there is some in tests
                    var pos = *(int*)&hasTs << 3;
                    var targets = new PointersStruct(destination.Data);
                    var target = (Timestamp*)*(byte**)((byte*)Unsafe.AsPointer(ref targets) + pos);
                    Debug.Assert(hasTs ? (byte*)target == destination.Data : (byte*)target == VoidWrite);
                    *target = timestamp;
#else
                    int pos = 0;
                    if (hasTs)
                    {
                        pos = 8;
                        destination.Write(0, timestamp);
                    }
#endif
                    if (tbs == null)
                    {
                        Debug.Assert(TypeHelper<T>.IsFixedSize);
                        destination.Write(pos, value);
                    }
                    else
                    {
                        tbs.Write(in value, destination.Slice(pos));
                    }

                    return pos + TypeEnumHelper<T>.FixedSize;
                }
                // Use custom TypeHelper<T>.BinaryConverter only when asked for isBinary
                tbs = tbs ?? JsonBinarySerializerEx<T>.Instance;
            }
            else
            {
                tbs = JsonBinarySerializerEx<T>.Instance;
            }

            return WriteVarSizeOrJson(in value, ref headerDestination, destination, temporaryBuffer, format, timestamp, tbs);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowTempBufferMustBeEmptyForFixedSize()
        {
            ThrowHelper.ThrowInvalidOperationException("temporaryBuffer is not empty for fixed size binary serialization.");
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static int WriteVarSizeOrJson<T>(in T value,
            ref DataTypeHeaderEx headerDestination,
            DirectBuffer destination,
            RetainedMemory<byte> temporaryBuffer,
            SerializationFormat format,
            Timestamp timestamp,
            IBinarySerializerEx<T> tbs)
        {
            try
            {
                var hasTs = (long)timestamp != default;

                var header = TypeEnumHelper<T>.DataTypeHeader;
                var vf = header.VersionAndFlags;

                vf.ConverterVersion = tbs.SerializerVersion;
                vf.IsBinary = tbs != JsonBinarySerializerEx<T>.Instance;
                vf.CompressionMethod = format.CompressionMethod();
                vf.IsTimestamped = hasTs;

                header.VersionAndFlags = vf;

                // Special case when header info is rewritten.
                // This relies on existing header check when we reuse
                // header, or each new header will have it's own count.
                if (header.TEOFS.TypeEnum == TypeEnumEx.TupleTN && tbs is IFixedArraySerializer fas)
                {
                    Debug.Assert(tbs.FixedSize > 0);
                    header.TupleNCount = checked((byte)fas.FixedArrayCount(value));
                }

                // Header is written to headerDestination
                var offset = 0;

                var existingHeader = headerDestination;
                if (existingHeader != default)
                {
                    if (existingHeader != header)
                    {
                        return (int)BinarySerializerErrorCode.HeaderMismatch;
                    }
                }
                else
                {
                    headerDestination = header;
                }

                if (hasTs)
                {
                    offset = Timestamp.Size;
                    destination.Write(0, (long)timestamp);
                }

                // TODO should use info from header + asserts,
                if (tbs.FixedSize > 0)
                {
                    ThrowHelper.FailFast("Fixed case");
                }

                destination.Write(offset, temporaryBuffer.Length);

                offset += 4; // var len payload size

                int rawLength;
                if (temporaryBuffer.IsEmpty)
                {
                    temporaryBuffer.Dispose();
                    rawLength = SizeOf(value, out temporaryBuffer, format);

                    if (!temporaryBuffer.IsEmpty && rawLength != temporaryBuffer.Length)
                    {
                        ThrowHelper.FailFast($"Wrong SizeOf<{typeof(T).Name}> implementation");
                    }

                    // Still empty, binary converter knows the size without serilizing
                    if (temporaryBuffer.IsEmpty)
                    {
                        if (format.CompressionMethod() != CompressionMethod.None)
                        {
                            ThrowHelper.FailFast("Compressed format must return non-empty temporaryBuffer");
                        }

                        if (Settings.DefensiveBinarySerializerWrite && !TypeHelper<T>.IsInternalBinarySerializer)
                        {
                            PopulateTempBuffer(rawLength, value, ref temporaryBuffer, format);
                        }
                    }
                }
                else
                {
                    rawLength = temporaryBuffer.Length;
                }

                if (offset + rawLength > destination.Length)
                {
                    // in finally: temporaryBuffer.Dispose();
                    return (int)BinarySerializerErrorCode.NotEnoughCapacity;
                }

                // Write temp buffer
                if (!temporaryBuffer.IsEmpty)
                {
                    temporaryBuffer.Span.CopyTo(destination.Slice(offset, rawLength).Span);
                    // in finally: rm.Dispose();
                }
                else
                {
                    var written = tbs.Write(value, destination.Slice(offset, rawLength));
                    ThrowHelper.AssertFailFast(rawLength == written, $"Wrong IBinarySerializer<{typeof(T).Name}> implementation");
                }

                return offset + rawLength;
            }
            finally
            {
                temporaryBuffer.Dispose();
            }
        }

        #endregion Write

        #region Read

        #region Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ReadOnlySpan<byte> source, out T value)
        {
            return Read(source, out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ReadOnlySpan<byte> source, out T value, out Timestamp timestamp)
        {
            fixed (byte* ptr = &source.GetPinnableReference())
            {
                var db = new DirectBuffer(source.Length, ptr);
                return Read(db, out value, out timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DataTypeHeaderEx header, ReadOnlySpan<byte> source, out T value)
        {
            return Read(header, source, out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DataTypeHeaderEx header, ReadOnlySpan<byte> source, out T value, out Timestamp timestamp)
        {
            fixed (byte* ptr = &source.GetPinnableReference())
            {
                var db = new DirectBuffer(source.Length, ptr);
                return Read(header, db, out value, out timestamp);
            }
        }

        #endregion Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DirectBuffer source, out T value)
        {
            return Read(source, out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DirectBuffer source, out T value, out Timestamp timestamp)
        {
            var consumed = Read(source.Read<DataTypeHeaderEx>(0), source.Slice(DataTypeHeaderEx.Size),
                out value,
                out timestamp);

            if (consumed > 0)
            {
                return DataTypeHeaderEx.Size + consumed;
            }

            return consumed; // error code
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DataTypeHeaderEx header, DirectBuffer source, out T value)
        {
            return Read(header, source.Slice(DataTypeHeaderEx.Size), out value, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DataTypeHeaderEx header, DirectBuffer source, out T value, out Timestamp timestamp)
        {
            var headerFs = header.FixedSize;
            if (header.VersionAndFlags.IsBinary && headerFs > 0) // IsFixedSize not possible with compression
            {
                // TODO sort out asserts, some conditions need fail fast
                // all these checks are non negligible
                Debug.Assert(TypeEnumHelper<T>.FixedSize == headerFs);
                if ((!TypeEnumHelper<T>.IsFixedSize || TypeEnumHelper<T>.FixedSize > source.Length)
                    ||
                    (TypeHelper<T>.FixedSize > 0 && (!header.IsScalar || TypeHelper<T>.FixedSize != TypeEnumHelper<T>.FixedSize))
                    )
                {
                    value = default;
                    timestamp = default;
                    return -1;
                }

                var versionAndFlags = header.VersionAndFlags;
                var tsSize = 0;
                if (versionAndFlags.IsTimestamped)
                {
                    tsSize = Timestamp.Size;
                    timestamp = source.Read<Timestamp>(0);
                    source = source.Slice(tsSize);
                }
                else
                {
                    timestamp = default;
                }

                if (TypeHelper<T>.IsFixedSize)
                {
                    value = source.Read<T>(tsSize);
                }
                else
                {
                    var bc = TypeHelper<T>.BinarySerializerEx;
                    if (bc != null)
                    {
                        bc.Read(source, out value);
                    }
                    else
                    {
                        value = source.Read<T>(tsSize);
                    }
                }

                return tsSize + headerFs;
            }

            return ReadVarSize(header, source, out value, out timestamp);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadVarSize<T>(DataTypeHeaderEx header, DirectBuffer source, out T value, out Timestamp timestamp)
        {
            IBinarySerializerEx<T> bc = null;

            if (header.VersionAndFlags.IsBinary)
            {
                bc = TypeHelper<T>.BinarySerializerEx;
            }

            var tsSize = header.VersionAndFlags.IsTimestamped ? Timestamp.Size : 0;
            var offset = tsSize + 4;

            timestamp = tsSize > 0 ? source.Read<Timestamp>(0) : default;

            var payloadSize = source.Read<int>(tsSize);

            // source: ts? + plLen + pl

            var calculatedSourceSize = offset + payloadSize;

            if (payloadSize < 0 || calculatedSourceSize > source.Length)
            {
                goto INVALID_RETURN;
            }

            RetainedMemory<byte> tempMemory = default;
            try
            {
                if (header.VersionAndFlags.CompressionMethod != CompressionMethod.None)
                {
                    if (source.Length < 4)
                    {
                        // raw size must be present in compressed payload
                        goto INVALID_RETURN;
                    }

                    var rawLength = source.Read<int>(offset);
                    var rawLengthAbs = Math.Abs(rawLength);

                    // compressed pl: rawLen + compressedPl

                    // we will decompress raw payload here and copy ts? and rawLength => plLen
                    tempMemory = BufferPool.RetainTemp(offset + rawLengthAbs);

                    // this has offset bytes free
                    var tmpSource = tempMemory.ToDirectBuffer().Slice(offset + rawLengthAbs);
                    var sourceUncompressed = tmpSource.Slice(offset);

                    var compressedRawSizeOffset = 4;

                    if (rawLength < 0)
                    {
                        // payload was not compressed
                        source.Slice(offset + compressedRawSizeOffset, rawLengthAbs).CopyTo(sourceUncompressed);
                    }
                    else
                    {
                        var decompressedLen = Decompress(source.Slice(offset), in sourceUncompressed,
                            header.VersionAndFlags.CompressionMethod);
                        if (decompressedLen <= 0)
                        {
                            goto INVALID_RETURN;
                        }
                    }

                    source = tmpSource;
                }

                // now source is uncompressed as if it was from beginning

                if (bc == null)
                {
                    bc = JsonBinarySerializerEx<T>.Instance;
                }

                var slice = source.Slice(offset, calculatedSourceSize - offset);
                var readSize1 = offset + bc.Read(slice, out value);

                if (readSize1 != calculatedSourceSize)
                {
                    goto INVALID_RETURN;
                }
            }
            finally
            {
                tempMemory.Dispose();
            }

            return calculatedSourceSize;

        INVALID_RETURN:
            value = default;
            timestamp = default;
            return -1;
        }

        #endregion Read

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(Stream stream, out T value, out Timestamp timestamp)
        {
#pragma warning disable 618
            if (stream is RecyclableMemoryStream rms && rms.IsSingleChunk)

            {
                return Read(rms.SingleChunk.Span, out value, out timestamp);
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
                    return Read(rms.SingleChunk.Span, out value, out timestamp);
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
#pragma warning restore 618
        }
    }
}
