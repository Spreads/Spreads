//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using JetBrains.dotMemoryUnit;
//using NUnit.Framework;
//using Spreads.Collections;
//using Spreads.Cursors;
//using Spreads.Utils;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Threading.Tasks;
//using Spreads.Cursors.Experimental;

//namespace Spreads.Core.Tests.Cursors
//{
//    [TestFixture]
//    public class ArithmeticTests
//    {
//        [Test]
//        public void CouldMapValues()
//        {
//            var sm = new SortedMap<int, double>
//            {
//                { 1, 1 }
//            };
//            var map = new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>((sm.GetEnumerator()), 2);
//            var map1 = new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double, MultiplyOp<double>,
//                SortedMapCursor<int, double>>>(map, 2);

//            Assert.AreEqual(2, map.First.Value);
//            Assert.AreEqual(4, map1.First.Value);
//        }

//        [Test, Ignore]
//        public void CouldMapValuesBenchmark()
//        {
//            var sm = new SortedMap<int, double>();
//            var count = 10000000;
//            //sm.AddLast(0, 0); // make irregular
//            for (int i = 2; i < count; i++)
//            {
//                sm.AddLast(i, i);
//            }

//            for (int r = 0; r < 10; r++)
//            {
//                var map =
//                    new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
//                        sm.GetEnumerator(), 2.0);
//                var map2 =
//                    new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double,
//                        MultiplyOp<double>, SortedMapCursor<int, double>>>(
//                        map, 2.0);
//                var sum = 0.0;
//                using (Benchmark.Run("ArithmeticSeries", count))
//                {
//                    foreach (var kvp in map2)
//                    {
//                        sum += kvp.Value;
//                    }
//                }
//                Assert.IsTrue(sum > 0);
//            }

//            for (int r = 0; r < 10; r++)
//            {
//                var map = sm
//                    //.Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
//                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
//                var sum = 0.0;
//                using (Benchmark.Run("LINQ", count))
//                {
//                    foreach (var kvp in map)
//                    {
//                        sum += kvp.Value;
//                    }
//                }
//                Assert.IsTrue(sum > 0);
//            }
//        }

//        [Test, Ignore]
//        [DotMemoryUnit(CollectAllocations = true)]
//        public void CouldMapValuesBenchmarkArithmeticVsMapCursor()
//        {
//            var sm = new SortedMap<int, double>();
//            var count = 10000000;
//            sm.AddLast(0, 0);
//            for (int i = 2; i < count; i++)
//            {
//                sm.AddLast(i, i);
//            }

//            for (int r = 0; r < 10; r++)
//            {
//                {
//                    var sum = 0.0;
//                    using (Benchmark.Run("SortedMap", count))
//                    {
//                        foreach (var kvp in sm)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }

//                {
//                    var map =
//                        new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(sm.GetEnumerator(),
//                            2.0);
//                    var map2 = map + 2;
//                    //new ArithmeticSeries<int, double, MultiplyOp<double>, ArithmeticSeries<int, double,
//                    //    MultiplyOp<double>, SortedMapCursor<int, double>>>(
//                    //    map.Initialize, 2.0);
//                    var sum = 0.0;
//                    using (Benchmark.Run("ArithmeticSeries", count))
//                    {
//                        foreach (var kvp in map2)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }

//                {
//                    var c =
//                        new Op<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
//                            sm.GetEnumerator(), 2.0);
//                    var c1 =
//                        new Op<int, double, AddOp<double>, Op<int, double,
//                            MultiplyOp<double>, SortedMapCursor<int, double>>>(
//                            c, 2.0);
//                    var series = new Series<int, double, Op<int, double, AddOp<double>, Op<int, double,
//                        MultiplyOp<double>, SortedMapCursor<int, double>>>>(c1);
//                    var sum = 0.0;
//                    using (Benchmark.Run("ArithmeticCursor", count))
//                    {
//                        foreach (var kvp in series)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }

