// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Spreads.Serialization;

namespace Spreads.DataTypes {

    /// <summary>
    /// A blittable structure to store positive price values with decimal precision up to 15 digits.
    /// Could be qualified to trade/buy/sell to add additional checks and logic to trading applications.
    /// Qualification is optional and doesn't affect eqiality and comparison.
    /// </summary>
    /// <remarks>
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |Q T S B|  -exp |        Int56 mantissa                         |
    /// +-------------------------------+-+-+---------------------------+
    /// |               Int56 mantissa                                  |
    /// +-------------------------------+-+-+---------------------------+
    /// R - reserved
    /// Q - is qualified, when set to 1 then T,S,B could be set to 1, otherwise T,S,B must be zero and do not have a special meaning
    /// T (trade) - is trade, when 1 then the price is of some actual trade
    /// S (sell)  - is Sell (Ask). When T = 0, S = 1 indicates an Ask order.
    /// B (buy)   - is Buy (Bid). When T = 0, B = 1 indicates an Bid order.
    /// When T = 1, S/B flags could have a special meaning depending on context.
    /// S and B are mutually exlusive.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 8)]
    [Serialization(BlittableSize = 8)]
    public struct Price : IComparable<Price>, IEquatable<Price> {


        public override int GetHashCode() {
            return (int)((_value & UnqualifiedMask) & (ulong)int.MaxValue);
        }

        private const ulong SignMask = ((1L << 55));
        private const ulong MantissaMask = ((1L << 56) - 1L);
        private const ulong MantissaValueMask = ((1L << 55) - 1L);
        private const ulong UnqualifiedMask = ((1L << 60) - 1L);
        private readonly ulong _value;

        private static decimal[] DecimalFractions10 = new decimal[] {
            1M,
            0.1M,
            0.01M,
            0.001M,
            0.0001M,
            0.00001M,
            0.000001M,
            0.0000001M,
            0.00000001M,
            0.000000001M,
            0.0000000001M,
            0.00000000001M,
            0.000000000001M,
            0.0000000000001M,
            0.00000000000001M,
            0.000000000000001M,
        };


        private static double[] DoubleFractions10 = new double[] {
            1,
            0.1,
            0.01,
            0.001,
            0.0001,
            0.00001,
            0.000001,
            0.0000001,
            0.00000001,
            0.000000001,
            0.0000000001,
            0.00000000001,
            0.000000000001,
            0.0000000000001,
            0.00000000000001,
            0.000000000000001,
        };

        private static long[] Powers10 = new long[] {
            1,
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1000000000,
            10000000000,
            100000000000,
            1000000000000,
            10000000000000,
            100000000000000,
            1000000000000000,
        };

        public ulong Exponent => (_value >> 56) & 15UL;
        public long Mantissa
        {
            get
            {
                var mantissaValue = (long)(_value & MantissaValueMask);
                if ((SignMask & _value) > 0UL) {
                    return -mantissaValue;
                }
                return mantissaValue;
            }
        }

        public decimal AsDecimal => (this);
        public double AsDouble => (double)(this);
        public Price AsUnqualified => IsQualified ? new Price(_value) : this;

        public bool IsQualified => _value >> 63 == 1UL;

        public bool? IsTrade
        {
            get
            {
                var firstTwo = _value >> 62;
                // 11
                if (firstTwo == 3UL) {
                    return true;
                }
                // 10
                if (firstTwo == 2UL) {
                    return false;
                }
                // 00
                if (firstTwo == 0UL) {
                    return null;
                }
                // 01
                throw new ApplicationException("IsTrade bit could only be set together with IsQualified bit");
            }
        }

        public bool? IsBuy
        {
            get
            {
                if (!IsQualified) return null;
                var thirdAndForth = (_value >> 60) & 3UL;
                // 1_11
                if (thirdAndForth == 3UL) {
                    throw new ApplicationException("IsSell and IsBuy bits must be mutually exclusive");
                }
                // 1_01
                if (thirdAndForth == 1UL) {
                    return true;
                }
                // 1_00 or 1_10
                return false;
            }
        }

        public bool? IsSell
        {
            get
            {
                if (!IsQualified) return null;
                var thirdAndForth = (_value >> 60) & 3UL;
                // 1_11
                if (thirdAndForth == 3UL) {
                    throw new ApplicationException("IsSell and IsBuy bits must be mutually exclusive");
                }
                // 1_10
                if (thirdAndForth == 2UL) {
                    return true;
                }
                // 1_00 or 1_01
                return false;
            }
        }

        public TradeSide? TradeSide
        {
            get
            {
                if (!IsQualified) return null;
                // ReSharper disable once PossibleInvalidOperationException
                if (IsBuy.Value) {
                    return DataTypes.TradeSide.Buy;
                }
                // ReSharper disable once PossibleInvalidOperationException
                if (IsSell.Value) {
                    return DataTypes.TradeSide.Sell;
                }
                return DataTypes.TradeSide.None;
            }
        }


        public Price(int exponent, long mantissaValue) {
            if ((ulong)exponent > 15) throw new ArgumentOutOfRangeException(nameof(exponent));
            ulong mantissa = mantissaValue > 0 ? (ulong)mantissaValue : SignMask | (ulong)(-mantissaValue);
            if (mantissa > MantissaMask) throw new ArgumentOutOfRangeException(nameof(mantissaValue));
            _value = ((ulong)exponent << 56) | ((ulong)mantissa);
        }

