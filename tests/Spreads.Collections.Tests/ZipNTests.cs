using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Threading.Tasks;
using System.Threading;

namespace Spreads.Collections.Tests {

    [TestFixture]
    public class ZipNTests {

        [Test]
        public void CouldMoveOnTheSameFirstAndLastPositionOfThreeSeries() {

            var sm1 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 1},
                    { 2, 2},
                    { 3, 3}
                });
            var sm2 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 2},
                    { 2, 4},
                    { 3, 6}
                });
            var sm3 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 3},
                    { 2, 6},
                    { 3, 9}
                });

            var series = new[] { sm1, sm2, sm3 };
            var sum = series.Zip((k, varr) => k * varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedFirst = zipNCursor.MoveFirst();
            Assert.IsTrue(movedFirst);
            Assert.AreEqual(6, zipNCursor.CurrentValue);

            var movedLast = zipNCursor.MoveLast();
            Assert.IsTrue(movedLast);
            Assert.AreEqual(3 * (3 + 6 + 9), zipNCursor.CurrentValue);

        }


        [Test]
        public void CouldMoveOnFirstAndLastPositionOfThreeSeries() {

            var sm1 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    //{ 1, 1},
                    { 2, 2},
                    //{ 3, 3}
                });
            var sm2 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 2},
                    { 2, 4},
                    { 3, 6}
                });
            var sm3 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 3},
                    { 2, 6},
                    { 3, 9}
                });

            var series = new[] { sm1, sm2, sm3 };
            var sum = series.Zip((k, varr) => k * varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedFirst = zipNCursor.MoveFirst();
            Assert.IsTrue(movedFirst);
            Assert.AreEqual((2 + 4 + 6) * 2, zipNCursor.CurrentValue);
            var movedLast = zipNCursor.MoveLast();
            Assert.IsTrue(movedLast);
            Assert.AreEqual((2 + 4 + 6) * 2, zipNCursor.CurrentValue);
        }

        [Test]
        public void CouldMoveOnFirstPositionOfThreeSeriesAndThenNext() {

            var sm1 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    //{ 1, 1},
                    { 2, 2},
                    { 3, 3}
                });
            var sm2 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 2},
                    { 2, 4},
                    { 3, 6}
                });
            var sm3 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 3},
                    { 2, 6},
                    { 3, 9}
                });

            var series = new[] { sm1, sm2, sm3 };
            var sum = series.Zip((k, varr) => k * varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedFirst = zipNCursor.MoveFirst();
            Assert.IsTrue(movedFirst);
            Assert.AreEqual((2 + 4 + 6) * 2, zipNCursor.CurrentValue);

            var movedNext = zipNCursor.MoveNext();
            Assert.IsTrue(movedNext);
            Assert.AreEqual((3 + 6 + 9) * 3, zipNCursor.CurrentValue);

        }


        [Test]
        public void CouldMoveAtPositionOfThreeEqualSeries() {

            var sm1 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 1},
                    { 2, 2},
                    { 3, 3}
                });
            var sm2 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 2},
                    { 2, 4},
                    { 3, 6}
                });
            var sm3 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 3},
                    { 2, 6},
                    { 3, 9}
                });

            var series = new[] { sm1, sm2, sm3 };
            var sum = series.Zip((k, varr) => k * varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedAtEQ = zipNCursor.MoveAt(2, Lookup.EQ);
            Assert.IsTrue(movedAtEQ);
            Assert.AreEqual((2 + 4 + 6) * 2, zipNCursor.CurrentValue);

            var movedAtLE = zipNCursor.MoveAt(2, Lookup.LE);
            Assert.IsTrue(movedAtLE);
            Assert.AreEqual((2 + 4 + 6) * 2, zipNCursor.CurrentValue);

            var movedAtGE = zipNCursor.MoveAt(2, Lookup.GE);
            Assert.IsTrue(movedAtGE);
            Assert.AreEqual((2 + 4 + 6) * 2, zipNCursor.CurrentValue);

            var movedAtLT = zipNCursor.MoveAt(2, Lookup.LT);
            Assert.IsTrue(movedAtLT);
            Assert.AreEqual((1 + 2 + 3) * 1, zipNCursor.CurrentValue);

            var movedAtGT = zipNCursor.MoveAt(2, Lookup.GT);
            Assert.IsTrue(movedAtGT);
            Assert.AreEqual((3 + 6 + 9) * 3, zipNCursor.CurrentValue);
        }


        [Test]
        public void CouldMoveAtPositionOfThreeDifferentSeries() {

            var sm1 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 1},
                    //{ 2, 2},
                    { 3, 3},
                    //{ 5, 5},
                    { 7, 7}
                });
            var sm2 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 2},
                    { 2, 4},
                    { 3, 6},
                    { 5, 10},
                    { 7, 14}

                });
            var sm3 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 3},
                    { 2, 6},
                    { 3, 9},
                    { 5, 15},
                    { 7, 21}
                });

            var series = new[] { sm1, sm2, sm3 };
            var sum = series.Zip((k, varr) => k * varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedAtEQ = zipNCursor.MoveAt(3, Lookup.EQ);
            Assert.IsTrue(movedAtEQ);
            Assert.AreEqual((3 + 6 + 9) * 3, zipNCursor.CurrentValue);

            var movedAtLE = zipNCursor.MoveAt(2, Lookup.LE);
            Assert.IsTrue(movedAtLE);
            Assert.AreEqual((1 + 2 + 3) * 1, zipNCursor.CurrentValue);

            var movedAtGE = zipNCursor.MoveAt(5, Lookup.GE);
            Assert.IsTrue(movedAtGE);
            Assert.AreEqual((7 + 14 + 21) * 7, zipNCursor.CurrentValue);

            var movedAtLT = zipNCursor.MoveAt(3, Lookup.LT);
            Assert.IsTrue(movedAtLT);
            Assert.AreEqual((1 + 2 + 3) * 1, zipNCursor.CurrentValue);

            var movedAtGT = zipNCursor.MoveAt(3, Lookup.GT);
            Assert.IsTrue(movedAtGT);
            Assert.AreEqual((7 + 14 + 21) * 7, zipNCursor.CurrentValue);
        }


        [Test]
        public void CouldMoveAtPositionOfThreeDifferentSeriesWithContinuous() {

            var sm1 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 1},
                    //{ 2, 2}, // Fill(100)
                    { 3, 3},
                    //{ 5, 5}, // Fill(100)
                    { 7, 7}
                });
            var sm2 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 2},
                    { 2, 4},
                    { 3, 6},
                    { 5, 10},
                    { 7, 14}

                });
            var sm3 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 3},
                    { 2, 6},
                    { 3, 9},
                    { 5, 15},
                    { 7, 21}
                });

            var series = new[] { sm1.Fill(100), sm2, sm3 };
            var sum = series.Zip((k, varr) => k * varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedAtEQ = zipNCursor.MoveAt(3, Lookup.EQ);
            Assert.IsTrue(movedAtEQ);
            Assert.AreEqual((3 + 6 + 9) * 3, zipNCursor.CurrentValue);

            var movedAtLE = zipNCursor.MoveAt(2, Lookup.LE);
            Assert.IsTrue(movedAtLE);
            Assert.AreEqual((100 + 4 + 6) * 2, zipNCursor.CurrentValue);

            var movedAtGE = zipNCursor.MoveAt(5, Lookup.GE);
            Assert.IsTrue(movedAtGE);
            Assert.AreEqual((100 + 10 + 15) * 5, zipNCursor.CurrentValue);

            var movedAtLT = zipNCursor.MoveAt(3, Lookup.LT);
            Assert.IsTrue(movedAtLT);
            Assert.AreEqual((100 + 4 + 6) * 2, zipNCursor.CurrentValue);

            var movedAtGT = zipNCursor.MoveAt(3, Lookup.GT);
            Assert.IsTrue(movedAtGT);
            Assert.AreEqual((100 + 10 + 15) * 5, zipNCursor.CurrentValue);
        }

        [Test]
        public void CouldMoveAtPositionOfThreeDifferentSeriesAllContinuous() {

            var sm1 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 1},
                    //{ 2, 2}, // Fill(100)
                    { 3, 3},
                    //{ 5, 5}, // Fill(100)
                    { 7, 7}
                });
            var sm2 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 2},
                    { 2, 4},
                    { 3, 6},
                    { 5, 10},
                    { 7, 14}

                });
            var sm3 = new SortedMap<int, int>(new Dictionary<int, int>()
                {
                    { 1, 3},
                    { 2, 6},
                    { 3, 9},
                    { 5, 15},
                    { 7, 21}
                });

            var series = new[] { sm1.Fill(100), sm2.Repeat(), sm3.Repeat() };
            var sum = series.Zip((k, varr) => k * varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedAtEQ = zipNCursor.MoveAt(3, Lookup.EQ);
            Assert.IsTrue(movedAtEQ);
            Assert.AreEqual((3 + 6 + 9) * 3, zipNCursor.CurrentValue);

            var movedAtLE = zipNCursor.MoveAt(2, Lookup.LE);
            Assert.IsTrue(movedAtLE);
            Assert.AreEqual((100 + 4 + 6) * 2, zipNCursor.CurrentValue);

            var movedAtGE = zipNCursor.MoveAt(5, Lookup.GE);
            Assert.IsTrue(movedAtGE);
            Assert.AreEqual((100 + 10 + 15) * 5, zipNCursor.CurrentValue);

            var movedAtLT = zipNCursor.MoveAt(3, Lookup.LT);
            Assert.IsTrue(movedAtLT);
            Assert.AreEqual((100 + 4 + 6) * 2, zipNCursor.CurrentValue);
            movedAtLT = zipNCursor.MoveAt(1, Lookup.LT);
            Assert.IsTrue(!movedAtLT);

            var movedAtGT = zipNCursor.MoveAt(3, Lookup.GT);
            Assert.IsTrue(movedAtGT);
            Assert.AreEqual((100 + 10 + 15) * 5, zipNCursor.CurrentValue);
            movedAtGT = zipNCursor.MoveAt(7, Lookup.GT);
            Assert.IsTrue(!movedAtGT);
            int val;
            var hasGTValue = zipNCursor.TryGetValue(8, out val);
            Assert.IsTrue(hasGTValue);
            Assert.AreEqual((100 + 14 + 21) * 8, val);
        }


        [Test]
        public void CouldZipMillionIntsWithMoveNext() {
            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();

            sm1.Add(0, 0);

            for (int i = 2; i < 5000000; i++) {
                sm1.Add(i, i);
            }

            var series = new[] { sm1, sm1, sm1, sm1, sm1, };// sm1, sm1, sm1, sm1, sm1,    sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum()).ToSortedMap();

            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            for (int i = 2; i < 5000000; i++) {
                Assert.AreEqual(series.Length * i, sum[i]);
            }

        }


        [Test]
        public void CouldZipMillionIntsWithMoveNextContX5() {
            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();
            var sm2 = new SortedMap<int, int>();
            sm1.Add(0, 0);
            sm2.Add(0, 0);

            for (int i = 2; i < 5000000; i = i + 2) {
                sm1.Add(i, i);
                sm2.Add(i + 1, i);
            }

            var series = new[] { sm1.Repeat(), sm2.Repeat(), sm1.Repeat(), sm2.Repeat(), sm1.Repeat() };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum()).ToSortedMap();

            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);


        }


        [Test]
        public void CouldZipMillionIntsWithMoveNextNoncont() {
            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();
            var sm2 = new SortedMap<int, int>();

            sm1.Add(0, 0);
            sm2.Add(0, 0);
            for (int i = 2; i < 5000000; i = i + 2) {
                sm1.Add(i, i);
                sm2.Add(i, i);
            }

            var series = new[] { sm1, sm2 };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum()).ToSortedMap();

            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            for (int i = 2; i < 5000000; i = i + 2) {
                Assert.AreEqual(series.Length * i, sum[i]);
            }


        }


        [Test]
        public void CouldZipMillionIntsWithMoveNextContinuous() {
            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();
            var sm2 = new SortedMap<int, int>();
            sm1.Add(0, 0);
            sm2.Add(0, 0);

            for (int i = 2; i < 5000000; i = i + 2) {
                sm1.Add(i, i);
                sm2.Add(i + 1, i);
            }

            var series = new[] { sm1.Repeat(), sm2.Repeat(), };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum()).ToSortedMap();

            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            for (int i = 2; i < 5000000; i = i + 2) {
                Assert.AreEqual(i * 2 - 2, sum[i]);
            }

        }


        [Test]
        public void CouldZipMillionIntsMovePreviousBenchmark() {
            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();

            sm1.Add(0, 0);

            for (int i = 2; i < 5000000; i++) {
                sm1.Add(i, i);
            }

            var series = new[] { sm1, sm1, sm1, sm1, sm1, };// sm1, sm1, sm1, sm1, sm1,        sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, };

            sw.Start();

            var cur = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var totalSum = 0L;
            while (cur.MovePrevious()) {
                totalSum += cur.CurrentValue;
            }

            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Total sum: {0}", totalSum);


        }

        // SortedDeque rules! At least for this pattern when we remove first and add last
        //Zip series:			5x1	    5x2	    5x10	    5x20	    50*2		1x500

        //time in sec			0.6	    1.032	4.364	    8.259	    10		    40
        //mops:									
        //SortedDeque			8.33	9.69	11.46	    12.11	    10.00		12.50
        //% above SL1			78%	    65%	    49%	        49%	        37%		    118%
        //% above FH			-4%	    0%	    70%	        102%	    -2%		    240%



        [Test]
        public void CouldZipMillionIntsMoveNextBenchmark() {

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();

            sm1.Add(0, 0);

            for (int i = 2; i < 5000000; i++) {
                sm1.Add(i, i);
            }

            var series = new[] { sm1, sm1, sm1, sm1, sm1, };// sm1, sm1, sm1, sm1, sm1,    sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum());
            var totalSum = 0L;
            foreach (var kvp in sum) {
                totalSum += kvp.Value;
            }
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Total sum: {0}", totalSum);


        }



        [Test]
        public void CouldZipMillionIntsx500() {

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();

            sm1.Add(0, 0);

            for (int i = 2; i < 1000000; i++) {
                sm1.Add(i, i);
            }

            var series = new[]
            {
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,

                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,

                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,

                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,

                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
                sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,  sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,
            };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum());
            var totalSum = 0L;
            foreach (var kvp in sum) {
                totalSum += kvp.Value;
            }
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Total sum: {0}", totalSum);
            Console.WriteLine("Number of series: {0}", series.Length);


        }


        [Test]
        public void CouldZipNonContinuousInRealTime() {

            var sm1 = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
            }
            sm1.IsMutable = true; // will mutate after the first batch

            Task.Run(() => {
                Thread.Sleep(1000);
                for (int i = count; i < count * 2; i++) {
                    sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //Thread.Sleep(50);
                }

                sm1.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            Task.Run(() => {
                Thread.Sleep(950);
                for (int i = count; i < count * 2; i++) {
                    sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
                    //Thread.Sleep(50);
                }

                sm2.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();


            var series = new[] { sm1, sm2 };

            sw.Start();
            var totalSum = 0.0;
            var sumCursor = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var c = 0;
            while (c < 5 && sumCursor.MoveNext()) {
                Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                totalSum += sumCursor.CurrentValue;
                c++;
            }

            while (sumCursor.MoveNext(CancellationToken.None).Result) {
                Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                Console.WriteLine("Value: " + sumCursor.CurrentValue);
                totalSum += sumCursor.CurrentValue;
                c++;
            }
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Total sum: {0}", totalSum);


        }


        [Test]
        public void CouldZipManyNonContinuousInRealTime() {

            var sm1 = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++) {
                sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
            }
            sm1.IsMutable = true; // will mutate after the first batch

            Task.Run(() => {
                Thread.Sleep(1000);
                for (int i = count; i < count * 2; i++) {
                    sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //Thread.Sleep(50);
                }

                sm1.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            Task.Run(() => {
                Thread.Sleep(950);
                for (int i = count; i < count * 2; i++) {
                    sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
                    //Thread.Sleep(50);
                }

                sm2.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();


            var series = new[] { sm1, sm2 };

            sw.Start();
            var totalSum = 0.0;
            var sumCursor = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var c = 0;
            while (c < 5 && sumCursor.MoveNext()) {
                //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                totalSum += sumCursor.CurrentValue;
                c++;
            }

            while (sumCursor.MoveNext(CancellationToken.None).Result) {
                //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                //Console.WriteLine("Value: " + sumCursor.CurrentValue);
                totalSum += sumCursor.CurrentValue;
                c++;
            }
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Total sum: {0}", totalSum);


        }


        [Test]
        public void CouldZipManyNonContinuousInRealTime2() {

            var sm1 = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 10000000;//000000;
            var mid = 1;

            for (int i = 0; i < mid; i++) {
                sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
            }
            sm1.IsMutable = true; // will mutate after the first batch

            Task.Run(() => {
                Thread.Sleep(100);
                for (int i = mid; i < count; i++) {
                    sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //Thread.Sleep(50);
                    //Thread.Sleep(1);
                }

                sm1.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            Task.Run(() => {
                Thread.Sleep(100);
                for (int i = mid; i < count; i++) {
                    sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
                    //Thread.Sleep(50);
                    //Thread.Sleep(1);
                }

                sm2.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();


            var series = new[] { sm1, sm2 };

            sw.Start();
            var totalSum = 0.0;
            var sumCursor = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var c = 0;
            //while (sumCursor.MoveNext()) {
            //    //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
            //    totalSum += sumCursor.CurrentValue;
            //    c++;
            //}

            Task.Run(async () => {
                while (await sumCursor.MoveNext(CancellationToken.None)) {
                    //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                    //Console.WriteLine("Value: " + sumCursor.CurrentValue);
                    totalSum += sumCursor.CurrentValue;
                    c++;
                    //if (c % 10000 == 0) {
                    //    Console.WriteLine(sw.ElapsedMilliseconds);
                    //}
                }
                
            }).Wait();
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Total sum: {0}", totalSum);

            //while (sumCursor.MoveNext(CancellationToken.None).Result) {
            //    //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
            //    //Console.WriteLine("Value: " + sumCursor.CurrentValue);
            //    totalSum += sumCursor.CurrentValue;
            //    c++;
            //    if (c % 10000 == 0) {
            //        Console.WriteLine(sw.ElapsedMilliseconds);
            //    }
            //}
            //sw.Stop();
            //Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            //Console.WriteLine("Total sum: {0}", totalSum);

        }


        [Test]
        public void CouldZipManyContinuousInRealTime2() {

            var sm1 = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < 1; i++) {
                sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
            }
            sm1.IsMutable = true; // will mutate after the first batch

            Task.Run(() => {
                //Thread.Sleep(1000);
                for (int i = 1; i < count; i++) {
                    sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //Thread.Sleep(50);
                }

                sm1.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            Task.Run(() => {
                //Thread.Sleep(950);
                for (int i = 1; i < count; i++) {
                    sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
                    //Thread.Sleep(50);
                }

                sm2.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();


            var series = new[] { sm1.Repeat(), sm2.Repeat() };

            sw.Start();
            var totalSum = 0.0;
            var sumCursor = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var c = 0;
            //while (sumCursor.MoveNext()) {
            //    //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
            //    totalSum += sumCursor.CurrentValue;
            //    c++;
            //}

            Task.Run(async () => {
                while (await sumCursor.MoveNext(CancellationToken.None)) {
                    //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                    //Console.WriteLine("Value: " + sumCursor.CurrentValue);
                    totalSum += sumCursor.CurrentValue;
                    c++;
                }
                sw.Stop();
                Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
                Console.WriteLine("Total sum: {0}", totalSum);
            }).Wait();



        }


        [Test]
        public void CouldZipManyContinuousInRealTime3() {

            var sm1 = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++) {
                sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
            }
            sm1.IsMutable = false; // will mutate after the first batch
            sm2.IsMutable = false;


            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();


            var series = new[] { sm1.Repeat(), sm2.Repeat(), sm1.Repeat(), sm2.Repeat(), sm1.Repeat(), sm2.Repeat(), sm1.Repeat(), sm2.Repeat(), sm1.Repeat(), sm2.Repeat() };

            sw.Start();
            var totalSum = 0.0;
            var sumCursor = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var c = 0;
            //while (sumCursor.MoveNext()) {
            //    //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
            //    totalSum += sumCursor.CurrentValue;
            //    c++;
            //}

            Task.Run(async () => {
                while (await sumCursor.MoveNext(CancellationToken.None)) {
                    //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                    //Console.WriteLine("Value: " + sumCursor.CurrentValue);
                    totalSum += sumCursor.CurrentValue;
                    c++;
                }
                sw.Stop();
                Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
                Console.WriteLine("Total sum: {0}", totalSum);
            }).Wait();



        }

        [Test]
        public void CouldZipContinuousInRealTime() {

            var sm1 = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
            }
            sm1.IsMutable = true; // will mutate after the first batch

            Task.Run(() => {
                Thread.Sleep(1000);
                for (int i = count; i < count * 2; i++) {
                    sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //Thread.Sleep(50);
                }

                sm1.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            Task.Run(() => {
                Thread.Sleep(950);
                for (int i = count; i < count * 2; i++) {
                    sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
                    //Thread.Sleep(50);
                }

                sm2.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();


            var series = new[] { sm1.Repeat(), sm2.Repeat() };

            sw.Start();
            var totalSum = 0.0;
            var sumCursor = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var c = 0;
            while (c < 5 && sumCursor.MoveNext()) {
                Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                totalSum += sumCursor.CurrentValue;
                c++;
            }


            Task.Run(async () => {
                while (await sumCursor.MoveNext(CancellationToken.None)) {
                    Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                    Console.WriteLine("Value: " + sumCursor.CurrentValue);
                    totalSum += sumCursor.CurrentValue;
                    c++;
                }
                sw.Stop();
                Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
                Console.WriteLine("Total sum: {0}", totalSum);
            }).Wait();



        }



        [Test]
        public void CouldZipManyContinuousInRealTime() {

            var sm1 = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++) {
                sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
            }
            sm1.IsMutable = true; // will mutate after the first batch

            Task.Run(() => {
                Thread.Sleep(1000);
                for (int i = count; i < count * 2; i++) {
                    sm1.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //Thread.Sleep(50);
                }

                sm1.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            Task.Run(() => {
                Thread.Sleep(950);
                for (int i = count; i < count * 2; i++) {
                    sm2.Add(DateTime.UtcNow.Date.AddSeconds(i), i * 3);
                    //Thread.Sleep(50);
                }

                sm2.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();


            var series = new[] { sm1.Repeat(), sm2.Repeat() };

            sw.Start();
            var totalSum = 0.0;
            var sumCursor = series.Zip((k, varr) => varr.Sum()).GetCursor();
            var c = 0;
            while (c < 5 && sumCursor.MoveNext()) {
                //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                totalSum += sumCursor.CurrentValue;
                c++;
            }

            while (sumCursor.MoveNext(CancellationToken.None).Result) {
                //Assert.AreEqual(c * 4.0, sumCursor.CurrentValue);
                //Console.WriteLine("Value: " + sumCursor.CurrentValue);
                totalSum += sumCursor.CurrentValue;
                c++;
            }
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Total sum: {0}", totalSum);


        }
    }
}
