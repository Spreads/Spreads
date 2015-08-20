using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Spreads.Collections;

namespace Spreads.Collections.Tests {

	[TestFixture]
	public class SortedMapTests {



		[SetUp]
		public void Init() {
		}

		

		[Test]
		public void CouldEnumerateGrowingSM() {
            var count = 1000000;
            var sw = new Stopwatch();
            sw.Start();
            var sm = new SortedMap<DateTime, double>();
            var c = sm.GetCursor();

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                c.MoveNext();
                Assert.AreEqual(i, c.CurrentValue);
            }
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

        }


        [Test]
        public void CouldEnumerateChangingSM() {
            var count = 1000000;
            var sw = new Stopwatch();
            sw.Start();
            var sm = new SortedMap<DateTime, double>();
            var c = sm.GetCursor();

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                var version = sm.version;
                if (i > 10)
                {
                    sm[DateTime.UtcNow.Date.AddSeconds(i - 10)] = i - 10 + 1;
                    Assert.IsTrue(sm.version > version);
                }
                c.MoveNext();
                Assert.AreEqual(i, c.CurrentValue);
            }
            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

        }

    }
}
