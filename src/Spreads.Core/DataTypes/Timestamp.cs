// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    // TODO JS serialization: [seconds, nanos] or [millis, nanos] or just float?

    /// <summary>
    /// A Timestamp stored as nanos since Unix epoch as Int64.
    /// 2^63: 9,223,372,036,854,780,000
    /// Nanos per day: 86,400,000,000,000 (2^47)
    /// Nanos per year: 31,557,600,000,000,000 (2^55)
    /// 292 years of nanos in 2^63 is ought to be enough for everyone (except JavaScript).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [Serialization(BlittableSize = 8)]
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [JsonFormatter(typeof(Formatter))]
    public readonly struct Timestamp : IComparable<Timestamp>, IEquatable<Timestamp>
    {
        public const int Size = 8;

        private static readonly long UnixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private readonly long _nanos;

        public Timestamp(long nanos)
        {
            // TODO it is possible to detect micros and millis if we limit epoch to > 1972
            // However, in JS multiplying my 1000 does not loses precision
            _nanos = nanos;
        }

        public DateTime DateTime => this;

        public long Nanos
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _nanos; }
        }

        public TimeSpan TimeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // Due to TimeService implementation we often have small nanos,
                // but zero means equality. One tick is as small as one nano for
                // most practical purposes when we do not work with nano resolution.
                var ticks = Nanos / 100;
                if (ticks == 0 && Nanos > 0)
                {
                    ticks = 1;
                }
                return new TimeSpan(ticks);
            }
        }

        public static implicit operator DateTime(Timestamp timestamp)
        {
            return new DateTime(UnixEpochTicks + timestamp._nanos / 100, DateTimeKind.Utc);
        }

        public static implicit operator Timestamp(DateTime dateTime)
        {
            Debug.Assert(dateTime.Kind != DateTimeKind.Local, "DateTime for Timestamp conversion is assumed to be UTC, got Local");
            var value = (dateTime.Ticks - UnixEpochTicks) * 100;
            return new Timestamp(value);
        }

        public static explicit operator long(Timestamp timestamp)
        {
            return timestamp._nanos;
        }

        public static explicit operator Timestamp(long nanos)
        {
            return new Timestamp(nanos);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(Timestamp other)
        {
            return _nanos.CompareTo(other._nanos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Timestamp other)
        {
            return _nanos == other._nanos;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is Timestamp timestamp && Equals(timestamp);
        }

        public override int GetHashCode()
        {
            return _nanos.GetHashCode();
        }

        public override string ToString()
        {
            return ((DateTime)this).ToString("O");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Timestamp operator -(Timestamp x)
        {
            return new Timestamp(-x._nanos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Timestamp operator -(Timestamp x, Timestamp y)
        {
            return new Timestamp(checked(x._nanos - y._nanos));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Timestamp operator +(Timestamp x, Timestamp y)
        {
            return new Timestamp(checked(x._nanos + y._nanos));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Timestamp x, Timestamp y)
        {
            return x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Timestamp x, Timestamp y)
        {
            return !x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Timestamp x, Timestamp y)
        {
            return x.CompareTo(y) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Timestamp x, Timestamp y)
        {
            return x.CompareTo(y) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(Timestamp x, Timestamp y)
        {
            return x.CompareTo(y) >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(Timestamp x, Timestamp y)
        {
            return x.CompareTo(y) <= 0;
        }

        internal class Formatter : IJsonFormatter<Timestamp>
        {
            // NB we use WriteInt64/ReadInt64 directly in types that have TS as a member.
            // If we decide to chenge this layout then those types should resolve to this formatter

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Serialize(ref JsonWriter writer, Timestamp value, IJsonFormatterResolver formatterResolver)
            {
                writer.WriteInt64((long)value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Timestamp Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                var timestamp = reader.ReadInt64();
                return (Timestamp)timestamp;
            }
        }
    }
}