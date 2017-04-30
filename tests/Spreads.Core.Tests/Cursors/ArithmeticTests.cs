// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using JetBrains.dotMemoryUnit;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors;
using Spreads.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class ArithmeticTests
    {
        [Test]
        public void CouldMapValues()
        {
            var sm = new SortedMap<int, double>
            {
                { 1, 1 }
            };
            var map = new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>((sm.GetEnumerator), 2);
            var map1 = new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double, MultiplyOp<double>,
                SortedMapCursor<int, double>>>(map.Initialize, 2);

            Assert.AreEqual(2, map.First.Value);
            Assert.AreEqual(4, map1.First.Value);
        }

        [Test, Ignore]
        public void CouldMapValuesBenchmark()
        {
            var sm = new SortedMap<int, double>();
            var count = 10000000;
            //sm.AddLast(0, 0); // make irregular
            for (int i = 2; i < count; i++)
            {
                sm.AddLast(i, i);
            }

            for (int r = 0; r < 10; r++)
            {
                var map =
                    new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
                        sm.GetEnumerator, 2.0);
                var map2 =
                    new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double,
                        MultiplyOp<double>, SortedMapCursor<int, double>>>(
                        map.Initialize, 2.0);
                var sum = 0.0;
                using (Benchmark.Run("ArithmeticSeries", count))
                {
                    foreach (var kvp in map2)
                    {
                        sum += kvp.Value;
                    }
                }
                Assert.IsTrue(sum > 0);
            }

            for (int r = 0; r < 10; r++)
            {
                var map = sm
                    //.Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
                var sum = 0.0;
                using (Benchmark.Run("LINQ", count))
                {
                    foreach (var kvp in map)
                    {
                        sum += kvp.Value;
                    }
                }
                Assert.IsTrue(sum > 0);
            }
        }

        [Test, Ignore]
        // TODO learn how to use dotMemory for total allocatoins count
        //[DotMemoryUnit(FailIfRunWithoutSupport = false)]
        public void MultipleEnumerationDoesntAllocate()
        {
            var sm = new SortedMap<int, double>();
            var count = 100;
            sm.AddLast(0, 0);
            for (int i = 2; i < count; i++)
            {
                sm.AddLast(i, i);
            }

            for (int r = 0; r < 10; r++)
            {
                var map =
                    new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
                        sm.GetEnumerator, 2.0);
                var sum = 0.0;
                var iterations = 100000;
                using (Benchmark.Run("ArithmeticSeries", count * iterations))
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        foreach (var kvp in map)
                        {
                            sum += kvp.Value;
                        }
                    }
                    //dotMemory.Check(memory =>
                    //{
                    //    Assert.That(
                    //        memory.GetObjects(where =>
                    //            where.Type.Is<ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>>()).ObjectsCount,
                    //        Is.EqualTo(1)
                    //    );
                    //});
                }

                Assert.IsTrue(sum > 0);
            }
            Benchmark.Dump();
        }

        [Test, Ignore]
        [DotMemoryUnit(CollectAllocations = true)]
        public void CouldMapValuesBenchmarkArithmeticVsMapCursor()
        {
            var sm = new SortedMap<int, double>();
            var count = 10000000;
            sm.AddLast(0, 0);
            for (int i = 2; i < count; i++)
            {
                sm.AddLast(i, i);
            }

            for (int r = 0; r < 10; r++)
            {
                {
                    var sum = 0.0;
                    using (Benchmark.Run("SortedMap", count))
                    {
                        foreach (var kvp in sm)
                        {
                            sum += kvp.Value;
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                {
                    var map =
                        new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(sm.GetEnumerator,
                            2.0);
                    var map2 = map * 2;
                    //new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double,
                    //    MultiplyOp<double>, SortedMapCursor<int, double>>>(
                    //    map.Initialize, 2.0);
                    var sum = 0.0;
                    using (Benchmark.Run("ArithmeticSeries", count))
                    {
                        foreach (var kvp in map2)
                        {
                            sum += kvp.Value;
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                {
                    var map =
                        new MapValuesSeries<int, double, double, SortedMapCursor<int, double>>(sm,
                            i => Apply(i, 2.0));
                    var map2 =
                        new
                            MapValuesSeries<int, double, double, MapValuesSeries<int, double, double,
                                SortedMapCursor<int, double>>>(map, i => Apply(i, 2.0));
                    var sum = 0.0;
                    using (Benchmark.Run("MapValuesSeries", count))
                    {
                        foreach (var kvp in map2)
                        {
                            sum += kvp.Value;
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                {
                    var map = ((sm as BaseSeries<int, double>) * 2) * 2;
                    var sum = 0.0;
                    using (Benchmark.Run("BaseSeries operator", count))
                    {
                        foreach (var kvp in map)
                        {
                            sum += kvp.Value;
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                {
                    var map = sm
                        .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
                        .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
                    var sum = 0.0;
                    using (Benchmark.Run("LINQ", count))
                    {
                        foreach (var kvp in map)
                        {
                            sum += kvp.Value;
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TValue Apply<TValue>(TValue input, TValue value)
        {
            if (typeof(TValue) == typeof(double))
            {
                var v1 = (double)(object)(input);
                var v2 = (double)(object)(value);

                return (TValue)(object)(double)(v1 * v2);
            }

            if (typeof(TValue) == typeof(float))
            {
                var v1 = (float)(object)(input);
                var v2 = (float)(object)(value);

                return (TValue)(object)(float)(v1 * v2);
            }

            if (typeof(TValue) == typeof(int))
            {
                var v1 = (int)(object)(input);
                var v2 = (int)(object)(value);

                return (TValue)(object)(int)(v1 * v2);
            }

            if (typeof(TValue) == typeof(long))
            {
                var v1 = (long)(object)(input);
                var v2 = (long)(object)(value);

                return (TValue)(object)(long)(v1 * v2);
            }

            if (typeof(TValue) == typeof(decimal))
            {
                var v1 = (decimal)(object)(input);
                var v2 = (decimal)(object)(value);

                return (TValue)(object)(decimal)(v1 * v2);
            }

            return ApplyDynamic(input, value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static TValue ApplyDynamic<TValue>(TValue input, TValue value)
        {
            var v1 = (dynamic)input;
            var v2 = (dynamic)value;
            return (TValue)(v1 * v2);
        }

        [Test]
        public void CouldMapValuesWithOperator()
        {
            var sm = new SortedMap<int, double>
            {
                { 1, 1 }
            } as BaseSeries<int, double>;
            var map = sm * 2;
            var map1 = map * 2;

            Assert.AreEqual(2, map.First.Value);
            Assert.AreEqual(4, map1.First.Value);
        }

        [Test, Ignore]
        public void CouldMapValuesWithOperatorBenchmark()
        {
            var sm = new SortedMap<int, double>();
            var count = 10000000;
            sm.AddLast(0, 0);
            for (int i = 2; i < count; i++)
            {
                sm.AddLast(i, i);
            }

            for (int r = 0; r < 10; r++)
            {
                var map = (sm as BaseSeries<int, double>) * 2;
                var map2 = map * 2;
                var sum = 0.0;
                using (Benchmark.Run("BaseSeries", count))
                {
                    foreach (var kvp in map2)
                    {
                        sum += kvp.Value;
                    }
                }
                Assert.IsTrue(sum > 0);
            }

            for (int r = 0; r < 10; r++)
            {
                var map = (sm as BaseSeries<int, double>)
                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
                var sum = 0.0;
                using (Benchmark.Run("LINQ", count))
                {
                    foreach (var kvp in map)
                    {
                        sum += kvp.Value;
                    }
                }
                Assert.IsTrue(sum > 0);
            }
        }
    }
}