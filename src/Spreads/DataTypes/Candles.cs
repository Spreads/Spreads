// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Spreads.DataTypes
{
    // MN zero fields are almost free to store due to Blosc shuffling + compression

    /// <summary>
    /// A blittable structure for bars
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
    [Serialization(BlittableSize = 24)]
    [DataContract]
    public struct OHLCV
    {
        // Uses 8 + 4*4 = 24 instead of 4*16 + 4 = 68 bytes for decimals + int
        // NB do not change field order
        // Close goes first to keep 8-bytes alignment
        private readonly Price _close;

        private readonly int _open;
        private readonly int _high;
        private readonly int _low;
        private readonly int _volume;

        [DataMember(Order = 1)]
        public Price Open => new Price((int)_close.Exponent, _open + (long)_close.Mantissa);

        [DataMember(Order = 2)]
        public Price High => new Price((int)_close.Exponent, _high + (long)_close.Mantissa);

        [DataMember(Order = 3)]
        public Price Low => new Price((int)_close.Exponent, _low + (long)_close.Mantissa);

        [DataMember(Order = 4)]
        public Price Close => _close;

        [DataMember(Order = 5)]
        public int Volume => _volume;

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
                _open = (int)((long)open.Mantissa - (long)close.Mantissa);
                _high = (int)((long)high.Mantissa - (long)close.Mantissa);
                _low = (int)((long)low.Mantissa - (long)close.Mantissa);
                _volume = volume;
            }
        }

        public OHLCV(decimal open, decimal high, decimal low, decimal close, int volume = 0, int precision = 6)
            : this(new Price(open, precision), new Price(high, precision), new Price(low, precision), new Price(close, precision), volume) { }
    }

    /// <summary>
    /// A blittable structure for bars with volume and weighted average price
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
    [Serialization(BlittableSize = 48)]
    [DataContract]
    public struct OHLCFull
    {
        private readonly OHLCV _ohlcv; // 24
        private readonly int _tradeCount; // 28
        private readonly int _openInterest; // 32
        private readonly decimal _value; //48

        [DataMember(Order = 1)]
        public Price Open => _ohlcv.Open;
        [DataMember(Order = 2)]
        public Price High => _ohlcv.High;
        [DataMember(Order = 3)]
        public Price Low => _ohlcv.Low;
        [DataMember(Order = 4)]
        public Price Close => _ohlcv.Close;
        [DataMember(Order = 5)]
        public int Volume => _ohlcv.Volume;
        [DataMember(Order = 6)]
        public int TradeCount => _tradeCount;
        [DataMember(Order = 7)]
        public int OpenInterest => _openInterest;
        [DataMember(Order = 8)]
        public decimal MoneyVolume => _value;

        public OHLCV OHLCV => _ohlcv;

        public OHLCFull(Price open, Price high, Price low, Price close, int volume = 0, decimal moneyVolume = 0, int tradeCount = 0, int openInterest = 0)
        {
            _ohlcv = new OHLCV(open, high, low, close, volume);
            _value = new Price(moneyVolume, 5);
            _tradeCount = tradeCount;
            _openInterest = openInterest;
        }

        public OHLCFull(decimal open, decimal high, decimal low, decimal close, int volume = 0, decimal moneyVolume = 0, int tradeCount = 0, int openInterest = 0, int precision = 6)
        {
            _ohlcv = new OHLCV(open, high, low, close, volume, precision);
            _value = moneyVolume;
            _tradeCount = tradeCount;
            _openInterest = openInterest;
        }

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