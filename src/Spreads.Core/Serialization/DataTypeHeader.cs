// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// DataType header for serialized data. First byte contains serialization format flags.
    /// Bytes 1-3 describe the serialized type and its subtypes (for composites and containers).
    /// Type information is stored as <see cref="TypeEnumOrFixedSize"/> 1-byte struct.
    /// </summary>
    /// <remarks>
    /// ```
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// | Version+Flags |     TEOFS     |     TEOFS1    |     TEOFS2    |
    /// +---------------------------------------------------------------+
    /// ```
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = Size)]
    public struct DataTypeHeader : IEquatable<DataTypeHeader>
    {
        public const int Size = 4;

        // First 4 bytes are always the same
        internal const int VersionAndFlagsOffset = 0;

        [FieldOffset(VersionAndFlagsOffset)]
        public VersionAndFlags VersionAndFlags;

        internal const int TeofsOffset = 1;

        [FieldOffset(TeofsOffset)]
        public TypeEnumOrFixedSize TEOFS;

        internal const int Teofs1Offset = 2;

        [FieldOffset(Teofs1Offset)]
        public TypeEnumOrFixedSize TEOFS1;

        internal const int Teofs2Offset = 3;

        [FieldOffset(Teofs2Offset)]
        public TypeEnumOrFixedSize TEOFS2;

        /// <summary>
        /// Count for <see cref="TypeEnum.TupleN"/> and <see cref="TypeEnum.TupleTN"/>
        /// is stored in the slot <see cref="TEOFS1"/> as <see cref="byte"/>.
        /// </summary>
        [FieldOffset(Teofs1Offset)]
        public byte TupleNCount;

        /// <summary>
        /// Type of <see cref="TypeEnum.TupleTN"/> elements is stored in the slot <see cref="TEOFS2"/>
        /// </summary>
        [FieldOffset(Teofs2Offset)]
        public TypeEnumOrFixedSize TupleTNTeofs;

        /// <summary>
        /// Fixed size of <see cref="TypeEnum.TupleN"/> is stored in the slot <see cref="TEOFS2"/>.
        /// Non-zero only if all <see cref="TypeEnum.TupleN"/> are of fixed size.
        /// </summary>
        [FieldOffset(Teofs2Offset)]
        public byte TupleNFixedSize;

        /// <summary>
        /// Size of <see cref="TypeEnum.FixedSize"/> type is stored as <see cref="short"/>
        /// in the two slots <see cref="TEOFS1"/> and <see cref="TEOFS2"/>.
        /// </summary>
        [FieldOffset(Teofs1Offset)]
        public short FixedSizeSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetFixedSize()
        {
            var size = TEOFS.Size;
            if (size > 0)
            {
                return size;
            }

            return FixedSizeComposite();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
         | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private int FixedSizeComposite()
        {
            var te = TEOFS.TypeEnum;

            switch (te)
            {
                case TypeEnum.None:
                    return 0;

                case TypeEnum.Int8:
                case TypeEnum.Int16:
                case TypeEnum.Int32:
                case TypeEnum.Int64:
                case TypeEnum.Int128:
                case TypeEnum.UInt8:
                case TypeEnum.UInt16:
                case TypeEnum.UInt32:
                case TypeEnum.UInt64:
                case TypeEnum.UInt128:
                case TypeEnum.Float16:
                case TypeEnum.Float32:
                case TypeEnum.Float64:
                case TypeEnum.Float128:
                case TypeEnum.Decimal32:
                case TypeEnum.Decimal64:
                case TypeEnum.Decimal128:
                case TypeEnum.Decimal:
                case TypeEnum.SmallDecimal:
                case TypeEnum.Bool:
                case TypeEnum.Utf16Char:
                case TypeEnum.UUID:
                case TypeEnum.DateTime:
                case TypeEnum.Timestamp:
                case TypeEnum.Symbol:
                case TypeEnum.Symbol32:
                case TypeEnum.Symbol64:
                case TypeEnum.Symbol128:
                case TypeEnum.Symbol256:
                    ThrowHelper.ThrowInvalidOperationException("Scalars must not reach this method.");
                    return -1;

                case TypeEnum.Binary:
                case TypeEnum.Utf8String:
                case TypeEnum.Utf16String:
                case TypeEnum.Json:
                    return -1;

                case TypeEnum.TupleTN:
                    {
                        var elSize = TEOFS1.Size;
                        if (elSize > 0)
                        {
                            return elSize * TupleNCount;
                        }

                        return -1;
                    }

                case TypeEnum.TupleT2:
                case TypeEnum.TupleT3:
                case TypeEnum.TupleT4:
                case TypeEnum.TupleT5:
                case TypeEnum.TupleT6:
                    {
                        var elCount = (byte)te - (byte)TypeEnum.TupleTN + 1;
                        var elSize = TEOFS1.Size;
                        if (elSize > 0)
                        {
                            return elSize * elCount;
                        }
                        if (TEOFS2 != default)
                        {
                            var subHeader = new DataTypeHeader
                            {
                                TEOFS = TEOFS1,
                                TEOFS1 = TEOFS2
                            };
                            return subHeader.FixedSizeComposite() * elCount;
                        }

                        return -1;
                    }

                case TypeEnum.Tuple2:
                    {
                        var el1Size = TEOFS1.Size;
                        var el2Size = TEOFS2.Size;
                        if (el1Size > 0 && el2Size > 0)
                        {
                            return el1Size + el2Size;
                        }
                        return -1;
                    }

                case TypeEnum.Tuple2Byte:
                    {
                        var el1Size = TEOFS1.Size;
                        var el2Size = TEOFS2.Size;
                        if (el1Size > 0 && el2Size > 0)
                        {
                            return Unsafe.SizeOf<byte>() + el1Size + el2Size;
                        }
                        return -1;
                    }

                case TypeEnum.Tuple2Long:
                    {
                        var el1Size = TEOFS1.Size;
                        var el2Size = TEOFS2.Size;
                        if (el1Size > 0 && el2Size > 0)
                        {
                            return Unsafe.SizeOf<long>() + el1Size + el2Size;
                        }
                        return -1;
                    }

                case TypeEnum.KeyIndexValue:
                    ThrowHelper.ThrowNotSupportedException();
                    return -1;

                case TypeEnum.Array:
                case TypeEnum.JaggedArray:
                case TypeEnum.ArrayOfTupleTN:
                case TypeEnum.NDArray:
                case TypeEnum.Table:
                case TypeEnum.Map:
                case TypeEnum.Series:
                case TypeEnum.Frame:
                    return -1;

                case TypeEnum.TupleN:
                    {
                        if (TupleNFixedSize > 0)
                        {
                            return TupleNFixedSize;
                        }

                        return -1;
                    }

                case TypeEnum.Variant:
                case TypeEnum.CompositeType:
                case TypeEnum.UserType:
                    return -1;

                case TypeEnum.FixedSize:
                    if (FixedSizeSize > 0)
                    {
                        return FixedSizeSize;
                    }
                    ThrowHelper.ThrowInvalidOperationException("TypeEnumEx.FixedSize must have FixedSizeSize in DataTypeHeader.");
                    return -1;

                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                    return -1;
            }
        }

        /// <summary>
        /// Positive number if a type has fixed size.
        /// </summary>
        public int FixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetFixedSize();
        }

        internal int WithoutVersionAndFlags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.As<DataTypeHeader, int>(ref Unsafe.AsRef( in this)) & (((1 << 24) - 1) << 8); // little endian only
        }

        [Obsolete("Calculates FixedSize, use FixedSize directly")]
        public bool IsFixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FixedSize > 0;
        }

        public bool IsScalar
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TEOFS1 == default)
                {
                    Debug.Assert(TEOFS2 == default);
                    return true;
                }
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(DataTypeHeader other)
        {
            return Unsafe.As<DataTypeHeader, int>(ref Unsafe.AsRef(in this)) == Unsafe.As<DataTypeHeader, int>(ref other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DataTypeHeader x, DataTypeHeader y)
        {
            return x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(DataTypeHeader x, DataTypeHeader y)
        {
            return !x.Equals(y);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is DataTypeHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }
    }
}
