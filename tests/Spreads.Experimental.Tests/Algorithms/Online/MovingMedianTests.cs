﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Algorithms.Online;
using Spreads.Utils;
using System;
using System.Diagnostics;

namespace Spreads.Core.Tests.Algorithms
{
    [Obsolete("Use Benchmark utility")]
    internal static class StopwatchExtensions
    {
        [Obsolete("Use Benchmark utility")]
        public static double MOPS(this Stopwatch stopwatch, int count, int decimals = 2)
        {
            return Math.Round((count * 0.001) / ((double)stopwatch.ElapsedMilliseconds), decimals);
        }
    }

    [Category("CI")]
    [TestFixture]
    public class MovingMedianTests
    {
        [Test, Explicit("long running")]
        public void CouldCalculateMovingMedian()
        {
            var rng = new System.Random();
            var count = 100000;
            var m = rng.Next(10, 200);
            Console.Write($"{m}|");
            var data = new double[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = rng.NextDouble();
            }
            var mm = new MovingMedian(m);
            var medians = new double[count - m + 1];
            var naiveMedians = new double[count - m + 1];

            var sw = new Stopwatch();
            sw.Restart();
            var result = mm.Rngmed(data, ref medians);
            sw.Stop();
            Assert.AreEqual(0, result);
            //Console.Write($"Elapsed LIGO {sw.ElapsedMilliseconds} msec, {sw.MOPS(count)} Mops");
            var ligoMops = sw.MOPS(count);
            Console.Write($"{ligoMops:f2}|");

            sw.Restart();
            var startIdx = m - 1;
            for (int i = startIdx; i < count; i++)
            {
                var arraySegment = new ArraySegment<double>(data, i - startIdx, m);
                naiveMedians[i - startIdx] = MovingMedian.NaiveMedian(arraySegment);
            }
            sw.Stop();
            //Console.WriteLine($"Elapsed NAIVE {sw.ElapsedMilliseconds} msec, {sw.MOPS(count):f2} Mops");
            var naiveMops = sw.MOPS(count);
            Console.Write($"{naiveMops:f2}|");
            Console.WriteLine($"{ligoMops / naiveMops:f2}");
        }

        [Test, Explicit("long running")]
        public void CouldCalculateMovingMedianManyTimes()
        {
            for (int r = 0; r < 100; r++)
            {
                CouldCalculateMovingMedian();
            }
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void MovingMedianIsCorrect()
        {
            var rng = new System.Random();
            var count = (int)TestUtils.GetBenchCount(1000000);
            var m = rng.Next(10, 50);
            Console.WriteLine($"Window size: {m}");
            var data = new double[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = rng.NextDouble();
            }
            var mm = new MovingMedian(m);
            var medians = new double[count - m + 1];
            var naiveMedians = new double[count - m + 1];

            using (Benchmark.Run("LIGO", count))
            {
                var result = mm.Rngmed(data, ref medians);
                Assert.AreEqual(0, result);
            }

            var startIdx = m - 1;

            using (Benchmark.Run("NAIVE", count))
            {
                for (int i = startIdx; i < count; i++)
                {
                    var arraySegment = new ArraySegment<double>(data, i - startIdx, m);
                    naiveMedians[i - startIdx] = MovingMedian.NaiveMedian(arraySegment);
                }
            }

            for (int i = 0; i < medians.Length; i++)
            {
                if (medians[i] != naiveMedians[i]) //&& i == 0
                {
                    Console.WriteLine($"{i + startIdx} - LIGO {medians[i]} - Naive {naiveMedians[i]}");
                }
                Assert.AreEqual(medians[i], naiveMedians[i]);
            }
        }

        [Test, Explicit("long running")]
        public void MovingMedianIsCorrectManyTimes()
        {
            for (int r = 0; r < 50; r++)
            {
                MovingMedianIsCorrect();
            }
        }

        // TODO restore
        //[Test, Explicit("long running")]
        //public void IncompleteMovingMedianIsCorrect()
        //{
        //    var rng = new System.Random();
        //    var count = 100000;
        //    var m = rng.Next(10, 199);
        //    Console.WriteLine($"Window size: {m}");
        //    var keys = new int[count];
        //    var data = new double[count];
        //    //var sm = new Series<int, double>(count);
        //    for (int i = 0; i < count; i++)
        //    {
        //        keys[i] = i;
        //        data[i] = rng.NextDouble();
        //        //sm.Add(i, data[i]);
        //    }
        //    var sm = Series<int, double>.OfSortedKeysAndValues(keys, data, count);
        //    sm.Complete();
        //    var medianSeries = sm.MovingMedian(m, true);
        //    var mm = new MovingMedian(m);
        //    var medians = new double[count];
        //    var naiveMedians = new double[count];
        //    var cursorMedians = new double[count];

        //    var sw = new Stopwatch();
        //    sw.Restart();
        //    for (int i = 0; i < count; i++)
        //    {
        //        medians[i] = mm.Update(data[i]);
        //    }
        //    //var result = mm.Rngmed(data, medians);
        //    sw.Stop();
        //    Console.WriteLine($"Elapsed LIGO {sw.ElapsedMilliseconds} msec, {sw.MOPS(count):f2} Mops");

        //    sw.Restart();

        //    for (int i = 0; i < count; i++)
        //    {
        //        var arraySegment = new ArraySegment<double>(data, Math.Max(i - m + 1, 0), Math.Min(m, i + 1));
        //        //if (i + 1 >= m && arraySegment.Count != m) {
        //        //    Assert.Fail("Wrong array segment");
        //        //} else {
        //        //    Console.WriteLine($"{i} - {arraySegment.Offset} - {arraySegment.Count}");
        //        //}
        //        naiveMedians[i] = MovingMedian.NaiveMedian(arraySegment);
        //    }
        //    sw.Stop();
        //    Console.WriteLine($"Elapsed NAIVE {sw.ElapsedMilliseconds} msec, {sw.MOPS(count):f2} Mops");

        //    sw.Restart();
        //    var cursor = medianSeries.GetCursor();
        //    while (cursor.MoveNextAsync())
        //    {
        //        cursorMedians[cursor.CurrentKey] = cursor.CurrentValue;
        //    }
        //    sw.Stop();
        //    Console.WriteLine($"Elapsed CURSOR {sw.ElapsedMilliseconds} msec, {sw.MOPS(count):f2} Mops");

        //    for (int i = 0; i < medians.Length; i++)
        //    {
        //        if (medians[i] != naiveMedians[i])
        //        {
        //            Console.WriteLine($"{i} - LIGO {medians[i]} - Naive {naiveMedians[i]}");
        //        }
        //        Assert.AreEqual(medians[i], naiveMedians[i]);

        //        if (naiveMedians[i] != cursorMedians[i])
        //        {
        //            Console.WriteLine($"{i} - CURSOR {cursorMedians[i]} - Naive {naiveMedians[i]}");
        //        }
        //        Assert.AreEqual(cursorMedians[i], naiveMedians[i]);
        //    }
        //}

        //[Test, Explicit("long running")]
        //public void IncompleteMovingMedianIsCorrectManyTimes()
        //{
        //    for (int r = 0; r < 50; r++)
        //    {
        //        IncompleteMovingMedianIsCorrect();
        //    }
        //}
    }
}
