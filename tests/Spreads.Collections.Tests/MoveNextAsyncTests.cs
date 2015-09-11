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
using System.Runtime.CompilerServices;


namespace Spreads.Collections.Tests {

    [TestFixture]
    public class MoveNextAsyncTests {

        [Test]
        public void CouldUseAwaiter()
        {

            var t = Task.Delay(1000);
            var awaiter = t.GetAwaiter();
            awaiter.OnCompleted(() => Console.WriteLine("I am completed"));
            awaiter.OnCompleted(() => Console.WriteLine("I am completed"));
            Thread.Sleep(2000);
        }

        [Test]
        public void UpdateEventIsTriggered() {
            var sm = new SortedMap<DateTime, double>();
            (sm as IUpdateable<DateTime, double>).OnData += (s, x) => {
                Console.WriteLine("Added {0} : {1}", x.Key,
                    x.Value);
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
            var count = 10000000; //00000;
            var sw = new Stopwatch();
            //var mre = new ManualResetEventSlim(true);
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            //sm.Add(DateTime.UtcNow.Date.AddSeconds(-2), 0);

            //sm.IsSynchronized = true;
            for (int i = 0; i < 5; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var addTask = Task.Run(async () => {

                await Task.Delay(50);
                //try {
                for (int i = 5; i < count; i++) {
                    //mre.Wait();
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //await Task.Delay(1);
                }
                //} catch(Exception e)
                //{
                //    Console.WriteLine(e.Message);
                //}
            });

            double sum = 0.0;
            var sumTask = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum += c.CurrentValue;
                }
                Assert.AreEqual(10, sum);
                var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);

                await Task.Delay(10);
                while (await c.MoveNext(CancellationToken.None)) {
                    //mre.Reset();
                    sum += c.CurrentValue;
                    if (c.CurrentKey == stop) {
                        break;
                    }
                    //mre.Set();
                }
                Console.WriteLine("Finished");
            });

            double sum2 = 0.0;
            var sumTask2 = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum2 += c.CurrentValue;
                }
                Assert.AreEqual(10, sum2);
                var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);

                await Task.Delay(100);
                while (await c.MoveNext(CancellationToken.None)) {
                    //mre.Reset();
                    sum2 += c.CurrentValue;
                    if (c.CurrentKey == stop) {
                        break;
                    }
                    //mre.Set();
                }
                Console.WriteLine("Finished");
            });

            double sum3 = 0.0;
            var sumTask3 = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum3 += c.CurrentValue;
                }
                Assert.AreEqual(10, sum3);
                var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);

                await Task.Delay(100);
                while (await c.MoveNext(CancellationToken.None)) {
                    //mre.Reset();
                    sum3 += c.CurrentValue;
                    if (c.CurrentKey == stop) {
                        break;
                    }
                    //mre.Set();
                }
                Console.WriteLine("Finished");
            });

            sumTask.Wait();
            sumTask2.Wait();
            sumTask3.Wait();
            addTask.Wait();

            sw.Stop();
            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++) {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);
            Assert.AreEqual(expectedSum, sum2);
            Assert.AreEqual(expectedSum, sum3);
        }


        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_spinwait() {

            for (int r = 0; r < 10; r++) {
                var count = 10000000;
                var sw = new Stopwatch();


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
                var sumTask = Task.Run(() => {
                    var c = sm.GetCursor();
                    while (c.MoveNext()) {
                        sum += c.CurrentValue;
                    }
                    Assert.AreEqual(10, sum);
                    var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);

                    sw.Start();

                    while (true) {
                        // spinwait
                        while (!c.MoveNext()) {
                        }
                        ;
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
                Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));
            }

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
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

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
            while (c.CurrentValue < count - 1.0) {
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
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

        }



        internal class TestCompareExchange {
            public static TestCompareExchange defalt = new TestCompareExchange();
            public static bool allocated = false;
            public TestCompareExchange() {
                allocated = true;
                Console.WriteLine("I am created");
            }
        }

        [Test]
        public void CompareExchangeAllocatesValue() {

            if (TestCompareExchange.allocated && (new TestCompareExchange()) != null) // the second part after && is not evaluated 
            {

            }
            Assert.IsFalse(TestCompareExchange.allocated);

            TestCompareExchange target = null;
            var original = Interlocked.CompareExchange(ref target, new TestCompareExchange(), (TestCompareExchange)null);
            Assert.AreEqual(null, original);
            Assert.IsTrue(TestCompareExchange.allocated);

            TestCompareExchange.allocated = false;
            target = null;
            original = Interlocked.CompareExchange(ref target, new TestCompareExchange(), TestCompareExchange.defalt);
            Assert.AreEqual(null, original);
            Assert.IsTrue(TestCompareExchange.allocated); // no exchange, but objetc is allocated
        }

    }
}
