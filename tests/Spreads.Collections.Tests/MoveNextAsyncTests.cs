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
    public class MoveNextAsyncTests {

        [Test]
        public void UpdateEventIsTriggered() {
            var sm = new SortedMap<DateTime, double>();
            (sm as IUpdateable<DateTime, double>).OnData += (s, x) => {
                Console.WriteLine("Added {0} : {1}", x.Key, x.Value);
            };

            sm.Add(DateTime.UtcNow.Date.AddSeconds(0), 0);

        }


        [Test]
        public void CouldMoveAsyncOnEmptySM() {
            var sm = new SortedMap<DateTime, double>();

            var c = sm.GetCursor();

            var moveTask = c.MoveNext(CancellationToken.None);

            sm.Add(DateTime.UtcNow.Date.AddSeconds(0), 0);

            var result = moveTask.Result;

            Assert.IsTrue(result);

        }

        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor() {
            var count = 1000000;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            //sm.IsSynchronized = true;
            for (int i = 0; i < 5; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var addTask = Task.Run(async () => {
                await Task.Delay(50);
                for (int i = 5; i < count; i++) {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //await Task.Delay(1);
                }
            });

            double sum = 0.0;
            var sumTask = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum += c.CurrentValue;
                }
                Assert.AreEqual(10, sum);
                var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);
                //await Task.Delay(50);
                while (await c.MoveNext(CancellationToken.None)) {
                    sum += c.CurrentValue;
                    //Console.WriteLine("Current key: {0}", c.CurrentKey);
                    if (c.CurrentKey == stop) {
                        break;
                    }
                }
            });


            sumTask.Wait();
            addTask.Wait();
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++) {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
        }


        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_StartEmpty() {
            var count = 1000000;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            //sm.IsSynchronized = true;

            var addTask = Task.Run(async () => {
                await Task.Delay(50);
                for (int i = 0; i < count; i++) {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //await Task.Delay(1);
                }
            });

            double sum = 0.0;
            var sumTask = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum += c.CurrentValue;
                }
                Assert.AreEqual(0, sum);
                var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);
                //await Task.Delay(50);
                while (await c.MoveNext(CancellationToken.None)) {
                    sum += c.CurrentValue;
                    //Console.WriteLine("Current key: {0}", c.CurrentKey);
                    if (c.CurrentKey == stop) {
                        break;
                    }
                }
            });


            sumTask.Wait();
            addTask.Wait();
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++) {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
        }

        [Test]
        public void MoveNextAsyncBenchmark() {

            // this benchmark shows that simple async enumeration gives 13+ mops,
            // this means than we should use parallel enumeration on joins.
            // the idea is that a chain of calculations could be somewhat heavy, e.g. rp(ma(log(zipLag(c p -> c/p))))
            // but they are optimized for single thread: when movenext is called on the outer cursor,
            // the whole chain enumerates synchronously.
            // During joins, we must make these evaluations parallel. The net overhead of tasks over existing data is visible
            // but not too big, while on real-time stream there is no alternative at all.
            // Join algos should be paralell and task-based by default

            var count = 1000000;
            var sw = new Stopwatch();

            var sm = new SortedMap<DateTime, double>();
            //sm.IsSynchronized = true;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sw.Start();
            double sum = 0.0;
            var c = sm.GetCursor();
            while (c.CurrentValue < count - 1.0)
            {
                Task.Run(async () => await c.MoveNext(CancellationToken.None)).Wait();
                sum += c.CurrentValue;
            }
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++) {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds*1.0), 2));


        }

    }
}
