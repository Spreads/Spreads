// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Experimental;
using Spreads.Collections.Internal;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Collections
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SeriesAppendTests
    {
        [Test]
        public void CouldAppendSeries()
        {
            var sa = new AppendSeries<int, int>(DataBlock.Create());

            Assert.IsTrue(sa.TryAddLast(1, 1).Result);
            Assert.IsFalse(sa.TryAddLast(1, 1).Result);

            Assert.IsTrue(sa.TryAddLast(2, 2).Result);

            Assert.Throws<KeyNotFoundException>(() =>
            {
                var _ = sa[0];
            });

            Assert.AreEqual(1, sa[1]);
            Assert.AreEqual(2, sa[2]);

            Assert.AreEqual(2, sa.Count());

            for (int i = 3; i < 42000; i++)
            {
                Assert.IsTrue(sa.TryAddLast(i, i).Result);
                Assert.AreEqual(i, sa.Last.Present.Value);
            }

            //// TODO remove when implemented
            //Assert.Throws<NotImplementedException>(() =>
            //{
            //    for (int i = 32000; i < 33000; i++)
            //    {
            //        Assert.IsTrue(sa.TryAddLast(i, i).Result);
            //    }
            //});

            sa.Dispose();
        }

        [Test, Explicit("long running")]
        public void CouldAppendSeriesBench()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
            }

            int count = 10_000_000;
            int rounds = 100;

            var sa = new AppendSeries<int, int>(DataBlock.Create());
            var sm = new SortedMap<int, int>();

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
                        if (!sa.TryAddLast(i, i).Result)
                        {
                            Console.WriteLine("Cannot add " + i);
                            return;
                        }
                    }
                }

                Console.WriteLine($"Added {((r + 1) * count / 1000000).ToString("N")}");
            }

            

            Benchmark.Dump();

            Console.WriteLine("Finished, press enter");
            Console.ReadLine();

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

            var sas = new List<AppendSeries<long, long>>();
            var counts = new[] { 10, 100, 1000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000 };
            foreach (var count in counts)
            {
                long rounds = 10;

                var sa = new AppendSeries<long, long>(DataBlock.Create());
                sas.Add(sa);
                var sm = new SortedMap<long, long>();
                for (int i = 0; i < count; i++)
                {
                    if (i == 3)
                    {
                        continue;
                    }

                    if (!sa.TryAddLastDirect(i, i))
                    {
                        Assert.Fail("Cannot add " + i);
                    }

                    if (!sm.TryAddLast(i, i).Result)
                    {
                        Assert.Fail("Cannot add " + i);
                    }
                }

                var mult = Math.Max(1, 1_000_000 / count);

                for (int r = 0; r < rounds; r++)
                {
                    AppendSeriesTGBench(count, mult, sa);

                    SortedMapTGBench(count, mult, sm);
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
        private static void AppendSeriesTGBench(int count, int mult, AppendSeries<long, long> sa)
        {
            using (Benchmark.Run($"AS.TG {count.ToString("N")}", count * mult))
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
        private static void SortedMapTGBench(int count, int mult, SortedMap<long, long> sm)
        {
            using (Benchmark.Run($"SM.TG {count.ToString("N")}", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (!sm.TryGetValue(i, out var val) || val != i)
                        {
                            if (i != 3)
                            {
                                Assert.Fail($"!sm.TryGetValue(i, out var val) || val {val} != i {i}");
                            }
                        }
                    }
                }
            }
        }
    }
}
