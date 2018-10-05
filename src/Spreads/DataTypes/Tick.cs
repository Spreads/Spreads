// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

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
}