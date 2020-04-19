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
        // TODO FilterKeys on Map should move filter before map. Map is lazy and this not reduce evaluations, but then
        // map could be fused if another map goes after it

        #region ContainerSeries

        public static Series<TKey, TValue, Filter<TKey, TValue, TCursor>> Filter<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, Func<TKey, TValue, bool> predicate)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Filter<TKey, TValue, TCursor>(series.GetContainerCursor(), predicate);
            return cursor.Source;
        }

        public static Series<TKey, TValue, Filter<TKey, TValue, TCursor>> Filter<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, Func<TKey, bool> predicate)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Filter<TKey, TValue, TCursor>(series.GetContainerCursor(), predicate);
            return cursor.Source;
        }

        public static Series<TKey, TValue, Filter<TKey, TValue, TCursor>> Filter<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, Func<TValue, bool> predicate)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Filter<TKey, TValue, TCursor>(series.GetContainerCursor(), predicate);
            return cursor.Source;
        }

        #endregion ContainerSeries

        //#region Combined filter

        // TODO! Filter predicate combination: key-only filter could be moved before map

        //#endregion Combined filter

        #region ISeries

        public static Series<TKey, TValue, Filter<TKey, TValue, Cursor<TKey, TValue>>> Filter<TKey, TValue>(
            this ISeries<TKey, TValue> series, Func<TKey, TValue, bool> predicate)
        {
            var cursor = new Filter<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), predicate);
            return cursor.Source;
        }

        public static Series<TKey, TValue, Filter<TKey, TValue, Cursor<TKey, TValue>>> Filter<TKey, TValue>(
            this ISeries<TKey, TValue> series, Func<TKey, bool> predicate)
        {
            var cursor = new Filter<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), predicate);
            return cursor.Source;
        }

        public static Series<TKey, TValue, Filter<TKey, TValue, Cursor<TKey, TValue>>> Filter<TKey, TValue>(
            this ISeries<TKey, TValue> series, Func<TValue, bool> predicate)
        {
            var cursor = new Filter<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), predicate);
            return cursor.Source;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, TValue, Filter<TKey, TValue, TCursor>> Filter<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, Func<TKey, TValue, bool> predicate)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Filter<TKey, TValue, TCursor>(series.GetEnumerator(), predicate);
            return cursor.Source;
        }

        public static Series<TKey, TValue, Filter<TKey, TValue, TCursor>> Filter<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, Func<TKey, bool> predicate)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Filter<TKey, TValue, TCursor>(series.GetEnumerator(), predicate);
            return cursor.Source;
        }

        public static Series<TKey, TValue, Filter<TKey, TValue, TCursor>> Filter<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, Func<TValue, bool> predicate)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Filter<TKey, TValue, TCursor>(series.GetEnumerator(), predicate);
            return cursor.Source;
        }

        #endregion Generic CursorSeries
    }
}