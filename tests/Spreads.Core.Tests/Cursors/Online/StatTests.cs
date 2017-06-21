// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Linq;

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

            var statDt = new Stat2<DateTime>();
            statDt.AnnualizedVolatility();
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

        [Test, Ignore]
        public void Stat2StDevBenchmark()
        {
            var count = 1000000;
            var width = 20;
            var sm = new SortedMap<int, double>();
            sm.Add(0, 0);
            for (int i = 2; i <= count; i++)
            {
                sm.Add(i, i);
            }

            var ds1 = new Deedle.Series<int, double>(sm.Keys.ToArray(), sm.Values.ToArray());

            for (int r = 0; r < 20; r++)
            {
                var sum = 0.0;
                using (Benchmark.Run("Stat2 Online N", count * width))
                {
                    foreach (var keyValuePair in sm.Stat2(width))
                    {
                        sum += keyValuePair.Value.StDev;
                    }
                }
                Assert.True(sum != 0);

                sum = 0.0;
                using (Benchmark.Run("Stat2 Online Width GE", count * width))
                {
                    foreach (var keyValuePair in sm.Stat2(width, Lookup.GE))
                    {
                        sum += keyValuePair.Value.StDev;
                    }
                }
                Assert.True(sum != 0);

                sum = 0.0;
                using (Benchmark.Run("Stat2 Window N", count * width))
                {
                    foreach (var keyValuePair in sm.Window(width).Map(x => x.Stat2()))
                    {
                        sum += keyValuePair.Value.StDev;
                    }
                }
                Assert.True(sum != 0);

                sum = 0.0;
                using (Benchmark.Run("Stat2 Window Width GE", count * width))
                {
                    foreach (var keyValuePair in sm.Window(width, Lookup.GE).Map(x => x.Stat2()))
                    {
                        sum += keyValuePair.Value.StDev;
                    }
                }
                Assert.True(sum != 0);

                sum = 0.0;
                using (Benchmark.Run("SortedMap enumeration", count))
                {
                    foreach (var keyValuePair in sm)
                    {
                        sum += keyValuePair.Value;
                    }
                }
                Assert.True(sum != 0);

                sum = 0.0;
                using (Benchmark.Run("Deedle.Stats.movingStdDev", count * width))
                {
                    var deedleStDev = Deedle.Stats.movingStdDev(width, ds1);
                    foreach (var keyValuePair in deedleStDev.Observations)
                    {
                        sum += keyValuePair.Value;
                    }
                }
                Assert.True(sum != 0);
            }

            Benchmark.Dump($"The window width is {width}. Stat2 MOPS are calculated as a number of calculated values multiplied by width, " +
                           $"which is equivalent to the total number of cursor moves for Window case. SortedMap line is for reference - it is the " +
                           $"speed of raw iteration over SM without Stat2/Windows overheads.");
        }
    }
}