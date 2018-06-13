// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Collections;

namespace Spreads.Tests.Cursors
{
    [TestFixture]
    public class AsyncCursorTests
    {
        //[Test]
        //public void CouldCancelMNA()
        //{
        //    var sm = new SortedMap<int, int>();

        //    sm.Add(1, 1);

        //    var cursor = sm.GetCursor();
        //    Assert.True(cursor.MoveNext());
        //    Assert.False(cursor.MoveNext());

        //    var cts = new CancellationTokenSource();

        //    var t = Task.Run(() => cursor.MoveNextAsync(cts.Token));

        //    cts.Cancel();

        //    Thread.Sleep(100);

        //    Assert.True(t.IsCanceled);
        //}

        //[Test]
        //public void CouldCancelDoAfter()
        //{
        //    var sm = new SortedMap<int, int>();

        //    sm.Add(1, 1);

        //    var cursor = sm.GetCursor();
        //    Assert.True(cursor.MoveNext());
        //    Assert.False(cursor.MoveNext());

        //    var cts = new CancellationTokenSource();

        //    var t = sm.Range(int.MinValue, 2, true, true).Do((k, v) =>
        //    {
        //        Console.WriteLine($"{k} - {v}");
        //    }, cts.Token);

        //    cts.Cancel();

        //    Thread.Sleep(1000);

        //    Assert.True(t.IsCanceled);
        //}

        [Test]
        public void DeadCursorDoesntCauseEndlessLoopInNotifyUpdate()
        {
            var sm = new SortedMap<int, int>();

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
            var sm = new SortedMap<int, int>();

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
            var sm = new SortedMap<int, int>();

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
            var sm = new SortedMap<int, int>();

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
        public void CouldCancelMapCursor()
        {
            var sm = new SortedMap<int, int>();

            sm.Add(1, 1);

            var range = sm.Map(x => x + 1).Range(0, Int32.MaxValue, true, true);

            var cursor = range.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            var cts = new CancellationTokenSource();

            var t = Task.Run(() =>
            {
                Thread.Sleep(100);
                var task = cursor.MoveNextAsync();
                Assert.True(task.IsCanceled);
                //task.Wait();
            });

            cts.Cancel();
            t.Wait();
        }
    }
}