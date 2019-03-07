// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization.Experimental
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
    public struct DataTypeHeaderEx : IEquatable<DataTypeHeaderEx>
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
        /// Count for <see cref="TypeEnumEx.TupleN"/> and <see cref="TypeEnumEx.TupleTN"/>
        /// is stored in the slot <see cref="TEOFS1"/> as <see cref="byte"/>.
        /// </summary>
        [FieldOffset(Teofs1Offset)]
        public byte TupleNCount;

        /// <summary>
        /// Type of <see cref="TypeEnumEx.TupleTN"/> elements is stored in the slot <see cref="TEOFS2"/>
        /// </summary>
        [FieldOffset(Teofs2Offset)]
        public TypeEnumOrFixedSize TupleTNTeofs;

        /// <summary>
        /// Fixed size of <see cref="TypeEnumEx.TupleN"/> is stored in the slot <see cref="TEOFS2"/>.
        /// Non-zero only if all <see cref="TypeEnumEx.TupleN"/> are of fixed size.
        /// </summary>
        [FieldOffset(Teofs2Offset)]
        public byte TupleNFixedSize;

        /// <summary>
        /// Size of <see cref="TypeEnumEx.FixedSize"/> type is stored as <see cref="short"/>
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
                case TypeEnumEx.None:
                    return 0;

                case TypeEnumEx.Int8:
                case TypeEnumEx.Int16:
                case TypeEnumEx.Int32:
                case TypeEnumEx.Int64:
                case TypeEnumEx.Int128:
                case TypeEnumEx.UInt8:
                case TypeEnumEx.UInt16:
                case TypeEnumEx.UInt32:
                case TypeEnumEx.UInt64:
                case TypeEnumEx.UInt128:
                case TypeEnumEx.Float16:
                case TypeEnumEx.Float32:
                case TypeEnumEx.Float64:
                case TypeEnumEx.Float128:
                case TypeEnumEx.Decimal32:
                case TypeEnumEx.Decimal64:
                case TypeEnumEx.Decimal128:
                case TypeEnumEx.Decimal:
                case TypeEnumEx.SmallDecimal:
                case TypeEnumEx.Bool:
                case TypeEnumEx.Utf16Char:
                case TypeEnumEx.UUID:
                case TypeEnumEx.DateTime:
                case TypeEnumEx.Timestamp:
                case TypeEnumEx.Symbol:
                case TypeEnumEx.Symbol32:
                case TypeEnumEx.Symbol64:
                case TypeEnumEx.Symbol128:
                case TypeEnumEx.Symbol256:
                    ThrowHelper.ThrowInvalidOperationException("Scalars must not reach this method.");
                    return -1;

                case TypeEnumEx.Binary:
                case TypeEnumEx.Utf8String:
                case TypeEnumEx.Utf16String:
                case TypeEnumEx.Json:
                    return -1;

                case TypeEnumEx.TupleTN:
                    {
                        var elSize = TEOFS1.Size;
                        if (elSize > 0)
                        {
                            return elSize * TupleNCount;
                        }

                        return -1;
                    }

                case TypeEnumEx.TupleT2:
                case TypeEnumEx.TupleT3:
                case TypeEnumEx.TupleT4:
                case TypeEnumEx.TupleT5:
                case TypeEnumEx.TupleT6:
                    {
                        var elCount = (byte)te - (byte)TypeEnumEx.TupleTN + 1;
                        var elSize = TEOFS1.Size;
                        if (elSize > 0)
                        {
                            return elSize * elCount;
                        }
                        if (TEOFS2 != default)
                        {
                            var subHeader = new DataTypeHeaderEx
                            {
                                TEOFS = TEOFS1,
                                TEOFS1 = TEOFS2
                            };
                            return subHeader.FixedSizeComposite() * elCount;
                        }

                        return -1;
                    }

                case TypeEnumEx.Tuple2:
                    {
                        var el1Size = TEOFS1.Size;
                        var el2Size = TEOFS2.Size;
                        if (el1Size > 0 && el2Size > 0)
                        {
                            return el1Size + el2Size;
                        }
                        return -1;
                    }

                case TypeEnumEx.Tuple2Byte:
                    {
                        var el1Size = TEOFS1.Size;
                        var el2Size = TEOFS2.Size;
                        if (el1Size > 0 && el2Size > 0)
                        {
                            return Unsafe.SizeOf<byte>() + el1Size + el2Size;
                        }
                        return -1;
                    }

                case TypeEnumEx.Tuple2Long:
                    {
                        var el1Size = TEOFS1.Size;
                        var el2Size = TEOFS2.Size;
                        if (el1Size > 0 && el2Size > 0)
                        {
                            return Unsafe.SizeOf<long>() + el1Size + el2Size;
                        }
                        return -1;
                    }

                case TypeEnumEx.KeyIndexValue:
                    ThrowHelper.ThrowNotSupportedException();
                    return -1;

                case TypeEnumEx.Array:
                case TypeEnumEx.JaggedArray:
                case TypeEnumEx.ArrayOfTupleTN:
                case TypeEnumEx.NDArray:
                case TypeEnumEx.Table:
                case TypeEnumEx.Map:
                case TypeEnumEx.Series:
                case TypeEnumEx.Frame:
                    return -1;

                case TypeEnumEx.TupleN:
                    {
                        if (TupleNFixedSize > 0)
                        {
                            return TupleNFixedSize;
                        }

                        return -1;
                    }

                case TypeEnumEx.Variant:
                case TypeEnumEx.CompositeType:
                case TypeEnumEx.UserType:
                    return -1;

                case TypeEnumEx.FixedSize:
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
        public bool Equals(DataTypeHeaderEx other)
        {
            return Unsafe.As<DataTypeHeaderEx, int>(ref Unsafe.AsRef(in this)) == Unsafe.As<DataTypeHeaderEx, int>(ref other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(DataTypeHeaderEx x, DataTypeHeaderEx y)
        {
            return x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(DataTypeHeaderEx x, DataTypeHeaderEx y)
        {
            return !x.Equals(y);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is DataTypeHeaderEx other && Equals(other);
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }
    }
}
