// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Spreads.DataTypes {

    /// <summary>
    /// A blittable structure to store ticks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
    [Serialization(BlittableSize = 24)]
    [DataContract]
    public struct Tick : IEquatable<Tick>, ITick {
        private readonly long _dateTimeUtcTicks;
        private readonly Quote _quote;

        /// <summary>
        /// UTC DateTime
        /// </summary>
        [DataMember(Order = 1, Name = "DateTime")]
        public DateTime DateTimeUtc => new DateTime(_dateTimeUtcTicks, DateTimeKind.Unspecified);

        [DataMember(Order = 2)]
        public Price Price => _quote.Price;

        [DataMember(Order = 3)]
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