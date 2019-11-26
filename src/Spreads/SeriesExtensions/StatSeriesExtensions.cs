// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable once CheckNamespace

using Spreads.DataTypes;
using Spreads.Deprecated;
using Spreads.Utils;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static partial class Series
    {
        // TODO SMA must return double, current version could return wrong values.

        #region ContainerSeries

        public static Series<TKey, TValue, SMA<TKey, TValue, TCursor>> SMA<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, int count, bool allowIncomplete = false)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new SMA<TKey, TValue, TCursor>(series.GetContainerCursor(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, TValue, SMA<TKey, TValue, TCursor>> SMA<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, TKey width, Lookup lookup = Lookup.GE)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new SMA<TKey, TValue, TCursor>(series.GetContainerCursor(), width, lookup);
            return cursor.Source;
        }

        public static Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>> Stat2<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, int count, bool allowIncomplete = false)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Stat2Cursor<TKey, TValue, TCursor>(series.GetContainerCursor(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>> Stat2<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, TKey width, Lookup lookup = Lookup.GE)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Stat2Cursor<TKey, TValue, TCursor>(series.GetContainerCursor(), width, lookup);
            return cursor.Source;
        }

        public static Stat2<TKey> Stat2<TKey, TValue, TCursor>(this ContainerSeries<TKey, TValue, TCursor> series)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var stat = new Stat2<TKey>();
            using (var cursor = series.GetContainerCursor())
            {
                var first = true;
                while (cursor.MoveNext())
                {
                    if (first)
                    {
                        stat._start = cursor.CurrentKey;
                        first = false;
                    }
                    stat.AddValue(DoubleUtil.GetDouble(cursor.CurrentValue));
                }

                stat._end = cursor.CurrentKey;
                stat._endValue = DoubleUtil.GetDouble(cursor.CurrentValue);
            }

            return stat;
        }

        #endregion ContainerSeries

        #region ISeries

        public static Series<TKey, TValue, SMA<TKey, TValue, Cursor<TKey, TValue>>>
            SMA<TKey, TValue>(this ISeries<TKey, TValue> series, int count, bool allowIncomplete = false)
        {
            var cursor = new SMA<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, TValue, SMA<TKey, TValue, Cursor<TKey, TValue>>>
            SMA<TKey, TValue>(this ISeries<TKey, TValue> series, TKey width, Lookup lookup = Lookup.GE)
        {
            var cursor = new SMA<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), width, lookup);
            return cursor.Source;
        }

        public static Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, Cursor<TKey, TValue>>> Stat2<TKey, TValue>(
            this ISeries<TKey, TValue> series, int count, bool allowIncomplete = false)
        {
            var cursor = new Stat2Cursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, Cursor<TKey, TValue>>> Stat2<TKey, TValue>(
            this ISeries<TKey, TValue> series, TKey width, Lookup lookup = Lookup.GE)
        {
            var cursor = new Stat2Cursor<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), width, lookup);
            return cursor.Source;
        }

        public static Stat2<TKey> Stat2<TKey, TValue>(this ISeries<TKey, TValue> series)
        {
            var stat = new Stat2<TKey>();
            using (var cursor = series.GetSpecializedCursor())
            {
                var first = true;
                while (cursor.MoveNext())
                {
                    if (first)
                    {
                        stat._start = cursor.CurrentKey;
                        first = false;
                    }
                    stat.AddValue(DoubleUtil.GetDouble(cursor.CurrentValue));
                }

                stat._end = cursor.CurrentKey;
                stat._endValue = DoubleUtil.GetDouble(cursor.CurrentValue);
            }

            return stat;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, TValue, SMA<TKey, TValue, TCursor>> SMA<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, int count, bool allowIncomplete = false)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new SMA<TKey, TValue, TCursor>(series.GetEnumerator(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, TValue, SMA<TKey, TValue, TCursor>> SMA<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, TKey width, Lookup lookup = Lookup.GE)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new SMA<TKey, TValue, TCursor>(series.GetEnumerator(), width, lookup);
            return cursor.Source;
        }

        public static Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>> Stat2<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, int count, bool allowIncomplete = false)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Stat2Cursor<TKey, TValue, TCursor>(series.GetEnumerator(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, Stat2<TKey>, Stat2Cursor<TKey, TValue, TCursor>> Stat2<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, TKey width, Lookup lookup = Lookup.GE)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Stat2Cursor<TKey, TValue, TCursor>(series.GetEnumerator(), width, lookup);
            return cursor.Source;
        }

        public static Stat2<TKey> Stat2<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var stat = new Stat2<TKey>();
            using (var cursor = series.GetEnumerator())
            {
                var first = true;
                while (cursor.MoveNext())
                {
                    if (first)
                    {
                        stat._start = cursor.CurrentKey;
                        first = false;
                    }
                    stat.AddValue(DoubleUtil.GetDouble(cursor.CurrentValue));
                }

                stat._end = cursor.CurrentKey;
                stat._endValue = DoubleUtil.GetDouble(cursor.CurrentValue);
            }

            return stat;
        }

        #endregion Generic CursorSeries
    }
}