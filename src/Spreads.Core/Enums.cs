// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;

namespace Spreads
{
    

    /// <summary>
    /// Indicates where a tick/instant is relative to a period.
    /// </summary>
    public enum PeriodTick : byte
    {
        // NB it is effectively 2 bits if ever need to pack densely

        /// <summary>
        /// Point in time. E.g. close price is instant even though it could be regular (every second).
        /// </summary>
        PointInTime = 0, // 00

        /// <summary>
        /// End of period.
        /// </summary>
        EndOfPeriod = 1, // 01 - 1 at the end

        /// <summary>
        /// Start of period.
        /// </summary>
        StartOfPeriod = 2, // 10 - 1 at the start

        /// <summary>
        /// Entire period.
        /// </summary>
        [Obsolete("Review, does it make any sense? Need an example.")]
        EntirePeriod = 3 // 11 - both bits are set
    }

    // UnitPeriod is 3 bits, 3 more left - but these bit tricks are irrelevant since we do not store
    // this info per each value

    /// <summary>
    /// Base unit of a period
    /// </summary>
    public enum UnitPeriod : byte
    {
        Tick = 0,          //               100 nanosec
        Millisecond = 1,   //              10 000 ticks
        Second = 2,        //          10 000 000 ticks
        Minute = 3,        //         600 000 000 ticks
        Hour = 4,          //      36 000 000 000 ticks
        Day = 5,           //     864 000 000 000 ticks
        Month = 6,         //                  Variable

        /// <summary>
        /// Static or constant
        /// </summary>
        Eternity = 7,      //                  Infinity
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct PeriodInfo
    {
        [FieldOffset(0)]
        public short PeriodLength;

        [FieldOffset(2)]
        public UnitPeriod UnitPeriod;

        [FieldOffset(3)]
        public PeriodTick PeriodTick;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct TimeSeriesInfo
    {
        /// <summary>
        /// When set to true, undefined (null) TZ value cannot be used with other time zones.
        /// Binary false values assumes that users know what they are doing and series with undefined
        /// time zones could be used with series with any time zone.
        /// </summary>
        public static bool StrictUtc { get; set; }

        public PeriodInfo PeriodInfo { get; set; }

        /// <summary>
        /// Time zone in <see href="https://en.wikipedia.org/wiki/Tz_database">tz database</see> format.
        /// </summary>
        /// <remarks>Null is undefined.</remarks>
        public string TZ { get; set; }
    }

    // TODO Instead of enum this could be a big struct. Known keys mostly correspond to storage
    internal enum KnownMetadataKeys
    {
        TimeZone,
        UnitPeriod,
        PeriodLength,
        Expression
    }
}