using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Diagnostics;

namespace Spreads.Collections.Tests {
    [TestFixture]
    public class CursorSeriesTests {
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
                    //throw new ApplicationException("Invalid value");
                }
                c++;
            }
            sw.Stop();
            //Assert.AreEqual(count - 20, c);
            Console.WriteLine($"Final c: {c}");
            Console.WriteLine("Window MA, elapsed: {0}, ops: {1}", sw.ElapsedMilliseconds, (int)((double)count / (sw.ElapsedMilliseconds / 1000.0)));
            Console.WriteLine("Calculation ops: {0}", (int)((double)count * 20.0 / (sw.ElapsedMilliseconds / 1000.0)));
            // 8.5 MOps, compare it with optimized SMA
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

    }
}
