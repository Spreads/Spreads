// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;
using Spreads.Serialization.Utf8Json;

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
    /// <remarks>
    /// Price cannot be floating point, but decimal is slow and fat 16 bytes, plus such precision is not needed.
    /// Volume is in price units, 4B lots is a lot.
    /// Tag is often needed to store additional data.
    ///
    /// This struct could represent a single or aggregate order at level in an order book, tag allows to lookup additoinal
    /// info without polluting and slowing down order book. Tag is application-specific. When not used, it has zero cost
    /// when data is serialized and compressed. When is memory, it guarantees alignment of Price field that could make
    /// processing somewhat faster (at least in theory).
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
    [Serialization(BlittableSize = 16)]
    [JsonFormatter(typeof(Formatter))]
    public readonly struct Quote : IEquatable<Quote>, IQuote, IDelta<Quote>
    {
        private readonly Price _price;
        private readonly int _volume;
        internal readonly int _tag;

        /// <inheritdoc />
        public Price Price => _price;

        /// <inheritdoc />
        public int Volume => _volume;

        /// <summary>
        /// An Int32 value that serves as padding or could be used to store any custom data.
        /// </summary>
        public int Tag => _tag;

        /// <summary>
        /// Quote constructor.
        /// </summary>
        public Quote(Price price, int volume)
        {
            _price = price;
            _volume = volume;
            _tag = default;
        }

        public Quote(Price price, int volume, int tag)
        {
            _price = price;
            _volume = volume;
            _tag = tag;
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
            return new Quote(next.Price - Price, next.Volume, next._tag);
        }

        /// <inheritdoc />
        public Quote AddDelta(Quote delta)
        {
            return new Quote(Price + delta.Price, delta.Volume, delta._tag);
        }

        internal class Formatter : IJsonFormatter<Quote>
        {
            public void Serialize(ref JsonWriter writer, Quote value, IJsonFormatterResolver formatterResolver)
            {
                writer.WriteBeginArray();

                formatterResolver.GetFormatter<Price>().Serialize(ref writer, value.Price, formatterResolver);

                writer.WriteValueSeparator();

                writer.WriteInt32(value.Volume);

                if (value.Tag != default)
                {
                    writer.WriteValueSeparator();

                    writer.WriteInt32(value.Tag);
                }

                writer.WriteEndArray();
            }

            public Quote Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                reader.ReadIsBeginArrayWithVerify();

                var price = formatterResolver.GetFormatter<Price>().Deserialize(ref reader, formatterResolver);

                reader.ReadIsValueSeparatorWithVerify();

                var volume = reader.ReadInt32();

                var tag = 0;
                if (reader.ReadIsValueSeparator())
                {
                    tag = reader.ReadInt32();
                }
                reader.ReadIsEndArrayWithVerify();

                return new Quote(price, volume, tag);
            }
        }
    }
}
