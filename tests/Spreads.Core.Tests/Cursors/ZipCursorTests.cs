// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Deedle;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class ZipCursorTests
    {
        public void CouldAddTwoSeriesWithSameKeys(double expected, SortedMap<int, double> sm1,
            SortedMap<int, double> sm2)
        {
            var count = sm1.Count;

            var c1 = sm1.GetEnumerator();
            var c2 = sm2.GetEnumerator();

            var ds1 = new Deedle.Series<int, double>(sm1.Keys.ToArray(), sm1.Values.ToArray());
            var ds2 = new Deedle.Series<int, double>(sm2.Keys.ToArray(), sm2.Values.ToArray());

            Assert.NotNull(c1.Comparer);
            Assert.NotNull(c2.Comparer);


            var zipSum = (sm1 + sm2);
            double actual = 0;
            var zipCursor = //zipSum.GetEnumerator();
                new Zip<int, double, double, SortedMapCursor<int, double>,
                        SortedMapCursor<int, double>>(
                        c1, c2); //.Map((k, v) => v.Item1 + v.Item2)

            var op2 = new Op2<int, double, AddOp<double>, Zip<int, double, double, SortedMapCursor<int, double>,
                SortedMapCursor<int, double>>>(zipCursor).Source;

            var zipCursorOp2 = op2.GetEnumerator();

            using (Benchmark.Run("Zip", count * 2))
            {
                while (zipCursorOp2.MoveNext())
                {
                    actual += zipCursorOp2.CurrentValue;
                }
            }
            Assert.AreEqual(expected, actual);

            //var zipped = sm1.Zip(sm2, (k, l, r) => l + r); // sm1.Zip(sm2).Map((k, v) => v.Item1 + v.Item2); //
            //actual = 0;
            //using (Benchmark.Run("Zip MN Extension", count * 2))
            //{
            //    foreach (var keyValuePair in zipped)
            //    {
            //        actual += keyValuePair.Value;
            //    }
            //}
            //Assert.AreEqual(expected, actual);

            //var zipN = new[] { sm1, sm2 }.Zip((k, varr) => varr[0] + varr[1], true).GetCursor();
            //actual = 0;
            //using (Benchmark.Run("ZipN", count * 2))
            //{
            //    while (zipN.MoveNext())
            //    {
            //        actual += zipN.CurrentValue;
            //    }
            //}
            //Assert.AreEqual(expected, actual);


            //var zipNOld = new[] { sm1, sm2 }.ZipOld((k, varr) => varr[0] + varr[1]).GetCursor();
            //actual = 0;
            //using (Benchmark.Run("ZipN (old)", count * 2))
            //{
            //    while (zipNOld.MoveNext())
            //    {
            //        actual += zipNOld.CurrentValue;
            //    }
            //}
            //Assert.AreEqual(expected, actual);

            //zipCursor.Reset();
            //actual = 0;
            //using (Benchmark.Run("Zip MP", count * 2))
            //{
            //    while (zipCursor.MovePrevious())
            //    {
            //        actual += zipCursor.CurrentValue;
            //    }
            //}
            //Assert.AreEqual(expected, actual);

            actual = 0;
            using (Benchmark.Run("LINQ", count * 2))
            {
                var linq = sm1.Zip(sm2, (l, r) => l.Value + r.Value);
                foreach (var d in linq)
                {
                    actual += d;
                }
            }
            Assert.AreEqual(expected, actual);

            actual = 0;
            using (Benchmark.Run("Deedle", count * 2))
            {
                var sum = ds1 + ds2;
                foreach (var v in sum.Values) // ds1.ZipInner(ds2).Values)
                {
                    actual += v; //.Item1 + v.Item2;
                }
            }
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void CouldAddTwoSeriesWithSameKeys()
        {
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();

            double expected = 0;

            sm1.Add(0, 0);
            sm2.Add(0, 0);

            for (int i = 2; i < 100000; i++)
            {
                expected += i + 2 * i;
                sm1.Add(i, i);
                sm2.Add(i, 2 * i);
            }

            CouldAddTwoSeriesWithSameKeys(expected, sm1, sm2);
        }

        [Test, Ignore]
        public void CouldAddTwoSeriesWithSameKeysBenchmark()
        {
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();

            double expected = 0;

            sm1.Add(0, 0);
            sm2.Add(0, 0);

            for (int i = 2; i < 1000000; i++)
            {
                expected += i + 2 * i;
                sm1.Add(i, i);
                sm2.Add(i, 2 * i);
            }

            for (int i = 0; i < 20; i++)
            {
                CouldAddTwoSeriesWithSameKeys(expected, sm1, sm2);
            }
            Benchmark.Dump();
        }

        [Test]
        public void CouldAddTwoSeriesWithDifferentKeys()
        {
            var count = 1000000;
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();

            var result = new SortedMap<int, double>();
            double expected = 0;

            for (int i = 0; i < count; i++)
            {
                sm1.Add(i, i);
                if (i % 3 == 0)
                {
                    sm2.Add(i, 2 * i);
                    expected += i + 2 * i;

                    result.Add(i, i + 2 * i);
                }
            }

            for (int r = 0; r < 1; r++)
            {
                var c1 = sm1.GetEnumerator();
                var c2 = sm2.GetEnumerator();
                var zipCursor =
                    new Zip<int, double, double, SortedMapCursor<int, double>,
                        SortedMapCursor<int, double>>(
                        c1, c2).Initialize();

                double actual = 0;
                using (Benchmark.Run("ZipDiscrete MN", sm1.Count + sm2.Count))
                {
                    while (zipCursor.MoveNext())
                    {
                        actual += zipCursor.Map((k, v) => v.Item1 + v.Item2).CurrentValue;
                    }
                }
                Assert.AreEqual(expected, actual);

                zipCursor.Reset();
                actual = 0;
                using (Benchmark.Run("ZipDiscrete MP", sm1.Count + sm2.Count))
                {
                    while (zipCursor.MovePrevious())
                    {
                        actual += zipCursor.Map((k, v) => v.Item1 + v.Item2).CurrentValue;
                    }
                }
                Assert.AreEqual(expected, actual);
            }

            Benchmark.Dump();
        }

        [Test]
        public void CouldUseBaseSeriesAddOperator()
        {
            var count = 1000000;
            var sm1 = new SortedChunkedMap<int, double>();
            var sm2 = new SortedChunkedMap<int, double>();

            var result = new SortedMap<int, double>();
            double expected = 0;

            sm1.Add(0, 0);
            sm2.Add(0, 0);

            for (int i = 2; i < count; i++)
            {
                sm1.Add(i, i);
                if (i % 1 == 0)
                {
                    sm2.Add(i, 2 * i);
                    expected += i + 2 * i;

                    result.Add(i, i + 2 * i);
                }
            }

            for (int round = 0; round < 20; round++)
            {
                double actual = 0;
                using (Benchmark.Run("ZipDiscrete", (int)sm1.Count + (int)sm2.Count))
                {
                    var calculated = sm1 + sm2;
                    using (var c = calculated.GetEnumerator())
                    {
                        while (c.MoveNext())
                        {
                            actual += c.CurrentValue;
                        }
                    }
                    //foreach (var cursorSeries in calculated)
                    //{
                    //    actual += cursorSeries.Value;
                    //}
                }

                Assert.AreEqual(expected, actual);

                //double actual1 = 0;
                //using (Benchmark.Run("LINQ", (int)sm1.Count + (int)sm2.Count))
                //{
                //    var linq = sm1.Zip(sm2, (l, r) => l.Value + r.Value);
                //    foreach (var l in linq)
                //    {
                //        actual1 += l;
                //    }
                //}

                //Assert.AreEqual(expected, actual);
            }

            Benchmark.Dump();
        }

        [Test]
        public void CouldAddContinuousSeries()
        {
            var count = 1000000; //0;
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();

            double expected = 0;

            for (int i = 1; i < count; i++)
            {
                if (i % 2 == 0)
                {
                    sm2.Add(i, 2 * i);
                    expected += 123 + 2 * i;
                }
                else
                {
                    sm1.Add(i, i);
                    expected += i + 42;
                }
            }

            for (int r = 0; r < 20; r++)
            {
                var fc1 = new Fill<int, double, SortedMapCursor<int, double>>(sm1.GetEnumerator(), 123);
                var fc2 = new Fill<int, double, SortedMapCursor<int, double>>(sm2.GetEnumerator(), 42);
                var zipCursor =
                    new Zip<int, double, double, Fill<int, double, SortedMapCursor<int, double>>,
                            Fill<int, double, SortedMapCursor<int, double>>>(
                            fc1, fc2).Map((k, v) => v.Item1 + v.Item2)
                        .Initialize();

                double actual = 0;
                using (Benchmark.Run("ZipContinuous MN", (sm1.Count + sm2.Count) * 2)
                ) // NB multiply by 2, we evaluate on missing, should count virtual values as well
                {
                    while (zipCursor.MoveNext())
                    {
                        actual += zipCursor.CurrentValue;
                    }
                }
                Assert.AreEqual(expected, actual);

                zipCursor.Reset();
                actual = 0;
                using (Benchmark.Run("ZipContinuous MP", (sm1.Count + sm2.Count) * 2)
                ) // NB multiply by 2, we evaluate on missing, should count virtual values as well
                {
                    while (zipCursor.MoveNext())
                    {
                        actual += zipCursor.CurrentValue;
                    }
                }
                Assert.AreEqual(expected, actual);
            }

            Benchmark.Dump();
        }

        [Test]
        public void CouldMoveAt()
        {
            var count = 100;
            var sm0 = new SortedMap<int, double>();
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();

            double expected = 0;

            for (int i = 1; i < count; i++)
            {
                sm0.Add(i, i);

                if (i % 3 == 0)
                {
                    sm2.Add(i, 2 * i);
                    expected += 123 + 2 * i;
                }
                else
                {
                    sm1.Add(i, i);
                    expected += i + 42;
                }
            }

            var fc = new Fill<int, double, SortedMapCursor<int, double>>(sm0.GetEnumerator(), -1);
            var fc1 = new Fill<int, double, SortedMapCursor<int, double>>(sm1.GetEnumerator(), 123);
            var fc2 = new Fill<int, double, SortedMapCursor<int, double>>(sm2.GetEnumerator(), 42);

            var zipCursor =
                new Zip<int, double, double, Fill<int, double, SortedMapCursor<int, double>>,
                        Fill<int, double, SortedMapCursor<int, double>>>(
                        fc, fc2).Map((k, v) => v.Item1 + v.Item2)
                    .Initialize();

            Assert.IsTrue(zipCursor.MoveAt(2, Lookup.EQ));
            Assert.AreEqual(44, zipCursor.CurrentValue);

            Assert.IsTrue(zipCursor.MoveAt(2, Lookup.LE));
            Assert.AreEqual(44, zipCursor.CurrentValue);

            Assert.IsTrue(zipCursor.MoveAt(2, Lookup.GE));
            Assert.AreEqual(44, zipCursor.CurrentValue);

            Assert.IsTrue(zipCursor.MoveAt(2, Lookup.LT));
            Assert.AreEqual(43, zipCursor.CurrentValue);

            Assert.IsTrue(zipCursor.MoveAt(2, Lookup.GT));
            Assert.AreEqual(9, zipCursor.CurrentValue);

            var zipCursor1 =
                new Zip<int, double, double, Fill<int, double, SortedMapCursor<int, double>>,
                        Fill<int, double, SortedMapCursor<int, double>>>(
                        fc1, fc2).Map((k, v) => v.Item1 + v.Item2)
                    .Initialize();

            Assert.IsTrue(zipCursor1.MoveAt(2, Lookup.EQ));
            Assert.AreEqual(44, zipCursor1.CurrentValue);

            Assert.IsTrue(zipCursor1.MoveAt(2, Lookup.LE));
            Assert.AreEqual(44, zipCursor1.CurrentValue);

            Assert.IsTrue(zipCursor1.MoveAt(2, Lookup.GE));
            Assert.AreEqual(44, zipCursor1.CurrentValue);

            Assert.IsTrue(zipCursor1.MoveAt(2, Lookup.LT));
            Assert.AreEqual(43, zipCursor1.CurrentValue);

            Assert.IsTrue(zipCursor1.MoveAt(2, Lookup.GT));
            Assert.AreEqual(129, zipCursor1.CurrentValue);
        }

        [Test, Ignore]
        public void DiscreteZipIsCorrectByRandomCheckBenchmark()
        {
            for (int r = 0; r < 1000; r++)
            {
                var sm1 = new SortedMap<int, int>();
                var sm2 = new SortedMap<int, int>();

                var rng = new Random(r);

                var prev1 = 0;
                var prev2 = 0;
                for (int i = 0; i < 100000; i = i + 1)
                {
                    prev1 = prev1 + rng.Next(1, 11);
                    sm1.Add(prev1, prev1);
                    prev2 = prev2 + rng.Next(1, 11);
                    sm2.Add(prev2, prev2);
                }
                sm1.Complete();
                sm2.Complete();

                DiscreteZipIsCorrectByRandomCheck(sm1, sm2, r);
            }
            Benchmark.Dump();
        }

        [Test]
        public void DiscreteZipIsCorrectByRandomCheck()
        {
            var sm1 = new SortedMap<int, int>();
            var sm2 = new SortedMap<int, int>();

            var rng = new Random(9);

            var prev1 = 0;
            var prev2 = 0;
            for (int i = 0; i < 1000; i = i + 1)
            {
                prev1 = prev1 + rng.Next(1, 11);
                sm1.Add(prev1, prev1);
                prev2 = prev2 + rng.Next(1, 11);
                sm2.Add(prev2, prev2);
            }
            sm1.Complete();
            sm2.Complete();
            DiscreteZipIsCorrectByRandomCheck(sm1, sm2, 9);
        }

        public void DiscreteZipIsCorrectByRandomCheck(SortedMap<int, int> sm1, SortedMap<int, int> sm2, int seed)
        {
            var series = new[] { sm1, sm2, };

            int[] expectedKeys;
            int[] expectedValues;
            int size;
            SortedMap<int, int> expectedMap;

            using (Benchmark.Run("Manual join", sm1.Count + sm2.Count))
            {
                var allKeys = sm1.keys.Union(sm2.keys).OrderBy(x => x).ToArray();

                expectedKeys = new int[allKeys.Length];

                expectedValues = new int[allKeys.Length];
                size = 0;
                for (int i = 0; i < allKeys.Length; i++)
                {
                    var val = 0;
                    KeyValuePair<int, int> temp;
                    var hasFirst = sm1.TryFind(allKeys[i], Lookup.EQ, out temp);
                    if (hasFirst)
                    {
                        val += temp.Value;
                        var hasSecond = sm2.TryFind(allKeys[i], Lookup.EQ, out temp);
                        if (hasSecond)
                        {
                            val += temp.Value;
                            expectedKeys[size] = allKeys[i];
                            expectedValues[size] = val;
                            size++;
                        }
                    }
                }
                expectedMap = SortedMap<int, int>.OfSortedKeysAndValues(expectedKeys, expectedValues, size);
            }

            SortedMap<int, int> sum;
            Series<int, int> ser;

            using (Benchmark.Run("Zip join", sm1.Count + sm2.Count))
            {
                sum = sm1.Zip(sm2, (v1, v2) => v1 + v2).ToSortedMap();
            }
            Assert.AreEqual(expectedMap.Count, sum.Count, "Expected size");
            foreach (var kvp in expectedMap)
            {
                Assert.AreEqual(kvp.Value, sum[kvp.Key]);
            }

            //using (Benchmark.Run("ZipN join", sm1.Count + sm2.Count))
            //{
            //    ser = series.ZipOld((k, varr) => varr.Sum());
            //    sum = ser.ToSortedMap();
            //}
            //Assert.AreEqual(expectedMap.Count, sum.Count, "Expected size");
            //foreach (var kvp in expectedMap)
            //{
            //    Assert.AreEqual(kvp.Value, sum[kvp.Key]);
            //}

            var sum1 = new SortedMap<int, int>();
            using (Benchmark.Run("Zip Async join", sm1.Count + sm2.Count))
            {
                var zip = sm1.Zip(sm2, (v1, v2) => v1 + v2);
                var cur = zip.GetCursor();
                var last = zip.Last.Key;
                Task.Run(async () =>
                    {
                        var prev = default(int);
                        while (await cur.MoveNext(CancellationToken.None))
                        {
                            if (cur.CurrentKey == prev)
                            {
                                Console.WriteLine($"Break on equal keys condition, seed {seed}");
                            }
                            prev = cur.CurrentKey;
                            sum1.Add(cur.CurrentKey, cur.CurrentValue);
                            //if (prev == last)
                            //{
                            //    Console.WriteLine("Next should be false");
                            //}
                        }
                    })
                    .Wait();
            }
            Assert.AreEqual(expectedMap.Count, sum1.Count, "Results of sync and async moves must be equal");
            foreach (var kvp in expectedMap)
            {
                Assert.AreEqual(kvp.Value, sum1[kvp.Key]);
            }

            //var sum2 = new SortedMap<int, int>();
            //using (Benchmark.Run("ZipN Async join", sm1.Count + sm2.Count))
            //{
            //    var cur = ser.GetCursor();

            //    var cur2 = cur.Clone();

            //    Task.Run(async () =>
            //        {
            //            while (await cur2.MoveNext(CancellationToken.None))
            //            {
            //                sum2.Add(cur2.CurrentKey, cur2.CurrentValue);
            //            }
            //        })
            //        .Wait();
            //}

            //Assert.AreEqual(sum.Count, sum2.Count, "Results of sync and async moves must be equal");
            //foreach (var kvp in expectedMap)
            //{
            //    Assert.AreEqual(kvp.Value, sum2[kvp.Key]);
            //}
        }

        [Test, Ignore]
        public void ContinuousZipIsCorrectByRandomCheckBenchmark()
        {
            for (int r = 0; r < 1000; r++)
            {
                var sm1 = new SortedMap<int, int>();
                var sm2 = new SortedMap<int, int>();

                var rng = new Random(r);

                var prev1 = 0;
                var prev2 = 0;
                for (int i = 0; i < 100000; i = i + 1)
                {
                    prev1 = prev1 + rng.Next(1, 11);
                    sm1.Add(prev1, prev1);
                    prev2 = prev2 + rng.Next(1, 11);
                    sm2.Add(prev2, prev2);
                }
                sm1.Complete();
                sm2.Complete();

                ContinuousZipIsCorrectByRandomCheck(sm1, sm2, r);
            }
            Benchmark.Dump();
        }

        [Test]
        public void ContinuousZipIsCorrectByRandomCheck()
        {
            var sm1 = new SortedMap<int, int>();
            var sm2 = new SortedMap<int, int>();

            var rng = new Random(9);

            var prev1 = 0;
            var prev2 = 0;
            for (int i = 0; i < 1000; i = i + 1)
            {
                prev1 = prev1 + rng.Next(1, 11);
                sm1.Add(prev1, prev1);
                prev2 = prev2 + rng.Next(1, 11);
                sm2.Add(prev2, prev2);
            }
            sm1.Complete();
            sm2.Complete();
            ContinuousZipIsCorrectByRandomCheck(sm1, sm2, 9);
        }

        public void ContinuousZipIsCorrectByRandomCheck(SortedMap<int, int> sm1, SortedMap<int, int> sm2, int seed)
        {
            var series = new ISeries<int, int>[] { sm1.Repeat(), sm2 };

            int[] expectedKeys;
            int[] expectedValues;
            int size;
            SortedMap<int, int> expectedMap;

            using (Benchmark.Run("Manual join", sm1.Count + sm2.Count))
            {
                var allKeys = sm1.keys.Union(sm2.keys).OrderBy(x => x).ToArray();

                expectedKeys = new int[allKeys.Length];

                expectedValues = new int[allKeys.Length];
                size = 0;
                for (int i = 0; i < allKeys.Length; i++)
                {
                    var val = 0;
                    KeyValuePair<int, int> temp;
                    var hasFirst = sm1.TryFind(allKeys[i], Lookup.LE, out temp);
                    if (hasFirst)
                    {
                        val += temp.Value;
                        var hasSecond = sm2.TryFind(allKeys[i], Lookup.EQ, out temp);
                        if (hasSecond)
                        {
                            val += temp.Value;
                            expectedKeys[size] = allKeys[i];
                            expectedValues[size] = val;
                            size++;
                        }
                    }
                }
                expectedMap = SortedMap<int, int>.OfSortedKeysAndValues(expectedKeys, expectedValues, size);
            }

            SortedMap<int, int> sum;
            Series<int, int> ser;

            using (Benchmark.Run("Zip join", sm1.Count + sm2.Count))
            {
                sum = sm1.Repeat().Zip(sm2, (v1, v2) => v1 + v2).ToSortedMap();
            }
            Assert.AreEqual(expectedMap.Count, sum.Count, "Expected size");
            foreach (var kvp in expectedMap)
            {
                Assert.AreEqual(kvp.Value, sum[kvp.Key]);
            }

            //using (Benchmark.Run("ZipN join", sm1.Count + sm2.Count))
            //{
            //    ser = series.ZipOld((k, varr) => varr.Sum());
            //    sum = ser.ToSortedMap();
            //}
            //Assert.AreEqual(expectedMap.Count, sum.Count, "Expected size");
            //foreach (var kvp in expectedMap)
            //{
            //    Assert.AreEqual(kvp.Value, sum[kvp.Key]);
            //}

            var sum1 = new SortedMap<int, int>();
            using (Benchmark.Run("Zip Async join", sm1.Count + sm2.Count))
            {
                var zip = sm1.Repeat().Zip(sm2, (v1, v2) => v1 + v2);
                var cur = zip.GetCursor();
                var last = zip.Last.Key;
                Task.Run(async () =>
                    {
                        var prev = default(int);
                        while (await cur.MoveNext(CancellationToken.None))
                        {
                            if (cur.CurrentKey == prev)
                            {
                                Console.WriteLine($"Break on equal keys condition, seed {seed}");
                            }
                            prev = cur.CurrentKey;
                            sum1.Add(cur.CurrentKey, cur.CurrentValue);
                            //if (prev == last)
                            //{
                            //    Console.WriteLine("Next should be false");
                            //}
                        }
                    })
                    .Wait();
            }
            Assert.AreEqual(expectedMap.Count, sum1.Count, "Results of sync and async moves must be equal");
            foreach (var kvp in expectedMap)
            {
                Assert.AreEqual(kvp.Value, sum1[kvp.Key]);
            }

            // TODO this uses Subscribe which is not implemented yet
            //var sum2 = new SortedMap<int, int>();
            //using (Benchmark.Run("ZipN Async join", sm1.Count + sm2.Count))
            //{
            //    var cur = ser.GetCursor();

            //    var cur2 = cur.Clone();

            //    Task.Run(async () =>
            //        {
            //            while (await cur2.MoveNext(CancellationToken.None))
            //            {
            //                sum2.Add(cur2.CurrentKey, cur2.CurrentValue);
            //            }
            //        })
            //        .Wait();
            //}

            //Assert.AreEqual(sum.Count, sum2.Count, "Results of sync and async moves must be equal");
            //foreach (var kvp in expectedMap)
            //{
            //    Assert.AreEqual(kvp.Value, sum2[kvp.Key]);
            //}
        }

        [Test, Ignore]
        public void CouldAddSeriesArrayWithSameKeys()
        {
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();
            var sm3 = new SortedMap<int, double>();

            double expected = 0;

            sm1.Add(0, 0);
            sm2.Add(0, 0);
            sm3.Add(0, 0);

            for (int i = 2; i < 1000000; i++)
            {
                expected += i + 2 * i + 3 * i;
                sm1.Add(i, i);
                sm2.Add(i, 2 * i);
                sm3.Add(i, 3 * i);
            }

            var sumSeries = new[] { sm1, sm2, sm3 }.Zip((k, varr) =>
            {
                var s = 0.0;
                for (int i = 0; i < varr.Length; i++)
                {
                    s += varr[i];
                }
                return s;
                //return varr.Sum(); // this allocates
            }, true);

            var count = sm1.Count;

            //var sumSeries2 = new[] { sm1, sm2, sm3 }.ZipOld((k, varr) =>
            //{
            //    var s = 0.0;
            //    for (int i = 0; i < varr.Length; i++)
            //    {
            //        s += varr[i];
            //    }
            //    return s;
            //    //return varr.Sum(); // this allocates
            //});

            for (int i = 0; i < 20; i++)
            {
                double actual = 0.0;

                using (Benchmark.Run("Zip", count * 3))
                {
                    foreach (var kvp in sumSeries)
                    {
                        actual += kvp.Value;
                    }
                }

                Assert.AreEqual(expected, actual);


                //actual = 0.0;
                //using (Benchmark.Run("ZipN old", count * 3))
                //{
                //    foreach (var kvp in sumSeries2)
                //    {
                //        actual += kvp.Value;
                //    }
                //}
                //Assert.AreEqual(expected, actual);
            }

            Benchmark.Dump();
        }
    }
}