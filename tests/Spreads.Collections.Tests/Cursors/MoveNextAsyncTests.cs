// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;
using HdrHistogram;


namespace Spreads.Collections.Tests.Cursors
{

    [TestFixture]
    public class MoveNextAsyncTests
    {

        private void NOP(long durationTicks)
        {
            Stopwatch sw = new Stopwatch();

            sw.Start();

            while (sw.ElapsedTicks < durationTicks)
            {

            }
        }

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
        public void CouldMoveAsyncOnEmptySM()
        {
            var sm = new SortedChunkedMap<DateTime, double>();
            var c = sm.GetCursor();
            var moveTask = c.MoveNext(CancellationToken.None);
            sm.Add(DateTime.UtcNow.Date.AddSeconds(0), 0);
            var result = moveTask.Result;
            Assert.IsTrue(result);
        }

        [Test]
        public void CouldMoveAsyncOnEmptySCM()
        {
            var sm = new SortedChunkedMap<DateTime, double>();
            var c = sm.GetCursor();
            var moveTask = c.MoveNext(CancellationToken.None);
            sm.Add(DateTime.UtcNow.Date.AddSeconds(0), 0);
            var result = moveTask.Result;
            Assert.IsTrue(result);
        }

        [Test]
        [Ignore("long running")]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursorManyTimes()
        {
            ////use the second Core/Processor for the test
            //Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);
            ////prevent "Normal" Processes from interrupting Threads
            //Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            ////prevent "Normal" Threads from interrupting this thread
            //Thread.CurrentThread.Priority = ThreadPriority.Highest;

            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.LowLatency;
            GCLatencyMode oldMode = GCSettings.LatencyMode;
            GCSettings.LatencyMode = GCLatencyMode.LowLatency;
            for (int round = 0; round < 10; round++)
            {
                CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor();
                //if (GC.TryStartNoGCRegion(100 * 1024 * 1024))
                //{
                //    CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor();
                //    GC.EndNoGCRegion();
                //}

            }
            GCSettings.LatencyMode = oldMode;
        }

        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor()
        {
            var count = 5000000;
            var sw = new Stopwatch();
            sw.Start();
            var syncAddCnt = 0;
            var sm = new SortedMap<DateTime, double>();
            sm.IsSynchronized = true;
            //var sm = new SortedChunkedMap<DateTime, double>();
            //sm.Add(DateTime.UtcNow.Date.AddSeconds(-2), 0);

            for (int i = 0; i < syncAddCnt; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            var histogram = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            double sum = 0;
            var cnt = 0;
            var histogram1 = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            var sumTask = Task.Run(async () =>
            {
                var c = sm.GetCursor();

                var startTick = sw.ElapsedTicks;

                while (await c.MoveNext(CancellationToken.None))
                {
                    sum += c.CurrentValue;
                    if ((int)c.CurrentValue != cnt)
                    {
                        //Console.WriteLine("Wrong sequence");
                        //Assert.Fail($"Wrong sequence: {c.CurrentValue} != {cnt}");
                        Trace.WriteLine($"Wrong sequence1: {c.CurrentValue} != {cnt}; thread {Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //Console.WriteLine("Async move");
                    }
                    cnt++;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    try
                    {
                        histogram1.RecordValue(nanos);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Nanos: {nanos}; " + e.Message);
                    }
                    startTick = sw.ElapsedTicks;
                }
            });

            double sum2 = 0;
            var cnt2 = 0;
            var histogram2 = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            var sumTask2 = Task.Run(async () =>
            {
                var c = sm.GetCursor();

                var startTick = sw.ElapsedTicks;

                while (await c.MoveNext(CancellationToken.None))
                {
                    sum2 += c.CurrentValue;
                    if ((int)c.CurrentValue != cnt2)
                    {
                        //Console.WriteLine("Wrong sequence");
                        //Assert.Fail($"Wrong sequence: {c.CurrentValue} != {cnt}");
                        Trace.WriteLine($"Wrong sequence2: {c.CurrentValue} != {cnt2}; thread {Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //Console.WriteLine("Async move");
                    }
                    cnt2++;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    try
                    {
                        histogram2.RecordValue(nanos);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Nanos: {nanos}; " + e.Message);
                    }
                    startTick = sw.ElapsedTicks;
                }
            });

            double sum3 = 0;
            var cnt3 = 0;
            var histogram3 = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            var sumTask3 = Task.Run(async () =>
            {
                var c = sm.GetCursor();

                var startTick = sw.ElapsedTicks;

                while (await c.MoveNext(CancellationToken.None))
                {
                    sum3 += c.CurrentValue;
                    if ((int)c.CurrentValue != cnt3)
                    {
                        //Console.WriteLine("Wrong sequence");
                        //Assert.Fail($"Wrong sequence: {c.CurrentValue} != {cnt}");
                        Trace.WriteLine($"Wrong sequence3: {c.CurrentValue} != {cnt3}; thread {Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //Console.WriteLine("Async move");
                    }
                    cnt3++;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    try
                    {
                        histogram3.RecordValue(nanos);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Nanos: {nanos}; " + e.Message);
                    }
                    startTick = sw.ElapsedTicks;
                }


            });

            Thread.Sleep(1);

            var addTask = Task.Run(() =>
            {
                //Console.WriteLine($"Adding from thread {Thread.CurrentThread.ManagedThreadId}");

                for (int i = syncAddCnt; i < count; i++)
                {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                }
                sm.Complete();

            });


            while (!sumTask.Wait(2000))
            {
                OptimizationSettings.Verbose = true;
                Trace.WriteLine($"cnt: {cnt}");
            }
            while (!sumTask2.Wait(2000))
            {
                //OptimizationSettings.Verbose = true;
                Trace.WriteLine($"cnt2: {cnt2}");
            }
            while (!sumTask3.Wait(2000))
            {
                //OptimizationSettings.Verbose = true;
                Trace.WriteLine($"cnt3: {cnt3}");
            }
            addTask.Wait();
            histogram.Add(histogram1);
            histogram.Add(histogram2);
            histogram.Add(histogram3);
            histogram.OutputPercentileDistribution(
                    writer: Console.Out,
                    percentileTicksPerHalfDistance: 3,
                    outputValueUnitScalingRatio: OutputScalingFactor.None);

            sw.Stop();
            Trace.Write($"Elapsed msec: {sw.ElapsedMilliseconds}; ");
            Trace.WriteLine($"Ops: {Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2)}");

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                expectedSum += i;
            }
            if (expectedSum != sum) Trace.WriteLine("Sum 1 is wrong");
            if (expectedSum != sum2) Trace.WriteLine("Sum 2 is wrong");
            if (expectedSum != sum3) Trace.WriteLine("Sum 3 is wrong");
            Assert.AreEqual(expectedSum, sum, "Sum 1");
            Assert.AreEqual(expectedSum, sum2, "Sum 2");
            Assert.AreEqual(expectedSum, sum3, "Sum 3");

            //sm.Dispose();

        }


        [Test]
        [Ignore("long running")]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_NoSemaphore_ManyTimes()
        {
            for (int r = 0; r < 20; r++)
            {
                CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_NoSemaphore();
                GC.Collect(2, GCCollectionMode.Forced);
            }
        }

        [Test]
        [Ignore("long running")]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_NoSemaphore()
        {

            var cts = new CancellationTokenSource();
            var ct = CancellationToken.None; // cts.Token; //

            var count = 10000000;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            sm.IsSynchronized = true;
            //var sm = new SortedChunkedMap<DateTime, double>();
            //sm.Add(DateTime.UtcNow.Date.AddSeconds(-2), 0);

            for (int i = 0; i < 5; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            var histogram = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            double sum = 0;
            var cnt = 0;
            var histogram1 = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            var sumTask = Task.Run(async () =>
            {
                var c = sm.GetCursor();

                var startTick = sw.ElapsedTicks;

                while (await c.MoveNext(ct))
                {
                    sum += c.CurrentValue;
                    if ((int)c.CurrentValue != cnt)
                    {
                        //Console.WriteLine("Wrong sequence");
                        //Assert.Fail($"Wrong sequence: {c.CurrentValue} != {cnt}");
                        Trace.WriteLine($"Wrong sequence1: {c.CurrentValue} != {cnt}; thread {Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //Console.WriteLine("Async move");
                    }
                    cnt++;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    try
                    {
                        histogram1.RecordValue(nanos);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Nanos: {nanos}; " + e.Message);
                    }
                    startTick = sw.ElapsedTicks;
                }
            });

            double sum2 = 0;
            var cnt2 = 0;
            var histogram2 = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            var sumTask2 = Task.Run(async () =>
            {
                var c = sm.GetCursor();

                var startTick = sw.ElapsedTicks;

                while (await c.MoveNext(ct))
                {
                    sum2 += c.CurrentValue;
                    if ((int)c.CurrentValue != cnt2)
                    {
                        //Console.WriteLine("Wrong sequence");
                        //Assert.Fail($"Wrong sequence: {c.CurrentValue} != {cnt}");
                        Trace.WriteLine($"Wrong sequence2: {c.CurrentValue} != {cnt2}; thread {Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //Console.WriteLine("Async move");
                    }
                    cnt2++;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    try
                    {
                        histogram2.RecordValue(nanos);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Nanos: {nanos}; " + e.Message);
                    }
                    startTick = sw.ElapsedTicks;
                }
            });

            double sum3 = 0;
            var cnt3 = 0;
            var histogram3 = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            var sumTask3 = Task.Run(async () =>
            {
                var c = sm.GetCursor();

                var startTick = sw.ElapsedTicks;

                while (await c.MoveNext(ct))
                {
                    sum3 += c.CurrentValue;
                    if ((int)c.CurrentValue != cnt3)
                    {
                        //Console.WriteLine("Wrong sequence");
                        //Assert.Fail($"Wrong sequence: {c.CurrentValue} != {cnt}");
                        Trace.WriteLine($"Wrong sequence3: {c.CurrentValue} != {cnt3}; thread {Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //Console.WriteLine("Async move");
                    }
                    cnt3++;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    try
                    {
                        histogram3.RecordValue(nanos);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Nanos: {nanos}; " + e.Message);
                    }
                    startTick = sw.ElapsedTicks;
                }


            });

            Thread.Sleep(1);

            var addTask = Task.Run(() =>
            {
                //Console.WriteLine($"Adding from thread {Thread.CurrentThread.ManagedThreadId}");
                try
                {
                    for (int i = 5; i < count; i++)
                    {
                        sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    }
                    sm.Complete();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    Environment.FailFast(ex.Message, ex);
                }
            });


            while (!sumTask.Wait(2000))
            {
                OptimizationSettings.Verbose = true;
                Trace.WriteLine($"cnt: {cnt}");
            }
            while (!sumTask2.Wait(2000))
            {
                //OptimizationSettings.Verbose = true;
                Trace.WriteLine($"cnt2: {cnt2}");
            }
            while (!sumTask3.Wait(2000))
            {
                //OptimizationSettings.Verbose = true;
                Trace.WriteLine($"cnt3: {cnt3}");
            }
            addTask.Wait();
            histogram.Add(histogram1);
            histogram.Add(histogram2);
            histogram.Add(histogram3);
            histogram.OutputPercentileDistribution(
                    writer: Console.Out,
                    percentileTicksPerHalfDistance: 3,
                    outputValueUnitScalingRatio: OutputScalingFactor.None);

            sw.Stop();
            Trace.Write($"Elapsed msec: {sw.ElapsedMilliseconds}; ");
            Trace.WriteLine($"Ops: {Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2)}");

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                expectedSum += i;
            }
            if (expectedSum != sum) Trace.WriteLine("Sum 1 is wrong");
            if (expectedSum != sum2) Trace.WriteLine("Sum 2 is wrong");
            if (expectedSum != sum3) Trace.WriteLine("Sum 3 is wrong");
            Assert.AreEqual(expectedSum, sum, "Sum 1");
            Assert.AreEqual(expectedSum, sum2, "Sum 2");
            Assert.AreEqual(expectedSum, sum3, "Sum 3");

            //sm.Dispose();

        }


        [Test]
        [Ignore("long running")]
        public void CouldMoveNextAsyncWhenChangingOrder_NoSemaphore()
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var sm = new SortedMap<int, int>();

            sm.IsSynchronized = true;
            var tcs = new TaskCompletionSource<bool>();
            var sumTask = Task.Run(async () =>
            {
                var c = sm.GetCursor();
                tcs.SetResult(true);
                Assert.IsTrue(await c.MoveNext(ct));
                // here we change order
                Assert.IsTrue(await c.MoveNext(ct));
                Assert.IsFalse(await c.MoveNext(ct));

            });

            tcs.Task.Wait(ct);

            sm.Add(1, 1);
            Thread.Sleep(100);
            //sm.Add(0, 0); // will through OOO
            sm.Add(2, 2);
            //sm.Add(3, 3); 
            sm.Complete();

            sumTask.Wait(ct);
        }

        //[Test]
        //[Ignore("This hangs by construction")]
        //public void SpinWait()
        //{
        //    ThreadPool.SetMaxThreads(20, 20);
        //    for (int i = 0; i < 10000; i++)
        //    {
        //        Task.Factory.StartNew(() =>
        //        {
        //            var sw = new SpinWait();
        //            while (true)
        //            {
        //                sw.SpinOnce();
        //            }
        //        });
        //    }
        //}



        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_spinwait()
        {


            for (int r = 0; r < 10; r++)
            {
                var count = 100000;
                var sw = new Stopwatch();


                var sm = new SortedMap<DateTime, double>();
                sm.IsSynchronized = true;
                for (int i = 0; i < 5; i++)
                {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                }

                var addTask = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    for (int i = 5; i < count; i++)
                    {
                        sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                        //await Task.Delay(1);
                    }
                });

                double sum = 0.0;
                var sumTask = Task.Run(() =>
                {
                    var c = sm.GetCursor();
                    while (c.MoveNext())
                    {
                        sum += c.CurrentValue;
                    }
                    Assert.AreEqual(10, sum);
                    var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);

                    sw.Start();

                    while (true)
                    {
                        // spinwait
                        while (!c.MoveNext())
                        {
                        }
                        ;
                        sum += c.CurrentValue;
                        //Console.WriteLine("Current key: {0}", c.CurrentKey);
                        if (c.CurrentKey == stop)
                        {
                            break;
                        }
                    }
                });


                sumTask.Wait();
                addTask.Wait();
                sw.Stop();

                double expectedSum = 0.0;
                for (int i = 0; i < count; i++)
                {
                    expectedSum += i;
                }
                Assert.AreEqual(expectedSum, sum);

                Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
                Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));
            }

        }


        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor_StartEmpty()
        {
            var count = 1000000;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            sm.IsSynchronized = true;

            var addTask = Task.Run(async () =>
            {
                await Task.Delay(50);
                for (int i = 0; i < count; i++)
                {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //await Task.Delay(1);
                }
            });

            double sum = 0.0;
            var sumTask = Task.Run(async () =>
            {
                var c = sm.GetCursor();
                while (c.MoveNext())
                {
                    sum += c.CurrentValue;
                }
                Assert.AreEqual(0, sum);
                var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);
                //await Task.Delay(50);
                while (await c.MoveNext(CancellationToken.None))
                {
                    sum += c.CurrentValue;
                    //Console.WriteLine("Current key: {0}", c.CurrentKey);
                    if (c.CurrentKey == stop)
                    {
                        break;
                    }
                }
            });


