// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using Spreads.Cursors;
using System;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static partial class Series
    {
        #region SortedMap

        //public static CursorSeries<TKey, TResult, MapCursor<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
        //    this SortedMap<TKey, TValue> series, Func<TKey, TValue, TResult> selector)
        //{
        //    var mapCursor = new MapCursor<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>(series.GetEnumerator(), selector);
        //    return mapCursor.Source;
        //}

        public static CursorSeries<TKey, TResult, MapCursor<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
            this SortedMap<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            var mapCursor = new MapCursor<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>(series.GetEnumerator(), selector);
            return mapCursor.Source;
        }

        #endregion SortedMap

        #region SCM

        //public static CursorSeries<TKey, TResult, MapCursor<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
        //    this SortedChunkedMap<TKey, TValue> series, Func<TKey, TValue, TResult> selector)
        //{
        //    var mapCursor = new MapCursor<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>(series.GetEnumerator(), selector);
        //    return mapCursor.Source;
        //}

        public static CursorSeries<TKey, TResult, MapCursor<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
            this SortedChunkedMap<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            var mapCursor = new MapCursor<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>(series.GetEnumerator(), selector);
            return mapCursor.Source;
        }

        #endregion SCM

        #region Combined map

        //public static CursorSeries<TKey, TResult, MapCursor<TKey, TSource, TResult, TCursor>> Map<TKey, TSource, TValue, TResult, TCursor>(
        //    this CursorSeries<TKey, TValue, MapCursor<TKey, TSource, TValue, TCursor>> series, Func<TKey, TValue, TResult> selector)
        //    where TCursor : ISpecializedCursor<TKey, TSource, TCursor>
        //{
        //    var combinedSelector = CoreUtils.CombineMaps(series._cursor._selector, selector);
        //    var mapCursor = new MapCursor<TKey, TSource, TResult, TCursor>(series._cursor._cursor, combinedSelector);
        //    return mapCursor.Source;
        //}

        public static CursorSeries<TKey, TResult, MapCursor<TKey, TSource, TResult, TCursor>> Map<TKey, TSource, TValue, TResult, TCursor>(
            this CursorSeries<TKey, TValue, MapCursor<TKey, TSource, TValue, TCursor>> series, Func<TValue, TResult> selector)
            where TCursor : ISpecializedCursor<TKey, TSource, TCursor>
        {
            var combinedSelector = CoreUtils.CombineMaps(series._cursor._selector, selector);
            var mapCursor = new MapCursor<TKey, TSource, TResult, TCursor>(series._cursor._cursor, combinedSelector);
            return mapCursor.Source;
        }

        #endregion Combined map

        #region ISeries

        //public static CursorSeries<TKey, TResult, MapCursor<TKey, TValue, TResult, SpecializedWrapper<TKey, TValue>>> Map<TKey, TValue, TResult>(
        //    this ISeries<TKey, TValue> series, Func<TKey, TValue, TResult> selector)
        //{
        //    var mapCursor = new MapCursor<TKey, TValue, TResult, SpecializedWrapper<TKey, TValue>>(series.GetSpecializedWrapper(), selector);
        //    return mapCursor.Source;
        //}

        public static CursorSeries<TKey, TResult, MapCursor<TKey, TValue, TResult, SpecializedWrapper<TKey, TValue>>> Map<TKey, TValue, TResult>(this ISeries<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            var mapCursor = new MapCursor<TKey, TValue, TResult, SpecializedWrapper<TKey, TValue>>(series.GetSpecializedWrapper(), selector);
            return mapCursor.Source;
        }

        #endregion ISeries

        // TODO cross-cursor specialization: range, arithmetics, etc.

        //// RangeSeries<TKey, TValue, TCursor>
        //public static MapValuesSeries<TKey, TValue, TResult, RangeSeries<TKey, TValue, TCursor>> Map<TKey, TValue, TResult, TCursor>(
        //    this RangeSeries<TKey, TValue, TCursor> series, Func<TValue, TResult> selector)
        //    where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        //{
        //    return new MapValuesSeries<TKey, TValue, TResult, RangeSeries<TKey, TValue, TCursor>>(series, selector);
        //}
    }
}