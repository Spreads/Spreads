using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Diagnostics;
using System.Threading;

namespace Spreads.Collections.Tests.Cursors {

    // TODO (low) SMA & StDev are from Extensions project and should be moved to its test project

    [TestFixture]
    public class MiscCursorsTests {

        [Test]
        public void CouldLagSeries() {
            var sm = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var zipLag = sm.Lag(1);//.ToSortedMap();
            var c = 1;
            foreach (var zl in zipLag) {
                if (c - 1 != zl.Value) {
                    throw new ApplicationException();
                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("ZipLag, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }

        [Test]
        public void CouldZipLagSeries() {
            var sm = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var zipLag = sm.ZipLag(1, (cur, prev) => cur + prev); //.ToSortedMap();
            var c = 1;
            foreach (var zl in zipLag) {
                if (c + (c - 1) != zl.Value) {
                    throw new ApplicationException();
                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("ZipLag, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }


        [Test]
        public void CouldCalculateAverageOnMovingWindow() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.Window(20, 1); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma) {
                var innersm = m.Value; //.ToSortedMap();
                if (innersm.Values.Average() != c + 8.5) {
                    Console.WriteLine(m.Value.Values.Average());
                    throw new ApplicationException("Invalid value");
                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("Window MA, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));
            Console.WriteLine("Calculation ops: {0}", (int)((double)count * 20.0 / (sw.ElapsedMilliseconds / 1000.0)));
            // 8.5 MOps, compare it with optimized SMA
        }

        /// <summary>
        /// This also applies to ZipLagAllowIncompleteCursor since Window is built on it
        /// </summary>
        [Test]
        public void CouldCalculateAverageOnMovingWindowWithStep() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.Window(20, 2);//.ToSortedMap();
            var c = 1;
            foreach (var m in ma) {
                var innersm = m.Value;//.ToSortedMap();
                if (innersm.Values.Average() != c + 8.5) {
                    Console.WriteLine(m.Value.Values.Average());
                    throw new ApplicationException("Invalid value");
                }
                c++;
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("Window MA, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));
            Console.WriteLine("Calculation ops: {0}", (int)((double)count * 20.0 / (sw.ElapsedMilliseconds / 1000.0)));
        }


        [Test]
        public void CouldZipSeries() {
            var sm = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            for (int i = 0; i < count; i++) {
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            var expected = 0.0;
            for (int i = 0; i < count; i++) {
                expected += i * 2; ;
            }

            var sw = new Stopwatch();
            sw.Start();
            var sum = (sm + sm2).Values.Sum();
            sw.Stop();
            Assert.AreEqual(expected, sum);
            Console.WriteLine("Repeat + zip, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }


        [Test]
        public void CouldCalculateMovingAverage() {
            var sm = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.SMA(20); //.ToSortedMap();

            var cursor = ma.GetCursor();
            cursor.MoveNext();
            //var cc = 0;
            //while (cursor.MoveNext())
            //{
            //    cc++;
            //}
            //Console.WriteLine(cc);
            //if (cursor.MoveNext())
            //{
            //    throw new ApplicationException("Moved next after MoveNext() returned false");
            //}
            //cursor.MoveFirst();
            var c = 1;
            //foreach (var m in ma) {
            cursor.Reset();
            while (cursor.MoveNext()) {
                if (cursor.CurrentValue != c + 8.5)
                {
                    Console.WriteLine(cursor.CurrentValue);// m.Value);
                    Console.WriteLine($"Error c: {c}");
                    throw new ApplicationException("Invalid value");
                }
                c++;
                if (c == 9999982)
                {
                    Console.WriteLine("Catch me");
                }
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("SMA, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));
            ma = null;
            
            GC.Collect(3, GCCollectionMode.Forced, true);
            Thread.Sleep(2000);
            // NB! In release mode this must print that ToSortedMap task exited, in Debug mode GC does not collect SM and weak reference stays alive
             
        }

        [Test]
        public void CouldCalculateMovingAverageIncomplete() {
            var sm = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.SMA(20, true); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma) {
                if (c <= 20) {
                    //Console.WriteLine(m.Value);
                } else if (m.Value != c - 19 + 8.5) {
                    Console.WriteLine(m.Value);
                    throw new ApplicationException("Invalid value");
                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("SMA, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));
            // 
        }


        [Test]
        public void CouldCalculateMovingStDev() {
            var sm = new SortedMap<DateTime, double>();

            var count = 5000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.StDev(20, false); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma) {
                //if (c < 30) {
                //    Console.WriteLine(m.Value);
                //} else
                // TODO on c = 9490618 we have a different value: 5.91740018927231
                //   Excel value       5.91607978309962

                if (Math.Abs(m.Value - 5.9160797830996161) > 0.0000001) {
                    Console.WriteLine(m.Value);
                    Console.WriteLine($"Error c: {c}");
                    throw new ApplicationException("Invalid value");

                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("SMA, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));
            // 
        }


        [Test]
        public void CouldCalculateMovingStDevIncomlete() {
            var sm = new SortedMap<DateTime, double>();

            var count = 5000000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.StDev(20, true); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma) {
                if (c < 30) {
                    Console.WriteLine(m.Value);
                } else
                // TODO on c = 9490618 we have a different value: 5.91740018927231
                //   Excel value       5.91607978309962

                if (Math.Abs(m.Value - 5.9160797830996161) > 0.0000001) {
                    Console.WriteLine(m.Value);
                    Console.WriteLine($"Error c: {c}");
                    throw new ApplicationException("Invalid value");

                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("SMA, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));
            // 
        }

    }
}
