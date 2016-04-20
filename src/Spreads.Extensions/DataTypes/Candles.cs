/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes {

    /// <summary>
    /// A blittable structure for bars
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 20)]
    public struct OHLC {
        // Uses 3*4 + 8 = 20 instead of 4*16 = 64 bytes for decimal
        private readonly int _open;
        private readonly int _high;
        private readonly int _low;
        private readonly Price _close;

        public Price Open => new Price((int)_close.Exponent, _open + (long)_close.Mantissa);
        public Price High => new Price((int)_close.Exponent, _high + (long)_close.Mantissa);
        public Price Low => new Price((int)_close.Exponent, _low + (long)_close.Mantissa);
        public Price Close => _close;

        public OHLC(Price open, Price high, Price low, Price close) {
            checked {
                // NB it is probably doable to adjust exponents, but 
                // providing prices with different precision indicates 
                // application logic flaw
                if (open.Exponent != high.Exponent || open.Exponent != low.Exponent || open.Exponent != close.Exponent)
                    throw new ArgumentException("Precision of all prices must be the same");
                _close = close;
                _open = (int)((long)open.Mantissa - (long)close.Mantissa);
                _high = (int)((long)high.Mantissa - (long)close.Mantissa);
                _low = (int)((long)low.Mantissa - (long)close.Mantissa);
            }
        }

        public OHLC(decimal open, decimal high, decimal low, decimal close, int precision = 6)
            : this(new Price(open, precision), new Price(high, precision), new Price(low, precision), new Price(close, precision)) { }
    }

    /// <summary>
    /// A blittable structure for bars with volume
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
    public struct OHLCVol {
        private readonly OHLC _ohlc;
        private readonly int _volume;

        public Price Open => _ohlc.Open;
        public Price High => _ohlc.High;
        public Price Low => _ohlc.Low;
        public Price Close => _ohlc.Close;
        public int Volume => _volume;

        public OHLCVol(Price open, Price high, Price low, Price close, int volume = 0) {
            _ohlc = new OHLC(open, high, low, close);
            _volume = volume;
        }

        public OHLCVol(decimal open, decimal high, decimal low, decimal close, int volume = 0, int precision = 6) {
            _ohlc = new OHLC(open, high, low, close, precision);
            _volume = volume;
        }
    }

    /// <summary>
    /// A blittable structure for bars with volume and weighted average price
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct OHLCFull {
        private readonly OHLCVol _ohlcVol;
        private readonly double _vWap;

        private readonly int _tradeCount;
        private readonly int _openInterest;

        public Price Open => _ohlcVol.Open;
        public Price High => _ohlcVol.High;
        public Price Low => _ohlcVol.Low;
        public Price Close => _ohlcVol.Close;
        public int Volume => _ohlcVol.Volume;

        public OHLCFull(Price open, Price high, Price low, Price close, int volume = 0, double weightedAveragePrice = 0, int tradeCount = 0, int openInterest = 0) {
            _ohlcVol = new OHLCVol(open, high, low, close, volume);
            _vWap = weightedAveragePrice;
            _tradeCount = tradeCount;
            _openInterest = openInterest;
        }

        public OHLCFull(decimal open, decimal high, decimal low, decimal close, int volume = 0, double weightedAveragePrice = 0, int tradeCount = 0, int openInterest = 0, int precision = 6) {
            _ohlcVol = new OHLCVol(open, high, low, close, volume, precision);
            _vWap = weightedAveragePrice;
            _tradeCount = tradeCount;
            _openInterest = openInterest;
        }
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