//                {
//                    var map =
//                        new MapValuesSeries<int, double, double, SortedMapCursor<int, double>>(sm.GetEnumerator(),
//                            i => Apply(i, 2.0));
//                    var map2 =
//                        new
//                            MapValuesSeries<int, double, double, MapValuesSeries<int, double, double,
//                                SortedMapCursor<int, double>>>(map, i => Apply2(i, 2.0));
//                    var sum = 0.0;
//                    using (Benchmark.Run("MapValuesSeries", count))
//                    {
//                        foreach (var kvp in map2)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }

//                {
//                    var map = (sm * 2) + 2;
//                    var sum = 0.0;
//                    using (Benchmark.Run("BaseSeries operator", count))
//                    {
//                        foreach (var kvp in map)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }

//                {
//                    var map = sm
//                        .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
//                        .Select(x => new KeyValuePair<int, double>(x.Key, x.Value + 2));
//                    var sum = 0.0;
//                    using (Benchmark.Run("LINQ", count))
//                    {
//                        foreach (var kvp in map)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }
//            }

//            Benchmark.Dump();
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private static TValue Apply<TValue>(TValue input, TValue value)
//        {
//            if (typeof(TValue) == typeof(double))
//            {
//                var v1 = (double)(object)(input);
//                var v2 = (double)(object)(value);

//                return (TValue)(object)(double)(v1 * v2);
//            }

//            if (typeof(TValue) == typeof(float))
//            {
//                var v1 = (float)(object)(input);
//                var v2 = (float)(object)(value);

//                return (TValue)(object)(float)(v1 * v2);
//            }

//            if (typeof(TValue) == typeof(int))
//            {
//                var v1 = (int)(object)(input);
//                var v2 = (int)(object)(value);

//                return (TValue)(object)(int)(v1 * v2);
//            }

//            if (typeof(TValue) == typeof(long))
//            {
//                var v1 = (long)(object)(input);
//                var v2 = (long)(object)(value);

//                return (TValue)(object)(long)(v1 * v2);
//            }

//            if (typeof(TValue) == typeof(decimal))
//            {
//                var v1 = (decimal)(object)(input);
//                var v2 = (decimal)(object)(value);

//                return (TValue)(object)(decimal)(v1 * v2);
//            }

//            return ApplyDynamic(input, value);

//            TValue ApplyDynamic<TValue>(TValue input1, TValue value1)
//            {
//                var v1 = (dynamic)input1;
//                var v2 = (dynamic)value1;
//                return (TValue)(v1 * v2);
//            }
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private static TValue Apply2<TValue>(TValue input, TValue value)
//        {
//            if (typeof(TValue) == typeof(double))
//            {
//                var v1 = (double)(object)(input);
//                var v2 = (double)(object)(value);

//                return (TValue)(object)(double)(v1 + v2);
//            }

//            if (typeof(TValue) == typeof(float))
//            {
//                var v1 = (float)(object)(input);
//                var v2 = (float)(object)(value);

//                return (TValue)(object)(float)(v1 + v2);
//            }

//            if (typeof(TValue) == typeof(int))
//            {
//                var v1 = (int)(object)(input);
//                var v2 = (int)(object)(value);

//                return (TValue)(object)(int)(v1 + v2);
//            }

//            if (typeof(TValue) == typeof(long))
//            {
//                var v1 = (long)(object)(input);
//                var v2 = (long)(object)(value);

//                return (TValue)(object)(long)(v1 + v2);
//            }

//            if (typeof(TValue) == typeof(decimal))
//            {
//                var v1 = (decimal)(object)(input);
//                var v2 = (decimal)(object)(value);

//                return (TValue)(object)(decimal)(v1 + v2);
//            }

//            return ApplyDynamic(input, value);

//            TValue ApplyDynamic<TValue>(TValue input1, TValue value1)
//            {
//                var v1 = (dynamic)input1;
//                var v2 = (dynamic)value1;
//                return (TValue)(v1 + v2);
//            }
//        }

//        [Test]
//        public void CouldMapValuesWithOperator()
//        {
//            var sm = new SortedMap<int, double>
//            {
//                { 1, 1 }
//            };
//            var map = sm * 2;
//            var map1 = map + 2;

