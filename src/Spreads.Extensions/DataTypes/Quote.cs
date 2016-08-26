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

    public interface IQuote
    {
        Price Price { get; }
        int Volume { get; }
    }

    public interface ITick : IQuote {
        DateTime DateTimeUtc { get; }
    }

    /// <summary>
    /// A blittable structure to store quotes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
    public struct Quote : IEquatable<Quote>, IQuote {
        private readonly Price _price;
        private readonly int _volume;
        private readonly int _reserved; // padding
        
        public Price Price => _price;
        public int Volume => _volume;

        public Quote(Price price, int volume) {
            _price = price;
            _volume = volume;
            _reserved = 0;
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
}
