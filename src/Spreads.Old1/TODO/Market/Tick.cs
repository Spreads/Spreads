// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization.Utf8Json;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Spreads.Native;

namespace Spreads.DataTypes
{
    // TODO TypeEnum, review if this type is needed at all, time is a key in series, non needed as a part of value.

    /// <summary>
    /// ITick interface.
    /// </summary>
    public interface ITick : IQuote
    {
        /// <summary>
        ///
        /// </summary>
        DateTime DateTimeUtc { get; }
    }

    /// <summary>
    /// A blittable structure to store ticks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
    [DataContract]
    public struct Tick : IEquatable<Tick>, ITick, IDelta<Tick>
    {
        private long _dateTimeUtcTicks;
        private Quote _quote;

        /// <summary>
        /// UTC DateTime
        /// </summary>
        [DataMember(Order = 1, Name = "DateTime")]
        public DateTime DateTimeUtc => new DateTime(_dateTimeUtcTicks, DateTimeKind.Unspecified);

        /// <inheritdoc />
        [DataMember(Order = 2)]
        public SmallDecimal Price => _quote.Price;

        /// <inheritdoc />
        [DataMember(Order = 3)]
        public int Volume => _quote.Volume;

        /// <summary>
        /// Tick constructor.
        /// </summary>
        public Tick(DateTime dateTimeUtc, SmallDecimal price, int volume)
        {
            // TODO (docs) need to document this behavior
            if (dateTimeUtc.Kind == DateTimeKind.Local) throw new ArgumentException(@"dateTime kind must be UTC or unspecified", nameof(dateTimeUtc));
            _dateTimeUtcTicks = dateTimeUtc.Ticks;
            _quote = new Quote(price, volume);
        }

        /// <inheritdoc />
        public bool Equals(Tick other)
        {
            return _dateTimeUtcTicks == other._dateTimeUtcTicks && _quote.Price == other.Price && _quote.Volume == other.Volume;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is Tick tick)
            {
                return Equals(tick);
            }
            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public Tick GetDelta(Tick next)
        {
            var newTick = new Tick();
            newTick._dateTimeUtcTicks = next._dateTimeUtcTicks - _dateTimeUtcTicks;
            var quoteDelta = _quote.GetDelta(next._quote);
            newTick._quote = quoteDelta;
            return newTick;
        }

        /// <inheritdoc />
        public Tick AddDelta(Tick delta)
        {
            var newTick = new Tick();
            newTick._dateTimeUtcTicks = delta._dateTimeUtcTicks + _dateTimeUtcTicks;
            var newDelta = _quote.AddDelta(delta._quote);
            newTick._quote = newDelta;
            return newTick;
        }
    }

    // TODO TypeEnum Tick with subtype SmallDecimal, need to learn to reuse the 4th byte in the header
    // TODO IDelta

    /// <summary>
    /// A blittable structure to store ticks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
    [BinarySerialization(blittableSize: 32)]
    [JsonFormatter(typeof(Formatter))]
    public readonly struct TickDecimal : IEquatable<TickDecimal>, IDelta<TickDecimal>
    {
        private const int DirectionOffset = 62;
        private const ulong TsMask = (1UL << DirectionOffset) - 1;
        private const ulong DirectionMask = 3UL << DirectionOffset;

        private readonly QuoteDecimal _quote;
        private readonly ulong _timestampDirection;
        private readonly long _data;

        public Timestamp Timestamp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Timestamp)(long)(_timestampDirection & TsMask);
        }

        public SmallDecimal Price
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _quote.Price;
        }

        public SmallDecimal Volume
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _quote.Volume;
        }

        public int Direction
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var directionBin = (_timestampDirection & DirectionMask) >> DirectionOffset;
                if (directionBin == 0b_01)
                {
                    return 1;
                }

                if (directionBin == 0b_10)
                {
                    return -1;
                }

                return 0;
            }
        }

        public long Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TickDecimal(Timestamp timestamp, SmallDecimal price, SmallDecimal volume, int direction = 0, long data = 0)
        {
            // TODO (low) try make this and Direction property branchless, this is the case with a lot of data and unpredictable value

            ulong directionBin = direction > 0
                ? (ulong)0b_01 << DirectionOffset
                : direction < 0
                    ? (ulong)0b_10 << DirectionOffset
                    : 0UL;

            var tsLong = (long)timestamp;

            if (tsLong <= 0)
            {
                ThrowHelper.ThrowArgumentException();
            }

            _timestampDirection = directionBin | (ulong)tsLong;
            _quote = new QuoteDecimal(price, volume);
            _data = data;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TickDecimal other)
        {
            return _timestampDirection == other._timestampDirection
                   && _quote.Equals(other._quote)
                   && _data == other._data;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is Tick tick)
            {
                return Equals(tick);
            }
            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public TickDecimal GetDelta(TickDecimal next)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public TickDecimal AddDelta(TickDecimal delta)
        {
            throw new NotImplementedException();
        }

        internal class Formatter : IJsonFormatter<TickDecimal>
        {
            public void Serialize(ref JsonWriter writer, TickDecimal value, IJsonFormatterResolver formatterResolver)
            {
                writer.WriteBeginArray();

                formatterResolver.GetFormatter<Timestamp>().Serialize(ref writer, value.Timestamp, formatterResolver);

                writer.WriteValueSeparator();

                formatterResolver.GetFormatter<SmallDecimal>().Serialize(ref writer, value.Price, formatterResolver);

                writer.WriteValueSeparator();

                formatterResolver.GetFormatter<SmallDecimal>().Serialize(ref writer, value.Volume, formatterResolver);

                writer.WriteValueSeparator();

                writer.WriteInt32(value.Direction);

                writer.WriteValueSeparator();

                writer.WriteInt64(value.Data);

                writer.WriteEndArray();
            }

            public TickDecimal Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                reader.ReadIsBeginArrayWithVerify();

                var timestamp = formatterResolver.GetFormatter<Timestamp>().Deserialize(ref reader, formatterResolver);

                reader.ReadIsValueSeparatorWithVerify();

                var price = formatterResolver.GetFormatter<SmallDecimal>().Deserialize(ref reader, formatterResolver);

                reader.ReadIsValueSeparatorWithVerify();

                var volume = formatterResolver.GetFormatter<SmallDecimal>().Deserialize(ref reader, formatterResolver);

                reader.ReadIsValueSeparatorWithVerify();

                var direction = reader.ReadInt32();

                reader.ReadIsValueSeparatorWithVerify();

                var data = reader.ReadInt64();

                reader.ReadIsEndArrayWithVerify();

                return new TickDecimal(timestamp, price, volume, direction, data);
            }
        }
    }
}
