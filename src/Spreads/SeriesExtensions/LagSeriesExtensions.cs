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

        public static Series<TKey, (TValue, (TKey, TValue)), Lag<TKey, TValue, TCursor>> Lag<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, TCursor>(series.GetContainerCursor(), lag + 1, step);
            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TValue, (TKey, TValue)), TResult, Lag<TKey, TValue, TCursor>>> ZipLag<TKey, TValue, TResult, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, Func<((TKey, TValue) current, (TKey, TValue) previous), TResult> selector, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, TCursor>(series.GetContainerCursor(), lag + 1, step);
            return cursor.Source.Map((k, v) => selector(((k, v.Item1), v.Item2)));
        }

        public static Series<TKey, TResult, Map<TKey, (TValue, (TKey, TValue)), TResult, Lag<TKey, TValue, TCursor>>> ZipLag<TKey, TValue, TResult, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, Func<(TValue current, TValue previous), TResult> selector, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, TCursor>(series.GetContainerCursor(), lag + 1, step);
            return cursor.Source.Map((k, v) => selector((v.Item1, v.Item2.Item2)));
        }

        #endregion ContainerSeries

        #region ISeries

        public static Series<TKey, (TValue, (TKey, TValue)), Lag<TKey, TValue, Cursor<TKey, TValue>>> Lag<TKey, TValue>(
            this ISeries<TKey, TValue> series, int lag = 1, int step = 1)
        {
            var cursor = new Lag<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), lag + 1, step);
            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TValue, (TKey, TValue)), TResult, Lag<TKey, TValue, Cursor<TKey, TValue>>>> ZipLag<TKey, TValue, TResult, TCursor>(
            this ISeries<TKey, TValue> series, Func<((TKey, TValue) current, (TKey, TValue) previous), TResult> selector, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), lag + 1, step);
            return cursor.Source.Map((k, v) => selector(((k, v.Item1), v.Item2)));
        }

        public static Series<TKey, TResult, Map<TKey, (TValue, (TKey, TValue)), TResult, Lag<TKey, TValue, Cursor<TKey, TValue>>>> ZipLag<TKey, TValue, TResult, TCursor>(
            this ISeries<TKey, TValue> series, Func<(TValue current, TValue previous), TResult> selector, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), lag + 1, step);
            return cursor.Source.Map((k, v) => selector((v.Item1, v.Item2.Item2)));
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, (TValue, (TKey, TValue)), Lag<TKey, TValue, TCursor>> Lag<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, TCursor>(series.GetEnumerator(), lag + 1, step);
            return cursor.Source;
        }

        public static Series<TKey, TResult, Map<TKey, (TValue, (TKey, TValue)), TResult, Lag<TKey, TValue, TCursor>>> ZipLag<TKey, TValue, TResult, TCursor>(
            this Series<TKey, TValue, TCursor> series, Func<((TKey, TValue) current, (TKey, TValue) previous), TResult> selector, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, TCursor>(series.GetEnumerator(), lag + 1, step);
            return cursor.Source.Map((k, v) => selector(((k, v.Item1), v.Item2)));
        }

        public static Series<TKey, TResult, Map<TKey, (TValue, (TKey, TValue)), TResult, Lag<TKey, TValue, TCursor>>> ZipLag<TKey, TValue, TResult, TCursor>(
            this Series<TKey, TValue, TCursor> series, Func<(TValue current, TValue previous), TResult> selector, int lag = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Lag<TKey, TValue, TCursor>(series.GetEnumerator(), lag + 1, step);
            return cursor.Source.Map((k, v) => selector((v.Item1, v.Item2.Item2)));
        }

        #endregion Generic CursorSeries
    }
}