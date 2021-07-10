// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Serialization.Serializers;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization
{
    // BinarySerializer is low-level, we do not need timestamp! We could do this manually.
    // We needed it before header separation, but now cold delete it.

    public static unsafe partial class BinarySerializer
    {
        /// <summary>
        /// Initializes all static readonly fields used in serialization so that they are treated as JIT constants
        /// or known sealed classes afterwards.
        /// Call this from anywhere before using <see cref="BinarySerializer"/> for type <typeparamref name="T"/>.
        /// For fixed size tuples performance difference could be 2x.
        /// </summary>
        public static int WarmUp<T>()
        {
            var size = SizeOf<T>(default, out var bufferWriter, SerializationFormat.Binary);
            bufferWriter?.Dispose();
            return size;
        }

        /// <summary>
        /// Size of payload length prefix is 4 bytes (<see cref="Int32"/>).
        /// </summary>
        public const int PayloadLengthSize = 4;

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

        // Problem: we changed API so that payload buffer does not contain header and length prefix,
        // this simplifies the nested case and is OK when we copy BW content to destination.
        // But e.g. in SQLite we could only provide a pointer to bind a blob, therefore
        // BW must already have header & length prefix to avoid additional copy.

        // TODO rewrite docs, new API
        /// <summary>
        ///
        /// Returns the size of serialized value payload plus <see cref="DataTypeHeader.Size"/>
        /// plus <see cref="PayloadLengthSize"/> if payload is not fixed size.
        /// You could use the returned value to allocate a buffer for subsequent
        /// write operation - it must have capacity greater or equal to the return
        /// value of <see cref="SizeOf{T}"/>.
        ///
        /// <para />
        ///
        /// When serialized payload length is only known after serialization, which is the case for variable size types and many containers,
        /// this method serializes the value into <paramref name="payload"/>.
        /// The buffer <paramref name="payload.temporaryBuffer"/> is owned by the caller, ownership is transferred to a Write method that disposes the buffer.
        ///
        /// <para />
        ///
        /// Returned <paramref name="payload.actualFormat"/> could differ from <paramref name="preferredFormat"/>
        /// when preferred format is binary but there is no custom binary serializer
        /// or if preferred format is compressed but payload length is less than <see cref="Settings.CompressionStartFrom"/>.
        ///
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="payload">A buffer with serialized payload and actual format. (optional, for cases when the serialized size is not known without performing serialization)</param>
        /// <param name="preferredFormat">Preferred serialization format.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(in T value,
            out BufferWriter payload,
            SerializationFormat preferredFormat = default
            )
        {
            int plLen;
            BufferWriter writer;
            SerializationFormat actualFormat;

            const int headerSize = DataTypeHeader.Size; //((*(sbyte*)&noHeader) - 1) & DataTypeHeader.Size;

            if (preferredFormat.IsBinary())
            {
                if (TypeEnumHelper<T>.IsFixedSize)
                {
                    payload = default;
                    return headerSize + TypeEnumHelper<T>.FixedSize;
                }

                if (BinarySerializer<T>.HasTypeSerializer)
                {
                    Debug.Assert(BinarySerializer<T>.TypeSerializer.FixedSize <= 0);

                    writer = BufferWriter.Create();
                    writer.Advance(headerSize + PayloadLengthSize);
                    plLen = BinarySerializer<T>.TypeSerializer.SizeOf(in value, writer);
                    if (writer.WrittenLength == headerSize + PayloadLengthSize)
                    {
                        writer.Dispose();
                        writer = null;
                    }

                    actualFormat = preferredFormat;
                    goto HAS_RAW_SIZE;
                }
            }

            writer = BufferWriter.Create();
            writer.Advance(headerSize + PayloadLengthSize);
            plLen = JsonBinarySerializer<T>.Instance.SizeOf(in value, writer);

            // clear binary bit
            actualFormat = (SerializationFormat)((byte)preferredFormat & ~VersionAndFlags.IsBinaryMask);

        HAS_RAW_SIZE:
            if (AdditionalCorrectnessChecks.Enabled && writer != null && headerSize + PayloadLengthSize + plLen != writer.WrittenLength)
            {
                FailWrongSerializerImplementation<T>(BinarySerializerFailCode.PayloadLengthNotEqualToSizeOf);
            }

            if (plLen <= 0)
            {
                payload = default;
                return plLen;
            }

            // if we reached this line then payload is var size and in addition to header will have plLen in Write

            if (preferredFormat.CompressionMethod() == CompressionMethod.None
                 || plLen < CompressionStartFrom // LT, value is inclusive
            )
            {
                if (writer != null)
                {
                    // clear compression bits, do not add another if
                    actualFormat = (SerializationFormat)((byte)actualFormat & ~VersionAndFlags.CompressionMethodMask);
                    // if (headerSize != 0)
                    // {
                    var header = TypeEnumHelper<T>.DataTypeHeader;
                    header.VersionAndFlags.SerializationFormat = actualFormat;
                    // ReSharper disable once PossibleNullReferenceException
                    writer.DangerousWriteAtOffset(value: header, offset: 0);
                    // }
                    // ReSharper disable once PossibleNullReferenceException
                    writer.DangerousWriteAtOffset(value: plLen, offset: headerSize);
                }

                payload = writer;
                // ReSharper disable once ArrangeRedundantParentheses
                return (headerSize + PayloadLengthSize) + plLen;
            }

            // compression replaces bufferWriter if successful
            payload = writer;

            // ReSharper disable once ArrangeRedundantParentheses
            return SizeOfCompressed(plLen, in value, ref payload, preferredFormat);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static int SizeOfCompressed<T>(int plLen, in T value, ref BufferWriter payload, SerializationFormat format)
        {
            Debug.Assert(((byte)format & VersionAndFlags.CompressionMethodMask) != 0);

            var header = TypeEnumHelper<T>.DataTypeHeader;

            if (payload == null)
            {
                Debug.Assert(format.IsBinary());

                payload = BufferWriter.Create();
                FillBufferWriter(plLen, value, ref payload, format);

                header.VersionAndFlags.SerializationFormat = SerializationFormat.Binary;
                payload.DangerousWriteAtOffset(value: header, offset: 0);
                payload.DangerousWriteAtOffset(value: plLen, offset: DataTypeHeader.Size);
            }

            // Compressed buffer will be prefixed with raw len.
            // ```
            // 0                   1                   2                   3
            // 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            // |     RawLen    |         Payload          ....
            // +---------------------------------------------------------------+
            // ```
            // If compression fails (returns non-positive number) or returns a
            // number larger than the raw size then we return uncompressed payload.
            var tempWriter = BufferWriter.Create();
            tempWriter.EnsureCapacity((DataTypeHeader.Size + PayloadLengthSize)
                                      + PayloadLengthSize + plLen);
            tempWriter.Advance(DataTypeHeader.Size + PayloadLengthSize);

            var compressedSize = Compress(payload.WrittenSpan.Slice(DataTypeHeader.Size + PayloadLengthSize),
                tempWriter.FreeBuffer.Slice(PayloadLengthSize).Span,
                format.CompressionMethod());

            Debug.Assert(compressedSize != 0);

            if (compressedSize <= 0 || PayloadLengthSize + compressedSize >= plLen)
            {
                tempWriter.Dispose();
                // clear compression bits
                format = (SerializationFormat)((byte)format & ~VersionAndFlags.CompressionMethodMask);

                header = TypeEnumHelper<T>.DataTypeHeader;
                header.VersionAndFlags.SerializationFormat = format;
                payload.DangerousWriteAtOffset(value: header, offset: 0);

                return plLen;
            }

            Debug.Assert(tempWriter.WrittenLength == 8);
            tempWriter.Write<int>(plLen);
            Debug.Assert(tempWriter.WrittenLength == 12);
            tempWriter.Advance(compressedSize);
            Debug.Assert(compressedSize > 0);

            header = TypeEnumHelper<T>.DataTypeHeader;
            header.VersionAndFlags.SerializationFormat = format;

            tempWriter.DangerousWriteAtOffset(value: header, offset: 0);
            tempWriter.DangerousWriteAtOffset(value: PayloadLengthSize + compressedSize, offset: DataTypeHeader.Size);

            payload.Dispose();
            payload = tempWriter;

            return (DataTypeHeader.Size + PayloadLengthSize + PayloadLengthSize) + compressedSize;
        }

        private static void FillBufferWriter<T>(int rawLength, T value, ref BufferWriter bufferWriter, SerializationFormat format)
        {
            Debug.Assert(bufferWriter.WrittenLength == 0);

            // Fixed size already returned and we do not compress it,
            // JSON always returns non-empty temp buffer.
            // The only option is that TBS returned size with empty buffer.
            Debug.Assert(BinarySerializer<T>.TypeSerializer != null && format.IsBinary());
            var tbs = BinarySerializer<T>.TypeSerializer;

            bufferWriter.EnsureCapacity(DataTypeHeader.Size + PayloadLengthSize + rawLength);
            bufferWriter.Advance(DataTypeHeader.Size + PayloadLengthSize);

            var written = tbs.Write(in value, bufferWriter.FreeBuffer);

            if (rawLength != written) { FailWrongSerializerImplementation<T>(BinarySerializerFailCode.WrittenNotEqualToSizeOf); }
            bufferWriter.Advance(written);
        }

        #endregion SizeOf

        #region Write

        #region Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(in T value,
            Span<byte> destination,
            BufferWriter payload,
            SerializationFormat format = default)
        {
            fixed (byte* ptr = &destination.GetPinnableReference())
            {
                var db = new DirectBuffer(destination.Length, ptr);
                return Write(value, db, payload, format);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(in T value,
            ref DataTypeHeader headerDestination,
            Span<byte> destination,
            BufferWriter payload,
            SerializationFormat format = default)
        {
            fixed (byte* ptr = &destination.GetPinnableReference())
            {
                var db = new DirectBuffer(destination.Length, ptr);
                return Write(value, ref headerDestination, db, payload, format);
            }
        }

        #endregion Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(in T value,
            DirectBuffer destination,
            BufferWriter payload,
            SerializationFormat format = default,
            bool noHeader = false)
        {
            int written;
            if (noHeader)
            {
                written = Write(value, ref Unsafe.AsRef<DataTypeHeader>(null), destination, payload, format,
                    checkHeader: false);
            }
            else
            {
                ref var headerDestination = ref *(DataTypeHeader*)destination.Data;
                destination = destination.Slice(DataTypeHeader.Size);

                written = Write(value, ref headerDestination, destination, payload, format, checkHeader: false);
            }

            if (written > 0)
            {
                return DataTypeHeader.Size + written;
            }

            return written; // error code
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(in T value,
            ref DataTypeHeader headerDestination,
            DirectBuffer destination,
            BufferWriter payload,
            SerializationFormat format = default)
        {
            return Write(value, ref headerDestination, destination, payload, format, checkHeader: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Write<T>(in T value,
            ref DataTypeHeader headerDestination,
            DirectBuffer destination,
            BufferWriter payload,
            SerializationFormat format,
            bool checkHeader)
        {
            var writeHeader = !Unsafe.AreSame(ref headerDestination, ref Unsafe.AsRef<DataTypeHeader>(null));

            // var tbs = TypeHelper<T>.TypeSerializer;

            // if fixed & binary just write
            if (format.IsBinary())
            {
                // Fixed size binary is never compressed regardless of requested format
                // We do not throw when type cannot be serialized with requested format,
                // it's best efforts only. Output is always "binary" and header shows
                // what we have actually written.

                if (TypeEnumHelper<T>.IsFixedSize)
                {
                    if (AdditionalCorrectnessChecks.Enabled && payload != null)
                    {
                        ThrowTempBufferMustBeEmptyForFixedSize();
                    }

                    if (writeHeader)
                    {
                        var header = TypeEnumHelper<T>.DefaultBinaryHeader;

                        if (checkHeader)
                        {
                            var existingHeader = headerDestination;
                            if (existingHeader != default && existingHeader != header)
                            {
                                return (int)BinarySerializerErrorCode.HeaderMismatch;
                            }
                        }

                        headerDestination = header;
                    }

                    FixedProxy<T>.Write(in value, destination);
                    return TypeEnumHelper<T>.FixedSize;
                }
            }

            return WriteVarSizeOrJson(in value, ref headerDestination, destination, payload, format, checkHeader);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static int WriteVarSizeOrJson<T>(in T value,
            ref DataTypeHeader headerDestination,
            DirectBuffer destination,
            BufferWriter payload,
            SerializationFormat format,
            bool checkHeader)
        {
            BinarySerializer<T> tbs;
            if (format.IsBinary())
            {
                tbs = BinarySerializer<T>.TypeSerializer ?? JsonBinarySerializer<T>.Instance;
            }
            else
            {
                tbs = JsonBinarySerializer<T>.Instance;
            }

            try
            {
                if (AdditionalCorrectnessChecks.Enabled && tbs.FixedSize > 0)
                {
                    ThrowHelper.FailFast("Fixed case must write this type");
                }

                int rawLength;
                if (payload == null)
                {
                    // When bufferWriter == null call to SizeOf must be very fast O(1)
                    rawLength = SizeOf(value, out payload, format) - (DataTypeHeader.Size + PayloadLengthSize);

                    // Still empty, binary converter knows the size without serializing
                    if (payload == null)
                    {
                        if (format.CompressionMethod() != CompressionMethod.None)
                        {
                            if (rawLength >= CompressionStartFrom)
                            {
                                ThrowHelper.FailFast("SizeOf with compressed format must return non-empty payload.");
                            }
                            else
                            {
                                // clear compression bits
                                format = (SerializationFormat)((byte)format & ~VersionAndFlags.CompressionMethodMask);
                            }
                        }

                        if (DefensiveBinarySerializerWrite && !BinarySerializer<T>.IsTypeSerializerInternal)
                        {
#if DEBUG
                            // ReSharper disable once PossibleNullReferenceException
                            if (typeof(T).Namespace.Contains("Spreads"))
                            {
                                throw new NotImplementedException($"Serializer for {typeof(T).Name} must be marked as IsInternalBinarySerializer in TypeHelper<T>.");
                            }
#endif
                            // format was given to SizeOf but returned payload is empty, which means format is ok
                            payload = BufferWriter.Create();
                            FillBufferWriter(rawLength, value, ref payload, format);
                        }
                    }
                }
                else
                {
                    rawLength = payload.WrittenLength - (DataTypeHeader.Size + PayloadLengthSize);
                }

                var writeHeader = !Unsafe.AreSame(ref headerDestination, ref Unsafe.AsRef<DataTypeHeader>(null));
                if (writeHeader)
                {
                    DataTypeHeader header;
                    if (payload != null)
                    {
                        header = payload.WrittenBuffer.Read<DataTypeHeader>(0);
                    }
                    else
                    {
                        header = TypeEnumHelper<T>.DataTypeHeader;
                        header.VersionAndFlags.SerializationFormat = format;
                    }

                    // Special case when header info is rewritten.
                    // This relies on existing header check when we reuse
                    // header, or each new header will have it's own count.
                    if (header.TEOFS.TypeEnum == TypeEnum.TupleTN && tbs is IFixedArraySerializer fas)
                    {
                        Debug.Assert(tbs.FixedSize > 0);
                        header.TupleNCount = checked((byte)fas.FixedArrayCount(value));
                    }

                    if (checkHeader)
                    {
                        var existingHeader = headerDestination;
                        if (existingHeader != default && existingHeader != header)
                        {
                            return (int)BinarySerializerErrorCode.HeaderMismatch;
                        }
                    }

                    headerDestination = header;
                }

                // ReSharper disable once RedundantTypeArgumentsOfMethod
                destination.Write<int>(0, rawLength);

                if (PayloadLengthSize + rawLength > destination.Length)
                {
                    // in finally: temporaryBuffer.Dispose();
                    return (int)BinarySerializerErrorCode.NotEnoughCapacity;
                }

                // Write temp buffer
                if (payload == null)
                {
                    var written = tbs.Write(value, destination.Slice(PayloadLengthSize, rawLength));
                    if (rawLength != written)
                    {
                        FailWrongSerializerImplementation<T>(BinarySerializerFailCode.WrittenNotEqualToSizeOf);
                    }
                }
                else
                {
                    payload.WrittenSpan.Slice(DataTypeHeader.Size + PayloadLengthSize)
                        .CopyTo(destination.Slice(PayloadLengthSize, rawLength).Span);
                    // in finally: rm.Dispose();
                }

                return PayloadLengthSize + rawLength;
            }
            finally
            {
                payload?.Dispose();
            }
        }

        #endregion Write

        #region Read

        #region Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(ReadOnlySpan<byte> source, out T value, bool skipTypeInfoValidation = false)
        {
            fixed (byte* ptr = &source.GetPinnableReference())
            {
                var sourceDb = new DirectBuffer(source.Length, ptr);
                return Read(sourceDb, out value, skipTypeInfoValidation);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DataTypeHeader header, ReadOnlySpan<byte> source, out T value, bool skipTypeInfoValidation = false)
        {
            fixed (byte* ptr = &source.GetPinnableReference())
            {
                var sourceDb = new DirectBuffer(source.Length, ptr);
                return Read(header, sourceDb, out value, skipTypeInfoValidation);
            }
        }

        #endregion Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DirectBuffer source, out T value, bool skipTypeInfoValidation = false)
        {
            var consumed = Read(source.Read<DataTypeHeader>(0), source.Slice(DataTypeHeader.Size), out value, skipTypeInfoValidation);

            if (consumed > 0)
            {
                return DataTypeHeader.Size + consumed;
            }

            return consumed; // error code
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(DataTypeHeader header, DirectBuffer source, out T value, bool skipTypeInfoValidation = false)
        {
            // Note that we do not check that T matches type info from the header.
            // It could be done by caller when needed.

            // We could get correct fixed size from header.FixedSize,
            // but using TypeEnumHelper<T>.FixedSize is 2.5x times faster.
            // T could be wrong, but we do check if fs > source.Length
            // so no reads beyond source are possible. If T is wrong
            // then returned value is garbage, but no data corruption
            // is possible. We ignore cases that to not corrupt data
            // or compromise safety, so header.FixedSize is only in Debug.Assert.

            // TODO Generic method to validate header against T and optional param to Read `bool validateHeader = false`.
            // When true we should also validate type enums.
            // TODO consider IsFixedSize in VersionAndFlags

            var fs = TypeEnumHelper<T>.FixedSize; // header.FixedSize;

            // We need to check IsBinary because header.FixedSize only tells the size as if format is binary
            if (header.VersionAndFlags.IsBinary && fs > 0)
            {
                // var typeHeader = TypeEnumHelper<T>.DataTypeHeader;
                //typeHeader.VersionAndFlags = default;
                //header.VersionAndFlags = default;

                // Debug.Assert(header.FixedSize == fs, "header.FixedSize == fs");
                // FixedSize not possible with compression, do not check

                if ((fs > source.Length || (!skipTypeInfoValidation && TypeEnumHelper<T>.DataTypeHeader.WithoutVersionAndFlags != header.WithoutVersionAndFlags))
#if DEBUG
                    ||
                    (TypeHelper<T>.FixedSize > 0 && (TypeHelper<T>.FixedSize != TypeEnumHelper<T>.FixedSize))
#endif
                    )
                {
                    value = default;
                    return -1;
                }

                Debug.Assert(BinarySerializer<T>.HasTypeSerializer ^ (BinarySerializer<T>.TypeSerializer == null));

                var consumed = FixedProxy<T>.Read(source, out value);
                Debug.Assert(consumed == fs);

                return fs;
            }

            return ReadVarSize(header, source, out value, skipTypeInfoValidation);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static int ReadVarSize<T>(DataTypeHeader header, DirectBuffer source, out T value, bool skipTypeInfoValidation)
        {
            if (!skipTypeInfoValidation &&
                TypeEnumHelper<T>.DataTypeHeader.WithoutVersionAndFlags != header.WithoutVersionAndFlags)
            {
                goto INVALID_RETURN;
            }

            BinarySerializer<T> bc = null;

            if (header.VersionAndFlags.IsBinary)
            {
                bc = BinarySerializer<T>.TypeSerializer;
            }

            var payloadLen = source.Read<int>(0);

            var calculatedSourceSize = PayloadLengthSize + payloadLen;

            if (payloadLen < 0 || calculatedSourceSize > source.Length)
            {
                goto INVALID_RETURN;
            }

            source = source.Slice(0, calculatedSourceSize);

            RetainedMemory<byte> tempMemory = default;
            try
            {
                if (header.VersionAndFlags.CompressionMethod != CompressionMethod.None)
                {
                    if (source.Length < PayloadLengthSize + PayloadLengthSize)
                    {
                        // raw size must be present in compressed payload
                        goto INVALID_RETURN;
                    }

                    // This is bullshit! If we do not compress then header must reflect that,
                    // avoid copy and negative length

                    var rawLen = source.Read<int>(PayloadLengthSize);
                    if (rawLen <= 0)
                    {
                        goto INVALID_RETURN;
                    }

                    // TODO This could be DOS target, e.g. | PlLen: 8 | RawLen: int.Max | 0 |
                    // In DS we have hard limit on max message size, but to apply it here we need
                    // another setting.
                    // One solution is to limit off-heap buffers above e.g. 100Mb to CPU count,
                    // above 200 Mb to CPU count /2, etc. Then use blocking collection.
                    // Huge unused buffers will be swapped so it's OK to have them around.
                    tempMemory = BufferPool.RetainTemp(PayloadLengthSize + rawLen);
                    Debug.Assert(tempMemory.IsPinned, "tempMemory.IsPinned");

                    // Temp buffer will contain uncompressed data with payload header
                    // as if there was no compression. We then replace source with it.
                    var tmpSource = tempMemory.ToDirectBuffer().Slice(0, PayloadLengthSize + rawLen);
                    var uncompressedPl = tmpSource.Slice(PayloadLengthSize);

                    // Compressed source is:
                    // plLen [ rawLen [compressedPl]]

                    var compressedPl = source.Slice(PayloadLengthSize + PayloadLengthSize, payloadLen - PayloadLengthSize);

                    if (rawLen < 0)
                    {
                        // payload was not compressed
                        compressedPl.CopyTo(uncompressedPl);
                    }
                    else
                    {
                        var decompressedLen = Decompress(compressedPl.Span, uncompressedPl.Span,
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
                    bc = JsonBinarySerializer<T>.Instance;
                }

                var slice = source.Slice(PayloadLengthSize);
                var readSize1 = bc.Read(slice, out value);

                // slice is of exact pl length
                if (readSize1 != slice.Length)
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
            return -1;
        }

        #endregion Read

        // TODO Extension
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read<T>(Stream stream, out T value)
        {
#pragma warning disable 618
            if (stream is RecyclableMemoryStream rms && rms.IsSingleChunk)

            {
                return Read(rms.SingleChunk.Span, out value);
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
                    return Read(rms.SingleChunk.Span, out value);
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
                    return Read(rms, out value);
                }
                finally
                {
                    rms.Dispose();
                }
            }
#pragma warning restore 618
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowTempBufferMustBeEmptyForFixedSize()
        {
            ThrowHelper.ThrowInvalidOperationException("temporaryBuffer is not empty for fixed size binary serialization.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void FailWrongSerializerImplementation<T>(BinarySerializerFailCode error)
        {
            ThrowHelper.FailFast($"Wrong IBinarySerializer<{typeof(T).Name}> implementation: {error}");
        }

        internal enum BinarySerializerFailCode
        {
            NonEmptyPayloadForFixedSize = 1,
            WrittenNotEqualToSizeOf = 2,
            PayloadLengthNotEqualToSizeOf = 3
        }
    }
}
