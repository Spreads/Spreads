// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static partial class Series
    {
        // TODO see the trick with implicit conversion in ContainerSeries.* operator. Here it is also needed.

        // TODO! Series'3 extensions

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
    }
}