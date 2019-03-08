// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
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
        /// <see cref="BinarySerializer"/> write method, which then disposes the buffer.
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
                var tbs = TypeHelper<T>.BinarySerializer;
                if (tbs != null)
                {
                    rawLength = tbs.SizeOf(in value, out temporaryBuffer);
                    if (!temporaryBuffer.IsEmpty && rawLength != temporaryBuffer.Length)
                    {
                        FailWrongSerializerImplementation<T>();
                    }
                    goto HAS_RAW_SIZE;
                }
            }

            rawLength = JsonBinarySerializer<T>.SizeOf(in value, out temporaryBuffer);
            Debug.Assert(rawLength == temporaryBuffer.Length);

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
            Debug.Assert(TypeHelper<T>.BinarySerializer != null && format.IsBinary());
            var tbs = TypeHelper<T>.BinarySerializer;
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
            SerializationFormat format = default)
        {
            fixed (byte* ptr = &destination.GetPinnableReference())
            {
                var db = new DirectBuffer(destination.Length, ptr);
                return Write(value, db, temporaryBuffer, format);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(T value,
            ref DataTypeHeader headerDestination,
            Span<byte> destination,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default)
        {
            fixed (byte* ptr = &destination.GetPinnableReference())
            {
                var db = new DirectBuffer(destination.Length, ptr);
                return Write(value, ref headerDestination, db, temporaryBuffer, format);
            }
        }

        #endregion Span overloads

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write<T>(in T value,
            DirectBuffer destination,
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default,
            bool noHeader = false)
        {
            int written;
            if (noHeader)
            {
                written = Write(value, ref Unsafe.AsRef<DataTypeHeader>(null), destination, temporaryBuffer, format,
                    checkHeader: false);
            }
            else
            {
                ref var headerDestination = ref *(DataTypeHeader*)destination.Data;
                destination = destination.Slice(DataTypeHeader.Size);

                written = Write(value, ref headerDestination, destination, temporaryBuffer, format, checkHeader: false);
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
            RetainedMemory<byte> temporaryBuffer = default,
            SerializationFormat format = default)
        {
            return Write(value, ref headerDestination, destination, temporaryBuffer, format, checkHeader: true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Write<T>(in T value,
            ref DataTypeHeader headerDestination,
            DirectBuffer destination,
            RetainedMemory<byte> temporaryBuffer,
            SerializationFormat format,
            bool checkHeader)
        {
            var writeHeader = !Unsafe.AreSame(ref headerDestination, ref Unsafe.AsRef<DataTypeHeader>(null));

            var tbs = TypeHelper<T>.BinarySerializer;

            // if fixed & binary just write
            if (format.IsBinary())
            {
                // Fixed size binary is never compressed regardless of requested format
                // We do not throw when type cannot be serialized with requested format,
                // it's best efforts only. Output is always "binary" and header shows
                // what we have actually written.

                if (TypeEnumHelper<T>.IsFixedSize)
                {
                    if (AdditionalCorrectnessChecks.Enabled && !temporaryBuffer.IsEmpty)
                    {
                        temporaryBuffer.Dispose();
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

                    if (tbs == null)
                    {
                        // Debug.Assert(TypeHelper<T>.IsFixedSize);
                        destination.Write(0, value);
                    }
                    else
                    {
                        tbs.Write(in value, destination);
                    }

                    return TypeEnumHelper<T>.FixedSize;
                }
            }

            return WriteVarSizeOrJson(in value, ref headerDestination, destination, temporaryBuffer, format, checkHeader);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static int WriteVarSizeOrJson<T>(in T value,
            ref DataTypeHeader headerDestination,
            DirectBuffer destination,
            RetainedMemory<byte> temporaryBuffer,
            SerializationFormat format,

            bool checkHeader)
        {
            var tbs = TypeHelper<T>.BinarySerializer ?? JsonBinarySerializer<T>.Instance;

            try
            {
                var writeHeader = !Unsafe.AreSame(ref headerDestination, ref Unsafe.AsRef<DataTypeHeader>(null));

                if (writeHeader)
                {
                    var header = TypeEnumHelper<T>.DataTypeHeader;
                    var vf = header.VersionAndFlags;

                    vf.ConverterVersion = tbs.SerializerVersion;
                    vf.IsBinary = tbs != JsonBinarySerializer<T>.Instance;
                    vf.CompressionMethod = format.CompressionMethod();

                    header.VersionAndFlags = vf;

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

                if (AdditionalCorrectnessChecks.Enabled && tbs.FixedSize > 0)
                {
                    ThrowHelper.FailFast("Fixed case must write this type");
                }

                int rawLength;
                if (temporaryBuffer.IsEmpty)
                {
                    Debug.Assert(temporaryBuffer._manager == null, "should not be possible with internal code");
                    temporaryBuffer.Dispose(); // noop for default, just in case

                    rawLength = SizeOf(value, out temporaryBuffer, format);

                    // Still empty, binary converter knows the size without serilizing
                    if (temporaryBuffer.IsEmpty)
                    {
                        if (format.CompressionMethod() != CompressionMethod.None)
                        {
                            ThrowHelper.FailFast("Compressed format must return non-empty temporaryBuffer");
                        }

                        if (Settings.DefensiveBinarySerializerWrite && !TypeHelper<T>.IsInternalBinarySerializer)
                        {
#if DEBUG
                            if (typeof(T).Namespace.Contains("Spreads"))
                            {
                                throw new NotImplementedException($"Serializer for {typeof(T).Name} must be marked as IsInternalBinarySerializer.");
                            }
#endif
                            PopulateTempBuffer(rawLength, value, ref temporaryBuffer, format);
                        }
                    }
                }
                else
                {
                    rawLength = temporaryBuffer.Length;
                }

                // ReSharper disable once RedundantTypeArgumentsOfMethod
                destination.Write<int>(0, temporaryBuffer.Length);

                const int offset = 4; // payload size

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
                    if (rawLength == written)
                    {
                        FailWrongSerializerImplementation<T>();
                    }
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

                Debug.Assert(header.FixedSize == fs, "header.FixedSize == fs");
                // FixedSize not possible with compression, do not check

                if ((fs > source.Length || (!skipTypeInfoValidation && TypeEnumHelper<T>.DataTypeHeader.WithoutVersionAndFlags != header.WithoutVersionAndFlags))
#if DEBUG
                    ||
                    (TypeHelper<T>.FixedSize > 0 && (!header.IsScalar || TypeHelper<T>.FixedSize != TypeEnumHelper<T>.FixedSize))
#endif
                    )
                {
                    value = default;
                    return -1;
                }

                Debug.Assert(TypeHelper<T>.HasBinarySerializer ^ (TypeHelper<T>.BinarySerializer == null));

                var tbs = TypeHelper<T>.BinarySerializer;
                if (tbs == null)
                {
                    value = source.Read<T>(0);
                }
                else
                {
                    Debug.Assert(TypeHelper<T>.BinarySerializer != null, "TypeHelper<T>.BinarySerializer != null");
                    var consumed = tbs.Read(source, out value);
                    Debug.Assert(consumed == fs);
                }

                return fs;
            }

            return ReadVarSize(header, source, out value, skipTypeInfoValidation);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
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

            IBinarySerializer<T> bc = null;

            if (header.VersionAndFlags.IsBinary)
            {
                bc = TypeHelper<T>.BinarySerializer;
            }

            var payloadSize = source.Read<int>(0);
            const int offset = 4;

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
                    bc = JsonBinarySerializer<T>.Instance;
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
            return -1;
        }

        #endregion Read

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
        private static void FailWrongSerializerImplementation<T>()
        {
            ThrowHelper.FailFast($"Wrong SizeOf<{typeof(T).Name}> implementation");
        }
    }
}
