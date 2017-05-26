﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors;
using Spreads.Utils;
using System.Linq;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class ZipCursorTests
    {
        public void CouldAddTwoSeriesWithSameKeys(double expected, SortedMap<int, double> sm1, SortedMap<int, double> sm2)
        {
            var count = sm1.Count;

            var c1 = sm1.GetEnumerator();
            var c2 = sm2.GetEnumerator();

            Assert.NotNull(c1.Comparer);
            Assert.NotNull(c2.Comparer);

            double actual = 0;
            double actual1 = 0;
            var zipCursor =
                new Zip<int, double, double, SortedMapCursor<int, double>,
                    SortedMapCursor<int, double>>(
                    c1, c2).Map((k, v) => v.Item1 + v.Item2).Initialize();

            using (Benchmark.Run("ZipDiscrete MN", count * 2))
            {
                while (zipCursor.MoveNext())
                {
                    actual += zipCursor.CurrentValue;
                }
            }
            Assert.AreEqual(expected, actual);

            var zipped = sm1.Zip(sm2, (k, l, r) => l + r); // sm1.Zip(sm2).Map((k, v) => v.Item1 + v.Item2); //
            actual = 0;
            using (Benchmark.Run("ZipDiscrete MN Extension", count * 2))
            {
                foreach (var keyValuePair in zipped)
                {
                    actual += keyValuePair.Value;
                }
            }
            Assert.AreEqual(expected, actual);

            zipCursor.Reset();
            actual = 0;
            using (Benchmark.Run("ZipDiscrete MP", count * 2))
            {
                while (zipCursor.MovePrevious())
                {
                    actual += zipCursor.CurrentValue;
                }
            }
            Assert.AreEqual(expected, actual);

            using (Benchmark.Run("LINQ", count * 2))
            {
                var linq = sm1.Zip(sm2, (l, r) => l.Value + r.Value);
                foreach (var d in linq)
                {
                    actual1 += d;
                }
            }
            Assert.AreEqual(expected, actual1);
        }

        [Test]
        public void CouldAddTwoSeriesWithSameKeys()
        {
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();

            double expected = 0;

            for (int i = 0; i < 100000; i++)
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

            for (int i = 2; i < 10000000; i++)
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
                        fc1, fc2).Map((k, v) => v.Item1 + v.Item2).Initialize();

                double actual = 0;
                using (Benchmark.Run("ZipContinuous MN", (sm1.Count + sm2.Count) * 2)) // NB multiply by 2, we evaluate on missing, should count virtual values as well
                {
                    while (zipCursor.MoveNext())
                    {
                        actual += zipCursor.CurrentValue;
                    }
                }
                Assert.AreEqual(expected, actual);

                zipCursor.Reset();
                actual = 0;
                using (Benchmark.Run("ZipContinuous MP", (sm1.Count + sm2.Count) * 2)) // NB multiply by 2, we evaluate on missing, should count virtual values as well
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
                    fc, fc2).Map((k, v) => v.Item1 + v.Item2).Initialize();

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
                    fc1, fc2).Map((k, v) => v.Item1 + v.Item2).Initialize();

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
    }
}