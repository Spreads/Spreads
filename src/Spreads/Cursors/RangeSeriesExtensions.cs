// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using System;

namespace Spreads.Cursors
{
    public static class RangeSeriesExtensions
    {
        public static RangeSeries<TKey, TValue, T> Range<T, TKey, TValue>(this T series,
            TKey startKey, TKey endKey, bool startInclusive, bool endInclusive)
            where T : CursorSeries<TKey, TValue, T>, ICursor<TKey, TValue>
        {
            return new RangeSeries<TKey, TValue, T>(series,
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        public static RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>> Range<TKey, TValue>(
            this SortedMap<TKey, TValue> series,
            TKey startKey, TKey endKey, bool startInclusive, bool endInclusive)
        {
            return new RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>>(series,
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        public static RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>> Range<TKey, TValue>(
            this SortedMap<TKey, TValue> series,
            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive, bool endInclusive)
        {
            return new RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>>(series,
                startKey, endKey, startInclusive, endInclusive);
        }

        public static RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>> Range<TKey, TValue>(
            this SortedChunkedMap<TKey, TValue> series,
            TKey startKey, TKey endKey, bool startInclusive, bool endInclusive)
        {
            return new RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>(series,
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }


        public static RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>> Range<TKey, TSource, TValue, TCursor>(
            this MapValuesSeries<TKey, TSource, TValue, TCursor> series,
            TKey startKey, TKey endKey, bool startInclusive, bool endInclusive)
            where TCursor : ICursor<TKey, TSource>
        {
            return new RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>>(series,
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        public static RangeSeries<TKey, TValue, ICursor<TKey, TValue>> Map<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            TKey startKey, TKey endKey, bool startInclusive, bool endInclusive)
        {
            return new RangeSeries<TKey, TValue, ICursor<TKey, TValue>>(series,
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }


    }
}