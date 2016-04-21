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
    /// A blittable structure to store quotes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 12)]
    public struct Quote : IEquatable<Quote> {
        private readonly Price _price;
        private readonly int _volume;
        
        public Price Price => _price;
        public int Volume => _volume;

        public Quote(Price price, int volume) {
            _price = price;
            _volume = volume;
        }

        public bool Equals(Quote other) {
            return _price == other._price && _volume == other._volume;
        }

        public override bool Equals(object obj) {
            try {
                var otherQuote = (Quote)obj;
                return this.Equals(otherQuote);
            } catch (InvalidCastException) {
                return false;
            }
        }
    }

    /// <summary>
    /// A blittable structure to store ticks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 20)]
    public struct Tick : IEquatable<Tick> {
        private readonly long _dateTimeUtcTicks;
        private readonly Quote _quote;


        /// <summary>
        /// UTC DateTime 
        /// </summary>
        public DateTime DateTimeUtc => new DateTime(_dateTimeUtcTicks, DateTimeKind.Unspecified);
        public Price Price => _quote.Price;
        public int Volume => _quote.Volume;

        public Tick(DateTime dateTimeUtc, Price price, int volume) {
            // TODO (docs) need to document this behavior
            if (dateTimeUtc.Kind == DateTimeKind.Local) throw new ArgumentException(@"dateTime kind must be UTC or unspecified", nameof(dateTimeUtc));
            _dateTimeUtcTicks = dateTimeUtc.Ticks;
            _quote = new Quote(price, volume);

        }

        public bool Equals(Tick other) {
            return _dateTimeUtcTicks == other._dateTimeUtcTicks && _quote.Price == other.Price && _quote.Volume == other.Volume;
        }

        public override bool Equals(object obj) {
            try {
                var otherQuote = (Tick)obj;
                return this.Equals(otherQuote);
            } catch (InvalidCastException) {
                return false;
            }
        }
    }

}
