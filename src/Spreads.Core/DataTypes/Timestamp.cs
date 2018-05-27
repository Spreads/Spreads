// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A Timestamp stored as nanoseconds since Unix epoch as Int64. Enough for 584 years.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    [Serialization(BlittableSize = 8)]
    public readonly struct Timestamp : IComparable<Timestamp>, IEquatable<Timestamp>
    {
        private static readonly long UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private readonly long _value;

        private Timestamp(long value)
        {
            Stopwatch.GetTimestamp();
            _value = value;
        }

        public DateTime DateTime => this;

        public static implicit operator DateTime(Timestamp timestamp)
        {
            return new DateTime(UnixEpoch + timestamp._value, DateTimeKind.Utc);
        }

        public static implicit operator Timestamp(DateTime dateTime)
        {
            var value = dateTime.ToUniversalTime().Ticks - UnixEpoch;
            return new Timestamp(value);
        }

        /// <inheritdoc />
        public int CompareTo(Timestamp other)
        {
            // ReSharper disable once ImpureMethodCallOnReadonlyValueField
            return _value.CompareTo(other._value);
        }

        public bool Equals(Timestamp other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Timestamp && Equals((Timestamp)obj);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}