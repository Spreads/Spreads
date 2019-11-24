// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class ValueTaskTests
    {
        [Test]
        public async Task CouldUseIDeltaMethods()
        {
            var tasks = new ValueTask<int>[4];

            tasks[0] = new ValueTask<int>(1);
            tasks[1] = new ValueTask<int>(Task.Run(async () =>
            {
                await Task.Delay(100);
                return 42;
            }));
            tasks[2] = new ValueTask<int>(Task.Run(async () =>
            {
                await Task.Delay(200);
                ThrowHelper.ThrowInvalidOperationException();
                return 0;
            }));

            tasks[3] = new ValueTask<int>(Task.Run<int>(async () => throw new OperationCanceledException()));

            await tasks.WhenAll();

            Assert.AreEqual(tasks[0].Result, 1);
            Assert.AreEqual(tasks[1].Result, 42);
            Assert.IsTrue(tasks[2].IsFaulted);
            Assert.IsTrue(tasks[3].IsCanceled);

            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = tasks[2].Result;
            });

            Assert.Throws<OperationCanceledException>(() =>
            {
                var _ = tasks[3].Result;
            });
        }

        [Test, Explicit("")]
        public void SortedMapNotifierTest()
        {
            var rounds = 100_000;
            for (int r = 0; r < rounds; r++)
            {
                var count = 1_000_000;
                var cnt1 = 0;
                var cnt2 = 0;
                var cnt3 = 0;
                var cnt4 = 0;

                var sm1 = new Series<int, int>(count);
                // sm1._isSynchronized = false;
                var addTask = Task.Run(async () =>
                {
                    // await Task.Delay(5000);
                    try
                    {
                        // sm1.TryAddLast(0, 0);
                        for (int i = 0; i < count; i++)
                        {
                            if (i != 2)
                            {
                                await sm1.TryAddLast(i, i);
                                Thread.SpinWait(5);

                                //if (i % 250000 == 0)
                                //{
                                //    GC.Collect(0, GCCollectionMode.Forced, false);
                                //}
                            }
                        }

                        await sm1.Complete();
                        //Console.WriteLine("cnt1: " + cnt1);
                        //Console.WriteLine("cnt2: " + cnt2);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });

                // addTask.Wait();
                using (Benchmark.Run("SM.Updated", count * 5))
                {
                    var t1 = Task.Run(async () =>
                {
                    Thread.CurrentThread.Name = "MNA1";
                    try
                    {
                        using (var cursor1 = sm1.GetAsyncCursor())
                        {
                            // Console.WriteLine("MNA1 started");
                            while (await cursor1.MoveNextAsync())
                            {
                                AsyncCursor.LogFinished();
                                if (cnt1 == 2)
                                {
                                    cnt1++;
                                    // Console.WriteLine("MNA1 moving");
                                }

                                if (cursor1.CurrentKey != cnt1)
                                {
                                    ThrowHelper.ThrowInvalidOperationException("Wrong cursor enumeration");
                                }

                                cnt1++;
                                //if (c % 250000 == 0)
                                //{
                                //    GC.Collect(0, GCCollectionMode.Forced, false);
                                //    Console.WriteLine(c);
                                //}
                            }

                            if (cnt1 != count)
                            {
                                ThrowHelper.ThrowInvalidOperationException($"1 Cannot move to count: c={cnt1}, count={count}");
                            }

                            if (AsyncCursor.SyncCount == 0)
                            {
                                Console.WriteLine("SyncCount == 0");
                            }

                            Thread.MemoryBarrier();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("MNA1 ex: " + e);
                    }
                });

                    var t2 = Task.Run(async () =>
                    {
                        Thread.CurrentThread.Name = "MNA2";
                        try
                        {
                            using (var cursor2 = sm1.GetAsyncCursor())
                            {
                                // Console.WriteLine("MNA2 started");
                                while (await cursor2.MoveNextAsync())
                                {
                                    AsyncCursor.LogFinished();
                                    if (cnt2 == 2)
                                    {
                                        cnt2++;
                                        // Console.WriteLine("MNA2 moving");
                                    }

                                    if (cursor2.CurrentKey != cnt2)
                                    {
                                        ThrowHelper.ThrowInvalidOperationException("Wrong cursor enumeration");
                                    }

                                    cnt2++;
                                }

                                if (cnt2 != count)
                                {
                                    ThrowHelper.ThrowInvalidOperationException($"2 Cannot move to count: c={cnt2}, count={count}");
                                }

                                if (AsyncCursor.SyncCount == 0)
                                {
                                    Console.WriteLine("SyncCount == 0");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("MNA2 ex: " + e);
                        }
                    });

                    var t3 = Task.Run(async () =>
                    {
                        Thread.CurrentThread.Name = "MNA3";
                        try
                        {
                            using (var cursor3 = sm1.GetAsyncCursor())
                            {
                                // Console.WriteLine("MNA2 started");
                                while (await cursor3.MoveNextAsync())
                                {
                                    AsyncCursor.LogFinished();
                                    if (cnt3 == 2)
                                    {
                                        cnt3++;
                                        // Console.WriteLine("MNA2 moving");
                                    }

                                    if (cursor3.CurrentKey != cnt3)
                                    {
                                        ThrowHelper.ThrowInvalidOperationException("Wrong cursor enumeration");
                                    }

                                    cnt3++;
                                }

                                if (cnt3 != count)
                                {
                                    ThrowHelper.ThrowInvalidOperationException($"3 Cannot move to count: c={cnt3}, count={count}");
                                }

                                if (AsyncCursor.SyncCount == 0)
                                {
                                    Console.WriteLine("SyncCount == 0");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("MNA3 ex: " + e);
                        }
                    });

                    var t4 = Task.Run(async () =>
                    {
                        Thread.CurrentThread.Name = "MNA4";
                        try
                        {
                            using (var cursor4 = sm1.GetAsyncCursor())
                            {
                                // Console.WriteLine("MNA2 started");
                                while (await cursor4.MoveNextAsync())
                                {
                                    AsyncCursor.LogFinished();
                                    if (cnt4 == 2)
                                    {
                                        cnt4++;
                                        // Console.WriteLine("MNA2 moving");
                                    }

                                    if (cursor4.CurrentKey != cnt4)
                                    {
                                        ThrowHelper.ThrowInvalidOperationException("Wrong cursor enumeration");
                                    }

                                    cnt4++;
                                }

                                if (cnt4 != count)
                                {
                                    ThrowHelper.ThrowInvalidOperationException($"4 Cannot move to count: c={cnt4}, count={count}");
                                }

                                if (AsyncCursor.SyncCount == 0)
                                {
                                    Console.WriteLine("SyncCount == 0");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("MNA3 ex: " + e);
                        }
                    });

                    var finished = false;
                    while (!finished)
                    {
                        finished = Task.WhenAll(addTask, t1, t2, t3, t4).Wait(2000);
                        //Console.WriteLine("cnt1: " + cnt1);
                        //Console.WriteLine("cnt2: " + cnt2);
                    }
                    Console.WriteLine($"{r}: Sync: {AsyncCursor.SyncCount}, Async: {AsyncCursor.AsyncCount}, Await: {AsyncCursor.AwaitCount}, Skipped: {AsyncCursor.SkippedCount}, Missed: {AsyncCursor.MissedCount}, Finished: {AsyncCursor.FinishedCount}");
                    AsyncCursor.ResetCounters();
                }
            }
            Benchmark.Dump();
        }

        //[Test, Explicit("")]
        //public async Task ReusableWhenAnyTest()
        //{
        //    var count = 10_000;

        //    var sm1 = new Spreads.Collections.Series<int, int>();
        //    var sm2 = new Spreads.Collections.Series<int, int>();

        //    var whenAny = new Spreads.Collections.Experimental.ReusableWhenAny2(sm1, sm2); //  ReusableValueTaskWhenAny<int>();

        //    var _ = Task.Run(async () =>
        //    {
        //        await Task.Delay(100);
        //        for (int i = 0; i < count; i++)
        //        {
        //            await sm1.TryAddLast(i, i);
        //        }
        //    });

        //    var __ = Task.Run(async () =>
        //    {
        //        await Task.Delay(100);
        //        for (int i = 0; i < count; i++)
        //        {
        //            await sm2.TryAddLast(i, i);
        //        }
        //    });

        //    var c = 0;
        //    using (Benchmark.Run("WhenAny", count))
        //    {
        //        Task.Run(async () =>
        //    {
        //        while (c < count)
        //        {
        //            await whenAny.GetTask(); //  new WhenAnyAwiter<int>(t1, t2);
        //            c++;
        //            // Console.WriteLine(c);
        //        }
        //    }).Wait();
        //    }

        //    Benchmark.Dump();
        //}
    }
}
