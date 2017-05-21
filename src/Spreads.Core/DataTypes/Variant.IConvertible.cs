// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.DataTypes
{
    public unsafe partial struct Variant : IConvertible
    {
        /// <inheritdoc/>
        public TypeCode GetTypeCode()
        {
            return Type.GetTypeCode(VariantHelper.GetType(this.TypeEnum));
        }

        /// <inheritdoc/>
        public bool ToBoolean(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToBoolean(obj);
        }

        /// <inheritdoc/>
        public byte ToByte(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToByte(obj);
        }

        /// <inheritdoc/>
        public char ToChar(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToChar(obj);
        }

        /// <inheritdoc/>
        public DateTime ToDateTime(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToDateTime(obj);
        }

        /// <inheritdoc/>
        public decimal ToDecimal(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToDecimal(obj);
        }

        /// <inheritdoc/>
        public double ToDouble(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToDouble(obj);
        }

        /// <inheritdoc/>
        public short ToInt16(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToInt16(obj);
        }

        /// <inheritdoc/>
        public int ToInt32(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToInt32(obj);
        }

        /// <inheritdoc/>
        public long ToInt64(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToInt64(obj);
        }

        /// <inheritdoc/>
        public sbyte ToSByte(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToSByte(obj);
        }

        /// <inheritdoc/>
        public float ToSingle(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToSingle(obj);
        }

        /// <inheritdoc/>
        public string ToString(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToString(obj);
        }

        /// <inheritdoc/>
        public object ToType(Type conversionType, IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ChangeType(obj, conversionType, provider);
        }

        /// <inheritdoc/>
        public ushort ToUInt16(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToUInt16(obj);
        }

        /// <inheritdoc/>
        public uint ToUInt32(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToUInt32(obj);
        }

        /// <inheritdoc/>
        public ulong ToUInt64(IFormatProvider provider)
        {
            var obj = ToObject();
            return Convert.ToUInt64(obj);
        }
    }
}