// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using Spreads.Deprecated;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// A static class that contains all extensions to ISeries interface and its implementations.
    /// </summary>
    public static partial class Series
    {
        #region Internal methods with Opt<TKey>

        internal static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Range<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series,
            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Range<TKey, TValue, TCursor>(
                series.GetContainerCursor(), startKey, endKey, startInclusive, endInclusive);
            return cursor.Source;
        }

        internal static Series<TKey, TValue, Range<TKey, TValue, SCursor<TKey, TValue>>> Range<TKey, TValue>(
            this Series<TKey, TValue> series,
            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive = true, bool endInclusive = true)
        {
            var cursor = new Range<TKey, TValue, SCursor<TKey, TValue>>(
                series.GetEnumerator(), startKey, endKey, startInclusive, endInclusive);
            return cursor.Source;
        }

        // TODO
//        internal static Series<TKey, TValue, Range<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>> Range<TKey, TValue>(
//            this SortedChunkedMap<TKey, TValue> series,
//            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive = true, bool endInclusive = true)
//        {
//            var cursor = new Range<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>(
//                series.GetEnumerator(), startKey, endKey, startInclusive, endInclusive);
//            return cursor.Source;
//        }

        internal static Series<TKey, TValue, Range<TKey, TValue, Cursor<TKey, TValue>>> Range<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive = true, bool endInclusive = true)
        {
            var cursor = new Range<TKey, TValue, Cursor<TKey, TValue>>(new Cursor<TKey, TValue>(
                series.GetCursor()), startKey, endKey, startInclusive, endInclusive);
            return cursor.Source;
        }

        internal static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Range<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series,
            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Range<TKey, TValue, TCursor>(
                series.GetEnumerator(), startKey, endKey, startInclusive, endInclusive);
            return cursor.Source;
        }

        internal static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Range<TKey, TValue, TCursor>(
            this Series<TKey, TValue, Range<TKey, TValue, TCursor>> series,
            Opt<TKey> startKey, Opt<TKey> endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            // adjust range to efficiently nest range series, e.g. After(x).Before(y)

            var newStartKey = Opt<TKey>.LargerOrMissing(series._cursor.StartKey, startKey, series.Comparer);
            var newStartInclusive = newStartKey.Equals(startKey) ? startInclusive : series._cursor.StartInclusive;

            var newEndKey = Opt<TKey>.SmallerOrMissing(series._cursor.EndKey, endKey, series.Comparer);
            var newEndInclusive = newEndKey.Equals(endKey) ? endInclusive : series._cursor.EndInclusive;

            var cursor = new Range<TKey, TValue, TCursor>(
                series._cursor._cursor, newStartKey, newEndKey, newStartInclusive, newEndInclusive);
            return cursor.Source;
        }

        #endregion Internal methods with Opt<TKey>

        public static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Range<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            // NB cast to Opt for overload resolution
            return series.Range(Opt.Present(startKey), Opt.Present(endKey), startInclusive, endInclusive);
        }

        //public static Series<TKey, TValue, Range<TKey, TValue, SortedMapCursor<TKey, TValue>>> Range<TKey, TValue>(
        //    this SortedMap<TKey, TValue> series,
        //    TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
        //{
        //    // NB cast to Opt for overload resolution
        //    return series.Range((Opt<TKey>)startKey, endKey, startInclusive, endInclusive);
        //}

        //public static Series<TKey, TValue, Range<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>> Range<TKey, TValue>(
        //    this SortedChunkedMap<TKey, TValue> series,
        //    TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
        //{
        //    return series.Range((Opt<TKey>)startKey, endKey, startInclusive, endInclusive);
        //}

        internal static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Range<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt.Present(startKey), Opt.Present(endKey), startInclusive, endInclusive);
        }

        internal static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Range<TKey, TValue, TCursor>(
            this Series<TKey, TValue, Range<TKey, TValue, TCursor>> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt.Present(startKey), Opt.Present(endKey), startInclusive, endInclusive);
        }

        public static Series<TKey, TValue, Range<TKey, TValue, Cursor<TKey, TValue>>> Range<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            TKey startKey, TKey endKey, bool startInclusive = true, bool endInclusive = true)
        {
            return series.Range(Opt.Present(startKey), Opt.Present(endKey), startInclusive, endInclusive);
        }

        // AFTER

        public static Series<TKey, TValue, Range<TKey, TValue, TCursor>> After<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series,
            TKey startKey, bool startInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt.Present(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        //public static Series<TKey, TValue, Range<TKey, TValue, SortedMapCursor<TKey, TValue>>> After<TKey, TValue>(
        //    this SortedMap<TKey, TValue> series,
        //    TKey startKey, bool startInclusive = true)
        //{
        //    return series.Range(startKey, Opt<TKey>.Missing, startInclusive, true);
        //}

        //public static Series<TKey, TValue, Range<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>> After<TKey, TValue>(
        //    this SortedChunkedMap<TKey, TValue> series,
        //    TKey startKey, bool startInclusive = true)
        //{
        //    return series.Range(startKey, Opt<TKey>.Missing, startInclusive, true);
        //}

        public static Series<TKey, TValue, Range<TKey, TValue, TCursor>> After<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series,
            TKey startKey, bool startInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt.Present(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        public static Series<TKey, TValue, Range<TKey, TValue, TCursor>> After<TKey, TValue, TCursor>(
            this Series<TKey, TValue, Range<TKey, TValue, TCursor>> series,
            TKey startKey, bool startInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt.Present(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        // TODO make public

        internal static Series<TKey, TResult, Map<TKey, TInput, TResult, Range<TKey, TInput, TCursor>>> After<TKey, TInput, TResult, TCursor>(
            this Series<TKey, TResult, Map<TKey, TInput, TResult, TCursor>> series,
            TKey startKey, bool startInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TInput, TCursor>
        {
            // NB trick - we move range before map, see how all maps are fused below
            // TODO (low) combine ranges in the example below
            var mapInner = series._cursor._cursor;
            var selector = series._cursor._selector;
            var range = new Range<TKey, TInput, TCursor>(mapInner, Opt.Present(startKey), Opt<TKey>.Missing, startInclusive, true);
            var map = new Map<TKey, TInput, TResult, Range<TKey, TInput, TCursor>>(range, selector);
            var res = map.Source;
            return res;
        }

        internal static Series<TKey, TResult, Map<TKey, TInput, TResult, Range<TKey, TInput, TCursor>>> After<TKey, TInput, TResult, TCursor>(
            this Series<TKey, TResult, Map<TKey, TInput, TResult, Range<TKey, TInput, TCursor>>> series,
            TKey startKey, bool startInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TInput, TCursor>
        {
            // NB trick - we move range before map and merge it with the inner range, see how all ranges are combined below
            var range = series._cursor._cursor.Source.After(startKey, startInclusive)._cursor;
            var selector = series._cursor._selector;
            var map = new Map<TKey, TInput, TResult, Range<TKey, TInput, TCursor>>(range, selector);
            var res = map.Source;
            return res;
        }

#if DEBUG
        /// <summary>
        /// Sample for the map combination. This tests mostly intellisense experience - see popup on m2
        /// with and without the two methods above
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private static void TestMovingRangeBeforeMap()
        {
            var sm = new SortedMap<int, double>
            {
                { 1, 1 }
            };
            Series<int, double, Range<int, double, SortedMapCursor<int, double>>> s1;
            s1 = sm.After(1);
            // ReSharper disable once UnusedVariable
            var m2 =
                (s1.Map((x) => x + 1)).After(1).After(1)
                .Map((x) => x + 1).After(1).Map((x) => x + 1).After(1).Map((x) => x + 1)
                .Map((x) => x + 1).After(1).Map((x) => x + 1).After(1).Map((x) => x + 1)
                .Map((x) => x + 1).After(1).Map((x) => x + 1).After(1).Map((x) => x + 1)
                .Map((x) => x + 1).After(1).Map((x) => x + 1).After(1).Map((x) => x + 1)
                .Map((x) => x + 1).After(1).Map((x) => x + 1).After(1).Map((x) => x + 1)
                .Map((x) => x + 1).After(1).Map((x) => x + 1).After(1).Map((x) => x + 1)
                .Map((x) => x + 1).After(1).Map((x) => x + 1).After(1).Map((x) => x + 1)
                ;
        }
#endif

        public static Series<TKey, TValue, Range<TKey, TValue, Cursor<TKey, TValue>>> After<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            TKey startKey, bool startInclusive = true)
        {
            return series.Range(Opt.Present(startKey), Opt<TKey>.Missing, startInclusive, true);
        }

        // BEFORE

        public static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Before<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series,
            TKey endKey, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt<TKey>.Missing, Opt.Present(endKey), true, endInclusive);
        }

        //public static Series<TKey, TValue, Range<TKey, TValue, SortedMapCursor<TKey, TValue>>> Before<TKey, TValue>(
        //            this SortedMap<TKey, TValue> series,
        //            TKey endKey, bool endInclusive = true)
        //{
        //    return series.Range(Opt<TKey>.Missing, endKey, true, endInclusive);
        //}

        //public static Series<TKey, TValue, Range<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>> Before<TKey, TValue>(
        //    this SortedChunkedMap<TKey, TValue> series,
        //    TKey endKey, bool endInclusive = true)
        //{
        //    return series.Range(Opt<TKey>.Missing, endKey, true, endInclusive);
        //}

        public static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Before<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series,
            TKey endKey, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt<TKey>.Missing, Opt.Present(endKey), true, endInclusive);
        }

        public static Series<TKey, TValue, Range<TKey, TValue, TCursor>> Before<TKey, TValue, TCursor>(
            this Series<TKey, TValue, Range<TKey, TValue, TCursor>> series,
            TKey endKey, bool endInclusive = true)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            return series.Range(Opt<TKey>.Missing, Opt.Present(endKey), true, endInclusive);
        }

        public static Series<TKey, TValue, Range<TKey, TValue, Cursor<TKey, TValue>>> Before<TKey, TValue>(
            this ISeries<TKey, TValue> series,
            TKey endKey, bool endInclusive = true)
        {
            return series.Range(Opt<TKey>.Missing, Opt.Present(endKey), true, endInclusive);
        }
    }
}