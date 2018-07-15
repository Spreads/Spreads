// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using NUnit.Framework;
using Spreads.Collections;
using Spreads.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class AsyncCursorTests
    {
        [Test]
        public void DeadCursorDoesntCauseEndlessLoopInNotifyUpdate()
        {
            var sm = new SortedChunkedMap<int, int>();

            sm.Add(1, 1);

            var cursor = sm.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            cursor.MoveNextAsync();

            cursor = null;

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.Collect(2, GCCollectionMode.Forced, true);

            //var t = Task.Run(() => cursor.MoveNextAsync(cts.Token));

            sm.Add(2, 2);
            sm.Add(3, 3);

            Assert.True(sm.Count == 3);
        }

        [Test]
        public void CancelledCursorDoesntCauseEndlessLoopInNotifyUpdate()
        {
            var sm = new SortedChunkedMap<int, int>();

            sm.Add(1, 1);

            var cursor = sm.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            cursor.MoveNextAsync();

            sm.Add(2, 2);
            sm.Add(3, 3);

            Assert.True(sm.Count == 3);
        }

        //[Test]
        //public void CancelledCursorThrowsOperationCancelledException()
        //{
        //    var sm = new SortedMap<int, int>();

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
        //    var sm = new SortedMap<int, int>();

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

        [Test]
        public void RangeCursorStopsBeforeEndKey()
        {
            var sm = new SortedChunkedMap<int, int>();

            sm.Add(1, 1);
            sm.Add(2, 2);
            sm.Add(3, 3);
            var range = sm.Range(int.MinValue, 2, true, true);

            //Assert.AreEqual(1, range.First.Value);

            var cursor = range.GetCursor();

            var source = cursor.Source;

            var cts = new CancellationTokenSource();

            var t = Task.Run(async () =>
            {
                var moved = await cursor.MoveNextAsync();
                Assert.True(moved);
                moved = await cursor.MoveNextAsync();
                Assert.True(moved);
                moved = await cursor.MoveNextAsync();
                Assert.False(moved);
                Assert.AreEqual(2, cursor.CurrentKey);
                Assert.AreEqual(2, cursor.CurrentValue);
            });

            t.Wait();
        }

        [Test]
        public void RangeCursorMovesAfterAwating()
        {
            var sm = new SortedChunkedMap<int, int>();

            sm.Add(1, 1);

            var range = sm.Range(int.MinValue, 2, true, true);

            //Assert.AreEqual(1, range.First.Value);

            var cursor = range.GetCursor();

            var source = cursor.Source;

            var cts = new CancellationTokenSource();

            var t = Task.Run(async () =>
            {
                var moved = await cursor.MoveNextAsync();
                Assert.True(moved);
                moved = await cursor.MoveNextAsync();
                Assert.True(moved);
                moved = await cursor.MoveNextAsync();
                Assert.False(moved);
                Assert.AreEqual(2, cursor.CurrentKey);
                Assert.AreEqual(2, cursor.CurrentValue);
            });
            Thread.Sleep(100);
            sm.Add(2, 2);
            sm.Add(3, 3);
            t.Wait();
        }

        [Test]
        public async Task NotificationWorksWhenCursorIsNotWating()
        {
            var sm = new SortedChunkedMap<int, int>();

            sm.Add(1, 1);
            sm.Add(2, 2);
            sm.Flush();

            var cursor = sm.GetCursor();
            Assert.True(await cursor.MoveNextAsync());
            Assert.True(await cursor.MoveNextAsync());

            Assert.IsTrue(cursor.MovePrevious());

            await sm.TryAdd(3, 3);

            await Task.Delay(250);

            Assert.AreEqual(1, cursor.CurrentKey);
            Assert.AreEqual(1, cursor.CurrentValue);
        }

        [Test]
        public async Task CouldCancelCursor()
        {
            var sm = new SortedChunkedMap<int, int>();

            var cursor = sm.GetCursor();

            var completable = cursor as IAsyncCompletable;

            var t = Task.Run(async () =>
            {
                try
                {
                    await cursor.MoveNextAsync();
                    Assert.Fail();
                }
                catch (Exception ex)
                {
                    Assert.True(true);
                }
            });

            Thread.Sleep(100);

            completable?.TryComplete(true, true);

            t.Wait();
        }

        [Test]
        public async Task CouldEnumerateSMInBatchMode()
        {
            var map = new SortedMap<int, int>();
            var count = 10_000_000;

            for (int i = 0; i < count; i++)
            {
                await map.TryAdd(i, i);
            }

#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
            var ae = map.GetAsyncEnumerator();
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
                await map.TryAdd(i, i);
            }
            await map.Complete();

            t.Wait();
        }

        [Test]
        public async Task CouldEnumerateSCMInBatchMode()
        {
            // Settings.SCMDefaultChunkLength = 5;
            var scm = new SortedChunkedMap<int, int>();
            var count = Settings.SCMDefaultChunkLength - 1;

            for (int i = 0; i < count; i++)
            {
                await scm.TryAdd(i, i);
            }

#pragma warning disable HAA0401 // Possible allocation of reference type enumerator
            var ae = scm.GetCursor();
#pragma warning restore HAA0401 // Possible allocation of reference type enumerator

            var t = Task.Run(async () =>
            {
                using (Benchmark.Run("SCM.AsyncEnumerator", count))
                {
                    var cnt = 0;
                    while (await ae.MoveNextAsync())
                    {
                        Assert.AreEqual(cnt, ae.Current.Key);
                        cnt++;
                    }

                    await ae.DisposeAsync();
                    // Assert.AreEqual(scm.Count, cnt);
                }

                Benchmark.Dump();
            });

            Thread.Sleep(1000);

            for (int i = count; i < count + 10; i++)
            {
                await scm.TryAdd(i, i);
                Thread.SpinWait(1000);
            }

            Thread.Sleep(2000);

            await scm.Complete();

            t.Wait();
        }
    }
}