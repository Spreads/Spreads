// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors;
using Spreads.Utils;

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

            using (Benchmark.Run("ZipDiscrete", count * 2))
            {
                var zipCursor =
                    new ZipCursor<int, double, double, double, SortedMapCursor<int, double>,
                        SortedMapCursor<int, double>>(
                        c1, c2, (_, v1, v2) => v1 + v2).Initialize();

                while (zipCursor.MoveNext())
                {
                    actual += zipCursor.CurrentValue;
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
            var count = 10000000;
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

            for (int r = 0; r < 20; r++)
            {
                var c1 = sm1.GetEnumerator();
                var c2 = sm2.GetEnumerator();

                double actual = 0;
                using (Benchmark.Run("ZipDiscrete", sm1.Count + sm2.Count))
                {
                    var zipCursor =
                        new ZipCursor<int, double, double, double, SortedMapCursor<int, double>,
                            SortedMapCursor<int, double>>(
                            c1, c2, (_, v1, v2) => v1 + v2).Initialize();

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
        public void CouldUseBaseSeriesAddOperator()
        {
            var count = 10000000;
            var sm1 = new SortedMap<int, double>();
            var sm2 = new SortedMap<int, double>();

            var result = new SortedMap<int, double>();
            double expected = 0;

            for (int i = 0; i < count; i++)
            {
                sm1.Add(i, i);
                if (i % 1 == 0)
                {
                    sm2.Add(i, 2 * i);
                    expected += i + 2 * i;

                    result.Add(i, i + 2 * i);
                }
            }

            var calculated = sm1 + sm2;

            for (int r = 0; r < 20; r++)
            {
                double actual = 0;
                using (Benchmark.Run("ZipDiscrete", sm1.Count + sm2.Count))
                {
                    foreach (var cursorSeries in calculated)
                    {
                        actual += cursorSeries.Value;
                    }
                }

                Assert.AreEqual(expected, actual);
            }

            Benchmark.Dump();
        }
    }
}