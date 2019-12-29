// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Cursors
{
    [Category("CI")]
    [TestFixture]
    public class AsyncCursorTests
    {
        [Test]
        public void DeadCursorDoesNotCauseEndlessLoopInNotifyUpdate()
        {
            var s = new AppendSeries<int, int>();

            s.Append(1, 1);

            var cursor = s.GetAsyncEnumerator();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            cursor.MoveNextAsync();
            cursor.Dispose();
            cursor = default;

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.Collect(2, GCCollectionMode.Forced, true);

            //var t = Task.Run(() => cursor.MoveNextAsync(cts.Token));

            s.Append(2, 2);
            s.Append(3, 3);

            Assert.True(s.RowCount == 3);

            s.Dispose();
        }

        [Test]
        public void CancelledCursorDoesntCauseEndlessLoopInNotifyUpdate()
        {
            var s = new AppendSeries<int,int>();

            s.Append(1, 1);

            var c = s.GetAsyncEnumerator();
            Assert.True(c.MoveNext());
            Assert.False(c.MoveNext());

            c.MoveNextAsync();

            s.Append(2, 2);
            s.Append(3, 3);

            Assert.True(s.RowCount == 3);

            c.Dispose();
            s.Dispose();
        }

        //[Test]
        //public void CancelledCursorThrowsOperationCancelledException()
        //{
        //    var sm = new Series<int, int>();

        //    sm.Add(1, 1);

        //    var cursor = sm.GetCursor();
        //    Assert.True(cursor.MoveNext());
        //    Assert.False(cursor.MoveNext());

        //    var cts = new CancellationTokenSource();

        //    var t = Task.Run(() =>
        //    {
        //        Thread.Sleep(100);
        //        var task = cursor.MoveNextAsync(cts.Token);
        //        Assert.True(task.IsCanceled);
        //        //task.Wait();
        //    });

        //    cts.Cancel();
        //    t.Wait();
        //}

        //[Test]
        //public void CouldCancelRangeCursor()
        //{
        //    var sm = new Series<int, int>();

        //    sm.Add(-1, -1);
        //    sm.Add(1, 1);
        //    sm.Add(2, 2);

        //    var range = sm.Range(0, 2, true, false);

        //    Assert.AreEqual(1, range.First.Present.Value);

        //    var cursor = range.GetCursor();

        //    var source = cursor.Source;
        //    Assert.AreEqual(1, source.Count());

        //    Assert.True(cursor.MoveNext());
        //    Assert.False(cursor.MoveNext());

        //    var cts = new CancellationTokenSource();

        //    var t = Task.Run(() =>
        //    {
        //        Thread.Sleep(100);
        //        var task = cursor.MoveNextAsync(cts.Token);
        //        Assert.True(task.IsCanceled);
        //    });

        //    cts.Cancel();
        //    t.Wait();
        //}

        //[Test]
        //public void RangeCursorStopsBeforeEndKey()
        //{
        //    var sm = new AppendSeries<int, int>();

        //    sm.Add(1, 1);
        //    sm.Add(2, 2);
        //    sm.Add(3, 3);
        //    var range = sm.Range(int.MinValue, 2, true, true);

        //    //Assert.AreEqual(1, range.First.Value);

        //    var cursor = range.GetAsyncEnumerator();

        //    var source = cursor.Source;

        //    var cts = new CancellationTokenSource();

        //    var t = Task.Run(async () =>
        //    {
        //        var moved = await cursor.MoveNextAsync();
        //        Assert.True(moved);
        //        moved = await cursor.MoveNextAsync();
        //        Assert.True(moved);
        //        moved = await cursor.MoveNextAsync();
        //        Assert.False(moved);
        //        Assert.AreEqual(2, cursor.CurrentKey);
        //        Assert.AreEqual(2, cursor.CurrentValue);
        //    });

        //    t.Wait();
        //}

        //[Test]
        //public void RangeCursorMovesAfterAwating()
        //{
        //    var sm = new Series<int, int>();

        //    sm.Add(1, 1);

        //    var range = sm.Range(int.MinValue, 2, true, true);

        //    //Assert.AreEqual(1, range.First.Value);

        //    var cursor = range.GetAsyncEnumerator();

        //    var t = Task.Run(async () =>
        //    {
        //        var moved = await cursor.MoveNextAsync();
        //        Assert.True(moved);
        //        moved = await cursor.MoveNextAsync();
        //        Assert.True(moved);
        //        moved = await cursor.MoveNextAsync();
        //        Assert.False(moved);
        //        Assert.AreEqual(2, cursor.CurrentKey);
        //        Assert.AreEqual(2, cursor.CurrentValue);
        //    });
        //    Thread.Sleep(100);
        //    sm.Add(2, 2);
        //    sm.Add(3, 3);


        //    // Not needed: sm.Complete(); we are working with range that ends at 2 and should have IsCompleted == true after moving to 2.

        //    t.Wait();

        //    cursor.Dispose();
        //}

        [Test]
        public async Task NotificationWorksWhenCursorIsNotWating()
        {
            var s = new AppendSeries<int, int>();

            s.Append(1, 1);
            s.Append(2, 2);

            var c = s.GetAsyncEnumerator();
            Assert.True(await c.MoveNextAsync());
            Assert.True(await c.MoveNextAsync());

            Assert.IsTrue(c.MovePrevious());

            s.Append(3, 3);

            await Task.Delay(250);

            Assert.AreEqual(1, c.CurrentKey);
            Assert.AreEqual(1, c.CurrentValue);
            c.Dispose();
            s.Dispose();
        }

        [Test]
        public async Task CouldCancelCursor()
        {
            var s = new AppendSeries<int, int>();

            var c = s.GetAsyncEnumerator();

            var t = Task.Run(async () =>
            {
                try
                {
                    await c.MoveNextAsync();
                    Assert.Fail();
                }
                catch (Exception ex)
                {
                    Assert.True(true);
                }
            });

            Thread.Sleep(100);

            c.TryComplete(true);

            t.Wait();

            c.Dispose();
            s.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public async Task CouldAsyncEnumerate()
        {
            var s = new AppendSeries<int, int>();
            var count = (int)TestUtils.GetBenchCount(10_000_000, 5);

            for (int i = 0; i < count; i++)
            {
                s.Append(i, i);
            }

            var t = Task.Run(async () =>
            {
                using (Benchmark.Run("SCM.AsyncEnumerator", count))
                {
                    var cnt = 0;
                    await foreach (var _ in s)
                    {
                        cnt++;
                    }

                    Assert.AreEqual(count * 2, cnt);
                }

                Benchmark.Dump();
            });

            for (int i = count; i < count * 2; i++)
            {
                s.Append(i, i);
            }

            s.MarkReadOnly();

            t.Wait();

            s.Dispose();
        }

        [Test]
        public async Task CouldEnumerateSMUsingCursor()
        {
            var s = new AppendSeries<int, int>();
            var count = (int)TestUtils.GetBenchCount();

            for (int i = 0; i < count; i++)
            {
                s.Append(i, i);
            }

#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
            var ae = s.GetAsyncEnumerator();
#pragma warning restore HAA0401 // Possible allocation of reference type enumerator

            var t = Task.Run(async () =>
            {
                using (Benchmark.Run("SCM.AsyncEnumerator", count))
                {
                    var cnt = 0;
                    while (await ae.MoveNextAsync())
                    {
                        cnt++;
                    }

                    await ae.DisposeAsync();
                    Assert.AreEqual(count * 2, cnt);
                }

                Benchmark.Dump();
            });

            for (int i = count; i < count * 2; i++)
            {
                s.Append(i, i);
            }

            s.MarkReadOnly();

            t.Wait();

            s.Dispose();
        }

        [Test]
        public async Task CouldEnumerateSCMInBatchMode()
        {
            var s = new AppendSeries<int, int>();
            var count = 1_000_000; // Settings.SCMDefaultChunkLength - 1;

            //for (int i = 0; i < count; i++)
            //{
            //    s.Append(i, i);
            //}

            Console.WriteLine("Added first half");

#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
            var ae = s.GetAsyncEnumerator();
#pragma warning restore HAA0401 // Possible allocation of reference type enumerator

            var t = Task.Run(async () =>
            {
                using (Benchmark.Run("SCM.AsyncEnumerator", count))
                {
                    //try
                    //{
                    var cnt = count;
                    while (await ae.MoveNextAsync())
                    {
                        if (cnt != ae.Current.Key)
                        {
                            ThrowHelper.ThrowInvalidOperationException($"cnt {cnt} != ae.Current.Key {ae.Current.Key}");
                        }

                        cnt++;
                    }

                    await ae.DisposeAsync();
                    //}
                    //catch (Exception ex)
                    //{
                    //    Console.WriteLine("EXCEPTION: " + ex.ToString());
                    //    Console.WriteLine("INNER: " + ex.InnerException?.ToString());
                    //    throw;
                    //}

                    // Assert.AreEqual(scm.Count, cnt);
                }

                Benchmark.Dump();
            });

            // Thread.Sleep(1000);

            for (int i = count; i < count * 2; i++)
            {
                s.Append(i, i);
                //Thread.SpinWait(50);
            }

            // Thread.Sleep(2000);

            s.MarkReadOnly();

            t.Wait();

            s.Dispose();
        }

        [Test]
        public async Task CouldEnumerateSCMUsingCursor()
        {
            var s = new AppendSeries<int, int>();
            var count = 1_000_000; // Settings.SCMDefaultChunkLength - 1;

            for (int i = 0; i < count; i++)
            {
                s.Append(i, i);
            }

            Console.WriteLine("Added first half");

#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
            var ae = s.GetAsyncEnumerator();
#pragma warning restore HAA0401 // Possible allocation of reference type enumerator

            var t = Task.Run(async () =>
            {
                using (Benchmark.Run("SCM.AsyncEnumerator", count))
                {
                    try
                    {
                        var cnt = 0;
                        while (await ae.MoveNextAsync())
                        {
                            if (cnt != ae.Current.Key)
                            {
                                ThrowHelper.ThrowInvalidOperationException();
                            }

                            cnt++;
                        }

                        await ae.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("EXCEPTION: " + ex.ToString());
                        throw;
                    }

                    // Assert.AreEqual(scm.Count, cnt);
                }

                Benchmark.Dump();
            });

            // Thread.Sleep(1000);

            for (int i = count; i < count * 2; i++)
            {
                s.Append(i, i);
                //Thread.SpinWait(50);
            }

            // Thread.Sleep(2000);

            s.MarkReadOnly();

            t.Wait();

            s.Dispose();
        }

        [Test, Ignore("manual")]
        public async Task CouldReadDataStreamWhileWritingFromManyThreads()
        {
            var map = new AppendSeries<int, int>();

            var writeCount = 0L;

            var count = 1000_000_000;
            var rounds = 1;

            var writeTask = Task.Run(async () =>
            {
                using (Benchmark.Run("Write", count * rounds, true))
                {
                    for (int j = 0; j < rounds; j++)
                    {
                        var t1 = Task.Run(() =>
                        {
                            try
                            {
                                for (int i = j * count; i < (j + 1) * count; i++)
                                {
                                    map.DangerousTryAppend(i, i);
                                    // Thread.SpinWait(10);
                                    // Thread.Yield();
                                    Interlocked.Increment(ref writeCount);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }
                        });
                        await t1;
                    }
                }
            });

            AsyncCursor<int, int, SCursor<int, int>> cursor = null;
            var cnt = 0L;
            var readTask = Task.Run(async () =>
            {
                for (int r = 0; r < 1; r++)
                {
                    using (cursor = new AsyncCursor<int, int, SCursor<int, int>>(map.GetCursor()))
                    {
                        using (Benchmark.Run("Read", count * rounds, true))
                        {
                            try
                            {
                                while (await cursor.MoveNextAsync())
                                {
                                    Interlocked.Increment(ref cnt);
                                    // Thread.Sleep(1);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }


                            // Left from coreclr 19161 tests, TODO remove when everything works OK
                            // here is a strong reference to cursor with side effects of printing to console
                            // Console.WriteLine("Last value: " + cursor.Current.Key);
                            // another strong reference after while loop, we dereference it's value and return from task
                            // lastKey1 = cursor.CurrentKey;
                        }
                    }
                }
            });


            var monitor = true;
            var monitorTask = Task.Run(async () =>
            {
                try
                {
                    var previousR = cursor?.CurrentKey;
                    var previousW = Volatile.Read(ref writeCount);
                    while (monitor)
                    {
                        await Task.Delay(1000);
                        var r = cursor.CurrentKey;
                        var w = Volatile.Read(ref writeCount);
                        Console.WriteLine($"R: {r:N0} - {((r - previousR) / 1000000.0):N2} Mops \t | W: {w:N0}- {((w - previousW) / 1000000.0):N2} Mops");

                        if (r == previousR)
                        {
                            Console.WriteLine($"IsAwaiting {cursor.IsTaskAwating} IsCompl {cursor.IsCompleted} Counter {cursor._counter}");
                        }

                        previousR = r;
                        previousW = w;
                        // cursor.TryComplete(false);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });

            await writeTask;
            Console.WriteLine("COMPLETE");
            map.MarkReadOnly();
            map.NotifyUpdate();
            Console.WriteLine("Read after map complete:" + Interlocked.Read(ref cnt));
            await readTask;
            Console.WriteLine("Read after finish:" + Interlocked.Read(ref cnt));
            // Console.WriteLine("Last key: " + lastKey);

            Benchmark.Dump();
            GC.KeepAlive(cursor);
            map.Dispose();
            monitor = false;
        }
    }
}