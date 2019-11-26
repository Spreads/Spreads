// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable once CheckNamespace

using Spreads.Collections;
using Spreads.Deprecated;

namespace Spreads
{
    public static partial class Series
    {
        #region ContainerSeries

        public static Series<TKey, (TKey, TValue), RepeatWithKey<TKey, TValue, TCursor>> RepeatWithKey<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new RepeatWithKey<TKey, TValue, TCursor>(series.GetContainerCursor());
            return cursor.Source;
        }

        public static Series<TKey, TValue, Repeat<TKey, TValue, TCursor>> Repeat<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Repeat<TKey, TValue, TCursor>(series.GetContainerCursor());
            return cursor.Source;
        }

        #endregion ContainerSeries

        #region ISeries

        public static Series<TKey, (TKey, TValue), RepeatWithKey<TKey, TValue, Cursor<TKey, TValue>>> RepeatWithKey<TKey, TValue>(
            this ISeries<TKey, TValue> series)
        {
            var cursor = new RepeatWithKey<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor());
            return cursor.Source;
        }

        public static Series<TKey, TValue, Repeat<TKey, TValue, Cursor<TKey, TValue>>> Repeat<TKey, TValue>(
            this ISeries<TKey, TValue> series)
        {
            var cursor = new Repeat<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor());
            return cursor.Source;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, (TKey, TValue), RepeatWithKey<TKey, TValue, TCursor>> RepeatWithKey<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new RepeatWithKey<TKey, TValue, TCursor>(series.GetEnumerator());
            return cursor.Source;
        }

        public static Series<TKey, TValue, Repeat<TKey, TValue, TCursor>> Repeat<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Repeat<TKey, TValue, TCursor>(series.GetEnumerator());
            return cursor.Source;
        }

        #endregion Generic CursorSeries
    }
}