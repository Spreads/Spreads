// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Spreads.Utils;

namespace Spreads.Core.Tests.Series
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SeriesAppendTests
    {
        [Test]
        public void CouldAppendSeries()
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

            for (int i = 3; i < 42000; i++)
            {
                if (i == 16385)
                {
                    Console.WriteLine();
                }
                Assert.IsTrue(sa.TryAppend(i, i));
                Assert.AreEqual(i, sa.Last.Present.Value);
            }


            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();

            sa.Dispose();

            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
        }

        [Test, Explicit("long running")]
        public void CouldAppendSeriesBench()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
            }

            int count = (int)TestUtils.GetBenchCount(10_000_000, 10_000_000);
            int rounds = (int)TestUtils.GetBenchCount(100, 1);

            var sa = new AppendSeries<int, int>();

            //for (int r = 0; r < rounds; r++)
            //{
            //    using (Benchmark.Run("SM.TryAddLast", count))
            //    {
            //        for (int i = r * count; i < (r + 1) * count; i++)
            //        {
            //            if (i == r * count + 3)
            //            {
            //                continue;
            //            }
            //            if (!sm.TryAddLast(i, i).Result)
            //            {
            //                Assert.Fail("Cannot add " + i);
            //            }
            //        }
            //    }
            //    Console.WriteLine($"Added {((r + 1) * count / 1000000).ToString("N")}");
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
                        if (!sa.TryAppend(i, i))
                        {
                            Console.WriteLine("Cannot add " + i);
                            return;
                        }
                    }
                }

                Console.WriteLine($"Added {((r + 1) * count / 1000000).ToString("N")}");
            }

            Benchmark.Dump();

            //Console.WriteLine("Finished, press enter");
            //Console.ReadLine();

            sa.Dispose();
        }

        [Test, Explicit("long running")]
        public void SearchOverLargeSeriesBench()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
            }

            // TODO disposal

            var sas = new List<Series<long, long>>();
            var counts = new[] { 10, 100, 1000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000 };
            foreach (var count in counts)
            {
                long rounds = 10;

                var sa = new AppendSeries<long, long>();
                sas.Add(sa);
                for (int i = 0; i < count; i++)
                {
                    if (i == 3)
                    {
                        continue;
                    }

                    if (!sa.DoTryAddLast(i, i))
                    {
                        Assert.Fail("Cannot add " + i);
                    }
                }

                var mult = Math.Max(1, 1_000_000 / count);

                for (int r = 0; r < rounds; r++)
                {
                    AppendSeriesTgvBench(count, mult, sa);
                }
            }

            Benchmark.Dump();

            foreach (var sa in sas)
            {
                sa.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void AppendSeriesTgvBench(int count, int mult, Series<long, long> sa)
        {
            using (Benchmark.Run($"AS.TG {count:N}", count * mult))
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
    }
}