        private Price(ulong value, TradeSide? tradeSide = null, bool? isTrade = null) {
            _value = value & UnqualifiedMask;
            if (tradeSide != null && tradeSide.Value != DataTypes.TradeSide.None) {
                if (tradeSide == DataTypes.TradeSide.Buy) {
                    _value = _value | (9UL << 60); // 9 ~ 1001
                } else if (tradeSide == DataTypes.TradeSide.Sell) { // 10 ~ 1010
                    _value = _value | (10UL << 60);
                }
            }
            if (isTrade != null && isTrade.Value) {
                _value = _value | (12UL << 60); // 12 ~ 1100
            }
        }

        public Price(decimal value, int precision = 5) : this(value, precision, null, null) { }

        public Price(decimal value, int precision = 5, TradeSide? tradeSide = null, bool? isTrade = null) {
            if ((ulong)precision > 15) throw new ArgumentOutOfRangeException(nameof(precision));
            if (value > MantissaMask * DecimalFractions10[precision]) throw new ArgumentOutOfRangeException(nameof(value));
            var mantissaValue = decimal.ToInt64(value * Powers10[precision]);
            ulong mantissa = mantissaValue > 0 ? (ulong)mantissaValue : SignMask | (ulong)(-mantissaValue);

            _value = ((ulong)precision << 56) | mantissa;
            if (tradeSide != null && tradeSide.Value != DataTypes.TradeSide.None) {
                if (tradeSide == DataTypes.TradeSide.Buy) {
                    _value = _value | (9UL << 60); // 9 ~ 1001
                } else if (tradeSide == DataTypes.TradeSide.Sell) { // 10 ~ 1010
                    _value = _value | (10UL << 60);
                }
            }
            if (isTrade != null && isTrade.Value) {
                _value = _value | (12UL << 60); // 12 ~ 1100
            }
        }

        public Price(Price price, TradeSide? tradeSide = null, bool? isTrade = null) : this(price._value, tradeSide, isTrade) {
        }

        public Price(double value, int precision = 5) {
            if ((ulong)precision > 15) throw new ArgumentOutOfRangeException(nameof(precision));
            if (value > MantissaMask * DoubleFractions10[precision]) throw new ArgumentOutOfRangeException(nameof(value));
            var mantissaValue = (long)(value * Powers10[precision]);
            ulong mantissa = mantissaValue > 0 ? (ulong)mantissaValue : SignMask | (ulong)(-mantissaValue);
            _value = ((ulong)precision << 56) | mantissa;
        }

        public Price(int value) {
            long mantissaValue = value;
            ulong mantissa = mantissaValue > 0 ? (ulong)mantissaValue : SignMask | (ulong)(-mantissaValue);
            _value = mantissa;
        }

        // NB only decimal is implicit because it doesn't lose precision
        // there are no conversions to other direction, only ctor

        public static explicit operator double(Price price) {
            return price.Mantissa * DoubleFractions10[price.Exponent];
        }

        public static explicit operator float(Price price) {
            return (float)(price.Mantissa * DoubleFractions10[price.Exponent]);
        }

        public static implicit operator decimal(Price price) {
            return price.Mantissa * DecimalFractions10[price.Exponent];
        }

        public int CompareTo(Price other) {
            var c = (int)this.Exponent - (int)other.Exponent;
            if (c == 0) {
                return this.Mantissa.CompareTo(other.Mantissa);
            }
            if (c > 0) {
                return (this.Mantissa * Powers10[c]).CompareTo(other.Mantissa);
            } else {
                return this.Mantissa.CompareTo(other.Mantissa * Powers10[-c]);
            }
        }

        public bool Equals(Price other) {
            return this.CompareTo(other) == 0;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Price && Equals((Price)obj);
        }

        public static bool operator ==(Price x, Price y) {
            return x.Equals(y);
        }

        public static bool operator !=(Price x, Price y) {
            return !x.Equals(y);
        }

        public static bool operator >(Price x, Price y) {
            return x.CompareTo(y) > 0;
        }

        public static bool operator <(Price x, Price y) {
            return x.CompareTo(y) < 0;
        }

        public static bool operator >=(Price x, Price y) {
            return x.CompareTo(y) >= 0;
        }

        public static bool operator <=(Price x, Price y) {
            return x.CompareTo(y) <= 0;
        }

        public static Price operator +(Price x, Price y) {
            if (x.Exponent == y.Exponent) {
                return new Price((int)x.Exponent, (long)(x.Mantissa + y.Mantissa));
            }
            return new Price((decimal)x + (decimal)y, (int)Math.Max(x.Exponent, y.Exponent));
        }

        public static Price operator -(Price x, Price y) {
            if (x.Exponent == y.Exponent) {
                return new Price((int)x.Exponent, (long)(x.Mantissa - y.Mantissa));
            }
            return new Price((decimal)x - (decimal)y, (int)Math.Max(x.Exponent, y.Exponent));
        }

        public static Price operator *(Price x, int y) {
            return new Price((int)x.Exponent, (long)(x.Mantissa * y));
        }

        public override string ToString() {
            var asDecimal = (decimal)this;
            return asDecimal.ToString(CultureInfo.InvariantCulture);
        }
    }

    public class InvalidPriceException : Exception {
        public InvalidPriceException(string message) : base(message) { }
    }
}
