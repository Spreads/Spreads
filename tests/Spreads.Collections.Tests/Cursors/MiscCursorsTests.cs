// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


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

namespace Spreads.Collections.Tests.Cursors
{

    // TODO (low) SMA & StDev are from Extensions project and should be moved to its test project

    [TestFixture]
    public class MiscCursorsTests
    {
        [Test]
        [Ignore("This test fails when using RunAll")]
        public void CouldLagSeriesManyTimes()
        {
            Parallel.For(0, 100, (x) =>
            {
                CouldLagSeries();
            });
        }

        [Test]
        public void CouldLagSeries()
        {
            var sm = new SortedMap<double, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++)
            {
                try
                {
                    sm.Add(i, i);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"i: {i}");
                    Console.WriteLine($"sm.count: {sm.Count}");
                    Console.WriteLine($"sm.IsRegular: {sm.IsRegular}");
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    throw e;
                }
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var lag = sm.Lag(1);//.ToSortedMap();
            var c = 1;
            foreach (var zl in lag)
            {
                if (c - 1 != zl.Value)
                {
                    throw new ApplicationException();
                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("ZipLag, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

            var repeated = lag.Repeat();

            for (int i = 1; i < 1000; i++)
            {
                double v;
                Assert.IsTrue(repeated.TryFind(i + 1.5, Lookup.EQ, out var kvp));
                Assert.AreEqual(i, kvp.Value);
            }

        }

        [Test]
        public void CouldZipLagSeries()
        {
            var sm = new SortedMap<DateTime, double>();

            var count = 10000000;

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var zipLag = sm.ZipLag(1, (cur, prev) => cur + prev); //.ToSortedMap();
            var c = 1;
            foreach (var zl in zipLag)
            {
                if (c + (c - 1) != zl.Value)
                {
                    throw new ApplicationException();
                }
                c++;
            }
            sw.Stop();
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("ZipLag, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));

        }


        [Test]
        public void CouldCloneZipLagSeries()
        {


            var count = 1000;
            var sm = new SortedMap<int, double>();
            for (int i = 0; i < count; i++)
            {
                sm.Add(i, i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var zipLag = sm.ZipLag(1, (cur, prev) => cur + prev); //.ToSortedMap();

            var zc = zipLag.GetCursor();
            zc.MoveNext();
            var zc2 = zc.Clone();
            Assert.AreEqual(zc.CurrentKey, zc2.CurrentKey);
            zc.MoveNext();
            zc2.MoveNext();
            Assert.AreEqual(zc.CurrentKey, zc2.CurrentKey);
            zc.MovePrevious();
            zc2.MovePrevious();
            Assert.AreEqual(zc.CurrentKey, zc2.CurrentKey);


            for (int i = 1; i < count; i++)
            {
                var expected = i + i - 1;
                double actual;
                var ok = zc.TryGetValue(i, out actual);
                Assert.AreEqual(expected, actual);
            }

            var sm2 = new SortedMap<int, double>();
            var zc3 = sm2.ZipLag(1, (cur, prev) => cur + prev).GetCursor();

            var t = Task.Run(async () =>
            {
                var c = 1; // first key is missing because we cannot create state at it
                while (await zc3.MoveNext(CancellationToken.None))
                {
                    var expected = c + c - 1;
                    Assert.AreEqual(expected, zc3.CurrentValue);
                    c++;
                }
            });

            for (int i = 0; i < count; i++)
            {
                sm2.Add(i, i);
            }
            sm2.Complete(); // without it MoveNextAsync will wait forever
            t.Wait();
        }


        [Test]
        public void CouldCalculateAverageOnMovingWindow()
        {
            var sm = new SortedMap<DateTime, double>();

            var count = 100000;

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.Window(20, 1); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma)
            {
                var innersm = m.Value; //.ToSortedMap();
                if (innersm.Values.Average() != c + 8.5)
                {
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
        public void CouldCalculateAverageOnMovingWindowWithStep()
        {
            var sm = new SortedMap<DateTime, double>();

            var count = 100000;

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.Window(20, 2);//.ToSortedMap();
            var c = 1;
            foreach (var m in ma)
            {
                var innersm = m.Value;//.ToSortedMap();
                if (innersm.Values.Average() != c + 8.5)
                {
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
        public void CouldZipSeries()
        {
            var sm = new SortedMap<DateTime, double>();
            var sm2 = new SortedMap<DateTime, double>();

            var count = 100000;

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            for (int i = 0; i < count; i++)
            {
                sm2.Add(DateTime.UtcNow.Date.AddSeconds(i * 2), i);
            }

            var expected = 0.0;
            for (int i = 0; i < count; i++)
            {
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
        public void CouldCalculateMovingAverage()
        {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.SMA(20); //.ToSortedMap();

            var cursor = ma.GetCursor();
            cursor.MoveNext();
            //var cc = 0;
            //while (cursor.MoveNextAsync())
            //{
            //    cc++;
            //}
            //Console.WriteLine(cc);
            //if (cursor.MoveNextAsync())
            //{
            //    throw new ApplicationException("Moved next after MoveNextAsync() returned false");
            //}
            //cursor.MoveFirst();
            var c = 1;
            //foreach (var m in ma) {
            cursor.Reset();
            while (cursor.MoveNext())
            {
                if (cursor.CurrentValue != c + 8.5)
                {
                    Console.WriteLine(cursor.CurrentValue);// m.Value);
                    Console.WriteLine($"Error c: {c}");
                    throw new ApplicationException("Invalid value");
                }
                c++;
                if (c == 999982)
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
        public void CouldCalculateMovingAverageIncomplete()
        {
            var sm = new SortedMap<DateTime, double>();

            var count = 100000;

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.SMA(20, true); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma)
            {
                if (c <= 20)
                {
                    //Console.WriteLine(m.Value);
                }
                else if (m.Value != c - 19 + 8.5)
                {
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
        public void CouldCalculateMovingStDev()
        {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 2; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.StDev(20, false); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma)
            {
                //if (c < 30) {
                //    Console.WriteLine(m.Value);
                //} else
                // TODO on c = 9490618 we have a different value: 5.91740018927231
                //   Excel value       5.91607978309962

                if (Math.Abs(m.Value - 5.9160797830996161) > 0.0000001)
                {
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
        public void CouldCalculateMovingStDevIncomlete()
        {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000000;

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            // slow implementation
            var sw = new Stopwatch();
            sw.Start();
            var ma = sm.StDev(20, true); //.ToSortedMap();
            var c = 1;
            foreach (var m in ma)
            {
                if (c < 30)
                {
                    Console.WriteLine(m.Value);
                }
                else
                // TODO on c = 9490618 we have a different value: 5.91740018927231
                //   Excel value       5.91607978309962

                if (Math.Abs(m.Value - 5.9160797830996161) > 0.0000001)
                {
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
        public void ZipLagIssue11Test()
        {
            OptimizationSettings.CombineFilterMapDelegates = true;

            var data = new SortedMap<int, double>();

            var count = 5000;

            for (int i = 0; i < count; i++)
            {
                data.Add(i, i);
            }

            var sma = data.SMA(20, true).ZipLag(1u, (c, p) => p).Lag(1u);
            Console.WriteLine($"laggedSma count: {sma.Count()}");

            var deviation = sma.StDev(10);
            var e = (deviation as IEnumerable<KeyValuePair<int, double>>).GetEnumerator();
            var cnt = 0;
            while (e.MoveNext())
            {
                cnt++;
            }
            Console.WriteLine(cnt);
            // this line or any other intermediate enumeration affect the last line
            Console.WriteLine($"deviation count: {deviation.Count()}");

            var direction = deviation;//.Map(x => (Math.Sign(x)));

            Console.WriteLine($"direction count: {direction.Count()}");

            var diff = direction.ZipLag(1u, (c, p) => c - p); //.ToSortedMap();

            Console.WriteLine($"Count: {diff.Count()}");

            Assert.IsTrue(diff.Count() > 0);
        }


        [Test]
        public void CouldScanSeries()
        {
            OptimizationSettings.CombineFilterMapDelegates = true;

            var data = new SortedMap<DateTime, double>();

            var count = 5000;

            for (int i = 0; i < count; i++)
            {
                data.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            var sign = 1;
            var runnningSum = data.Zip(data, (d1, d2) => d1 + d2).Scan(0.0, (st, k, v) =>
            {
                if (st > 100)
                {
                    sign = -1;
                }
                if (st < -100)
                {
                    sign = 1;
                }
                return st += sign * v;
            });
            var runnningSumSm = runnningSum.ToSortedMap();

            Assert.AreEqual(runnningSum.Count(), count);
        }


        [Test]
        public void RangeMustBeEmptyWhenOutOfRange()
        {
            var sm = new SortedMap<DateTime, double>
            {
                {new DateTime(2006, 1, 1), 1.0}
            };
            var test = sm.Range(new DateTime(2005, 1, 1), new DateTime(2005, 1, 2)).ToSortedMap();
            Assert.IsTrue(test.IsEmpty);
        }
    }
}
