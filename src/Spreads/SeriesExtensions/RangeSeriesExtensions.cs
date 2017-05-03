// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using Spreads.Cursors;

namespace Spreads
{
    /// <summary>
    /// A static class that contains all extensions to ISeries interface and its implementations.
    /// </summary>
    public static partial class Series
    {
        // Even if this works, avoid public extensions on generic T type
        internal static RangeSeries<TKey, TValue, T> Range<T, TKey, TValue>(this T series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
            where T : AbstractCursorSeries<TKey, TValue, T>, ISpecializedCursor<TKey, TValue, T>, new()
        {
            return new RangeSeries<TKey, TValue, T>(series,
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        public static RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>> Range<TKey, TValue>(
            this SortedMap<TKey, TValue> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>>(series.GetEnumerator(),
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        public static RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>> Range<TKey, TValue>(
            this SortedChunkedMap<TKey, TValue> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>(series.GetEnumerator(),
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        public static RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>>
            Range<TKey, TSource, TValue, TCursor>(
            this MapValuesSeries<TKey, TSource, TValue, TCursor> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TSource, TCursor>
        {
            return new RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>>(series,
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        internal static RangeSeries<TKey, TValue, TCursor> Range<TKey, TValue, TCursor>(
            this RangeSeries<TKey, TValue, TCursor> series,
            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            // adjust range to efficiently nest range series, e.g. After(x).Before(y)

            var newStartKey = Opt<TKey>.LargerOrMissing(series.StartKey, startKey, series.Comparer);
            var newStartInclusive = newStartKey.Equals(startKey) ? startInclusive : series.StartInclusive;

            var newEndKey = Opt<TKey>.SmallerOrMissing(series.EndKey, endKey, series.Comparer);
            var newEndInclusive = newEndKey.Equals(endKey) ? endInclusive : series.EndInclusive;

            return new RangeSeries<TKey, TValue, TCursor>(series._cursor,
                newStartKey, newEndKey, newStartInclusive, newEndInclusive);
        }

        public static RangeSeries<TKey, TValue, SpecializedWrapper<TKey, TValue>> Range<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SpecializedWrapper<TKey, TValue>>
                (new SpecializedWrapper<TKey, TValue>(series.GetCursor()),
                new Opt<TKey>(startKey), new Opt<TKey>(endKey), startInclusive, endInclusive);
        }

        // AFTER

        public static RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>> After<TKey, TValue>(
            this SortedMap<TKey, TValue> series,
            TKey startKey, bool startInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>>(series.GetEnumerator(),
                new Opt<TKey>(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        public static RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>> After<TKey, TValue>(
            this SortedChunkedMap<TKey, TValue> series,
            TKey startKey, bool startInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>(series.GetEnumerator(),
                new Opt<TKey>(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        public static RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>> After<TKey, TSource, TValue, TCursor>(
            this MapValuesSeries<TKey, TSource, TValue, TCursor> series,
            TKey startKey, bool startInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TSource, TCursor>
        {
            return new RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>>(series,
                new Opt<TKey>(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        public static RangeSeries<TKey, TValue, TCursor> After<TKey, TValue, TCursor>(
            this RangeSeries<TKey, TValue, TCursor> series,
            TKey startKey, bool startInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(new Opt<TKey>(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        public static RangeSeries<TKey, TValue, SpecializedWrapper<TKey, TValue>> After<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            TKey startKey, bool startInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SpecializedWrapper<TKey, TValue>>(new SpecializedWrapper<TKey, TValue>(series.GetCursor()),
                new Opt<TKey>(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        // BEFORE

        public static RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>> Before<TKey, TValue>(
            this SortedMap<TKey, TValue> series,
            TKey endKey, bool endInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SortedMapCursor<TKey, TValue>>(series.GetEnumerator(),
                Opt<TKey>.Missing, new Opt<TKey>(endKey), true, endInclusive);
        }

        public static RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>> Before<TKey, TValue>(
            this SortedChunkedMap<TKey, TValue> series,
            TKey endKey, bool endInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>(series.GetEnumerator(),
                Opt<TKey>.Missing, new Opt<TKey>(endKey), true, endInclusive);
        }

        public static RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>> Before<TKey, TSource, TValue, TCursor>(
            this MapValuesSeries<TKey, TSource, TValue, TCursor> series,
            TKey endKey, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TSource, TCursor>
        {
            return new RangeSeries<TKey, TValue, MapValuesSeries<TKey, TSource, TValue, TCursor>>(series,
                Opt<TKey>.Missing, new Opt<TKey>(endKey), true, endInclusive);
        }

        public static RangeSeries<TKey, TValue, TCursor> Before<TKey, TValue, TCursor>(
            this RangeSeries<TKey, TValue, TCursor> series,
            TKey endKey, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt<TKey>.Missing, new Opt<TKey>(endKey), true, endInclusive);
        }

        public static RangeSeries<TKey, TValue, SpecializedWrapper<TKey, TValue>> Before<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            TKey endKey, bool endInclusive = true)
        {
            return new RangeSeries<TKey, TValue, SpecializedWrapper<TKey, TValue>>(new SpecializedWrapper<TKey, TValue>(series.GetCursor()),
                Opt<TKey>.Missing, new Opt<TKey>(endKey), true, endInclusive);
        }
    }
}