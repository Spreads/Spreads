using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Spreads
{
    public static class DateTimeExtensions
    {
        public static long ToInt64(this DateTime dt)
        {
            return ((Int64)dt.Ticks | ((Int64)dt.Kind << 62));
        }

        public static DateTime ToDateTime(this long dt) {
            return new DateTime((long)(dt & ~(3L << 62)), (DateTimeKind)(((ulong)dt) >> 62));
        }

        // TODO tests!!!

        private static Dictionary<string, string> _normalizer = new Dictionary<string, string>();

        static DateTimeExtensions() {
            // Add all shortcuts that are deeemed convenient
            _normalizer.Add("", "UTC");
            _normalizer.Add("moscow", "Europe/Moscow");
            _normalizer.Add("moex", "Europe/Moscow");
            _normalizer.Add("ru", "Europe/Moscow");
            _normalizer.Add("newyork", "America/New_York");
            _normalizer.Add("ny", "America/New_York");
            _normalizer.Add("nyse", "America/New_York");
            _normalizer.Add("nasdaq", "America/New_York");
            _normalizer.Add("chicago", "America/Chicago");
            _normalizer.Add("london", "Europe/London");
            _normalizer.Add("lse", "Europe/London");
            _normalizer.Add("ice", "Europe/London");
            _normalizer.Add("uk", "Europe/London");
            _normalizer.Add("gb", "Europe/London");
            _normalizer.Add("cme", "America/Chicago");
        }

        /// Returns UTC DateTime with Kind.Unspecified
        public static DateTime ConvertToUtcWithUncpecifiedKind(this DateTime dateTime, string tzFrom) {
            string tz;
            if (!_normalizer.TryGetValue(tzFrom.ToLowerInvariant(), out tz)) {
                tz = tzFrom;
            }
            //tz = tz.ToLowerInvariant();
            if (tz.ToLowerInvariant() == "utc") {
                if (dateTime.Kind == DateTimeKind.Local) throw new ArgumentException("Cannot treat local time as Utc, please specify kind = Utc or Uncpecified");
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            }
            var utcTimeZone = DateTimeZoneProviders.Tzdb["UTC"];
            var givenTz = DateTimeZoneProviders.Tzdb[tz];

            var timeToConvert = new LocalDateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour,
                dateTime.Minute, dateTime.Second, (int)(dateTime.Ticks % TimeSpan.TicksPerSecond)).InZoneStrictly(givenTz); ;
            DateTime utcTime = timeToConvert.ToDateTimeUtc();
            return DateTime.SpecifyKind(utcTime, DateTimeKind.Unspecified);
        }

        /// Returns UTC DateTime with Kind.Unspecified
        public static DateTime ConvertToZoneWithUncpecifiedKind(this DateTime utcDateTime, string tzTo) {
            string tz;
            if (!_normalizer.TryGetValue(tzTo.ToLowerInvariant(), out tz)) {
                tz = tzTo;
            }
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