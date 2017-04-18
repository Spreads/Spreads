// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using Spreads.Cursors;
using System;

namespace Spreads
{
    public static partial class Series
    {
        // Even if this works, avoid public extensions on generic types
        internal static MapValuesSeries<TKey, TValue, TResult, T> Map<T, TKey, TValue, TResult>(this T series,
            Func<TValue, TResult> selector)
            where T : CursorSeries<TKey, TValue, T>, ICursor<TKey, TValue>
        {
            return new MapValuesSeries<TKey, TValue, TResult, T>(series, selector);
        }

        public static MapValuesSeries<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>> Map<TKey, TValue, TResult>(this SortedMap<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            return new MapValuesSeries<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>(series, selector);
        }

        public static MapValuesSeries<TKey, TSource, TResult, TCursor> Map<TKey, TSource, TValue, TResult, TCursor>(
            this MapValuesSeries<TKey, TSource, TValue, TCursor> series, Func<TValue, TResult> selector)
            where TCursor : ICursor<TKey, TSource>
        {
            return new MapValuesSeries<TKey, TSource, TResult, TCursor>(series._series, CoreUtils.CombineMaps(series._selector, selector));
        }

        public static MapValuesSeries<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>> Map<TKey, TValue, TResult>(this SortedChunkedMap<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            return new MapValuesSeries<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>(series, selector);
        }

        public static MapValuesSeries<TKey, TValue, TResult, ICursor<TKey, TValue>> Map<TKey, TValue, TResult>(this ISeries<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            return new MapValuesSeries<TKey, TValue, TResult, ICursor<TKey, TValue>>(series, selector);
        }

        // RangeSeries<TKey, TValue, TCursor>
        public static MapValuesSeries<TKey, TValue, TResult, RangeSeries<TKey, TValue, TCursor>> Map<TKey, TValue, TResult, TCursor>(
            this RangeSeries<TKey, TValue, TCursor> series, Func<TValue, TResult> selector)
            where TCursor : ICursor<TKey, TValue>
        {
            return new MapValuesSeries<TKey, TValue, TResult, RangeSeries<TKey, TValue, TCursor>>(series, selector);
        }
    }
}