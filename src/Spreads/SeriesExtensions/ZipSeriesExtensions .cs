// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static partial class Series
    {
        // TODO see the trick with implicit conversion in ContainerSeries.* operator. Here it is also needed.

        // TODO! Series'3 extensions

        // TODO convenience methods on ValueTuple'3...'8 that accept a selector and flatten nested tuples

        // NB having methods that accept a selector achieves nothing in performance terms, only convenient signature

        #region ContainerSeries

        public static Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>> Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(
            this ContainerSeries<TKey, TLeft, TCursorLeft> series, ContainerSeries<TKey, TRight, TCursorRight> other)
            where TCursorLeft : ISpecializedCursor<TKey, TLeft, TCursorLeft>
            where TCursorRight : ISpecializedCursor<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(series.GetContainerCursor(), other.GetContainerCursor());
            return zip.Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this ContainerSeries<TKey, TLeft, TCursorLeft> series, ContainerSeries<TKey, TRight, TCursorRight> other, Func<TKey, TLeft, TRight, TResult> selector)
            where TCursorLeft : ISpecializedCursor<TKey, TLeft, TCursorLeft>
            where TCursorRight : ISpecializedCursor<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(series.GetContainerCursor(), other.GetContainerCursor());
            return zip.Map((k, t) => selector(k, t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this ContainerSeries<TKey, TLeft, TCursorLeft> series, ContainerSeries<TKey, TRight, TCursorRight> other, Func<TLeft, TRight, TResult> selector)
            where TCursorLeft : ISpecializedCursor<TKey, TLeft, TCursorLeft>
            where TCursorRight : ISpecializedCursor<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(series.GetContainerCursor(), other.GetContainerCursor());
            return zip.Map((k, t) => selector(t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this (ContainerSeries<TKey, TLeft, TCursorLeft> left, ContainerSeries<TKey, TRight, TCursorRight> right) tuple, Func<TKey, TLeft, TRight, TResult> selector)
            where TCursorLeft : ISpecializedCursor<TKey, TLeft, TCursorLeft>
            where TCursorRight : ISpecializedCursor<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(tuple.left.GetContainerCursor(), tuple.right.GetContainerCursor());
            return zip.Map((k, t) => selector(k, t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this (ContainerSeries<TKey, TLeft, TCursorLeft> left, ContainerSeries<TKey, TRight, TCursorRight> right) tuple, Func<TLeft, TRight, TResult> selector)
            where TCursorLeft : ISpecializedCursor<TKey, TLeft, TCursorLeft>
            where TCursorRight : ISpecializedCursor<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(tuple.left.GetContainerCursor(), tuple.right.GetContainerCursor());
            return zip.Map((k, t) => selector(t.Item1, t.Item2)).Source;
        }

        #endregion ContainerSeries

        #region ISeries

        public static Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>> Zip<TKey, TLeft, TRight>(
            this ISeries<TKey, TLeft> series, ISeries<TKey, TRight> other)
        {
            var zip = new Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>(series.GetSpecializedCursor(), other.GetSpecializedCursor());
            return zip.Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>>> Zip<TKey, TLeft, TRight, TResult>(
            this ISeries<TKey, TLeft> series, ISeries<TKey, TRight> other, Func<TKey, TLeft, TRight, TResult> selector)
        {
            var zip = new Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>(series.GetSpecializedCursor(), other.GetSpecializedCursor());
            return zip.Map((k, t) => selector(k, t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>>> Zip<TKey, TLeft, TRight, TResult>(
            this ISeries<TKey, TLeft> series, ISeries<TKey, TRight> other, Func<TLeft, TRight, TResult> selector)
        {
            var zip = new Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>(series.GetSpecializedCursor(), other.GetSpecializedCursor());
            return zip.Map((k, t) => selector(t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>>> Zip<TKey, TLeft, TRight, TResult>(
            this (ISeries<TKey, TLeft> left, ISeries<TKey, TRight> right) tuple, Func<TKey, TLeft, TRight, TResult> selector)
        {
            var zip = new Zip<TKey, TLeft, TRight, Cursor<TKey, TLeft>, Cursor<TKey, TRight>>(tuple.left.GetSpecializedCursor(), tuple.right.GetSpecializedCursor());
            return zip.Map((k, t) => selector(k, t.Item1, t.Item2)).Source;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>> Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(
            this Series<TKey, TLeft, TCursorLeft> series, Series<TKey, TRight, TCursorRight> other)
            where TCursorLeft : ICursorSeries<TKey, TLeft, TCursorLeft>
            where TCursorRight : ICursorSeries<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(series.GetEnumerator(), other.GetEnumerator());
            return zip.Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this Series<TKey, TLeft, TCursorLeft> series, Series<TKey, TRight, TCursorRight> other, Func<TKey, TLeft, TRight, TResult> selector)
            where TCursorLeft : ICursorSeries<TKey, TLeft, TCursorLeft>
            where TCursorRight : ICursorSeries<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(series.GetEnumerator(), other.GetEnumerator());
            return zip.Map((k, t) => selector(k, t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this Series<TKey, TLeft, TCursorLeft> series, Series<TKey, TRight, TCursorRight> other, Func<TLeft, TRight, TResult> selector)
            where TCursorLeft : ICursorSeries<TKey, TLeft, TCursorLeft>
            where TCursorRight : ICursorSeries<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(series.GetEnumerator(), other.GetEnumerator());
            return zip.Map((k, t) => selector(t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this (Series<TKey, TLeft, TCursorLeft> left, Series<TKey, TRight, TCursorRight> right) tuple, Func<TKey, TLeft, TRight, TResult> selector)
            where TCursorLeft : ICursorSeries<TKey, TLeft, TCursorLeft>
            where TCursorRight : ICursorSeries<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(tuple.left.GetEnumerator(), tuple.right.GetEnumerator());
            return zip.Map((k, t) => selector(k, t.Item1, t.Item2)).Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>> Zip<TKey, TLeft, TRight, TResult, TCursorLeft, TCursorRight>(
            this (Series<TKey, TLeft, TCursorLeft> left, Series<TKey, TRight, TCursorRight> right) tuple, Func<TLeft, TRight, TResult> selector)
            where TCursorLeft : ICursorSeries<TKey, TLeft, TCursorLeft>
            where TCursorRight : ICursorSeries<TKey, TRight, TCursorRight>
        {
            var zip = new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>(tuple.left.GetEnumerator(), tuple.right.GetEnumerator());
            return zip.Map((k, t) => selector(t.Item1, t.Item2)).Source;
        }

        #endregion Generic CursorSeries

        #region Series array

        public static ISeries<TKey, TValue[]> Zip<TKey, TValue>(
            [NotNull] this ISeries<TKey, TValue>[] series, bool reuseArray = false)
        {
            if (series == null) throw new ArgumentNullException(nameof(series));
            TValue[] reusedArray = null;

            if (series.Length == 0)
            {
                return Empty<TKey, TValue[]>();
            }

            if (reuseArray)
            {
                reusedArray = new TValue[series.Length];
            }

            if (series.Length == 1)
            {
                var map =
                series[0].Map((k, v) =>
                    {
                        var arr = reuseArray ? reusedArray : new TValue[series.Length];
                        // ReSharper disable once PossibleNullReferenceException
                        arr[0] = v;
                        return arr;
                    });
                return map;
            }

            var cursor = new Cursor<TKey, TValue[]>(new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(
                series[0].GetSpecializedCursor(), series[1].GetSpecializedCursor())
                .Map((k, t) =>
                {
                    var arr = reuseArray ? reusedArray : new TValue[series.Length];
                    // ReSharper disable once PossibleNullReferenceException
                    arr[0] = t.Item1;
                    arr[1] = t.Item2;
                    return arr;
                }));

            for (var i = 2; i < series.Length; i++)
            {
                var idx = i;
                cursor = new Cursor<TKey, TValue[]>(new Zip<TKey, TValue[], TValue, Cursor<TKey, TValue[]>, Cursor<TKey, TValue>>(
                    cursor, series[idx].GetSpecializedCursor())
                    .Map((k, t) =>
                    {
                        t.Item1[idx] = t.Item2;
                        return t.Item1;
                    }));
            }

            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, TValue[], TResult, Cursor<TKey, TValue[]>>> Zip<TKey, TValue, TResult>(
            this ISeries<TKey, TValue>[] series, Func<TKey, TValue[], TResult> selector, bool reuseArray = false)
        {
            return series.Zip(reuseArray).Map(selector);
        }

        public static Series<TKey, TResult, Map<TKey, TValue[], TResult, Cursor<TKey, TValue[]>>> Zip<TKey, TValue, TResult, TCursor>(
            this Series<TKey, TValue, TCursor>[] series, Func<TKey, TValue[], TResult> selector, bool reuseArray = false)
            where TCursor : ICursorSeries<TKey, TValue, TCursor>
        {
            // TODO rework...
            return series.Cast<ISeries<TKey, TValue>>().ToArray().Zip(reuseArray).Map(selector);
        }

        #endregion Series array
    }
}