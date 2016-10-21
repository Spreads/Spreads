// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spreads.DataTypes {
    
    // 4 bytes alternatives to DateTime and TimeSpan when we need only a part of them

    // NB Remeber that we need these for untyped access, e.g. when other side of 
    // interop knows nothing and need to present at least something from data,
    // e.g. at least print

    /// <summary>
    /// Number of days since zero.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
    public struct Date {
        private readonly int _value;

        public Date(DateTime datetime) {
            _value = (int)(datetime.Ticks / TimeSpan.TicksPerDay);
        }

        public static explicit operator DateTime(Date date) {
            return new DateTime(date._value * TimeSpan.TicksPerDay, DateTimeKind.Unspecified);
        }

        public static explicit operator Date(DateTime datetime) {
            return new Date(datetime);
        }

        public override string ToString() {
            return ((DateTime)this).ToString("yyyy-MM-dd");
        }

        public string ToString(string format) {
            return ((DateTime)this).ToString(format);
        }
    }

    /// <summary>
    /// Number of milliseconds in a day
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
    public struct Time {
        private readonly int _value;

        public Time(DateTime datetime) {
            _value = (int)((datetime.Ticks % TimeSpan.TicksPerDay) / TimeSpan.TicksPerMillisecond);
        }

        public Time(TimeSpan timespan) {
            _value = (int)(timespan.Ticks / TimeSpan.TicksPerMillisecond);
        }

        public static explicit operator TimeSpan(Time time) {
            return new TimeSpan(time._value * TimeSpan.TicksPerMillisecond);
        }

        public static explicit operator Time(TimeSpan timespan) {
            return new Time(timespan);
        }

        public override string ToString() {
            return ((TimeSpan)this).ToString("hh:mm:ss.fff");
        }

        public string ToString(string format) {
            return ((TimeSpan)this).ToString(format);
        }
    }
}
