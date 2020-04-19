// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Native;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A blittable 64-bit structure to store small(ish) fixed-point decimal values with precision up to 16 digits.
    /// It is implemented similarly to <see cref="decimal"/> and only uses Int56 to stores significant digits
    /// instead of Int96 in <see cref="decimal"/>. All conversion to and from other numeric types and string
    /// are done via <see cref="decimal"/>.
    ///
    /// <para />
    ///
    /// Note that this is not IEEE 754-2008 decimal64 but a smaller counterpart
    /// to <see cref="decimal"/> that is easy to implement cross-platform and
    /// is useful for storing small precise values such as prices or small
    /// quantities such as Ethereum wei.
    /// This is also not DEC64 (http://dec64.com/) that has similar design but
    /// different binary layout.
    ///
    /// </summary>
    /// <remarks>
    ///
    /// Binary layout:
    ///
    /// ```
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |S|NaN|  Scale  |                  UInt56                       |
    /// +-------------------------------+-+-+---------------------------+
    /// ```
    /// <para />
    ///
    /// S - sign.
    ///
    /// <para />
    ///
    /// NaN - when both bits are set then the value is not a valid SmallDecimal and is
    /// equivalent to <see cref="double.NaN"/> or <see cref="double.PositiveInfinity"/>
    /// or or <see cref="double.NegativeInfinity"/>. Used only when converting
    /// from <see cref="double"/> or <see cref="float"/>.
    ///
    /// <para />
    ///
    /// Scale - 0-28 power of 10 to divide Int56 to get a value.
    ///
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
    [BinarySerialization(Size)]
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [JsonFormatter(typeof(Formatter))]
    public readonly unsafe struct SmallDecimal :
        IInt64Diffable<SmallDecimal>,
        IEquatable<SmallDecimal>,
        IConvertible //, IDelta<SmallDecimal>

    {
        [StructLayout(LayoutKind.Explicit)]
        internal ref struct DecCalc
        {
            [FieldOffset(0)]
            internal uint uflags;

            [FieldOffset(4)]
            internal uint uhi;

            [FieldOffset(8)]
            internal uint ulo;

            [FieldOffset(12)]
            internal uint umid;

            /// <summary>
            /// The low and mid fields combined in little-endian order
            /// </summary>
            [FieldOffset(8)]
            internal ulong ulomidLE;

            // Sign mask for the flags field. A value of zero in this bit indicates a
            // positive Decimal value, and a value of one in this bit indicates a
            // negative Decimal value.
            internal const int SignShiftDc = 31;

            internal const uint SignMaskDc = 1u << 31;

            // Scale mask for the flags field. This byte in the flags field contains
            // the power of 10 to divide the Decimal value by. The scale byte must
            // contain a value between 0 and 28 inclusive.
            internal const int ScaleMaskDc = 0x00FF0000;

            // Number of bits scale is shifted by.
            internal const int ScaleShiftDc = 16;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static DecCalc FromDecimal(decimal value)
            {
                return *(DecCalc*) &value;
            }

            internal int Scale
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (byte) ((uflags & ScaleMaskDc) >> ScaleShiftDc);
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => uflags = (uint) ((value << ScaleShiftDc) & ScaleMaskDc);
            }

            internal uint Negative
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (uflags & SignMaskDc) >> 31;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => uflags |= value << 31;
            }

            // for debug
            public override string ToString()
            {
                return $"{Convert.ToString(uflags, 2)} - scale {Scale} - hi {uhi} - lo {ulo} - mid {umid} - ulomidLE {ulomidLE}";
            }

            public void Truncate()
            {
                // if cannot truncate throw
                ThrowValueTooBigOrTooSmall();
            }
        }

        public const int Size = 8;

        public static SmallDecimal Zero = default(SmallDecimal);

        private const int SignShift = 63;
        private const ulong SignMask = (1UL << SignShift);

        private const int NaNShift = 61;
        private const ulong NaNMask = 0b_11;

        private const int ScaleShift = 56;
        private const ulong ScaleMask = 31UL;

        private const ulong UInt56Mask = (1UL << ScaleShift) - 1UL;

        private const long MaxValueLong = (1L << ScaleShift) - 1L;
        private const long MinValueLong = -MaxValueLong;

        public static SmallDecimal MaxValue = new SmallDecimal(MaxValueLong);
        public static SmallDecimal MinValue = new SmallDecimal(MinValueLong);
        public static SmallDecimal NaN = new SmallDecimal(unchecked((ulong) (-1)), false); // all bits are set to avoid misuse, not just two NaN ones.

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
                if (unchecked((uint) decimals) > 16)
                {
                    ThrowWrongDecimals();
                }
                else
                {
                    value = Math.Round(value, decimals, rounding);
                }
            }

            var dc = DecCalc.FromDecimal(value);

            if ((dc.uhi != 0 || dc.ulomidLE > UInt56Mask))
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

            _value = (unchecked((ulong) value) & SignMask) | (ulong) Math.Abs(value);
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
            var abs = (ulong) Math.Abs(value);
            _value = (unchecked((ulong) value) & SignMask) | abs;
        }

        public SmallDecimal(uint value)
        {
            _value = value;
        }

        // smaller ints are expanded to int/uint automatically

        public bool IsNaN
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((_value >> NaNShift) & NaNMask) == NaNMask;
        }

        #region Internal members

        internal uint Sign
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (uint) (_value >> SignShift);
        }

        internal uint Scale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (uint) ((_value >> ScaleShift) & ScaleMask);
        }

        internal ulong UInt56
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value & UInt56Mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong FromDecCalc(DecCalc dc)
        {
            return ((ulong) dc.Negative << SignShift)
                   | (((ulong) dc.Scale & ScaleMask) << ScaleShift)
                   | (dc.ulomidLE & UInt56Mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DecCalc ToDecCalc()
        {
            if (IsNaN)
            {
                ThrowNaN();
            }

            var dc = new DecCalc
            {
                ulomidLE = UInt56,
                uflags = (Sign << DecCalc.SignShiftDc) | (Scale << DecCalc.ScaleShiftDc)
            };
            return dc;
        }

        #endregion Internal members

        // NB only decimal is implicit because it doesn't lose precision
        // there is no implicit conversions from decimal, only ctor

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator double(SmallDecimal value)
        {
            return (double) (decimal) value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator float(SmallDecimal value)
        {
            return (float) (decimal) value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator decimal(SmallDecimal value)
        {
            DecCalc dc = value.ToDecCalc();
            return *(decimal*) &dc;
        }

        // Casts from floats could produce NaN. Such cast implies truncate operation
        // and usually the intent is to convert floats to SmallDecimals for better
        // compression, while precision loss is OK.
        // TODO (review) this is opinionated, NaN should be out of scope. However this is also not important ATM.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator SmallDecimal(double value)
        {
#if NETCOREAPP3_0
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
#if NETCOREAPP3_0
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
            return new SmallDecimal(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SmallDecimal(uint value)
        {
            return new SmallDecimal(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SmallDecimal(decimal value)
        {
            return new SmallDecimal(value);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(SmallDecimal other)
        {
            return ((decimal) this).CompareTo(other);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SmallDecimal other)
        {
            if (Scale == other.Scale)
            {
                return _value == other._value;
            }

            return (decimal) this == (decimal) other;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
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
            return new SmallDecimal((decimal) x + (decimal) y, (int) x.Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator -(SmallDecimal x, SmallDecimal y)
        {
            return new SmallDecimal((decimal) x - (decimal) y, (int) x.Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator *(SmallDecimal x, int y)
        {
            return new SmallDecimal((decimal) x * y, (int) x.Scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SmallDecimal operator *(int y, SmallDecimal x)
        {
            return new SmallDecimal((decimal) x * y, (int) x.Scale);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var asDecimal = (decimal) this;
            return asDecimal.ToString(CultureInfo.InvariantCulture);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return ((decimal) this).GetHashCode();
        }

        #region IConvertible

        /// <inheritdoc />
        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        /// <inheritdoc />
        public bool ToBoolean(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public char ToChar(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public byte ToByte(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public short ToInt16(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public int ToInt32(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public uint ToUInt32(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public long ToInt64(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public float ToSingle(IFormatProvider provider)
        {
            return (float) this;
        }

        /// <inheritdoc />
        public double ToDouble(IFormatProvider provider)
        {
            return (double) this;
        }

        /// <inheritdoc />
        public decimal ToDecimal(IFormatProvider provider)
        {
            return this;
        }

        /// <inheritdoc />
        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException();
        }

        /// <inheritdoc />
        public string ToString(IFormatProvider provider)
        {
            return ((decimal) this).ToString(provider);
        }

        /// <inheritdoc />
        public object ToType(Type conversionType, IFormatProvider provider)
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
            var newInt56Value = (long) UInt56 + diff;

            if (newInt56Value > MaxValueLong || newInt56Value < MinValueLong)
            {
                ThrowValueTooBigOrTooSmall();
            }

            ulong newValue;
            if (newInt56Value < 0)
            {
                newValue = SignMask
                           | ((ulong) Scale << ScaleShift)
                           | (ulong) (-newInt56Value);
            }
            else
            {
                newValue = ((ulong) Scale << ScaleShift)
                           | (ulong) newInt56Value;
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

            return (long) UInt56 - (long) other.UInt56;
        }

        #endregion IInt64Diffable

        internal class Formatter : IJsonFormatter<SmallDecimal>
        {
            public void Serialize(ref JsonWriter writer, SmallDecimal value, IJsonFormatterResolver formatterResolver)
            {
                var df = formatterResolver.GetFormatter<decimal>();
                df.Serialize(ref writer, value, formatterResolver);
            }

            public SmallDecimal Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                var df = formatterResolver.GetFormatter<decimal>();

                var d = df.Deserialize(ref reader, formatterResolver);

                // if we are reading SD then it was probably written as SD
                // if we are reading decimal we cannot silently truncate from serialized
                // value - that's the point of storing as decimal: not to lose precision
                return new SmallDecimal(d, truncate: false);
            }
        }

        #region Throw helpers

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWrongDecimals()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(
                "Decimals must be in 0-16 range.");
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
                "Cannot store the given value without precision loss and truncate parameter is false");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowValueTooBigOrTooSmall()
        {
            ThrowHelper.ThrowArgumentException(
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