// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Deprecated;
using Spreads.Utils;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Spreads.Core.Tests.Cursors.Online
{
    [TestFixture]
    public class StatTests
    {
        [Test]
        public void OnlineVarianceIsStable()
        {
            var count = 300000; // fails with 400k, small values become not equal
            var stat = new Stat2<int>();

            var forward = new double[count + 1];
            var backward = new double[count + 1];

            for (int i = 1; i <= count; i++)
            {
                stat.AddValue(i);
                if (i > 1)
                {
                    forward[i] = stat.StDev;
                    //Console.WriteLine($"{i} - {stat.StDev}");
                }
            }

            for (int i = count; i >= 1; i--)
            {
                if (i > 1)
                {
                    backward[i] = stat.StDev;
                    //Console.WriteLine($"{i} - {stat.StDev}");
                }
                stat.RemoveValue(i);
            }

            for (int i = 1; i <= count; i++)
            {
                if (i > 1)
                {
                    Assert.AreEqual(forward[i], backward[i]);
                    if (i < 20 || i > count - 20)
                    {
                        // matches to Excel VAR.S/ STDEV.S
                        Console.WriteLine($"{i} - {forward[i]} - {backward[i]}");
                    }
                }
            }

            //var statDt = new Stat2<DateTime>();
            //statDt.AnnualizedVolatility();
        }

        [Test]
        public void CouldUseStat2Extension()
        {
            var count = 30;

            var sm = new SortedMap<int, double>();
            for (int i = 1; i <= count; i++)
            {
                sm.Add(i, i);
            }

            foreach (var keyValuePair in sm.Stat2(50, Lookup.LE))
            {
                Console.WriteLine($"{keyValuePair.Key} - {keyValuePair.Value.StDev}");
            }
        }

        [Test, Explicit("long running")]
        public void Stat2StDevBenchmark()
        {
            var comparer = KeyComparer<int>.Default;
            var count = 1_000_000;
            var width = 20;
            var sm = new SortedMap<int, double>(count, comparer);
            sm.Add(0, 0);
            for (int i = 2; i <= count; i++)
            {
                sm.Add(i, i);
            }

            // var ds1 = new Deedle.Series<int, double>(sm.Keys.ToArray(), sm.Values.ToArray());

            var sum = 0.0;

            for (int r = 0; r < 10; r++)
            {
                var sum1 = 0.0;
                using (Benchmark.Run("Stat2 Online N", count * width))
                {
                    foreach (var stat2 in sm.Stat2(width).Values)
                    {
                        sum1 += stat2.StDev;
                    }
                }
                Assert.True(sum1 != 0);

                var sum2 = 0.0;
                using (Benchmark.Run("Stat2 Online Width GE", count * width))
                {
                    foreach (var stat2 in sm.Stat2(width - 1, Lookup.GE).Values)
                    {
                        sum2 += stat2.StDev;
                    }
                }
                Assert.True(sum2 != 0);

                var sum3 = 0.0;
                using (Benchmark.Run("Stat2 Window N", count * width))
                {
                    foreach (var stat2 in sm.Window(width).Map(x => x.Stat2()).Values)
                    {
                        sum3 += stat2.StDev;
                    }
                }
                Assert.True(sum3 != 0);

                var sum4 = 0.0;
                using (Benchmark.Run("Stat2 Window Width GE", count * width))
                {
                    foreach (var stat2 in sm.Window(width - 1, Lookup.GE).Map(x => x.Stat2()).Values)
                    {
                        sum4 += stat2.StDev;
                    }
                }
                Assert.True(sum4 != 0);

                //var sum5 = 0.0;
                //using (Benchmark.Run("Deedle (online)", count * width))
                //{
                //    var deedleStDev = Deedle.Stats.movingStdDev(width, ds1);
                //    foreach (var keyValuePair in deedleStDev.Values)
                //    {
                //        sum5 += keyValuePair;
                //    }
                //}
                //Assert.True(sum5 != 0);

                Assert.True(Math.Abs(sum1 / sum2 - 1) < 0.000001);
                Assert.True(Math.Abs(sum1 / sum3 - 1) < 0.000001);
                Assert.True(Math.Abs(sum1 / sum4 - 1) < 0.000001);
                // Assert.True(Math.Abs(sum1 / sum5 - 1) < 0.000001);

                sum = 0.0;
                using (Benchmark.Run("SortedMap enumeration", count))
                {
                    var cursor = sm.GetEnumerator();
                    while (cursor.MoveNext())
                    {
                        sum += cursor.CurrentValue;
                    }
                }
                Assert.True(sum != 0);

            }

            Benchmark.Dump($"The window width is {width}. Stat2 MOPS are calculated as a number of calculated values multiplied by width, " +
                           $"which is equivalent to the total number of cursor moves for Window case. SortedMap line is for reference - it is the " +
                           $"speed of raw iteration over SM without Stat2/Windows overheads (and not multiplied by width).");
        }
    }
}