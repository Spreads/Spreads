// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Spreads.Utils;

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
            var map = new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(sm, 2);
            var map1 = new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>>(map, 2);

            Assert.AreEqual(2, map.First.Value);
            Assert.AreEqual(4, map1.First.Value);
        }

        [Test, Ignore]
        public void CouldMapValuesBenchmark()
        {
            var sm = new SortedMap<int, double>();
            var count = 10000000;
            //sm.AddLast(0, 0);
            for (int i = 2; i < count; i++)
            {
                sm.AddLast(i, i);
            }

            for (int r = 0; r < 10; r++)
            {
                var sw = new Stopwatch();
                sw.Restart();
                var map =
                    new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(sm, 2.0);
                var map2 =
                    new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>>(
                        map, 2.0);
                var sum = 0.0;
                foreach (var kvp in map2)
                {
                    sum += kvp.Value;
                }
                sw.Stop();
                Assert.IsTrue(sum > 0);

                Console.WriteLine($"Mops {sw.MOPS(count)}");
            }

            //for (int r = 0; r < 10; r++)
            //{
            //    var sw = new Stopwatch();
            //    sw.Restart();
            //    var map = sm
            //        //.Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
            //        .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
            //    var sum = 0.0;
            //    foreach (var kvp in map)
            //    {
            //        sum += kvp.Value;
            //    }
            //    sw.Stop();
            //    Assert.IsTrue(sum > 0);

            //    Console.WriteLine($"LINQ Mops {sw.MOPS(count)}");
            //}
        }

        [Test, Ignore]
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
                var sw = new Stopwatch();

                {
                    sw.Restart();

                    var sum = 0.0;
                    foreach (var kvp in sm)
                    {
                        sum += kvp.Value;
                    }
                    sw.Stop();
                    Assert.IsTrue(sum > 0);
                    Console.WriteLine($"SortedMap {sw.MOPS(count)}");
                }

                {
                    sw.Restart();
                    var map =
                        new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(sm, 2.0);
                    var map2 =
                        new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>>(
                            map, 2.0);

                    var sum = 0.0;
                    foreach (var kvp in map2)
                    {
                        sum += kvp.Value;
                    }
                    sw.Stop();
                    Assert.IsTrue(sum > 0);
                    Console.WriteLine($"ArithmeticSeries {sw.MOPS(count)}");
                }

                {
                    sw.Restart();
                    var map = new MapValuesSeries<int, double, double, SortedMapCursor<int, double>>(sm, i => Apply(i, 2.0));
                    var map2 = new MapValuesSeries<int, double, double, MapValuesSeries<int, double, double, SortedMapCursor<int, double>>>(map, i => Apply(i, 2.0));
                    var sum = 0.0;
                    foreach (var kvp in map2)
                    {
                        sum += kvp.Value;
                    }
                    sw.Stop();
                    Assert.IsTrue(sum > 0);

                    Console.WriteLine($"MapValuesSeries {sw.MOPS(count)}");
                }

                {

                    sw.Restart();
                    var map = sm
                        .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
                        .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
                    var sum = 0.0;
                    foreach (var kvp in map)
                    {
                        sum += kvp.Value;
                    }
                    sw.Stop();
                    Assert.IsTrue(sum > 0);

                    Console.WriteLine($"LINQ {sw.MOPS(count)}");

                }
            }
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
            } as BaseSeries<int, double, ICursor<int, double>>;
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
                var sw = new Stopwatch();
                sw.Restart();
                var map = (sm as BaseSeries<int, double, ICursor<int, double>>) * 2;
                var map2 = map * 2;
                var sum = 0.0;
                foreach (var kvp in map2)
                {
                    sum += kvp.Value;
                }
                sw.Stop();
                Assert.IsTrue(sum > 0);

                Console.WriteLine($"Mops {sw.MOPS(count)}");
            }

            for (int r = 0; r < 10; r++)
            {
                var sw = new Stopwatch();
                sw.Restart();
                var map = (sm as BaseSeries<int, double, ICursor<int, double>>)
                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
                var sum = 0.0;
                foreach (var kvp in map)
                {
                    sum += kvp.Value;
                }
                sw.Stop();
                Assert.IsTrue(sum > 0);

                Console.WriteLine($"LINQ Mops {sw.MOPS(count)}");
            }
        }
    }
}