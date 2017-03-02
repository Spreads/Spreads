// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


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
        public int Reserved => _reserved;

        public Quote(Price price, int volume) {
            _price = price;
            _volume = volume;
            _reserved = 0;
        }

        public Quote(Price price, int volume, int reserved) {
            _price = price;
            _volume = volume;
            _reserved = reserved;
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
