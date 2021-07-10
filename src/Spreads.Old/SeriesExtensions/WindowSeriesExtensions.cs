﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable once CheckNamespace

using Spreads.Deprecated;

namespace Spreads
{
    public static partial class Series
    {
        #region ContainerSeries

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>> Window<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, int count, bool allowIncomplete = false)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Window<TKey, TValue, TCursor>(series.GetContainerCursor(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>> Window<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, TKey width, Lookup lookup = Lookup.GE)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Window<TKey, TValue, TCursor>(series.GetContainerCursor(), width, lookup);
            return cursor.Source;
        }

        #endregion ContainerSeries

        #region ISeries

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, Cursor<TKey, TValue>>>, Window<TKey, TValue, Cursor<TKey, TValue>>>
            Window<TKey, TValue>(this ISeries<TKey, TValue> series, int count, bool allowIncomplete = false)
        {
            var cursor = new Window<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), count, allowIncomplete);
            return cursor.Source;
        }

        public static ISeries<TKey, ISeries<TKey, TValue>> Window_<TKey, TValue>(this ISeries<TKey, TValue> series, int count, bool allowIncomplete = false)
        {
            var cursor = new Window<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), count, allowIncomplete);
            var untyped = (ISeries<TKey, ISeries<TKey, TValue>>)cursor.Source.Map(x => (ISeries<TKey, TValue>)x);
            return untyped;
        }

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, Cursor<TKey, TValue>>>, Window<TKey, TValue, Cursor<TKey, TValue>>>
            Window<TKey, TValue>(this ISeries<TKey, TValue> series, TKey width, Lookup lookup = Lookup.GE)
        {
            var cursor = new Window<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), width, lookup);
            return cursor.Source;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>> Window<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, int count, bool allowIncomplete = false)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Window<TKey, TValue, TCursor>(series.GetEnumerator(), count, allowIncomplete);
            return cursor.Source;
        }

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>> Window<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, TKey width, Lookup lookup = Lookup.GE)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Window<TKey, TValue, TCursor>(series.GetEnumerator(), width, lookup);
            return cursor.Source;
        }

        #endregion Generic CursorSeries
    }
}