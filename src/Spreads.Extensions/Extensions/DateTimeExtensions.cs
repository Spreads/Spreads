/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Spreads {
    public static class DateTimeExtensions {
        public static long ToInt64(this DateTime dt) {
            return dt.ToBinary(); // ((Int64)dt.Ticks | ((Int64)dt.Kind << 62)); for local, ToBinary() transforms local to UTC, for other types, it is slightly faster and does the same
        }

        public static DateTime ToDateTime(this long dt) {

            const ulong mask = (3UL << 62);
            ulong asUlong;
            unchecked {
                asUlong = (ulong)dt;
            }
            var cleared = (asUlong & mask);
            var kind = cleared >> 62;
            return new DateTime((long)(dt & ~(3L << 62)), (DateTimeKind)kind);

        }

        // TODO tests!!!

        private static readonly Dictionary<string, string> Normalizer = new Dictionary<string, string>();

        static DateTimeExtensions() {
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
        public static DateTime ConvertToUtcWithUncpecifiedKind(this DateTime dateTime, string tzFrom) {
            string tz;
            if (!Normalizer.TryGetValue(tzFrom.ToLowerInvariant(), out tz)) {
                tz = tzFrom;
            }
            //tz = tz.ToLowerInvariant();
            if (tz.ToLowerInvariant() == "utc") {
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
                    millis,
                    tickWithinMillis
                ).InZoneStrictly(givenTz); ;
            DateTime utcTime = timeToConvert.ToDateTimeUtc();
            return DateTime.SpecifyKind(utcTime, DateTimeKind.Unspecified);
        }

        /// Returns UTC DateTime with Kind.Unspecified
        public static DateTime ConvertToZoneWithUncpecifiedKind(this DateTime utcDateTime, string tzTo) {
            string tz;
            if (!Normalizer.TryGetValue(tzTo.ToLowerInvariant(), out tz)) {
                tz = tzTo;
            }
            //TODO!implement this
            //tz = tz.ToLowerInvariant();
            if (tz.ToLowerInvariant() == "utc") {
                if (utcDateTime.Kind == DateTimeKind.Local) throw new ArgumentException("Cannot treat local time as Utc, please specify kind = Utc or Uncpecified");
                return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Unspecified);
            }
            var utcTimeZone = DateTimeZoneProviders.Tzdb["UTC"];
            var givenTz = DateTimeZoneProviders.Tzdb[tz];

            var timeToConvert = new LocalDateTime(utcDateTime.Year, utcDateTime.Month, utcDateTime.Day, utcDateTime.Hour,
                utcDateTime.Minute, utcDateTime.Second, (int)(utcDateTime.Ticks % TimeSpan.TicksPerSecond)).InUtc();
            DateTime zonedTime = timeToConvert.WithZone(givenTz).ToDateTimeUnspecified();
            Debug.Assert(zonedTime.Kind == DateTimeKind.Unspecified);
            return zonedTime; //DateTime.SpecifyKind(utcTime, DateTimeKind.Unspecified);
        }
    }
}