//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using System;
//using System.Runtime.CompilerServices;

//namespace Spreads.DataTypes
//{
//    public partial struct Variant : IComparable<Variant>, IConvertible
//    {
//        #region IComparable

//        /// <inheritdoc />
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public int CompareTo(Variant other)
//        {
//            if (TypeEnum != other.TypeEnum)
//            {
//                ThrowInvalidOperationExceptionTypesDoNotMatch(TypeEnum, other.TypeEnum);
//            }

//            // unwrap potentially boxed known types
//            var typeEnum = TypeEnum;
//            if ((int)typeEnum < KnownSmallTypesLimit)
//            {
//                switch (typeEnum)
//                {
//                    case TypeEnum.Int8:
//                        return (Get<sbyte>()).CompareTo(other.Get<sbyte>());

//                    case TypeEnum.Int16:
//                        return (Get<short>()).CompareTo(other.Get<short>());

//                    case TypeEnum.Int32:
//                        return (Get<int>()).CompareTo(other.Get<int>());

//                    case TypeEnum.Int64:
//                        return (Get<long>()).CompareTo(other.Get<long>());

//                    case TypeEnum.UInt8:
//                        return (Get<byte>()).CompareTo(other.Get<byte>());

//                    case TypeEnum.UInt16:
//                        return (Get<ushort>()).CompareTo(other.Get<ushort>());

//                    case TypeEnum.UInt32:
//                        return (Get<uint>()).CompareTo(other.Get<uint>());

//                    case TypeEnum.UInt64:
//                        return (Get<ulong>()).CompareTo(other.Get<ulong>());

//                    case TypeEnum.Float32:
//                        return (Get<float>()).CompareTo(other.Get<float>());

//                    case TypeEnum.Float64:
//                        return (Get<double>()).CompareTo(other.Get<double>());

//                    case TypeEnum.Decimal:
//                        return (Get<decimal>()).CompareTo(other.Get<decimal>());

//                    case TypeEnum.SmallDecimal:
//                        return (Get<SmallDecimal>()).CompareTo(other.Get<SmallDecimal>());

//                    case TypeEnum.Money:
//                        ThrowHelper.ThrowNotImplementedException(); return 0;
//                    //return value.Get<Money>();

//                    case TypeEnum.DateTime:
//                        return (Get<DateTime>()).CompareTo(other.Get<DateTime>());

//                    case TypeEnum.Timestamp:
//                        return (Get<Timestamp>()).CompareTo(other.Get<Timestamp>());

//                    case TypeEnum.Bool:
//                        return (Get<bool>()).CompareTo(other.Get<bool>());

//                    case TypeEnum.ErrorCode:
//                        ThrowNotSupportedException(TypeEnum.ErrorCode);
//                        goto default;

//                    default:
//                        ThrowHelper.ThrowArgumentOutOfRangeException(); return 0;
//                }
//            }

//            // all other types are stored directly as objects
//            if (_object != null)
//            {
//                if (_object is IComparable ic)
//                {
//                    return ic.CompareTo(other._object);
//                }
//            }

//            ThrowHelper.ThrowNotImplementedException(); return 0;
//        }

//        [MethodImpl(MethodImplOptions.NoInlining)]
//        private static void ThrowInvalidOperationExceptionTypesDoNotMatch(TypeEnum first, TypeEnum second)
//        {
//            throw new InvalidOperationException($"Variant types do not match: {first.ToString()} vs {second.ToString()}");
//        }

//        [MethodImpl(MethodImplOptions.NoInlining)]
//        private static void ThrowNotSupportedException(TypeEnum typeEnum)
//        {
//            throw new NotSupportedException($"Comparison not supported for the type {typeEnum.ToString()}");
//        }

//        #endregion IComparable

//        #region IConvertible

//        /// <inheritdoc/>
//        public TypeCode GetTypeCode()
//        {
//            return Type.GetTypeCode(VariantHelper.GetType(this.TypeEnum));
//        }

//        /// <inheritdoc/>
//        public bool ToBoolean(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToBoolean(obj);
//        }

//        /// <inheritdoc/>
//        public byte ToByte(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToByte(obj);
//        }

//        /// <inheritdoc/>
//        public char ToChar(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToChar(obj);
//        }

//        /// <inheritdoc/>
//        public DateTime ToDateTime(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToDateTime(obj);
//        }

//        /// <inheritdoc/>
//        public decimal ToDecimal(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToDecimal(obj);
//        }

//        /// <inheritdoc/>
//        public double ToDouble(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToDouble(obj);
//        }

//        /// <inheritdoc/>
//        public short ToInt16(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToInt16(obj);
//        }

//        /// <inheritdoc/>
//        public int ToInt32(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToInt32(obj);
//        }

//        /// <inheritdoc/>
//        public long ToInt64(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToInt64(obj);
//        }

//        /// <inheritdoc/>
//        public sbyte ToSByte(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToSByte(obj);
//        }

//        /// <inheritdoc/>
//        public float ToSingle(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToSingle(obj);
//        }

//        /// <inheritdoc/>
//        public string ToString(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToString(obj, provider);
//        }

//        /// <inheritdoc/>
//        public object ToType(Type conversionType, IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ChangeType(obj, conversionType, provider);
//        }

//        /// <inheritdoc/>
//        public ushort ToUInt16(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToUInt16(obj);
//        }

//        /// <inheritdoc/>
//        public uint ToUInt32(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToUInt32(obj);
//        }

//        /// <inheritdoc/>
//        public ulong ToUInt64(IFormatProvider provider)
//        {
//            var obj = ToObject();
//            return Convert.ToUInt64(obj);
//        }

//        #endregion IConvertible
//    }
//}