            sumTask.Wait();
            addTask.Wait();
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

        }



        [Test]
        public void CouldUseCacheExtensionMethod()
        {
            var count = 3;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedMap<DateTime, double>();
            sm.IsSynchronized = true;

            var addTask = Task.Run(async () =>
            {
                await Task.Delay(50);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                        //await Task.Delay(1);
                    }
                    sm.Complete();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });

            var cached = sm;//.Cache();

            double sum = 0.0;
            var sumTask = Task.Run(async () =>
            {
                try
                {
                    var c = cached.GetCursor();
                    while (c.MoveNext())
                    {
                        sum += c.CurrentValue;
                    }
                    Assert.AreEqual(0, sum);
                    var stop = DateTime.UtcNow.Date.AddSeconds(count - 1);
                    //await Task.Delay(50);
                    while (await c.MoveNext(CancellationToken.None))
                    //while (c.MoveNextAsync())
                    {
                        sum += c.CurrentValue;
                        Console.WriteLine($"Current: {c.CurrentKey} - {c.CurrentValue}");
                        if (c.CurrentKey == stop)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + ":\n" + ex.ToString());
                }
            });

            sumTask.Wait();
            addTask.Wait();
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

            try
            {

            }
            finally
            {
                foreach (var kvp in sm)
                {
                    Console.WriteLine($"SM: {kvp.Key} - {kvp.Value}");
                }
            }

