// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NodaTime;
using Spreads.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Spreads.Deprecated;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static class DateTimeExtensions
    {
        public static long ToInt64(this DateTime dt)
        {
            return dt.ToBinary(); // ((Int64)dt.Ticks | ((Int64)dt.Kind << 62)); for local, ToBinary() transforms local to UTC, for other types, it is slightly faster and does the same
        }

        public static DateTime ToDateTime(this long dt)
        {
            const ulong mask = (3UL << 62);
            ulong asUlong;
            unchecked
            {
                asUlong = (ulong)dt;
            }
            var cleared = (asUlong & mask);
            var kind = cleared >> 62;
            return new DateTime((long)(dt & ~(3L << 62)), (DateTimeKind)kind);
        }

        // TODO tests!!!

        private static readonly Dictionary<string, string> Normalizer = new Dictionary<string, string>();

        static DateTimeExtensions()
        {
            // Add all shortcuts that are deemed convenient
            Normalizer.Add("", "UTC");
            Normalizer.Add("moscow", "Europe/Moscow");
            Normalizer.Add("moex", "Europe/Moscow");
            Normalizer.Add("ru", "Europe/Moscow");
            Normalizer.Add("newyork", "America/New_York");
            Normalizer.Add("ny", "America/New_York");
            Normalizer.Add("nyse", "America/New_York");
            Normalizer.Add("nasdaq", "America/New_York");
            Normalizer.Add("chicago", "America/Chicago");
            Normalizer.Add("london", "Europe/London");
            Normalizer.Add("lse", "Europe/London");
            Normalizer.Add("ice", "Europe/London");
            Normalizer.Add("uk", "Europe/London");
            Normalizer.Add("gb", "Europe/London");
            Normalizer.Add("cme", "America/Chicago");
        }

        /// Returns UTC DateTime with Kind.Unspecified
        public static DateTime ConvertToUtcWithUncpecifiedKind(this DateTime dateTime, string tzFrom)
        {
            string tz;
            if (!Normalizer.TryGetValue(tzFrom.ToLowerInvariant(), out tz))
            {
                tz = tzFrom;
            }
            //tz = tz.ToLowerInvariant();
            if (tz.ToLowerInvariant() == "utc")
            {
                if (dateTime.Kind == DateTimeKind.Local) throw new ArgumentException("Cannot treat local time as Utc, please specify kind = Utc or Uncpecified");
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            }
            var utcTimeZone = DateTimeZoneProviders.Tzdb["UTC"];
            var givenTz = DateTimeZoneProviders.Tzdb[tz];
            var tickWithinSecond = (int)(dateTime.Ticks % TimeSpan.TicksPerSecond);
            var millis = tickWithinSecond / 10000;
            var tickWithinMillis = tickWithinSecond % 10000;

            var timeToConvert = new LocalDateTime(dateTime.Year,
                    dateTime.Month,
                    dateTime.Day,
                    dateTime.Hour,
                    dateTime.Minute,
                    dateTime.Second,
                    millis).PlusTicks(tickWithinMillis).InZoneStrictly(givenTz); ;
            DateTime utcTime = timeToConvert.ToDateTimeUtc();
            return DateTime.SpecifyKind(utcTime, DateTimeKind.Unspecified);
        }

        /// Returns UTC DateTime with Kind.Unspecified
        public static DateTime ConvertToZoneWithUncpecifiedKind(this DateTime utcDateTime, string tzTo)
        {
            string tz;
            if (!Normalizer.TryGetValue(tzTo.ToLowerInvariant(), out tz))
            {
                tz = tzTo;
            }
            //TODO!implement this
            //tz = tz.ToLowerInvariant();
            if (tz.ToLowerInvariant() == "utc")
            {
                if (utcDateTime.Kind == DateTimeKind.Local) throw new ArgumentException("Cannot treat local time as Utc, please specify kind = Utc or Uncpecified");
                return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Unspecified);
            }
            var utcTimeZone = DateTimeZoneProviders.Tzdb["UTC"];
            var givenTz = DateTimeZoneProviders.Tzdb[tz];

            var timeToConvert = new LocalDateTime(utcDateTime.Year, utcDateTime.Month, utcDateTime.Day, utcDateTime.Hour,
                utcDateTime.Minute, utcDateTime.Second, utcDateTime.Millisecond).PlusTicks((int)(utcDateTime.Ticks % TimeSpan.TicksPerMillisecond)).InUtc();
            DateTime zonedTime = timeToConvert.WithZone(givenTz).ToDateTimeUnspecified();
            Debug.Assert(zonedTime.Kind == DateTimeKind.Unspecified);
            return zonedTime; //DateTime.SpecifyKind(utcTime, DateTimeKind.Unspecified);
        }

        public static DateTime ToTimeFrameStart(this DateTime moment, UnitPeriod unitPeriod, uint length = 1)
        {
            // TODO (low): support offset, e.g. 1 hours that starts at 15th minute

            if (length == 0) throw new InvalidOperationException("Length is zero");
            long divisor;
            switch (unitPeriod)
            {
                case UnitPeriod.Tick:
                    if (length != 1) throw new InvalidOperationException("Tick length != 1 is meaningless");
                    return moment;

                case UnitPeriod.Millisecond:
                    divisor = TimeSpan.TicksPerMillisecond * length;
                    return new DateTime((moment.Ticks / (divisor)) * (divisor), moment.Kind);

                case UnitPeriod.Second:
                    divisor = TimeSpan.TicksPerSecond * length;
                    return new DateTime((moment.Ticks / (divisor)) * (divisor), moment.Kind);

                case UnitPeriod.Minute:
                    divisor = TimeSpan.TicksPerMinute * length;
                    return new DateTime((moment.Ticks / (divisor)) * (divisor), moment.Kind);

                case UnitPeriod.Hour:
                    divisor = TimeSpan.TicksPerHour * length;
                    return new DateTime((moment.Ticks / (divisor)) * (divisor), moment.Kind);

                case UnitPeriod.Day:
                    divisor = TimeSpan.TicksPerDay * length;
                    return new DateTime((moment.Ticks / (divisor)) * (divisor), moment.Kind);

                case UnitPeriod.Month:
                    if (length != 1) throw new NotSupportedException();
                    return new DateTime(moment.Year, moment.Month, 1, 0, 0, 0, moment.Kind);

                case UnitPeriod.Eternity:
                    return DateTime.MinValue;

                default:
                    throw new ArgumentOutOfRangeException(nameof(unitPeriod), unitPeriod, null);
            }
        }

        /// <summary>
        /// Get history of offsets with keys as UTC time. Used to convert from UTC to zoned time.
        /// </summary>
        /// <returns></returns>
        public static Series<DateTime, long> GetOffsetsFromUtc(string tzFrom, bool standardOffsetOnly = false)
        {
            string tz;
            if (!Normalizer.TryGetValue(tzFrom.ToLowerInvariant(), out tz))
            {
                tz = tzFrom;
            }
            var sortedMap = new Series<DateTime, long>();
            if (tz.ToLowerInvariant() == "utc")
            {
                sortedMap.Set(new DateTime(0L, DateTimeKind.Utc), 0);
            }
            else
            {
                var givenTz = DateTimeZoneProviders.Tzdb[tz];

                var intervals = givenTz.GetZoneIntervals(Instant.FromDateTimeUtc(
                        // https://en.wikipedia.org/wiki/International_Meridian_Conference
                        new DateTime(1884, 10, 22, 12, 0, 0, DateTimeKind.Utc)
                    ), Instant.MaxValue);
                foreach (var interval in intervals)
                {
                    var intervalStart = interval.Start.ToDateTimeUtc();
                    var offset = standardOffsetOnly ? interval.StandardOffset : interval.WallOffset;
                    var offsetTicks = offset.Ticks;
                    sortedMap.TryAddLast(intervalStart, offsetTicks);
                }
            }
            sortedMap.Complete();
            return sortedMap;
        }

        /// <summary>
        /// Get history of offsets with keys as zoned time. Used to convert from zoned to UTC time.
        /// </summary>
        /// <returns></returns>
        public static Series<DateTime, long> GetOffsetsFromZoned(string tzFrom, bool standardOffsetOnly = false)
        {
            string tz;
            if (!Normalizer.TryGetValue(tzFrom.ToLowerInvariant(), out tz))
            {
                tz = tzFrom;
            }
            var sortedMap = new Series<DateTime, long>();
            if (tz.ToLowerInvariant() == "utc")
            {
                sortedMap.Set(new DateTime(0L, DateTimeKind.Unspecified), 0);
            }
            else
            {
                var givenTz = DateTimeZoneProviders.Tzdb[tz];
                var intervals = givenTz.GetZoneIntervals(Instant.FromDateTimeUtc(
                    // https://en.wikipedia.org/wiki/International_Meridian_Conference
                    new DateTime(1884, 10, 22, 12, 0, 0, DateTimeKind.Utc)
                ), Instant.MaxValue);
                foreach (var interval in intervals)
                {
                    var localStart = interval.IsoLocalStart.ToDateTimeUnspecified();
                    var offset = standardOffsetOnly ? interval.StandardOffset : interval.WallOffset;
                    var offsetTicks = offset.Ticks;
                    sortedMap.TryAddLast(localStart, offsetTicks);
                }
            }
            sortedMap.Complete();
            return sortedMap;
        }
    }
}