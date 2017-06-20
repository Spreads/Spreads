// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors.Internal;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Linq;

namespace Spreads.Core.Tests.Cursors.Internal
{
    // TODO move to a separate file, see #8
    public static class SeriesContract
    {
        public static bool ClonedCursorsAreIndependent<TKey, TValue>(IReadOnlySeries<TKey, TValue> lazySeries)
        {
            var sm = lazySeries.ToSortedMap();
            var mc = sm.GetCursor();

            var c1 = lazySeries.GetCursor();
            var c2 = c1.Clone();

            while (mc.MoveNext())
            {
                var c3 = c2.Clone();
                Assert.True(c1.MoveNext());
                Assert.True(c2.MoveNext());
                Assert.True(c3.MoveNext());

                Assert.AreEqual(c1.Current, mc.Current);
                Assert.AreEqual(c1.Current, c2.Current);
                Assert.AreEqual(c1.Current, c3.Current);

                c3.Dispose();
            }

            while (mc.MovePrevious())
            {
                var c3 = c2.Clone();
                Assert.True(c1.MovePrevious());
                Assert.True(c2.MovePrevious());
                Assert.True(c3.MovePrevious());

                Assert.AreEqual(c1.Current, mc.Current);
                Assert.AreEqual(c1.Current, c2.Current);
                Assert.AreEqual(c1.Current, c3.Current);

                c3.Dispose();
            }

            if (mc.MoveLast())
            {
                Assert.True(c1.MoveLast());
                Assert.True(c2.MoveLast());
                Assert.AreEqual(c1.Current, mc.Current);
                Assert.AreEqual(c1.Current, c2.Current);
            }

            if (mc.MoveFirst())
            {
                Assert.True(c1.MoveFirst());
                Assert.True(c2.MoveFirst());
                Assert.AreEqual(c1.Current, mc.Current);
                Assert.AreEqual(c1.Current, c2.Current);
            }

            return true;
        }

        /// <summary>
        /// When lazy series is materialized to SortedMap, at each materialized key the result of lookup on the
        /// lazy series must match the lookup on the materialized series.
        /// </summary>
        public static bool MoveAtShouldWorkOnLazySeries<TKey, TValue>(IReadOnlySeries<TKey, TValue> lazySeries)
        {
            var materializedSeries = lazySeries.ToSortedMap();
            var c = lazySeries.GetCursor();

            foreach (var kvp in materializedSeries)
            {
                // EQ
                {
                    Assert.True(materializedSeries.TryFind(kvp.Key, Lookup.EQ, out var materialized));
                    lazySeries.TryFind(kvp.Key, Lookup.EQ, out var lazy);
                    Assert.AreEqual(materialized, lazy);

                    Assert.True(c.MoveAt(kvp.Key, Lookup.EQ));
                    Assert.AreEqual(materialized, c.Current);
                }

                // LT
                {
                    if (materializedSeries.TryFind(kvp.Key, Lookup.LT, out var materialized))
                    {
                        Assert.True(lazySeries.TryFind(kvp.Key, Lookup.LT, out var lazy));
                        Assert.AreEqual(materialized, lazy);

                        Assert.True(c.MoveAt(kvp.Key, Lookup.LT));
                        Assert.AreEqual(materialized, c.Current);
                    }
                }

                // LE
                {
                    if (materializedSeries.TryFind(kvp.Key, Lookup.LE, out var materialized))
                    {
                        Assert.True(lazySeries.TryFind(kvp.Key, Lookup.LE, out var lazy));
                        Assert.AreEqual(materialized, lazy);

                        Assert.True(c.MoveAt(kvp.Key, Lookup.LE));
                        Assert.AreEqual(materialized, c.Current);
                    }
                }

                // GT
                {
                    if (materializedSeries.TryFind(kvp.Key, Lookup.GT, out var materialized))
                    {
                        Assert.True(lazySeries.TryFind(kvp.Key, Lookup.GT, out var lazy));
                        Assert.AreEqual(materialized, lazy);

                        Assert.True(c.MoveAt(kvp.Key, Lookup.GT));
                        Assert.AreEqual(materialized, c.Current);
                    }
                }

                // GE
                {
                    if (materializedSeries.TryFind(kvp.Key, Lookup.GE, out var materialized))
                    {
                        Assert.True(lazySeries.TryFind(kvp.Key, Lookup.GE, out var lazy));
                        Assert.AreEqual(materialized, lazy);

                        Assert.True(c.MoveAt(kvp.Key, Lookup.GE));
                        Assert.AreEqual(materialized, c.Current);
                    }
                }
            }

            return true;
        }
    }

