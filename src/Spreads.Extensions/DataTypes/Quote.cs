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
    /// A blittable structure to store quotes or ticks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 20)]
    public struct Quote : IEquatable<Quote> {
        private readonly long _dateTimeUtcTicks;
        private readonly Price _price;
        private readonly int _amount;
        

        /// <summary>
        /// UTC DateTime 
        /// </summary>
        public DateTime DateTimeUtc => new DateTime(_dateTimeUtcTicks, DateTimeKind.Unspecified);
        public Price Price => _price;
        public int Amount => _amount;

        public Quote(DateTime dateTimeUtc, Price price, int amount) {
            // TODO (docs) need to document this behavior
            if (dateTimeUtc.Kind == DateTimeKind.Local) throw new ArgumentException(@"dateTime kind must be UTC or unspecified", nameof(dateTimeUtc));
            _dateTimeUtcTicks = dateTimeUtc.Ticks;
            _price = price;
            _amount = amount;
        }

        public bool Equals(Quote other) {
            return _dateTimeUtcTicks == other._dateTimeUtcTicks && _price == other._price && _amount == other._amount;
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
}
