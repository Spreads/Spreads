﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// ReSharper disable once CheckNamespace

using Spreads.Deprecated;

namespace Spreads
{
    public static partial class Series
    {
        #region ContainerSeries

        public static Series<TKey, TValue, Fill<TKey, TValue, TCursor>> Fill<TKey, TValue, TCursor>(
            this ContainerSeries<TKey, TValue, TCursor> series, TValue value)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Fill<TKey, TValue, TCursor>(series.GetContainerCursor(), value);
            return cursor.Source;
        }

        #endregion ContainerSeries

        #region ISeries

        //public static Series<TKey, TValue, Fill<TKey, TValue, Cursor<TKey, TValue>>> Fill<TKey, TValue>(
        //    this ISeries<TKey, TValue> series, TValue value)
        //{
        //    var cursor = new Fill<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor(), value);
        //    return cursor.Source;
        //}

        #endregion ISeries

        #region ISpecializedSeries

        public static Series<TKey, TValue, Fill<TKey, TValue, TCursor>> Fill<TKey, TValue, TCursor>(
            this ISeries<TKey, TValue, TCursor> series, TValue value)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Fill<TKey, TValue, TCursor>(series.GetCursor(), value);
            return cursor.Source;
        }

        #endregion ISpecializedSeries

        #region Series'3

        public static Series<TKey, TValue, Fill<TKey, TValue, TCursor>> Fill<TKey, TValue, TCursor>(
            this Series<TKey, TValue, TCursor> series, TValue value)
            where TCursor : ICursor<TKey, TValue, TCursor>
        {
            var cursor = new Fill<TKey, TValue, TCursor>(series.GetEnumerator(), value);
            return cursor.Source;
        }

        #endregion Series'3
    }
}