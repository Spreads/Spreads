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
	public class OperatorsTests {

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

        [Test]
        public void CouldMoveAtGE() {
            var scm = new SortedMap<int, int>(50);
            for (int i = 0; i < 100; i++) {
                scm[i] = i;
            }

            var cursor = scm.GetCursor();

            cursor.MoveAt(-100, Lookup.GE);

            Assert.AreEqual(0, cursor.CurrentKey);
            Assert.AreEqual(0, cursor.CurrentValue);

            var shouldBeFalse = cursor.MoveAt(-100, Lookup.LE);
            Assert.IsFalse(shouldBeFalse);


        }


        [Test]
        public void CouldMoveAtLE() {
            var scm = new SortedMap<long, long>();
            for (long i = int.MaxValue; i < int.MaxValue*4L; i = i + int.MaxValue) {
                scm[i] = i;
            }

            var cursor = scm.GetCursor();

            var shouldBeFalse = cursor.MoveAt(0, Lookup.LE);
            Assert.IsFalse(shouldBeFalse);

        }

    }
}