    [TestFixture]
    public class SpanOpTests
    {
        [Test]
        public void CouldCalculateSMAWithCount()
        {
            var count = 20;
            var sm = new SortedMap<int, double>();
            for (int i = 1; i <= count; i++)
            {
                sm.Add(i, i);
            }

            DoTest(true);
            DoTest(false);

            DoTestViaSpanOpCount(true);
            DoTestViaSpanOpCount(false);

            void DoTest(bool allowIncomplete)
            {
                // TODO separate tests/cases for true/false
                var smaOp = new MAvgCount<int, double, SortedMapCursor<int, double>>(10, allowIncomplete);
                var smaCursor =
                    new SpanOpImpl<int,
                        double,
                        double,
                        MAvgCount<int, double, SortedMapCursor<int, double>>,
                        SortedMapCursor<int, double>
                    >(sm.GetEnumerator(), smaOp);

                // this monster type must be hidden in the same way Lag hides its implementation
                Series<int, double, SpanOpImpl<int, double, double, MAvgCount<int, double, SortedMapCursor<int, double>>, SortedMapCursor<int, double>>> smaSeries;
                smaSeries = smaCursor.Source;

                var sm2 = smaSeries.ToSortedMap();

                Assert.AreEqual(sm2.First, smaSeries.First);
                Assert.AreEqual(sm2.Last, smaSeries.Last);

                Assert.True(SeriesContract.MoveAtShouldWorkOnLazySeries(smaSeries));
                Assert.True(SeriesContract.ClonedCursorsAreIndependent(smaSeries));

                if (!allowIncomplete)
                {
                    var trueSma = sm.Window(10).Map(x => x.Values.Average());
                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                Debug.WriteLine("SMA");
                foreach (var keyValuePair in smaSeries)
                {
                    Debug.WriteLine($"{keyValuePair.Key} - {keyValuePair.Value}");
                }
            }

            void DoTestViaSpanOpCount(bool allowIncomplete)
            {
                // TODO separate tests/cases for true/false
                var onlineOp = new OnlineSumAvg<int, double, SortedMapCursor<int, double>>();
                var smaOp = new SpanOpCount<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>(10, allowIncomplete, onlineOp);
                var smaCursor =
                    new SpanOpImpl<int,
                        double,
                        double,
                        SpanOpCount<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>,
                        SortedMapCursor<int, double>
                    >(sm.GetEnumerator(), smaOp);

                // this monster type must be hidden in the same way Lag hides its implementation
                Series<int, double, SpanOpImpl<int, double, double, SpanOpCount<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>, SortedMapCursor<int, double>>> smaSeries;
                smaSeries = smaCursor.Source;

                var sm2 = smaSeries.ToSortedMap();

                Assert.AreEqual(sm2.First, smaSeries.First);
                Assert.AreEqual(sm2.Last, smaSeries.Last);

                Assert.True(SeriesContract.MoveAtShouldWorkOnLazySeries(smaSeries));
                Assert.True(SeriesContract.ClonedCursorsAreIndependent(smaSeries));

                if (!allowIncomplete)
                {
                    var trueSma = sm.Window(10).Map(x => x.Values.Average());
                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                Debug.WriteLine("SMA");
                foreach (var keyValuePair in smaSeries)
                {
                    Debug.WriteLine($"{keyValuePair.Key} - {keyValuePair.Value}");
                }
            }
        }

        [Test]
        public void CouldCalculateSMAWithWidthEQ()
        {
            Assert.Throws<NotImplementedException>(() =>
            {
                var count = 20;
                var sm = new SortedMap<int, double>();
                sm.Add(0, 0);
                for (int i = 2; i <= count; i++)
                {
                    sm.Add(i, i);
                }
                sm.Remove(11);
                sm.Remove(12);
                var onlineOp = new OnlineSumAvg<int, double, SortedMapCursor<int, double>>();
                var smaOp = new SpanOpWidth<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>
                    (2, Lookup.EQ, onlineOp);
                var smaSeries =
                    new SpanOpImpl<int,
                        double,
                        double,
                        SpanOpWidth<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>,
                        SortedMapCursor<int, double>
                    >(sm.GetEnumerator(), smaOp).Source;

                foreach (var keyValuePair in smaSeries)
                {
                    Trace.WriteLine($"{keyValuePair.Key} - {keyValuePair.Value}");
                }
            });
        }

        [Test]
        public void CouldCalculateSMAWithWidth()
        {
            var count = 20;
            var sm = new SortedMap<int, double>();
            for (int i = 1; i <= count; i++)
            {
                sm.Add(i, i);
            }

            DoTest(Lookup.EQ);
            DoTest(Lookup.GE);
            DoTest(Lookup.GT);
            DoTest(Lookup.LE);
            DoTest(Lookup.LT);

            DoTestViaSpanOpWidth(Lookup.EQ);
            DoTestViaSpanOpWidth(Lookup.GE);
            DoTestViaSpanOpWidth(Lookup.GT);
            DoTestViaSpanOpWidth(Lookup.LE);
            DoTestViaSpanOpWidth(Lookup.LT);

            void DoTest(Lookup lookup)
            {
                // width 9 is the same as count = 10 for the regular int series
                var smaOp = new MAvgWidth<int, double, SortedMapCursor<int, double>>(9, lookup);
                var smaCursor =
                    new SpanOpImpl<int,
                        double,
                        double,
                        MAvgWidth<int, double, SortedMapCursor<int, double>>,
                        SortedMapCursor<int, double>
                    >(sm.GetEnumerator(), smaOp);

                // this monster type must be hidden in the same way Lag hides its implementation
                Series<int, double, SpanOpImpl<int, double, double, MAvgWidth<int, double, SortedMapCursor<int, double>>, SortedMapCursor<int, double>>> smaSeries;
                smaSeries = smaCursor.Source;

                var sm2 = smaSeries.ToSortedMap();

                Assert.AreEqual(sm2.First, smaSeries.First);
                Assert.AreEqual(sm2.Last, smaSeries.Last);

                Assert.True(SeriesContract.MoveAtShouldWorkOnLazySeries(smaSeries));
                Assert.True(SeriesContract.ClonedCursorsAreIndependent(smaSeries));

                if (lookup == Lookup.EQ || lookup == Lookup.GE)
                {
                    var trueSma = sm.Window(10).Map(x => x.Values.Average());
                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                if (lookup == Lookup.GT)
                {
                    var trueSma = sm.Window(11).Map(x => x.Values.Average());
                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                if (lookup == Lookup.LE)
                {
                    var smaOp1 = new MAvgCount<int, double, SortedMapCursor<int, double>>(10, true);
                    var trueSma =
                        new SpanOpImpl<int,
                            double,
                            double,
                            MAvgCount<int, double, SortedMapCursor<int, double>>,
                            SortedMapCursor<int, double>
                        >(sm.GetEnumerator(), smaOp1).Source;

                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                if (lookup == Lookup.LT)
                {
                    var smaOp1 = new MAvgCount<int, double, SortedMapCursor<int, double>>(9, true);
                    var trueSma =
                        new SpanOpImpl<int,
                            double,
                            double,
                            MAvgCount<int, double, SortedMapCursor<int, double>>,
                            SortedMapCursor<int, double>
                        >(sm.GetEnumerator(), smaOp1).Source;

                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                Debug.WriteLine("SMA");
                foreach (var keyValuePair in smaSeries)
                {
                    Debug.WriteLine($"{keyValuePair.Key} - {keyValuePair.Value}");
                }
            }

            void DoTestViaSpanOpWidth(Lookup lookup)
            {
                // width 9 is the same as count = 10 for the regular int series

                var onlineOp = new OnlineSumAvg<int, double, SortedMapCursor<int, double>>();
                var smaOp = new SpanOpWidth<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>(9, lookup, onlineOp);
                var smaCursor =
                    new SpanOpImpl<int,
                        double,
                        double,
                        SpanOpWidth<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>,
                        SortedMapCursor<int, double>
                    >(sm.GetEnumerator(), smaOp);

                // this monster type must be hidden in the same way Lag hides its implementation

                Series<int, double, SpanOpImpl<int, double, double, SpanOpWidth<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>, SortedMapCursor<int, double>>> smaSeries;
                smaSeries = smaCursor.Source;

                var sm2 = smaSeries.ToSortedMap();

                Assert.AreEqual(sm2.First, smaSeries.First);
                Assert.AreEqual(sm2.Last, smaSeries.Last);

                Assert.True(SeriesContract.MoveAtShouldWorkOnLazySeries(smaSeries));
                Assert.True(SeriesContract.ClonedCursorsAreIndependent(smaSeries));

                if (lookup == Lookup.EQ || lookup == Lookup.GE)
                {
                    var trueSma = sm.Window(10).Map(x => x.Values.Average());
                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                if (lookup == Lookup.GT)
                {
                    var trueSma = sm.Window(11).Map(x => x.Values.Average());
                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                if (lookup == Lookup.LE)
                {
                    var smaOp1 = new MAvgCount<int, double, SortedMapCursor<int, double>>(10, true);
                    var trueSma =
                        new SpanOpImpl<int,
                            double,
                            double,
                            MAvgCount<int, double, SortedMapCursor<int, double>>,
                            SortedMapCursor<int, double>
                        >(sm.GetEnumerator(), smaOp1).Source;

                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                if (lookup == Lookup.LT)
                {
                    var smaOp1 = new MAvgCount<int, double, SortedMapCursor<int, double>>(9, true);
                    var trueSma =
                        new SpanOpImpl<int,
                            double,
                            double,
                            MAvgCount<int, double, SortedMapCursor<int, double>>,
                            SortedMapCursor<int, double>
                        >(sm.GetEnumerator(), smaOp1).Source;

                    Assert.True(trueSma.Keys.SequenceEqual(smaSeries.Keys));
                    Assert.True(trueSma.Values.SequenceEqual(smaSeries.Values));
                }

                Debug.WriteLine("SMA");
                foreach (var keyValuePair in smaSeries)
                {
                    Debug.WriteLine($"{keyValuePair.Key} - {keyValuePair.Value}");
                }
            }
        }

        [Test, Ignore]
        public void SMADirectAndIndirectSpanOpBenchmark()
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            var count = 1000000;
            var width = 20;
            var sm = new SortedMap<int, double>();
            sm.Add(0, 0); // make irregular, it's faster but more memory
            for (int i = 2; i <= count; i++)
            {
                sm.Add(i, i);
            }

            var directSMA =
                new SpanOpImpl<int,
                    double,
                    double,
                    MAvgCount<int, double, SortedMapCursor<int, double>>,
                    SortedMapCursor<int, double>
                >(sm.GetEnumerator(), new MAvgCount<int, double, SortedMapCursor<int, double>>(width, false)).Source;

            var indirectSma =
                new SpanOpImpl<int,
                    double,
                    double,
                    SpanOpCount<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>,
                    SortedMapCursor<int, double>
                >(sm.GetEnumerator(),
                    new SpanOpCount<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>(width, false, new OnlineSumAvg<int, double, SortedMapCursor<int, double>>())).Source;

            var indirectSmaCombined =
                new SpanOpImpl<int,
                    double,
                    double,
                    SpanOp<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>,
                    SortedMapCursor<int, double>
                >(sm.GetEnumerator(),
                    new SpanOp<int, double, double, SortedMapCursor<int, double>, OnlineSumAvg<int, double, SortedMapCursor<int, double>>>(width, false, new OnlineSumAvg<int, double, SortedMapCursor<int, double>>())).Source;

            var windowSma = sm.Window(width).Map(x =>
            {
                var sum = 0.0;
                var c = 0;
                foreach (var keyValuePair in x)
                {
                    sum += keyValuePair.Value;
                    c++;
                }
                return sum / c; // x.CursorDefinition.Count TODO
            });

            for (int round = 0; round < 20; round++)
            {
                double sum1 = 0.0;
                using (Benchmark.Run("SMA Direct", count * width))
                {
                    foreach (var keyValuePair in directSMA)
                    {
                        sum1 += keyValuePair.Value;
                    }
                }

                double sum2 = 0.0;
                using (Benchmark.Run("SMA Indirect", count * width))
                {
                    foreach (var keyValuePair in indirectSma)
                    {
                        sum2 += keyValuePair.Value;
                    }
                }

                double sum2_2 = 0.0;
                using (Benchmark.Run("SMA Indirect Combined", count * width))
                {
                    foreach (var keyValuePair in indirectSmaCombined)
                    {
                        sum2_2 += keyValuePair.Value;
                    }
                }

                double sum3 = 0.0;
                using (Benchmark.Run("SMA Window", count * width))
                {
                    foreach (var keyValuePair in windowSma)
                    {
                        sum3 += keyValuePair.Value;
                    }
                }

                var sumxx = 0.0;
                using (Benchmark.Run("SortedMap", count))
                {
                    foreach (var keyValuePair in sm)
                    {
                        sumxx += keyValuePair.Value;
                    }
                }

                Assert.AreEqual(sum1, sum2);
                Assert.AreEqual(sum1, sum2_2);
                Assert.AreEqual(sum1, sum3);
            }

            Benchmark.Dump($"The window width is {width}. SMA MOPS are calculated as a number of calculated values multiplied by width, " +
                           $"which is equivalent to the total number of cursor moves for Window case. SortedMap line is for reference - it is the " +
                           $"speed of raw iteration over SM without Windows overheads.");
        }

        [Test, Ignore]
        public void WindowDirectAndIndirectSpanOpBenchmark()
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            var count = 100000;
            var width = 20;
            var sm = new SortedMap<int, double>();
            sm.Add(0, 0); // make irregular, it's faster but more memory
            for (int i = 2; i <= count; i++)
            {
                sm.Add(i, i);
            }

            var op = new OnlineWindow<int, double, SortedMapCursor<int, double>>();
            var spanOp =
                new SpanOpCount<int, double, Range<int, double, SortedMapCursor<int, double>>,
                    SortedMapCursor<int, double>, OnlineWindow<int, double, SortedMapCursor<int, double>>>(20, false,
                    op);
            var window =
                new SpanOpImpl<int,
                    double,
                    Range<int, double, SortedMapCursor<int, double>>,
                    SpanOpCount<int, double, Range<int, double, SortedMapCursor<int, double>>, SortedMapCursor<int, double>, OnlineWindow<int, double, SortedMapCursor<int, double>>>,
                    SortedMapCursor<int, double>
                >(sm.GetEnumerator(), spanOp).Source
                    .Map(x =>
                    {
                        var sum = 0.0;
                        var c = 0;
                        foreach (var keyValuePair in x.Source)
                        {
                            sum += keyValuePair.Value;
                            c++;
                        }
                        return sum / c; // x.CursorDefinition.Count TODO
                    });

            var spanOpCombined =
                new SpanOp<int, double, Range<int, double, SortedMapCursor<int, double>>,
                    SortedMapCursor<int, double>, OnlineWindow<int, double, SortedMapCursor<int, double>>>(20, false,
                    op);
            var windowCombined =
                new SpanOpImpl<int,
                    double,
                    Range<int, double, SortedMapCursor<int, double>>,
                    SpanOp<int, double, Range<int, double, SortedMapCursor<int, double>>, SortedMapCursor<int, double>, OnlineWindow<int, double, SortedMapCursor<int, double>>>,
                    SortedMapCursor<int, double>
                >(sm.GetEnumerator(), spanOpCombined).Source
                .Map(x =>
                {
                    var sum = 0.0;
                    var c = 0;
                    foreach (var keyValuePair in x.Source)
                    {
                        sum += keyValuePair.Value;
                        c++;
                    }
                    return sum / c; // x.CursorDefinition.Count TODO
                });

            var windowExtension = sm.Window(width).Map(x =>
            {
                var sum = 0.0;
                var c = 0;
                foreach (var keyValuePair in x)
                {
                    sum += keyValuePair.Value;
                    c++;
                }
                return sum / c; // x.CursorDefinition.Count TODO
            });

            for (int round = 0; round < 20; round++)
            {
                double sum1 = 0.0;
                using (Benchmark.Run("Window SpanOpCount", count * width))
                {
                    foreach (var keyValuePair in window)
                    {
                        sum1 += keyValuePair.Value;
                    }
                }

                double sum2 = 0.0;
                using (Benchmark.Run("Window SpanOp", count * width))
                {
                    foreach (var keyValuePair in windowCombined)
                    {
                        sum2 += keyValuePair.Value;
                    }
                }

                double sum3 = 0.0;
                using (Benchmark.Run("Window Extension", count * width))
                {
                    foreach (var keyValuePair in windowExtension)
                    {
                        sum3 += keyValuePair.Value;
                    }
                }


                Assert.AreEqual(sum1, sum2);
                Assert.AreEqual(sum1, sum3);
            }

            Benchmark.Dump($"The window width is {width}.");
        }
    }
}