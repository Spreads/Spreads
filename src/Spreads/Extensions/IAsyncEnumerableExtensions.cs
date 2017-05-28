using Spreads.Enumerators;
using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    public static class AsyncEnumerableExtensions
    {
        public static IAsyncEnumerable<KeyValuePair<DateTime, TAggr>> TimeSlice<TValue, TAggr>(
            this IEnumerable<KeyValuePair<DateTime, TValue>> series, Func<TValue, TAggr> initState,
            Func<TAggr, TValue, TAggr> aggregator, UnitPeriod unitPeriod, int periodLength = 1, int offset = 0)
        {
            return new TimeSliceAsyncEnumerable<TValue, TAggr>(series, initState, aggregator, unitPeriod, periodLength, offset);
        }
    }
}