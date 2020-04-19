// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using Spreads.Deprecated;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static partial class Series
    {
        #region ContainerSeries

        public static Series<TKey, TResult, Map<TKey, TValue, TResult, TCursor>> Map<TKey, TValue, TResult, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, Func<TKey, TValue, TResult> selector)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Map<TKey, TValue, TResult, TCursor>(series.GetContainerCursor(), selector);
            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, TValue, TResult, TCursor>> Map<TKey, TValue, TResult, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, Func<TValue, TResult> selector)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Map<TKey, TValue, TResult, TCursor>(series.GetContainerCursor(), selector);
            return cursor.Source;
        }

        #endregion ContainerSeries

        //#region SortedMap

        //public static Series<TKey, TResult, Map<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
        //    this SortedMap<TKey, TValue> series, Func<TKey, TValue, TResult> selector)
        //{
        //    var mapCursor = new Map<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>(series.GetAsyncEnumerator(), selector);
        //    return mapCursor.Source;
        //}

        //public static Series<TKey, TResult, Map<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
        //    this SortedMap<TKey, TValue> series, Func<TValue, TResult> selector)
        //{
        //    var mapCursor = new Map<TKey, TValue, TResult, SortedMapCursor<TKey, TValue>>(series.GetAsyncEnumerator(), selector);
        //    return mapCursor.Source;
        //}

        //#endregion SortedMap

        //#region SCM

        //public static Series<TKey, TResult, Map<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
        //    this SortedChunkedMap<TKey, TValue> series, Func<TKey, TValue, TResult> selector)
        //{
        //    var mapCursor = new Map<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>(series.GetAsyncEnumerator(), selector);
        //    return mapCursor.Source;
        //}

        //public static Series<TKey, TResult, Map<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
        //    this SortedChunkedMap<TKey, TValue> series, Func<TValue, TResult> selector)
        //{
        //    var mapCursor = new Map<TKey, TValue, TResult, SortedChunkedMapCursor<TKey, TValue>>(series.GetAsyncEnumerator(), selector);
        //    return mapCursor.Source;
        //}

        //#endregion SCM

        #region Combined map

        public static Series<TKey, TResult, Map<TKey, TSource, TResult, TCursor>> Map<TKey, TSource, TValue, TResult, TCursor>(
            this Series<TKey, TValue, Map<TKey, TSource, TValue, TCursor>> series, Func<TKey, TValue, TResult> selector)
            where TCursor : ICursor<TKey, TSource, TCursor>
        {
            var combinedSelector = CoreUtils.CombineMaps(series._cursor._selector, selector);
            var cursor = new Map<TKey, TSource, TResult, TCursor>(series._cursor._cursor, combinedSelector);
            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, TSource, TResult, TCursor>> Map<TKey, TSource, TValue, TResult, TCursor>(
            this Series<TKey, TValue, Map<TKey, TSource, TValue, TCursor>> series, Func<TValue, TResult> selector)
            where TCursor : ICursor<TKey, TSource, TCursor>
        {
            var combinedSelector = CoreUtils.CombineMaps(series._cursor._selector, selector);
            var cursor = new Map<TKey, TSource, TResult, TCursor>(series._cursor._cursor, combinedSelector);
            return cursor.Source;
        }

        #endregion Combined map

        #region ISeries

        public static Series<TKey, TResult, Map<TKey, TValue, TResult, Cursor<TKey, TValue>>> Map<TKey, TValue, TResult>(
            this ISeries<TKey, TValue> series, Func<TKey, TValue, TResult> selector)
        {
            var cursor = new Map<TKey, TValue, TResult, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), selector);
            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, TValue, TResult, Cursor<TKey, TValue>>> Map<TKey, TValue, TResult>(this ISeries<TKey, TValue> series, Func<TValue, TResult> selector)
        {
            var cursor = new Map<TKey, TValue, TResult, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), selector);
            return cursor.Source;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, TResult, Map<TKey, TValue, TResult, TCursor>> Map<TKey, TValue, TResult, TCursor>(
            this Series<TKey, TValue, TCursor> series, Func<TKey, TValue, TResult> selector)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            // TODO review how to combine maps (former ICanMapValues interface)
            var cursor = new Map<TKey, TValue, TResult, TCursor>(series.GetEnumerator(), selector);
            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, TValue, TResult, TCursor>> Map<TKey, TValue, TResult, TCursor>(
            this Series<TKey, TValue, TCursor> series, Func<TValue, TResult> selector)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            // TODO review how to combine maps (former ICanMapValues interface)
            var cursor = new Map<TKey, TValue, TResult, TCursor>(series.GetEnumerator(), selector);
            return cursor.Source;
        }

        #endregion Generic CursorSeries
    }
}