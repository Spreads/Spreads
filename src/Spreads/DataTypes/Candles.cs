// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Spreads.DataTypes
{
    // TODO TypeEnum

    // NB zero fields are almost free to store due to Blosc shuffling + compression

    /// <summary>
    /// A blittable structure for bars.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
    [DataContract]
    public struct OHLCV : IDelta<OHLCV>
    {
        // Uses 8 + 4*4 = 24 instead of 4*16 + 4 = 68 bytes for decimals + int
        // NB do not change field order
        // Close goes first to keep 8-bytes alignment
        internal Price _close;

        internal int _open;
        internal int _high;
        internal int _low;
        internal int _volume;

        /// <summary>
        /// Open.
        /// </summary>
        [DataMember(Order = 1)]
        public Price Open => new Price(_close.Exponent, _open + _close.Mantissa);

        /// <summary>
        /// High.
        /// </summary>
        [DataMember(Order = 2)]
        public Price High => new Price(_close.Exponent, _high + _close.Mantissa);

        /// <summary>
        /// Low.
        /// </summary>
        [DataMember(Order = 3)]
        public Price Low => new Price(_close.Exponent, _low + _close.Mantissa);

        /// <summary>
        /// Close.
        /// </summary>
        [DataMember(Order = 4)]
        public Price Close => _close;

        /// <summary>
        /// Volume.
        /// </summary>
        [DataMember(Order = 5)]
        public int Volume => _volume;

        /// <summary>
        /// OHLCV constructor.
        /// </summary>
        public OHLCV(Price open, Price high, Price low, Price close, int volume)
        {
            checked
            {
                // NB it is probably doable to adjust exponents, but
                // providing prices with different precision indicates
                // application logic flaw
                if (open.Exponent != high.Exponent || open.Exponent != low.Exponent || open.Exponent != close.Exponent)
                    throw new ArgumentException("Precision of all prices must be the same");
                _close = close;
                _open = (int)(open.Mantissa - close.Mantissa);
                _high = (int)(high.Mantissa - close.Mantissa);
                _low = (int)(low.Mantissa - close.Mantissa);
                _volume = volume;
            }
        }

        /// <summary>
        /// OHLCV constructor.
        /// </summary>
        public OHLCV(decimal open, decimal high, decimal low, decimal close, int volume = 0, int precision = 6)
            : this(new Price(open, precision), new Price(high, precision), new Price(low, precision), new Price(close, precision), volume) { }

        /// <inheritdoc />
        public OHLCV GetDelta(OHLCV next)
        {
            var newCandle = new OHLCV();
            // NB only Price diff matters, other fields already diffed in a different way
            // Volume diff changes sign often that is bad
            // This IDiffable impl reduces size by more than 2x, while
            // an implementation with all fields delta even slightly increases compressed size
            // probbaly due to frequent sign changes, which are bad for byteshuffling compression
            newCandle._close = next.Close - _close;
            return newCandle;
        }

        /// <inheritdoc />
        public OHLCV AddDelta(OHLCV delta)
        {
            var newCandle = new OHLCV();
            newCandle._close = delta.Close + _close;
            return newCandle;
        }
    }

    /// <summary>
    /// A blittable structure for bars with additional info.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
    [DataContract]
    public struct OHLCFull
    {
        internal readonly OHLCV _ohlcv; // 32
        internal readonly int _tradeCount; // 36
        internal readonly int _openInterest; // 40
        internal readonly decimal _value; //56

        /// <summary>
        /// Open.
        /// </summary>
        [DataMember(Order = 1)]
        public Price Open => _ohlcv.Open;

        /// <summary>
        /// High.
        /// </summary>
        [DataMember(Order = 2)]
        public Price High => _ohlcv.High;

        /// <summary>
        /// Low.
        /// </summary>
        [DataMember(Order = 3)]
        public Price Low => _ohlcv.Low;

        /// <summary>
        /// Close.
        /// </summary>
        [DataMember(Order = 4)]
        public Price Close => _ohlcv.Close;

        /// <summary>
        /// Volume.
        /// </summary>
        [DataMember(Order = 5)]
        public int Volume => _ohlcv.Volume;

        /// <summary>
        /// TradeCount.
        /// </summary>
        [DataMember(Order = 6)]
        public int TradeCount => _tradeCount;

        /// <summary>
        /// OpenInterest.
        /// </summary>
        [DataMember(Order = 7)]
        public int OpenInterest => _openInterest;

        /// <summary>
        /// MoneyVolume.
        /// </summary>
        [DataMember(Order = 8)]
        public decimal MoneyVolume => _value;

        /// <summary>
        /// OHLCV.
        /// </summary>
        public OHLCV OHLCV => _ohlcv;

        /// <summary>
        /// OHLCFull constructor.
        /// </summary>
        public OHLCFull(Price open, Price high, Price low, Price close, int volume = 0, decimal moneyVolume = 0M, int tradeCount = 0, int openInterest = 0)
        {
            _ohlcv = new OHLCV(open, high, low, close, volume);
            _value = moneyVolume;
            _tradeCount = tradeCount;
            _openInterest = openInterest;
        }

        /// <summary>
        /// OHLCFull constructor.
        /// </summary>
        public OHLCFull(decimal open, decimal high, decimal low, decimal close, int volume = 0, decimal moneyVolume = 0M, int tradeCount = 0, int openInterest = 0, int precision = 6)
        {
            _ohlcv = new OHLCV(open, high, low, close, volume, precision);
            _value = moneyVolume;
            _tradeCount = tradeCount;
            _openInterest = openInterest;
        }

        /// <summary>
        /// Convert OHLCVFull to OHLCV.
        /// </summary>
        public static implicit operator OHLCV(OHLCFull ohlcFull)
        {
            return ohlcFull.OHLCV;
        }

        // NB avoid implicit convertion to Price/decimal because we do not know if
        // corresponding DateTime in a series is for Open or Close
    }

    /// <summary>
    /// A blittable structure for bars with volume and weighted average price
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Candle
    {
        private readonly UnitPeriod _unitPeriod;
        private readonly int _periodLength;
        private readonly long _startDateTimeUtcTicks;
        private readonly OHLCFull _ohlcFull;
    }
}