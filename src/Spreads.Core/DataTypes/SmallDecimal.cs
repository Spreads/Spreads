// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#define OWN_MATH

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A blittable 64-bit structure to store fixed-point decimal values with a precision of up to 17 digits.
    /// It is implemented similarly to <see cref="decimal"/> and only uses 58 bits to store significant digits
    /// instead of 96 bits in <see cref="decimal"/>.
    ///
    /// <para />
    ///
    /// Note that this is not IEEE 754-2008 decimal64 but a smaller counterpart
    /// to <see cref="decimal"/> that is easy to implement cross-platform and
    /// is useful for storing small precise values such as prices or small
    /// quantities such as Ethereum wei.
    /// This is also not DEC64 (http://dec64.com/) that has a different binary layout.
    ///
    /// </summary>
    /// <remarks>
    ///
    /// Binary layout:
    ///
    /// <code>
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |S|  Scale  |                    UInt58                         |
    /// +-------------------------------+-+-+---------------------------+
    /// </code>
    /// <para />
    /// S - sign.
    /// <para />
    /// Scale - 0-28 power of 10 to divide UInt58 to get a an absolute decimal value.
    /// Scale values above 28 are invalid, except in the case of <see cref="NaN"/>, when
    /// all 64 bits of SmallDecimal are ones and the scale equals to 31.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
    [BuiltInDataType(Size)]
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public readonly unsafe partial struct SmallDecimal :
        IInt64Diffable<SmallDecimal>,
        IEquatable<SmallDecimal>,
        IConvertible //, IDelta<SmallDecimal>

    {
        public const int Size = 8;

        public static SmallDecimal Zero = default(SmallDecimal);

        private const int SignShift = 63;
        private const ulong SignMask = (1UL << SignShift);
        private const uint SignMaskInt = (1u << 31);

        private const int ScaleShift = 58;
        private const ulong ScaleMask = 31UL;
        private const ulong ScaleValueMask = 31UL << 58;
        private const ulong ScaleMax = 28UL;

        private const ulong MantissaMask = (1UL << ScaleShift) - 1UL;

        private const long MaxValueLong = (1L << ScaleShift) - 1L;
        private const long MinValueLong = -MaxValueLong;

        public static SmallDecimal MaxValue = new(MaxValueLong);
        public static SmallDecimal MinValue = new(MinValueLong);

        /// <summary>
        /// A placeholder to represent not-a-number or absence of a value. Any operation with <see cref="NaN"/> will throw
        /// instead of propagating a NaN value. The primary use-case is a replacement of a <see cref="Nullable{T}"/> with this
        /// special value. Use <see cref="IsNaN"/> to check if a SmallDecimal value is valid or present.
        /// </summary>
        public static SmallDecimal NaN = new(unchecked((ulong)(-1)), false); // all bits are set to avoid misuse, not just two NaN ones.

        private readonly ulong _value;

        // ReSharper disable once UnusedParameter.Local
        private SmallDecimal(ulong value, bool _)
        {
            _value = value;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="value">Decimal value.</param>
        /// <param name="decimals">The number of decimal places in the return value.</param>
        /// <param name="rounding">Rounding option.</param>
        /// <param name="truncate">
        /// True means precision loss is allowed by reducing decimals number.
        /// False means the constructor will throw an
        /// exception if it is not possible to store <paramref name="value"/>
        /// without precision loss
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SmallDecimal(decimal value, int decimals,
            MidpointRounding rounding,
            bool truncate)
        {
            if (decimals != -1)
            {
                if (unchecked((uint)decimals) > ScaleMax)
                {
                    ThrowWrongDecimals();
                }
                else
                {
                    value = Math.Round(value, decimals, rounding);
                }
            }

            var dc = DecCalc.FromDecimal(value);

            if ((dc.uhi != 0 || dc.ulomidLE > MantissaMask))
            {
                if (truncate)
                {
                    // will throw if cannot truncate
                    dc.Truncate();
                }
                else
                {
                    if (dc.Scale == 0)
                    {
                        ThrowValueTooBigOrTooSmall();
                    }

                    ThrowPrecisionLossNoTruncate();
                }
            }

            _value = FromDecCalc(dc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal(decimal value)
            : this(value, -1, default, false)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal(decimal value, bool truncate)
            : this(value, -1, default, truncate)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal(decimal value, int decimals, MidpointRounding rounding)
            : this(value, decimals, rounding, false)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal(decimal value, int decimals)
            : this(value, decimals, MidpointRounding.ToEven, false)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SmallDecimal(double value)
            : this(new decimal(value), -1)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal(double value, int decimals)
            : this(new decimal(value), decimals)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SmallDecimal(float value)
            : this(new decimal(value), -1)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal(float value, int decimals)
            : this(new decimal(value), decimals)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal(long value)
        {
            if (value > MaxValueLong || value < MinValueLong)
            {
                ThrowValueTooBigOrTooSmall();
            }

            _value = (unchecked((ulong)value) & SignMask) | (ulong)Math.Abs(value);
        }

        public SmallDecimal(ulong value)
        {
            if (value > MaxValueLong)
            {
                ThrowValueTooBigOrTooSmall();
            }

            _value = value;
        }

        public SmallDecimal(int value)
        {
            var abs = (ulong)Math.Abs(value);
            _value = (unchecked((ulong)value) & SignMask) | abs;
        }

        public SmallDecimal(uint value)
        {
            _value = value;
        }

        // smaller ints are expanded to int/uint automatically

        public bool IsNaN
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value == ulong.MaxValue;
        }

        #region Internal members

        internal uint Sign
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (uint)(_value >> SignShift);
        }

        internal uint Scale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (uint)((_value >> ScaleShift) & ScaleMask);
        }

        /// <summary>
        /// A number of decimal digits, from 0 to 28.
        /// </summary>
        public int Decimals => (int)Scale;

        internal ulong Mantissa
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value & MantissaMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong FromDecCalc(DecCalc dc)
        {
            return ((ulong)dc.Negative << SignShift)
                   | (((ulong)dc.Scale & ScaleMask) << ScaleShift)
                   | (dc.ulomidLE & MantissaMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DecCalc ToDecCalc()
        {
            if (IsNaN)
            {
                ThrowNaN();
            }

            var dc = new DecCalc {ulomidLE = Mantissa, uflags = (Sign << DecCalc.SignShiftDc) | (Scale << DecCalc.ScaleShiftDc)};
            return dc;
        }

        #endregion Internal members

        // NB only decimal is implicit because it doesn't lose precision
        // there is no implicit conversions from decimal, only ctor

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator double(SmallDecimal value)
        {
            return (double)(decimal)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator float(SmallDecimal value)
        {
            return (float)(decimal)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator decimal(SmallDecimal value)
        {
            DecCalc dc = value.ToDecCalc();
            return *(decimal*)&dc;
        }

        // Casts from floats could produce NaN. Such cast implies truncate operation
        // and usually the intent is to convert floats to SmallDecimals for better
        // compression, while precision loss is OK.
        // TODO (review) this is opinionated, NaN should be out of scope. However this is also not important ATM.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator SmallDecimal(double value)
        {
#if NETCOREAPP
            if (double.IsNaN(value) || !double.IsFinite(value))
#else
            if (double.IsNaN(value) || double.IsPositiveInfinity(value) || double.IsNegativeInfinity(value))
#endif
            {
                return NaN;
            }

            return new SmallDecimal(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator SmallDecimal(float value)
        {
#if NETCOREAPP
            if (float.IsNaN(value) || !float.IsFinite(value))
#else
            if (float.IsNaN(value) || float.IsPositiveInfinity(value) || float.IsNegativeInfinity(value))
#endif
            {
                return NaN;
            }

            return new SmallDecimal(value);
        }

        // int <=32 to SmallDecimal are implicit

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SmallDecimal(int value)
        {
            return new(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SmallDecimal(uint value)
        {
            return new(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SmallDecimal(decimal value)
        {
            return new(value);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(SmallDecimal other)
        {
            return ((decimal)this).CompareTo(other);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SmallDecimal other)
        {
            if (Scale == other.Scale)
            {
                return _value == other._value;
            }

            return (decimal)this == (decimal)other;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            return obj is SmallDecimal value && Equals(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SmallDecimal x, SmallDecimal y)
        {
            return x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SmallDecimal x, SmallDecimal y)
        {
            return !x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SmallDecimal x, SmallDecimal y)
        {
            return x.CompareTo(y) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SmallDecimal x, SmallDecimal y)
        {
            return x.CompareTo(y) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(SmallDecimal x, SmallDecimal y)
        {
            return x.CompareTo(y) >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(SmallDecimal x, SmallDecimal y)
        {
            return x.CompareTo(y) <= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator -(SmallDecimal value)
        {
            var newValue = value._value ^ SignMask;
            return new SmallDecimal(newValue, false); // private ctor
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator +(SmallDecimal x, SmallDecimal y)
        {
            return new((decimal)x + (decimal)y, (int)x.Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator -(SmallDecimal x, SmallDecimal y)
        {
            return new((decimal)x - (decimal)y, (int)Math.Max(x.Scale, y.Scale));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator *(SmallDecimal x, int y)
        {
#if OWN_MATH
            ulong value = (x._value ^ ((ulong)(unchecked((uint)y) & SignMaskInt) << 32)) & ~MantissaMask;

            if (y < 0)
                y = -y;

            ulong newMatissa = checked(x.Mantissa * (uint)y);

            if (newMatissa > MaxValueLong)
                ThrowHelper.ThrowOverflowException(); // TODO (low) Better error

            value |= newMatissa;

            return Unsafe.As<ulong, SmallDecimal>(ref value);
#else
            return new((decimal)x * y, (int)x.Scale);
#endif

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator *(SmallDecimal x, SmallDecimal y)
        {
#if _OWN_MATH // TODO Do not enable before proper tests
            uint scale = x.Scale + y.Scale;

            if(scale > ScaleMax)
                ThrowHelper.ThrowOverflowException(); // TODO Error message/type

            ulong value = ((x._value ^ y._value) & ~SignMask) | ((ulong)scale << ScaleShift);

            ulong newMatissa = checked(x.Mantissa * y.Mantissa);

            if (newMatissa > MaxValueLong)
                ThrowHelper.ThrowOverflowException(); // TODO (low) Better error

            value |= newMatissa;

            return Unsafe.As<ulong, SmallDecimal>(ref value);
#else
            return new((decimal)x * (decimal)y, (int)(x.Scale + y.Scale));
#endif

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator *(int y, SmallDecimal x)
        {
            return x * y;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var asDecimal = (decimal)this;
            return asDecimal.ToString(CultureInfo.InvariantCulture);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return ((decimal)this).GetHashCode();
        }

        #region IConvertible

        /// <inheritdoc />
        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        /// <inheritdoc />
        public bool ToBoolean(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public char ToChar(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public sbyte ToSByte(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public byte ToByte(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public short ToInt16(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public ushort ToUInt16(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public int ToInt32(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public uint ToUInt32(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public long ToInt64(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public ulong ToUInt64(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public float ToSingle(IFormatProvider? provider)
        {
            return (float)this;
        }

        /// <inheritdoc />
        public double ToDouble(IFormatProvider? provider)
        {
            return (double)this;
        }

        /// <inheritdoc />
        public decimal ToDecimal(IFormatProvider? provider)
        {
            return this;
        }

        /// <inheritdoc />
        public DateTime ToDateTime(IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public string ToString(IFormatProvider? provider)
        {
            return ((decimal)this).ToString(provider);
        }

        /// <inheritdoc />
        public object ToType(Type conversionType, IFormatProvider? provider)
        {
            throw new InvalidCastException();
        }

        #endregion IConvertible

        #region IDelta (TODO)

        //public SmallDecimal AddDelta(SmallDecimal delta)
        //{
        //    return this + delta;
        //}

        //public SmallDecimal GetDelta(SmallDecimal other)
        //{
        //    return other - this;
        //}

        #endregion IDelta (TODO)

        #region IInt64Diffable

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SmallDecimal Add(long diff)
        {
            var newMantissa = (long)Mantissa + diff;

            if (newMantissa > MaxValueLong || newMantissa < MinValueLong)
            {
                ThrowValueTooBigOrTooSmall();
            }

            ulong newValue;
            if (newMantissa < 0)
            {
                newValue = SignMask
                           | ((ulong)Scale << ScaleShift)
                           | (ulong)(-newMantissa);
            }
            else
            {
                newValue = ((ulong)Scale << ScaleShift)
                           | (ulong)newMantissa;
            }

            return new SmallDecimal(newValue, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Diff(SmallDecimal other)
        {
            if (Scale != other.Scale)
            {
                ThrowScalesNotEqualInDiff();
            }

            return (long)Mantissa - (long)other.Mantissa;
        }

        #endregion IInt64Diffable

        #region Throw helpers

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWrongDecimals()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(
                $"Decimals must be in 0-{ScaleMax} range.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNaN()
        {
            ThrowHelper.ThrowInvalidOperationException("Value is NaN");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowPrecisionLossNoTruncate()
        {
            ThrowHelper.ThrowArgumentException(
                "Cannot store the given value without precision loss and the truncate parameter is false");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowValueTooBigOrTooSmall()
        {
            ThrowHelper.ThrowOverflowException(
                "Value is either too big (> MaxValue) or too small (< MinValue).");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowScalesNotEqualInDiff()
        {
            ThrowHelper.ThrowNotSupportedException("Scales must be equal for Diff");
        }

        #endregion Throw helpers
    }
}
