// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Threading;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A Timestamp is stored as UTC nanos since Unix epoch.
    /// </summary>
    /// <remarks>
    /// Timestamp is used as a faster and more convenient replacement of <see cref="DateTime"/>
    /// in Spreads, because DateTime is very problematic data structure in .NET.
    ///
    /// <para />
    ///
    /// First, it has <see cref="DateTimeKind"/>
    /// property with a problematic <see cref="DateTimeKind.Local"/> value. When
    /// a value of this kind is serialized it doesn't have any meaning outside the serializer machine.
    /// <see cref="DateTimeKind.Utc"/> and <see cref="DateTimeKind.Unspecified"/> have different binary layout,
    /// but there is probably no meaningful way to treat unspecified kind other than UTC. The kind bits in
    /// <see cref="DateTime"/> add no value but make the same values binary incompatible.
    /// <see cref="Timestamp"/> time is always UTC and time zone information should be stored
    /// separately.
    ///
    /// <para />
    ///
    /// Second, <see cref="DateTime"/> structure has <see cref="LayoutKind.Auto"/>, even though internally
    /// it is just <see cref="ulong"/> and millions of code lines depend on its binary layout so that
    /// Microsoft is unlikely to change it ever. The auto layout makes <see cref="DateTime"/>  and
    /// every other structure that contains it as a field not blittable. <see cref="Timestamp"/> is blittable
    /// and very fast.
    ///
    /// <para />
    ///
    /// <see cref="Timestamp"/> is serialized to JSON as decimal string with seconds, e.g. "1552315408.792075401".
    /// This is one of the formats supported by <see href="https://www.npmjs.com/package/microtime">microtime</see>
    /// package. A value is stored as string to preserve precision, but in JavaScript it could be trivially
    /// converted to a number with plus sign `+"123456789.123456789"`. If you need to keep nanoseconds
    /// precision in JavaScript then split the string by dot and use two values separately.
    /// The Timestamp JSON deserializer supports decimal as a number as well as a string.
    ///
    /// <para />
    ///
    /// <see cref="TimeService"/> provides unique monotonic timestamps and could work across processes via shared memory.
    ///
    /// <para />
    /// Range:
    /// ```
    /// 2^63: 9,223,372,036,854,780,000
    /// Nanos per day: 86,400,000,000,000 (2^47)
    /// Nanos per year: 31,557,600,000,000,000 (2^55)
    /// 292 years of nanos in 2^63 is ought to be enough for everyone living now and their grand grand grand children.
    /// ```
    /// </remarks>
    /// <seealso cref="TimeService"/>
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    [BuiltInDataType(Size)]
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public readonly struct Timestamp : IComparable<Timestamp>, IEquatable<Timestamp>
    {
        public const int Size = 8;

        private static readonly long UnixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).Ticks;

        public static Timestamp Now => DateTime.UtcNow;

        /// <summary>
        /// Compared to Now.Seconds this uses `DateTime.Now.ToUniversalTime()` instead of `DateTime.UtcNow`.
        /// The former call is slightly faster but less precise.
        /// </summary>
        public static double NowSeconds => ((Timestamp)DateTime.Now.ToUniversalTime()).Seconds;

        private readonly long _nanos;

        public Timestamp(long nanos)
        {
            _nanos = nanos;
        }

        public DateTime DateTime => this;

        public long Nanos
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _nanos;
        }

        public long Micros
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _nanos / 1000;
        }

        public long Millis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _nanos / 1000_000;
        }

        public double Seconds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (double)_nanos / 1_000_000_000L;
        }

        /// <summary>
        /// Returns <see cref="TimeSpan"/> with nanoseconds *rounded up* to ticks.
        /// </summary>
        public TimeSpan TimeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // Due to TimeService implementation we often have small nanos,
                // but zero means equality. One tick is as small as one nanosecond for
                // most practical purposes when we do not work with nanosecond resolution.
                var ticks = _nanos / 100;
                if (ticks * 100 < _nanos)
                {
                    ticks++;
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
            // Need to use compare because subtraction will wrap
            // to positive for very large neg numbers, etc.
            if (_nanos < other._nanos) return -1;
            if (_nanos > other._nanos) return 1;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Timestamp other)
        {
            return _nanos == other._nanos;
        }

        public override bool Equals(object? obj)
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


    }
}