            Thread.Sleep(1000);
        }


        [Test]
        public void CouldUseDoExtensionMethod()
        {
            var count = 100;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedChunkedMap<DateTime, double>();
            sm.IsSynchronized = true;

            var addTask = Task.Run(async () =>
            {
                await Task.Delay(50);
                for (int i = 0; i < count; i++)
                {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                }
                // signal data completion
                sm.Complete();
            });

            var cached = sm; //.Cache();

            double sum = 0.0;

            var doTask = cached.Do((k, v) =>
            {
                Console.WriteLine($"{k} - {v}");
                sum += v;
            });


            addTask.Wait();
            doTask.Wait();
            //Thread.Sleep(100);
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

        }


        [Test]
        public void CouldUseDoExtensionMethodOnRange()
        {
            var count = 100;
            var sw = new Stopwatch();
            sw.Start();

            var sm = new SortedChunkedMap<DateTime, double>();
            sm.IsSynchronized = true;

            var addTask = Task.Run(async () =>
            {
                await Task.Delay(50);
                for (int i = 0; i < count; i++)
                {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                }
                // signal data completion
                sm.Complete();
            });

            var cached = sm.After(DateTime.UtcNow.Date.AddSeconds(0), true);

            double sum = 0.0;

            var doTask = cached.Do((k, v) =>
            {
                sum += v;
                Console.WriteLine($"{k} : {v}");
            });


            addTask.Wait();
            doTask.Wait(1000);
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

        }

        [Test]
        public void MoveNextAsyncBenchmark()
        {

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

            for (int i = 0; i < count; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.Complete();
            sw.Start();
            double sum = 0.0;
            var c = sm.GetCursor();
            Task.Run(async () =>
            {
                while (await c.MoveNext(CancellationToken.None))
                {
                    sum += c.CurrentValue;
                }
            }).Wait();
            sw.Stop();

            double expectedSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

        }



        internal class TestCompareExchange
        {
            public static TestCompareExchange defalt = new TestCompareExchange();
            public static bool allocated = false;
            public TestCompareExchange()
            {
                allocated = true;
                Console.WriteLine("I am created");
            }
        }

        [Test]
        public void CompareExchangeAllocatesValue()
        {

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