//            Assert.AreEqual(2, map.First.Present.Value);
//            foreach (var pair in map1)
//            {
//                Assert.AreEqual(4, pair.Value);
//            }

//            using (var c = map1.GetEnumerator())
//            {
//                Assert.True(c.MoveNext());
//                Assert.AreEqual(4, c.CurrentValue);
//            }

//            Assert.AreEqual(4, map1.First.Present.Value);

//            Console.WriteLine(sm.Count());
//        }

//        [Test, Ignore]
//        public void CouldMapValuesWithOperatorBenchmark()
//        {
//            var sm = new SortedMap<int, double>();
//            var count = 10000000;
//            sm.AddLast(0, 0);
//            for (int i = 2; i < count; i++)
//            {
//                sm.AddLast(i, i);
//            }

//            for (int r = 0; r < 10; r++)
//            {
//                var map = sm * 2;
//                var map2 = map * 2;
//                var sum = 0.0;
//                using (Benchmark.Run("BaseSeries", count))
//                {
//                    foreach (var kvp in map2)
//                    {
//                        sum += kvp.Value;
//                    }
//                }
//                Assert.IsTrue(sum > 0);
//            }

//            for (int r = 0; r < 10; r++)
//            {
//                var map = sm
//                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2))
//                    .Select(x => new KeyValuePair<int, double>(x.Key, x.Value * 2));
//                var sum = 0.0;
//                using (Benchmark.Run("LINQ", count))
//                {
//                    foreach (var kvp in map)
//                    {
//                        sum += kvp.Value;
//                    }
//                }
//                Assert.IsTrue(sum > 0);
//            }
//        }

//        [Test, Ignore]
//        public void CouldUseStructSeries()
//        {
//            var sm = new SortedMap<int, double>();
//            var count = 10000000;
//            sm.AddLast(0, 0); // make irregular
//            for (int i = 2; i < count; i++)
//            {
//                sm.AddLast(i, i);
//            }

//            for (int r = 0; r < 10; r++)
//            {
//                var sum = 0.0;
//                {
//                    using (Benchmark.Run("SortedMap", count))
//                    {
//                        foreach (var kvp in sm)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }

//                sum = 0.0;
//                {
//                    var map =
//                        new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
//                            sm.GetEnumerator(), 2.0);
//                    var map2 =
//                        new ArithmeticSeries<int, double, AddOp<double>, ArithmeticSeries<int, double,
//                            MultiplyOp<double>, SortedMapCursor<int, double>>>(
//                            map, 2.0);

//                    using (Benchmark.Run("ArithmeticSeries", count))
//                    {
//                        foreach (var kvp in map2)
//                        {
//                            sum += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum > 0);
//                }
//                var sum1 = 0.0;
//                {
//                    var c =
//                        new Op<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
//                            sm.GetEnumerator(), 2.0);
//                    var c1 =
//                        new Op<int, double, AddOp<double>, Op<int, double,
//                            MultiplyOp<double>, SortedMapCursor<int, double>>>(
//                            c, 2.0);

//                    using (Benchmark.Run("ArithmeticCursor", count))
//                    {
//                        foreach (var kvp in c1.Source)
//                        {
//                            sum1 += kvp.Value;
//                        }
//                    }
//                    Assert.IsTrue(sum1 > 0);
//                }

//                Assert.AreEqual(sum, sum1);
//            }

//            Benchmark.Dump("Compare enumeration speed of SortedMap and two arithmetic implementations using class and struct (item workload is multiply by 2 then add 2).");
//        }

//        [Test, Ignore]
//        // TODO learn how to use dotMemory for total allocatoins count
//        //[DotMemoryUnit(FailIfRunWithoutSupport = false)]
//        public void MultipleEnumerationDoesntAllocate()
//        {
//            var sm = new SortedMap<int, double>();
//            var count = 100;
//            sm.AddLast(0, 0);
//            for (int i = 2; i < count; i++)
//            {
//                sm.AddLast(i, i);
//            }

