// ReSharper disable once CheckNamespace

using Spreads.Collections;
using System.Collections.Generic;
using Spreads.Deprecated;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    // NB there are problems with extensions on ISpecializedSeries
    // * resolution - ambiguous extensions
    // * boxing - CursorSeries will be boxed to the interface

    //#region ISpecializedSeries
    //public static CursorSeries<TKey, TResult, MapCursor<TKey, TValue, TResult, TCursor>> Map<TKey, TValue, TResult, TCursor>(
    //    this ISpecializedSeries<TKey, TValue, TCursor> series, Func<TKey, TValue, TResult> selector)
    //    where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    //{
    //    var mapCursor = new MapCursor<TKey, TValue, TResult, TCursor>(series.GetCursor(), selector);
    //    return mapCursor.Source;
    //}
    //#endregion

    public static partial class Series
    {
        public static Series<TKey, TValue, Empty<TKey, TValue>> Empty<TKey, TValue>()
        {
            var cursor = new Empty<TKey, TValue>();
            return cursor.Source;
        }

        public static Series<TKey, TValue, Fill<TKey, TValue, Empty<TKey, TValue>>> Constant<TKey, TValue>(TValue value)
        {
            return Empty<TKey, TValue>().Fill(value);
        }

        public static Series<TKey, TValue, Cursor<TKey, TValue>> ReadOnly<TKey, TValue>(
            this ISeries<TKey, TValue> series)
        {
            return new Series<TKey, TValue, Cursor<TKey, TValue>>(series.GetSpecializedCursor());
        }

        // TODO
        //public static Series<TKey, TValue, TCursor> ReadOnly<TKey, TValue, TCursor>(
        //    this ContainerSeries<TKey, TValue, TCursor> series)
        //    where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        //{
        //    return new Series<TKey, TValue, TCursor>(series.GetContainerCursor());
        //}

        public static SortedMap<TKey, TValue> ToSortedMap<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> enumerable)
        {
            var sm = new SortedMap<TKey, TValue>();
            foreach (var keyValuePair in enumerable)
            {
                sm.Add(keyValuePair.Key, keyValuePair.Value);
            }

            return sm;
        }

//        public static SortedMap<TKey, TValue> ToSortedMap<TKey, TValue>(
//            this IDataStream<TKey, TValue> dataStream)
//        {
//            var sm = new SortedMap<TKey, TValue>(dataStream.Comparer);
//            foreach (var keyValuePair in dataStream)
//            {
//                sm.AddLast(keyValuePair.Key, keyValuePair.Value);
//            }
//
//            return sm;
//        }
    }
}