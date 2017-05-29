// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static partial class Series
    {
        #region ContainerSeries

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>> Window<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, int width = 1, int step = 1)
            where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        {
            var cursor = new Window<TKey, TValue, TCursor>(series.GetContainerCursor(), width, step);
            return cursor.Source;
        }

        #endregion ContainerSeries

        #region ISeries

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, Cursor<TKey, TValue>>>, Window<TKey, TValue, Cursor<TKey, TValue>>>
            Window<TKey, TValue>(this ISeries<TKey, TValue> series, int width = 1, int step = 1)
        {
            var cursor = new Window<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), width, step);
            return cursor.Source;
        }

        #endregion ISeries

        #region Generic CursorSeries

        public static Series<TKey, Series<TKey, TValue, Range<TKey, TValue, TCursor>>, Window<TKey, TValue, TCursor>> Window<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, int width = 1, int step = 1)
            where TCursor : ICursorSeries<TKey, TValue, TCursor>
        {
            var cursor = new Window<TKey, TValue, TCursor>(series.GetEnumerator(), width, step);
            return cursor.Source;
        }

        #endregion Generic CursorSeries
    }
}