//            for (int r = 0; r < 10; r++)
//            {
//                var map =
//                    new ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
//                        sm.GetEnumerator(), 2.0);

//                var sum = 0.0;
//                var sum1 = 0.0;
//                var sum2 = 0.0;
//                var iterations = 100000;

//                void Run(ref double s)
//                {
//                    try
//                    {
//                        // using it here completely eliminates allocations, an instance is created for
//                        // all iterations inside each thread
//                        //using (var mapX = map + 2)
//                        {
//                            for (int i = 0; i < iterations; i++)
//                            {
//                                // here static caching helps, but not completely eliminates allocations because
//                                // two threads compete for a single static slot very often
//                                using (var mapX = map + 2)
//                                using (var c = (mapX).GetEnumerator())
//                                {
//                                    //Assert.IsTrue(c.State == CursorState.Initialized);
//                                    //Assert.IsTrue(c._cursor.State == CursorState.Initialized);

//                                    var countCheck = 0;
//                                    while (c.MoveNext())
//                                    {
//                                        s += c.CurrentKey;
//                                        countCheck++;
//                                    }
//                                    if (sm.Count != countCheck)
//                                    {
//                                        Console.WriteLine($"Expected {sm.Count} vs actual {countCheck}");
//                                    }
//                                    if (sm.Count != countCheck) { Assert.Fail(); }
//                                }
//                            }
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine(ex.Message + Environment.NewLine + ex);
//                    }
//                }

//                var cc =
//                    new Op<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>(
//                        sm.GetEnumerator(), 2.0);

//                var cc1 =
//                    new Op<int, double, AddOp<double>, Op<int, double,
//                        MultiplyOp<double>, SortedMapCursor<int, double>>>(
//                        cc, 2.0);
//                var series = new Series<int, double, Op<int, double, AddOp<double>, Op<int, double,
//                    MultiplyOp<double>, SortedMapCursor<int, double>>>>(cc1);

//                void Run2(ref double s)
//                {
//                    try
//                    {
//                        for (int i = 0; i < iterations; i++)
//                        {
//                            using (var c = series.GetEnumerator())
//                            {
//                                var countCheck = 0;
//                                while (c.MoveNext())
//                                {
//                                    s += c.CurrentKey;
//                                    countCheck++;
//                                }
//                                if (sm.Count != countCheck)
//                                {
//                                    Console.WriteLine($"Expected {sm.Count} vs actual {countCheck}");
//                                }
//                                if (sm.Count != countCheck) { Assert.Fail(); }
//                            }
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine(ex.Message + Environment.NewLine + ex);
//                    }
//                }

//                using (Benchmark.Run("ArithmeticSeries", count * iterations))
//                {
//                    var t = Task.Run(() => Run(ref sum1));
//                    var t1 = Task.Run(() => Run(ref sum2));
//                    Run(ref sum);
//                    t.Wait();
//                    t1.Wait();

//                    //dotMemory.Check(memory =>
//                    //{
//                    //    Assert.That(
//                    //        memory.GetObjects(where =>
//                    //            where.Type.Is<ArithmeticSeries<int, double, MultiplyOp<double>, SortedMapCursor<int, double>>>()).ObjectsCount,
//                    //        Is.EqualTo(1)
//                    //    );
//                    //});
//                }

//                Assert.IsTrue(sum > 0);
//                Assert.AreEqual(sum, sum1);
//                Assert.AreEqual(sum, sum2);

//                using (Benchmark.Run("ArithmeticCursor", count * iterations))
//                {
//                    var t = Task.Run(() => Run2(ref sum1));
//                    var t1 = Task.Run(() => Run2(ref sum2));
//                    Run2(ref sum);
//                    t.Wait();
//                    t1.Wait();
//                }

//                Assert.IsTrue(sum > 0);
//                Assert.AreEqual(sum, sum1);
//                Assert.AreEqual(sum, sum2);
//            }
//            Benchmark.Dump("Compare multiple allocations and subsequent enumerations of arithmetic series.");
//        }
//    }
//}