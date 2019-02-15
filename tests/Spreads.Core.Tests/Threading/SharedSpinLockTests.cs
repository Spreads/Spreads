// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class SharedSpinLockTests
    {
        [Test]
        public unsafe void CouldAcquireReleaseLock()
        {
            var ptr = (long*)Marshal.AllocHGlobal(8);
            *ptr = 0;
            var wpid = Wpid.Create();
            var wpid2 = Wpid.Create();

            var sl = new SharedSpinLock(ptr);

            Assert.AreEqual(Wpid.Empty, sl.TryAcquireLock(wpid, spinLimit: 0)); // fast path

            Assert.AreEqual(Wpid.Empty, sl.TryReleaseLock(wpid));

            Assert.AreEqual(Wpid.Empty, sl.TryAcquireLock(wpid));

            var sw = new Stopwatch();
            sw.Start();
            Assert.AreEqual(wpid, sl.TryAcquireLock(wpid2, spinLimit: 1000));
            sw.Stop();
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");

            Assert.AreEqual(Wpid.Empty, sl.TryReleaseLock(wpid));
        }

        [Test, Explicit("long running")]
        public unsafe void UncontentedBench()
        {
            var ptr = (long*)Marshal.AllocHGlobal(8);
            *ptr = 0;
            var wpid = Wpid.Create();

            var sl = new SharedSpinLock(ptr);

            var count = 10_000_000;

            for (int _ = 0; _ < 10; _++)
            {
                using (Benchmark.Run("Uncontented", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        sl.TryAcquireLock(wpid);
                        sl.TryReleaseLock(wpid);
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void ContentedBench()
        {
            // for (int _ = 0; _ < 10; _++)
            {
                ContentedBenchImpl();
            }
            Benchmark.Dump();
        }

        public unsafe void ContentedBenchImpl()
        {
            var count = 1000;
            var threadCountPerLock = 4;
            var lockCount = 96;
            var jobLength = 1000;

            var timings = new long[count * threadCountPerLock * lockCount];
            var commitTimingsArr = new (long, long)[count * threadCountPerLock * lockCount];

            var threads = new Thread[lockCount][];
            var locks = new SharedSpinLock[lockCount];
            var sums = new long[lockCount];

            for (int i = 0; i < lockCount; i++)
            {
                var ptr = (long*)Marshal.AllocHGlobal(8);
                *ptr = 0;
                var sl = new SharedSpinLock(ptr);
                locks[i] = sl;
                threads[i] = new Thread[threadCountPerLock];
            }

            var cts = new CancellationTokenSource();
            var startMre = new ManualResetEventSlim(false);
            var pauseMre = new ManualResetEventSlim(false);
            var endEvent = new CountdownEvent(lockCount * threadCountPerLock);

            for (int li = 0; li < lockCount; li++)
            {
                var liLocal = li;
                var lockLi = locks[liLocal];
                var threadArrayLi = threads[liLocal];

                for (int ti = 0; ti < threadCountPerLock; ti++)
                {
                    var t = new Thread(() =>
                    {
                        try
                        {
                            var wpid = Wpid.Create();
                            while (!cts.IsCancellationRequested)
                            {
                                startMre.Wait();

                                for (int i = 0; i < count; i++)
                                {
                                    if (default != lockLi.TryAcquireLock(wpid))
                                    {
                                        Assert.Fail("Cannot acquire lock");
                                    }

                                    // pathological case when lock holder is blocked/preempted
                                    Thread.Yield();

                                    //for (int j = 0; j < jobLength; j++)
                                    //{
                                    //    Volatile.Write(ref sums[liLocal], Volatile.Read(ref sums[liLocal]) + 1);
                                    //}

                                    if (default != lockLi.TryReleaseLock(wpid))
                                    {
                                        Assert.Fail("Cannot release lock");
                                    }
                                }

                                endEvent.Signal();
                                pauseMre.Wait();
                                // Console.WriteLine("restarted");
                            }

                            Console.WriteLine("EXITED");
                        }
                        catch
                        {
                            Console.WriteLine("ERROR");
                        }
                    });
                    t.Priority = ThreadPriority.Normal;
                    t.Start();

                    threadArrayLi[ti] = t;
                }
            }

            var rels = 0L;
            var tos = 0L;
            for (int _ = 0; _ < 200; _++)
            {
                // how many jobs we could do while number of threads is much bigger then number of cores
                using (Benchmark.Run("Contented", count * threadCountPerLock * lockCount))
                {
                    startMre.Set();
                    endEvent.Wait();
                }

                endEvent.Reset();
                startMre.Reset();

                pauseMre.Set();
                Thread.Sleep(100);
                pauseMre.Reset();

                Console.Write("                                                                    SEM RELs: " + (SharedSpinLock.SemaphoreReleaseCount - rels));
                Console.Write(" TOs: " + (SharedSpinLock.SemaphoreTimeoutCount - tos));
                Console.WriteLine(" CNT: " + (SharedSpinLock._semaphores.Count(x => x.Item1 != default)));

                rels = SharedSpinLock.SemaphoreReleaseCount;
                tos = SharedSpinLock.SemaphoreTimeoutCount;
            }

            cts.Cancel();
            //foreach (var thread in threads.SelectMany(t => t))
            //{
            //    thread.Join();
            //}
        }

        public void ContentedBenchImplTasks()
        {
            var count = 1000000;
            var threadCountPerLock = 2;
            var lockCount = 2;
            var jobLength = 1000;

            var timings = new long[count * threadCountPerLock * lockCount];
            var commitTimingsArr = new (long, long)[count * threadCountPerLock * lockCount];

            var threads = new Task[lockCount][];
            var locks = new SharedSpinLock[lockCount];
            var sums = new long[lockCount];

            for (int i = 0; i < lockCount; i++)
            {
                var ptr = Marshal.AllocHGlobal(8);
                Marshal.WriteInt64(ptr, 0, 0);

                var sl = new SharedSpinLock(ptr);
                locks[i] = sl;
                threads[i] = new Task[threadCountPerLock];
            }
            
            var cts = new CancellationTokenSource();
            var startMre = new ManualResetEventSlim(false);
            var pauseMre = new ManualResetEventSlim(false);
            var endEvent = new CountdownEvent(lockCount * threadCountPerLock);

            // how many jobs we could do while number of threads is much bigger then number of cores
            using (Benchmark.Run("Contented", count * threadCountPerLock * lockCount))
            {
                for (int li = 0; li < lockCount; li++)
                {
                    var liLocal = li;
                    var lockLi = locks[liLocal];
                    var threadArrayLi = threads[liLocal];

                    for (int ti = 0; ti < threadCountPerLock; ti++)
                    {
                        var t = Task.Run(async () =>
                        {
                            try
                            {
                                // while (!cts.IsCancellationRequested)
                                {
                                    var wpid = Wpid.Create((uint)ti + 1);
                                    for (int i = 0; i < count; i++)
                                    {
                                        if (default != lockLi.TryAcquireLock(wpid))
                                        {
                                            Assert.Fail("Cannot acquire lock");
                                        }

                                        // pathological case when lock holder is blocked/preempted
                                        Thread.Yield();

                                        for (int j = 0; j < jobLength; j++)
                                        {
                                            Volatile.Write(ref sums[liLocal], Volatile.Read(ref sums[liLocal]) + 1);
                                        }

                                        if (default != lockLi.TryReleaseLock(wpid))
                                        {
                                            Assert.Fail("Cannot release lock");
                                        }
                                    }

                                    // Console.WriteLine("restarted");
                                }

                                // Console.WriteLine("EXITED");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("ERROR: " + ex);
                            }
                        });

                        threadArrayLi[ti] = t;
                    }
                }

                Task.WaitAll(threads.SelectMany(x => x).ToArray());
            }

            //for (int _ = 0; _ < 20; _++)
            //{
            //    // how many jobs we could do while number of threads is much bigger then number of cores
            //    using (Benchmark.Run("Contented", count * threadCountPerLock * lockCount))
            //    {
            //        startMre.Set();
            //        endEvent.Wait();
            //    }

            //    endEvent.Reset();
            //    startMre.Reset();

            //    pauseMre.Set();
            //    Thread.Sleep(100);
            //    pauseMre.Reset();
            //}

            cts.Cancel();
            //foreach (var thread in threads.SelectMany(t => t))
            //{
            //    thread.Join();
            //}
            Thread.Sleep(1000);
        }
    }
}