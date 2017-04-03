// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading;
using NUnit.Framework;
using Spreads.Collections;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class AsyncCursorTests
    {
        [Test]
        public void CouldCancelMNA()
        {
            var sm = new SortedMap<int, int>();

            sm.Add(1, 1);

            var cursor = sm.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            var cts = new CancellationTokenSource();

            var t = Task.Run(() => cursor.MoveNext(cts.Token));

            cts.Cancel();

            Thread.Sleep(100);

            Assert.True(t.IsCanceled);
        }

        [Test]
        public void CouldCancelDoAfter()
        {
            var sm = new SortedMap<int, int>();

            sm.Add(1, 1);

            var cursor = sm.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            var cts = new CancellationTokenSource();

            var t = sm.After(0, Lookup.GT).Do((k, v) =>
            {
                Console.WriteLine($"{k} - {v}");
            }, cts.Token);

            cts.Cancel();

            Thread.Sleep(1000);

            Assert.True(t.IsCanceled);
        }


        [Test]
        public void DeadCursorDoesntCauseEndlessLoopInNotifyUpdate()
        {
            var sm = new SortedMap<int, int>();

            sm.Add(1, 1);

            var cursor = sm.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            var cts = new CancellationTokenSource();

            cursor.MoveNext(cts.Token);

            cursor = null;

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.Collect(2, GCCollectionMode.Forced, true);

            //var t = Task.Run(() => cursor.MoveNext(cts.Token));

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

            var cts = new CancellationTokenSource();

            cursor.MoveNext(cts.Token);

            cts.Cancel();


            sm.Add(2, 2);
            sm.Add(3, 3);


            Assert.True(sm.Count == 3);
        }


        [Test]
        public void CancelledCursorThrowsOperationCancelledException()
        {
            var sm = new SortedMap<int, int>();

            sm.Add(1, 1);

            var cursor = sm.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            var cts = new CancellationTokenSource();

            var t = Task.Run(() =>
            {
                Thread.Sleep(100);
                var task = cursor.MoveNext(cts.Token);
                Assert.True(task.IsCanceled);
                //task.Wait();
            });

            cts.Cancel();
            t.Wait();
        }

        [Test]
        public void CouldCancelRangeCursor()
        {
            var sm = new SortedMap<int, int>();

            sm.Add(1, 1);

            var range = sm.After2(0, Lookup.GE);

            Assert.AreEqual(1, range.First.Value);

            var cursor = range.GetCursor();

            var source = cursor.Source;
            Console.WriteLine(source.Count());

            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            var cts = new CancellationTokenSource();

            var t = Task.Run(() =>
            {
                Thread.Sleep(100);
                var task = cursor.MoveNext(cts.Token);
                Assert.True(task.IsCanceled);
            });

            cts.Cancel();
            t.Wait();
        }

        [Test]
        public void CouldCancelMapCursor()
        {
            var sm = new SortedMap<int, int>();

            sm.Add(1, 1);

            var range = sm.Map(x => x + 1).After2(0, Lookup.GE);

            var cursor = range.GetCursor();
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());

            var cts = new CancellationTokenSource();

            var t = Task.Run(() =>
            {
                Thread.Sleep(100);
                var task = cursor.MoveNext(cts.Token);
                Assert.True(task.IsCanceled);
                //task.Wait();
            });

            cts.Cancel();
            t.Wait();
        }
    }
}