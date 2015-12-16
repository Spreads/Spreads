using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Diagnostics;

namespace Spreads.Collections.Tests.Cursors {
    [TestFixture]
    public class MissingValuesCursorsTests {

        [Test]
        public void CouldRepeatSeries() {
            var sm = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            for (int i = 0; i < count; i++) {
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i * 2 + 1), i);
            }

            var expected = 0.0;
            for (int i = 0; i < count; i++) {
                expected += i * 2; ;
            }

            var sw = new Stopwatch();
            sw.Start();
            var sum = (sm.Repeat() + sm2).Values.Sum();
            sw.Stop();
            Assert.AreEqual(expected, sum);
            Console.WriteLine("Repeat + zip, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }

        [Test]
        public void CouldFillSeries() {
            var sm = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            for (int i = 0; i < count; i++) {
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i * 2 + 1), i);
            }

            var expected = 0.0;
            for (int i = 0; i < count; i++) {
                expected += i; ;
            }

            var sw = new Stopwatch();
            sw.Start();
            var sum = (sm.Fill(0) + sm2).Values.Sum();
            sw.Stop();
            Assert.AreEqual(expected, sum);
            Console.WriteLine("Repeat + zip, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }


        [Test]
        public void CouldRepeatMapSeries() {
            var sm = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            for (int i = 0; i < count; i++) {
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i * 2 + 1), i);
            }

            var expected = 0.0;
            for (int i = 0; i < count; i++) {
                expected += i * 2 + 1 + 1;
            }
            OptimizationSettings.CombineFilterMapDelegates = false;
            var sw = new Stopwatch();
            sw.Start();
            var sum = (sm.Repeat().Map(x => x + 1.0).Repeat().Map(x => x + 1.0) + sm2).Values.Sum(); //
            sw.Stop();
            //Assert.AreEqual(expected, sum);
            Console.WriteLine("Repeat + zip, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }

        [Test]
        public void CouldRepeatSeriesAsync() {
            new ZipNTests().CouldZipContinuousInRealTime();
        }

        [Test]
        public void CouldRepeatSingleValueSeries() {
            var sm = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < 1; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            for (int i = 0; i < count; i++) {
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i * 2 + 1), i);
            }

            var expected = 0.0;
            for (int i = 0; i < count; i++) {
                expected += i; ;
            }


            var sw = new Stopwatch();
            sw.Start();
            var sum = (sm.Repeat() + sm2).Values.Sum();
            sw.Stop();
            Assert.AreEqual(expected, sum);
            Console.WriteLine("Repeat + zip, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }

        [Test]
        public void CouldRepeatEmptySeries() {
            var sm = new SortedMap<DateTime, double>();

            var c = sm.Repeat().GetCursor();

            Assert.IsFalse(c.MoveNext());

        }
    }
}
