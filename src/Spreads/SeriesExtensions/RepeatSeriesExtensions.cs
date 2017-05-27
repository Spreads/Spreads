// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static partial class Series
    {
        #region ContainerSeries

        public static Series<TKey, (TKey, TValue), Repeat<TKey, TValue, TCursor>> Repeat<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, TValue value)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var fillCursor = new Repeat<TKey, TValue, TCursor>(series.GetContainerCursor());
            return fillCursor.Source;
        }

        #endregion ContainerSeries

        #region ISeries

        public static Series<TKey, (TKey, TValue), Repeat<TKey, TValue, Cursor<TKey, TValue>>> Repeat<TKey, TValue>(
            this ISeries<TKey, TValue> series, TValue value)
        {
            var fillCursor = new Repeat<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor());
            return fillCursor.Source;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, (TKey, TValue), Repeat<TKey, TValue, TCursor>> Repeat<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, TValue value)
            where TCursor : ICursorSeries<TKey, TValue, TCursor>
        {
            var fillCursor = new Repeat<TKey, TValue, TCursor>(series.GetEnumerator());
            return fillCursor.Source;
        }

        #endregion Generic CursorSeries
    }
}