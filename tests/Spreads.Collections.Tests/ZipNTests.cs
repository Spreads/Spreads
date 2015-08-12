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
        public void CouldMoveOnTheSameFirstPositionOfThreeSeries() {

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

            var series = new[] {sm1, sm2, sm3};
            var sum = series.Zip((k, varr) => k*varr.Sum());

            var zipNCursor = sum.GetCursor();
            var movedFirst = zipNCursor.MoveFirst();
            Assert.IsTrue(movedFirst);
            Assert.AreEqual(6, zipNCursor.CurrentValue);

        }


        [Test]
        public void CouldMoveOnFirstPositionOfThreeSeries() {

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
            Assert.AreEqual((2+4+6)*2, zipNCursor.CurrentValue);

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
        public void CouldZipMillionInts()
        {
            var sw = new Stopwatch();
            
            var sm1 = new SortedMap<int, int>();

            sm1.Add(0, 0);

            for (int i = 2; i < 5000000; i++)
            {
                sm1.Add(i, i);
            }

            var series = new[] {sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,};//   sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum()).ToSortedMap();

            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            for (int i = 2; i < 5000000; i++) {
                Assert.AreEqual(series.Length* i, sum[i]);
            }

            
        }


        [Test]
        public void CouldZipMillionIntsBenchmark() {

            // this test measures isolated performance of ZipN, without ToSortedMap

            var sw = new Stopwatch();

            var sm1 = new SortedMap<int, int>();

            sm1.Add(0, 0);

            for (int i = 2; i < 5000000; i++) {
                sm1.Add(i, i);
            }

            var series = new[] {sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1,};//       sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, sm1, };

            sw.Start();

            var sum = series.Zip((k, varr) => varr.Sum());
            var totalSum = 0L;
            foreach (var kvp in sum)
            {
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
    }
}
