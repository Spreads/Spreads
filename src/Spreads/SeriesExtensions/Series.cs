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
            var fillCursor = new Fill<TKey, TValue, Empty<TKey, TValue>>(new Empty<TKey, TValue>(), value);
            return fillCursor.Source;
        }
    }
}