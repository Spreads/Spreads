// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// Marker struct to have type safety for integer identity id.
    /// </summary>
    /// <remarks>See <see href="http://stackoverflow.com/questions/39179385/c-sharp-marker-structures-performance">details on marker structs in C#</see>.
    /// It is similar to type aliases in F#.
    /// Useful for preventing wrong usage of some index as an Id or for method overloading.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
    [Serialization(BlittableSize = 4)]
    public struct IdentityId : IEquatable<IdentityId>, IConvertible
    {
        /// <summary>
        /// Invalid/zero/none identity id.
        /// </summary>
        public static IdentityId None = new IdentityId() { _value = 0 };

        private int _value;

        private IdentityId(int value)
        {
            if (value <= 0) throw new ArgumentException("IdentityId must be positive");
            _value = value;
        }

        /// <inheritdoc />
        public bool Equals(IdentityId other)
        {
            return _value.Equals(other._value);
        }

        #region IConvertible

        /// <inheritdoc />
        public TypeCode GetTypeCode()
        {
            return TypeCode.Int32;
        }

        /// <inheritdoc />
        public bool ToBoolean(IFormatProvider provider)
        {
            throw new InvalidCastException("Cannot convert IdentityId to bool.");
        }

        /// <inheritdoc />
        public byte ToByte(IFormatProvider provider)
        {
            throw new InvalidCastException("Cannot convert IdentityId to byte.");
        }

        /// <inheritdoc />
        public char ToChar(IFormatProvider provider)
        {
            throw new InvalidCastException("Meaningless conversion of IdentityId to char value.");
        }

        /// <inheritdoc />
        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException("Meaningless conversion of IdentityId to DateTime value.");
        }

        /// <inheritdoc />
        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new InvalidCastException("Meaningless conversion of IdentityId to decimal value.");
        }

        /// <inheritdoc />
        public double ToDouble(IFormatProvider provider)
        {
            throw new InvalidCastException("Meaningless conversion of IdentityId to floating-point value.");
        }

        /// <inheritdoc />
        public short ToInt16(IFormatProvider provider)
        {
            return (short)_value;
        }

        /// <inheritdoc />
        public int ToInt32(IFormatProvider provider)
        {
            return _value;
        }

        /// <inheritdoc />
        public long ToInt64(IFormatProvider provider)
        {
            return (long)_value;
        }

        /// <inheritdoc />
        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new InvalidCastException("Cannot convert IdentityId to sbyte.");
        }

        /// <inheritdoc />
        public float ToSingle(IFormatProvider provider)
        {
            throw new InvalidCastException("Meaningless conversion of IdentityId to floating-point value.");
        }

        /// <inheritdoc />
        public string ToString(IFormatProvider provider)
        {
            return _value.ToString();
        }

        /// <inheritdoc />
        public object ToType(Type conversionType, IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public ushort ToUInt16(IFormatProvider provider)
        {
            return (ushort)_value;
        }

        /// <inheritdoc />
        public uint ToUInt32(IFormatProvider provider)
        {
            return (uint)_value;
        }

        /// <inheritdoc />
        public ulong ToUInt64(IFormatProvider provider)
        {
            return (ulong)_value;
        }

        #endregion IConvertible

        /// <summary>
        /// Explicit cast from int to IdentityId.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator IdentityId(int value)
        {
            return new IdentityId(value);
        }

        /// <summary>
        /// Explicit cast from IdentityId to int.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator int(IdentityId identityId)
        {
            return identityId._value;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is IdentityId IdentityId)
            {
                return this.Equals(IdentityId);
            }
            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode() => _value.GetHashCode();

        /// <inheritdoc />
        public override string ToString()
        {
            return _value.ToString();
        }

        /// <summary>
        /// Equals operator.
        /// </summary>
        public static bool operator ==(IdentityId id1, IdentityId id2)
        {
            return id1.Equals(id2);
        }

        /// <summary>
        /// Not equals operator.
        /// </summary>
        public static bool operator !=(IdentityId id1, IdentityId id2)
        {
            return !id1.Equals(id2);
        }
    }
}