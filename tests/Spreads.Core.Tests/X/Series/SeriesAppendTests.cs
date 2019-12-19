// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.X.Series
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SeriesAppendTests
    {
        [Test]
        [TestCase(5)]
        [TestCase(500)]
        [TestCase(50000)]
        public void CouldAppendSeries(int count)
        {
            //var counts = new[] { 50, 50000 };
            //foreach (var count in counts)
            {
                var sa = new AppendSeries<int, int>();

                Assert.IsTrue(sa.TryAppend(1, 1));
                Assert.AreEqual(1, sa.RowCount, "Row count == 1");
                Assert.IsFalse(sa.TryAppend(1, 1));

                Assert.IsTrue(sa.TryAppend(2, 2));

                Assert.Throws<KeyNotFoundException>(() =>
                {
                    var _ = sa[0];
                });

                Assert.AreEqual(1, sa[1]);
                Assert.AreEqual(2, sa[2]);

                Assert.AreEqual(2, sa.Count());

                for (int i = 3; i < count; i++)
                {
                    Assert.IsTrue(sa.TryAppend(i, i));
                    Assert.AreEqual(i, sa.Last.Present.Value);
                }

                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();

                //using (var cursor = sa.GetEnumerator())
                //{
                //    for (int i = 1; i < count; i++)
                //    {
                //        Assert.IsTrue(cursor.MoveNext(), $"could MN {i}");
                //        Assert.AreEqual(i, cursor.CurrentKey);
                //        Assert.AreEqual(i, cursor.CurrentValue);
                //    }
                //}

                //using (var cursor = sa.GetEnumerator())
                //{
                //    for (int i = count - 1; i >= 1; i--)
                //    {
                //        Assert.IsTrue(cursor.MovePrevious(), $"could MP {i}");
                //        Assert.AreEqual(i, cursor.CurrentKey);
                //        Assert.AreEqual(i, cursor.CurrentValue);
                //    }
                //}

                using (var cursor = sa.GetEnumerator())
                {
                    Assert.IsTrue(cursor.Move(count + 1, false) == 0);
                    Assert.IsTrue(cursor.State == CursorState.Initialized);
                }

                using (var cursor = sa.GetEnumerator())
                {
                    Assert.AreEqual(count - 1, cursor.Move(count + 1, true));
                    Assert.IsTrue(cursor.State == CursorState.Moving);
                    Assert.AreEqual(sa.Last.Present.Key, cursor.CurrentKey);
                    Assert.AreEqual(sa.Last.Present.Value, cursor.CurrentValue);
                }

                using (var cursor = sa.GetEnumerator())
                {
                    Assert.AreEqual(-(count - 1), cursor.Move(-count - 1, true));
                    Assert.IsTrue(cursor.State == CursorState.Moving);
                    Assert.AreEqual(sa.First.Present.Key, cursor.CurrentKey);
                    Assert.AreEqual(sa.First.Present.Value, cursor.CurrentValue);
                }

                sa.Dispose();

                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
            }
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void CouldAppendSeriesBench()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
            }

            int count = (int)TestUtils.GetBenchCount(10_000_000);
            int rounds = (int)TestUtils.GetBenchCount(100, 1);

            var sl = new SortedList<int, int>();
            var sa = new AppendSeries<int, int>(new MovingWindowOptions<int>(10));
            var di = new Dictionary<int,int>();

            //for (int r = 0; r < rounds; r++)
            //{
            //    using (Benchmark.Run("SL.Add", count))
            //    {
            //        for (int i = r * count; i < (r + 1) * count; i++)
            //        {
            //            if (i == r * count + 3)
            //            {
            //                continue;
            //            }

            //            sl.Add(i, i);
            //        }
            //    }
            //    Console.WriteLine($"Added {((r + 1) * count / 1000000):N}");
            //}

            //for (int r = 0; r < rounds; r++)
            //{
            //    using (Benchmark.Run("DI.Add", count))
            //    {
            //        for (int i = r * count; i < (r + 1) * count; i++)
            //        {
            //            if (i == r * count + 3)
            //            {
            //                continue;
            //            }

            //            di.Add(i, i);
            //        }
            //    }
            //    Console.WriteLine($"Added {((r + 1) * count / 1000000):N}");
            //}

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Append", count))
                {
                    for (int i = r * count; i < (r + 1) * count; i++)
                    {
                        if (i == r * count + 3)
                        {
                            continue;
                        }

                        if (!sa.DangerousTryAppend(i, i))
                        {
                            Console.WriteLine("Cannot add " + i);
                            return;
                        }
                    }
                }

                Console.WriteLine($"Added {((r + 1) * count / 1000000):N}");
            }

            Benchmark.Dump();

            sa.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void SearchOverLargeSeriesBench()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
            }

#if DEBUG
            var counts = new[] { 10, 100, 1000 };

#else
            var counts = new[] { 10, 100, 1000, 10_000, 20_000, 40_000, 100_000, 1_000_000, 10_000_000 };
            //var counts = new[] { 1_000_000 };

#endif
            foreach (var count in counts)
            {
                long rounds = TestUtils.GetBenchCount(10, 1);

                var sa = new AppendSeries<int, int>();
                var sl = new SortedList<int, int>();
                var dict = new Dictionary<int,int>();

                for (int i = 0; i < count; i++)
                {
                    if (i == 3)
                    {
                        continue;
                    }

                    if (!sa.DangerousTryAppend(i, i))
                    {
                        Assert.Fail("Cannot add " + i);
                    }

                    sl.Add(i, i);
                    dict.Add(i, i);
                }

                var mult = Math.Max(1, 1_00_000 / count);

                if (count < 20000)
                {
                    mult *= 10;
                }

                for (int r = 0; r < rounds; r++)
                {
                    AppendSeriesTgvBench(count, mult, sa);
                    SortedListTgvBench(count, mult, sl);
                    DictionaryTgvBench(count, mult, dict);
                }
                sa.Dispose();
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void AppendSeriesTgvBench(int count, int mult, Series<int, int> sa)
        {
            using (Benchmark.Run($"AS {count:N}", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!sa.TryGetValue(i, out var val) || val != i)
                        {
                            if (i != 3)
                            {
                                Assert.Fail($"!sa.TryGetValue(i, out var val) || val {val} != i {i}");
                            }
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void SortedListTgvBench(int count, int mult, SortedList<int, int> sl)
        {
            using (Benchmark.Run($"SL {count:N}", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!sl.TryGetValue(i, out var val) || val != i)
                        {
                            if (i != 3)
                            {
                                Assert.Fail($"!sl.TryGetValue(i, out var val) || val {val} != i {i}");
                            }
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void DictionaryTgvBench(int count, int mult, Dictionary<int, int> dictionary)
        {
            using (Benchmark.Run($"DI {count:N}", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!dictionary.TryGetValue(i, out var val) || val != i)
                        {
                            if (i != 3)
                            {
                                Assert.Fail($"!sl.TryGetValue(i, out var val) || val {val} != i {i}");
                            }
                        }
                    }
                }
            }
        }
    }
}
