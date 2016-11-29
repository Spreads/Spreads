// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Spreads.Algorithms.Math;

namespace Spreads.Core.Tests {

    [TestFixture]
    public class MovingMedianTests {

        [Test, Ignore]
        public void CouldCalculateMovingMedian() {
            var rng = new System.Random();
            var count = 100000;
            var m = rng.Next(10, 200);
            Console.Write($"{m}|");
            var data = new double[count];
            for (int i = 0; i < count; i++) {
                data[i] = rng.NextDouble();
            }
            var mm = new MovingMedian();
            var medians = new double[count - m + 1];
            var naiveMedians = new double[count - m + 1];

            var sw = new Stopwatch();
            sw.Restart();
            var result = mm.Rngmed(data, m, medians);
            sw.Stop();
            Assert.AreEqual(0, result);
            //Console.Write($"Elapsed LIGO {sw.ElapsedMilliseconds} msec, {sw.MOPS(count)} Mops");
            var ligoMops = sw.MOPS(count);
            Console.Write($"{ligoMops:f2}|");


            sw.Restart();
            var startIdx = m - 1;
            for (int i = startIdx; i < count; i++) {
                var arraySegment = new ArraySegment<double>(data, i - startIdx, m);
                naiveMedians[i - startIdx] = GetMedian(arraySegment);
            }
            sw.Stop();
            //Console.WriteLine($"Elapsed NAIVE {sw.ElapsedMilliseconds} msec, {sw.MOPS(count):f2} Mops");
            var naiveMops = sw.MOPS(count);
            Console.Write($"{naiveMops:f2}|");
            Console.WriteLine($"{ligoMops/naiveMops:f2}");

        }

        [Test, Ignore]
        public void CouldCalculateMovingMedianManyTimes() {
            for (int r = 0; r < 100; r++) {
                CouldCalculateMovingMedian();
            }
        }

        [Test, Ignore]
        public void MovingMedianIsCorrect() {
            var rng = new System.Random();
            var count = 1000000;
            var m = rng.Next(10, 50);
            Console.WriteLine($"Window size: {m}");
            var data = new double[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = rng.NextDouble();
            }
            var mm = new MovingMedian();
            var medians = new double[count - m + 1];
            var naiveMedians = new double[count - m + 1];

            var sw = new Stopwatch();
            sw.Restart();
            var result = mm.Rngmed(data, m, medians);
            sw.Stop();
            Assert.AreEqual(0, result);
            Console.WriteLine($"Elapsed LIGO {sw.ElapsedMilliseconds} msec, {sw.MOPS(count):f2} Mops");

            sw.Restart();
            var startIdx = m - 1;
            for (int i = startIdx; i < count; i++)
            {
                var arraySegment = new ArraySegment<double>(data, i - startIdx, m);
                naiveMedians[i - startIdx] = GetMedian(arraySegment);
            }
            sw.Stop();
            Console.WriteLine($"Elapsed NAIVE {sw.ElapsedMilliseconds} msec, {sw.MOPS(count):f2} Mops");

            for (int i = 0; i < medians.Length; i++)
            {
                if (medians[i] != naiveMedians[i]) //&& i == 0
                {
                    Console.WriteLine($"{i + startIdx} - LIGO {medians[i]} - Naive {naiveMedians[i]}");
                }
                Assert.AreEqual(medians[i], naiveMedians[i]);
            }
        }

        [Test, Ignore]
        public void MovingMedianIsCorrectManyTimes()
        {
            for (int r = 0; r < 50; r++) {
                MovingMedianIsCorrect();
            }
        }

        public static double GetMedian(ArraySegment<double> sourceNumbers) {
            //Framework 2.0 version of this method. there is an easier way in F4        
            if (sourceNumbers == null || sourceNumbers.Count == 0)
                throw new System.Exception("Median of empty array not defined.");

            //make sure the list is sorted, but use a new array
            double[] sortedPNumbers = sourceNumbers.ToArray();
            Array.Sort(sortedPNumbers);

            //get the median
            int size = sortedPNumbers.Length;
            int mid = size / 2;
            double median = (size % 2 != 0) ? (double)sortedPNumbers[mid] : ((double)sortedPNumbers[mid] + (double)sortedPNumbers[mid - 1]) / 2;
            return median;
        }
    }
}
