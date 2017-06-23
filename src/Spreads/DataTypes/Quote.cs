// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Spreads.DataTypes
{
    /// <summary>
    /// IQuote interface.
    /// </summary>
    public interface IQuote
    {
        /// <summary>
        /// Price.
        /// </summary>
        Price Price { get; }

        /// <summary>
        /// Volume.
        /// </summary>
        int Volume { get; }
    }

    /// <summary>
    /// A blittable structure to store quotes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
    [Serialization(BlittableSize = 16)]
    [DataContract]
    public struct Quote : IEquatable<Quote>, IQuote, IDelta<Quote>
    {
        private readonly Price _price;
        private readonly int _volume;
        internal int _reserved;

        /// <inheritdoc />
        [DataMember(Order = 1)]
        public Price Price => _price;

        /// <inheritdoc />
        [DataMember(Order = 2)]
        public int Volume => _volume;

        /// <summary>
        /// An Int32 value that serves as padding or could be used to store any custom data.
        /// </summary>
        public int Reserved => _reserved;

        /// <summary>
        /// Quote constructor.
        /// </summary>
        public Quote(Price price, int volume)
        {
            _price = price;
            _volume = volume;
            _reserved = 0;
        }

        public Quote(Price price, int volume, int reserved)
        {
            _price = price;
            _volume = volume;
            _reserved = reserved;
        }

        /// <inheritdoc />
        public bool Equals(Quote other)
        {
            return _price == other._price && _volume == other._volume;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is Quote quote)
            {
                return Equals(quote);
            }
            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public Quote GetDelta(Quote next)
        {
            return new Quote(next.Price - Price, next.Volume, next._reserved);
        }

        /// <inheritdoc />
        public Quote AddDelta(Quote delta)
        {
            return new Quote(Price + delta.Price, delta.Volume, delta._reserved);
        }
    }
}