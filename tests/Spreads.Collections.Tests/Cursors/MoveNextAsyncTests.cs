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


namespace Spreads.Collections.Tests.Cursors {

    [TestFixture]
    public class MoveNextAsyncTests {

        private void NOP(long durationTicks) {
            Stopwatch sw = new Stopwatch();

            sw.Start();

            while (sw.ElapsedTicks < durationTicks) {

            }
        }

        [Test]
        public void CouldUseAwaiter() {

            var t = Task.Delay(1000);
            var awaiter = t.GetAwaiter();
            awaiter.OnCompleted(() => Console.WriteLine("I am completed"));
            awaiter.OnCompleted(() => Console.WriteLine("I am completed"));
            Thread.Sleep(2000);
        }

        [Test]
        public void UpdateEventIsTriggered() {
            var sm = new SortedMap<DateTime, double>();
            (sm as IObservableEvents<DateTime, double>).OnNext += (kvp) => {
                Console.WriteLine("Added {0} : {1}", kvp.Key, kvp.Value);
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
        public void CouldMoveAsyncOnEmptySCM() {
            var sm = new SortedChunkedMap<DateTime, double>();
            var c = sm.GetCursor();
            var moveTask = c.MoveNext(CancellationToken.None);
            sm.Add(DateTime.UtcNow.Date.AddSeconds(0), 0);
            var result = moveTask.Result;
            Assert.IsTrue(result);
        }

        [Test]
        [Ignore]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursorManyTimes() {
            for (int round = 0; round < 10000; round++) {
                CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor();
            }
        }

        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor() {
            var count = 1000000;
            var sw = new Stopwatch();
            //var mre = new ManualResetEventSlim(true);
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            //var sm = new SortedChunkedMap<DateTime, double>();
            //sm.Add(DateTime.UtcNow.Date.AddSeconds(-2), 0);

            for (int i = 0; i < 5; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var addTask = Task.Run(async () => {

                await Task.Delay(10);
                for (int i = 5; i < count; i++) {
                    //mre.Wait();
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //await Task.Delay(1);
                    //NOP(500);
                }
                sm.Complete();

            });

            double sum = 0.0;
            int cnt = 0;
            var sumTask = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    if ((int)c.CurrentValue != cnt) {
                        Console.WriteLine("Wrong sequence");
                    } else {
                        //Console.WriteLine("Sync move");
                    }

                    sum += c.CurrentValue;
                    cnt++;
                }
                //Assert.AreEqual(10, sum);

                await Task.Delay(30);
                while (await c.MoveNext(CancellationToken.None)) {
                    //mre.Reset();
                    sum += c.CurrentValue;
                    if ((int)c.CurrentValue != cnt) {
                        //Console.WriteLine("Wrong sequence");
                        Assert.Fail($"Wrong sequence: {c.CurrentValue} != {cnt}");
                    } else {
                        //Console.WriteLine("Async move");
                    }
                    cnt++;
                    //mre.Set();
                }
                //Console.WriteLine("Finished 1");
            });

            double sum2 = 0.0;
            var sumTask2 = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum2 += c.CurrentValue;
                }
                Assert.AreEqual(10, sum2);

                await Task.Delay(50);
                while (await c.MoveNext(CancellationToken.None)) {
                    //mre.Reset();
                    sum2 += c.CurrentValue;
                    //mre.Set();
                }
                //Console.WriteLine("Finished 2");
            });

            double sum3 = 0.0;
            var sumTask3 = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum3 += c.CurrentValue;
                }
                Assert.AreEqual(10, sum3);

                await Task.Delay(100);
                while (await c.MoveNext(CancellationToken.None)) {
                    //mre.Reset();
                    sum3 += c.CurrentValue;
                    //mre.Set();
                }
                //Console.WriteLine("Finished 3");
            });

            sumTask.Wait();
            sumTask2.Wait();
            sumTask3.Wait();
            addTask.Wait();

            sw.Stop();
            Console.Write($"Elapsed msec: {sw.ElapsedMilliseconds - 50}; ");
            Console.WriteLine(@"Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++) {
                expectedSum += i;
            }
            if (expectedSum != sum) Console.WriteLine("Sum 1 is wrong");
            if (expectedSum != sum2) Console.WriteLine("Sum 2 is wrong");
            if (expectedSum != sum3) Console.WriteLine("Sum 3 is wrong");
            Assert.AreEqual(expectedSum, sum, "Sum 1");
            Assert.AreEqual(expectedSum, sum2, "Sum 2");
            Assert.AreEqual(expectedSum, sum3, "Sum 3");

            sm.Dispose();

        }


        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_spinwait() {

            OptimizationSettings.ArrayPool = new BaseArrayPool();


            for (int r = 0; r < 10; r++) {
                var count = 100000;
                var sw = new Stopwatch();


                var sm = new SortedMap<DateTime, double>();
                sm.IsSynchronized = true;
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
            sm.IsSynchronized = true;

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
        public void CouldUseCacheExtensionMethod() {
            var count = 100;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            sm.IsSynchronized = true;

            var addTask = Task.Run(async () => {
                await Task.Delay(50);
                for (int i = 0; i < count; i++) {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //await Task.Delay(1);
                }
            });

            var cached = sm.Cache();

            double sum = 0.0;
            var sumTask = Task.Run(async () => {
                var c = cached.GetCursor();
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
        public void CouldUseDoExtensionMethod() {
            var count = 100;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedChunkedMap<DateTime, double>();
            sm.IsSynchronized = true;

            var addTask = Task.Run(async () => {
                await Task.Delay(50);
                for (int i = 0; i < count; i++) {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                }
                // signal data completion
                sm.Complete();
            });

            var cached = sm; //.Cache();

            double sum = 0.0;

            cached.Do((k, v) => {
                sum += v;
            });


            addTask.Wait();
            Thread.Sleep(100);
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
        public void CouldUseDoExtensionMethodOnRange() {
            var count = 100;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedChunkedMap<DateTime, double>();
            sm.IsSynchronized = true;

            var addTask = Task.Run(async () => {
                await Task.Delay(50);
                for (int i = 0; i < count; i++) {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                }
                // signal data completion
                sm.Complete();
            });

            var cached = sm.Cache().After(DateTime.UtcNow.Date.AddSeconds(0), Lookup.EQ);

            double sum = 0.0;

            cached.Do((k, v) => {
                sum += v;
                Console.WriteLine($"{k} : {v}");
            });


            addTask.Wait();
            Thread.Sleep(100);
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

            var count = 10000000;
            var sw = new Stopwatch();

            var sm = new SortedMap<DateTime, double>();
            
            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.Complete();
            sw.Start();
            double sum = 0.0;
            var c = sm.GetCursor();
            Task.Run(async () => {
                while (await c.MoveNext(CancellationToken.None)) {
                    sum += c.CurrentValue;
                }
            }).Wait();
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
