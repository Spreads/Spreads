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

                var sm1 = new Spreads.Collections.SortedMap<int, int>(count);
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
                                sm1.TryAddLast(i, i);
                                Thread.SpinWait(30);

                                //if (i % 250000 == 0)
                                //{
                                //    GC.Collect(0, GCCollectionMode.Forced, false);
                                //}
                            }
                        }

                        await sm1.Complete();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });

                // addTask.Wait();

                Task.Run(async () =>
                {
                    using (Benchmark.Run("SM.Updated", count))
                    {
                        Thread.CurrentThread.Name = "MNA";
                        try
                        {
                            var c = 0;
                            using (var cursor = sm1.GetCursor())
                            {
                                while (await cursor.MoveNextAsync())
                                {
                                    AsyncCursor.LogFinished();
                                    if (c == 2)
                                    {
                                        c++;
                                    }

                                    if (cursor.CurrentKey != c)
                                    {
                                        ThrowHelper.ThrowInvalidOperationException("Wrong cursor enumeration");
                                    }

                                    c++;
                                    //if (c % 250000 == 0)
                                    //{
                                    //    GC.Collect(0, GCCollectionMode.Forced, false);
                                    //    Console.WriteLine(c);
                                    //}
                                }

                                if (c != count)
                                {
                                    ThrowHelper.ThrowInvalidOperationException($"Cannot move to count: c={c}, count={count}");
                                }
                                Thread.MemoryBarrier();
                                if (AsyncCursor.SyncCount == 0)
                                {
                                    Console.WriteLine("SyncCount == 0");
                                }

                                Console.WriteLine(
                                    $"{r}: Sync: {AsyncCursor.SyncCount}, Async: {AsyncCursor.AsyncCount}, Await: {AsyncCursor.AwaitCount}, Skipped: {AsyncCursor.SkippedCount}, Finished: {AsyncCursor.FinishedCount}");
                                Thread.MemoryBarrier();
                                AsyncCursor.ResetCounters();
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("MNA ex: " + e);
                        }
                    }

                    // Console.WriteLine(c);
                }).ContinueWith(t =>
                {
                    addTask.Wait();
                }).Wait();
            }
            Benchmark.Dump();
        }

        //[Test, Explicit("")]
        //public async Task ReusableWhenAnyTest()
        //{
        //    var count = 10_000;

        //    var sm1 = new Spreads.Collections.SortedMap<int, int>();
        //    var sm2 = new Spreads.Collections.SortedMap<int, int>();

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
