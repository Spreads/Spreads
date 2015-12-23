using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Collections;

namespace Spreads.Collections.Tests.Contracts {

    [TestFixture]
    public class ReadmeTests {
        private readonly SortedMap<int, int> _upper = new SortedMap<int, int> { { 2, 2 }, { 4, 4 } };
        private readonly SortedMap<int, int> _lower = new SortedMap<int, int> { { 1, 10 }, { 3, 30 }, { 5, 50 } };

        [Test]
        public void ZipNFromLogoAndReadmeIsEmpty()
        {
            var sum = (_upper + _lower);
            Assert.AreEqual(0, sum.Count());
        }

        [Test]
        public void ZipNFromLogoAndReadmeRepeatWorks() {
            var sum = (_upper.Repeat() + _lower);
            Assert.AreEqual(2, sum.Count());
            Assert.AreEqual(32, sum[3]);
            Assert.AreEqual(54, sum[5]);
        }

        [Test]
        public void ZipNFromLogoAndReadmeRepeatFillWorks() {
            var sum = (_upper.Repeat() + _lower.Fill(42));

            // Item[] like any moves are defined only on observed keys
            Assert.AreEqual(4, sum.Count());
            Assert.AreEqual(44, sum[2]);
            Assert.AreEqual(32, sum[3]);
            Assert.AreEqual(46, sum[4]);
            Assert.AreEqual(54, sum[5]);

            // To get a value from continuous series at non-existing key, we should use TryGetValue method
            int v;
            Assert.IsTrue(sum.TryGetValue(6, out v));
            Assert.AreEqual(46, v);

            // Try find is series mirrow to cursor moves: we cannot move exactly to 6 here
            KeyValuePair<int, int> kvp;
            Assert.IsFalse(sum.TryFind(6, Lookup.EQ, out kvp));
            Assert.IsFalse(sum.TryFind(6, Lookup.GE, out kvp));

            // But we can move to the last key 5 if we try find a kvp at a key that is less or equal to 6
            Assert.IsTrue(sum.TryFind(6, Lookup.LE, out kvp));
            Assert.IsTrue(sum.TryFind(6, Lookup.LT, out kvp));
            Assert.AreEqual(5, kvp.Key);
            Assert.AreEqual(54, kvp.Value);

            // Or to the first key from another side
            Assert.IsFalse(sum.TryFind(0, Lookup.EQ, out kvp));
            Assert.IsFalse(sum.TryFind(1, Lookup.EQ, out kvp));
            Assert.IsFalse(sum.TryFind(0, Lookup.LE, out kvp));

            Assert.IsTrue(sum.TryFind(0, Lookup.GE, out kvp));
            Assert.AreEqual(2, kvp.Key);
            Assert.AreEqual(44, kvp.Value);

            Assert.IsTrue(sum.TryFind(1, Lookup.GE, out kvp));
            Assert.AreEqual(2, kvp.Key);
            Assert.AreEqual(44, kvp.Value);
        }


        [Test]
        public void ZipNFromLogoAndReadmeRepeatCouldMoveCursorCorrectly() {
            var upper = new SortedMap<int, int> { { 2, 2 }, { 4, 4 } };
            var lower = new SortedMap<int, int> { { 1, 10 }, { 3, 30 }, { 5, 50 } };
            var sum = (upper.Repeat() + lower);
            var cursor = sum.GetCursor();

            Assert.AreEqual(32, sum[3]);
            Assert.AreEqual(54, sum[5]);

            Assert.IsFalse(cursor.MoveAt(1, Lookup.EQ));
            Assert.IsTrue(cursor.MoveAt(1, Lookup.GE));
            Assert.AreEqual(3, cursor.CurrentKey);
            Assert.AreEqual(32, cursor.CurrentValue);

            // move forward

            Assert.IsTrue(cursor.MoveNext());
            Assert.AreEqual(5, cursor.CurrentKey);
            Assert.AreEqual(54, cursor.CurrentValue);

            // finished
            Assert.IsFalse(cursor.MoveNext());

            //// move back

            Assert.IsTrue(cursor.MovePrevious());
            Assert.AreEqual(3, cursor.CurrentKey);
            Assert.AreEqual(32, cursor.CurrentValue);

            // async moves
            Assert.IsTrue(cursor.MoveNext(CancellationToken.None).Result);
            Assert.AreEqual(5, cursor.CurrentKey);
            Assert.AreEqual(54, cursor.CurrentValue);

            var moved = false;
            var t = Task.Run(async () => {
                moved = await cursor.MoveNext(CancellationToken.None);
            });

            // add new value
            lower.Add(6, 60);
            t.Wait();
            Assert.IsTrue(moved);
            Assert.AreEqual(6, cursor.CurrentKey);
            Assert.AreEqual(4 + 60, cursor.CurrentValue);

            // when all sources are marked as immutable/complete, MNA must return false
            var t2 = Task.Run(async () => {
                moved = await cursor.MoveNext(CancellationToken.None);
            });
            upper.IsMutable = false;
            lower.IsMutable = false;
            t2.Wait();
            Assert.IsFalse(moved);

        }

        [Test]
        public void ZipNFromLogoAndReadmeRepeatFillCouldMoveCursorCorrectly()
        {
            var upper = new SortedMap<int, int> { { 2, 2 }, { 4, 4 } };
            var lower = new SortedMap<int, int> { { 1, 10 }, { 3, 30 }, { 5, 50 } };
            var sum = (upper.Repeat() + lower.Fill(42));
            var cursor = sum.GetCursor();

            Assert.IsFalse(cursor.MoveAt(1, Lookup.EQ));
            Assert.IsTrue(cursor.MoveAt(1, Lookup.GE));
            Assert.AreEqual(2, cursor.CurrentKey);
            Assert.AreEqual(44, cursor.CurrentValue);

            // move forward

            Assert.IsTrue(cursor.MoveNext());
            Assert.AreEqual(3, cursor.CurrentKey);
            Assert.AreEqual(32, cursor.CurrentValue);

            Assert.IsTrue(cursor.MoveNext());
            Assert.AreEqual(4, cursor.CurrentKey);
            Assert.AreEqual(46, cursor.CurrentValue);

            Assert.IsTrue(cursor.MoveNext());
            Assert.AreEqual(5, cursor.CurrentKey);
            Assert.AreEqual(54, cursor.CurrentValue);

            // finished
            Assert.IsFalse(cursor.MoveNext());

            //// move back

            Assert.IsTrue(cursor.MovePrevious());
            Assert.AreEqual(4, cursor.CurrentKey);
            Assert.AreEqual(46, cursor.CurrentValue);

            Assert.IsTrue(cursor.MovePrevious());
            Assert.AreEqual(3, cursor.CurrentKey);
            Assert.AreEqual(32, cursor.CurrentValue);

            Assert.IsTrue(cursor.MovePrevious());
            Assert.AreEqual(2, cursor.CurrentKey);
            Assert.AreEqual(44, cursor.CurrentValue);

            // async moves
            Assert.IsTrue(cursor.MoveNext(CancellationToken.None).Result);
            Assert.AreEqual(3, cursor.CurrentKey);
            Assert.AreEqual(32, cursor.CurrentValue);


            Assert.IsTrue(cursor.MoveNext(CancellationToken.None).Result);
            Assert.AreEqual(4, cursor.CurrentKey);
            Assert.AreEqual(46, cursor.CurrentValue);

            Assert.IsTrue(cursor.MoveNext(CancellationToken.None).Result);
            Assert.AreEqual(5, cursor.CurrentKey);
            Assert.AreEqual(54, cursor.CurrentValue);

            var moved = false;
            var t = Task.Run(async () =>
            {
                moved = await cursor.MoveNext(CancellationToken.None);
            });

            // add new value
            upper.Add(6, 6);
            t.Wait();
            Assert.IsTrue(moved);
            Assert.AreEqual(6, cursor.CurrentKey);
            Assert.AreEqual(6 + 42, cursor.CurrentValue);

            // when all sources are marked as immutable/complete, MNA must return false
            var t2 = Task.Run(async () => {
                moved = await cursor.MoveNext(CancellationToken.None);
            });
            upper.IsMutable = false;
            lower.IsMutable = false;
            t2.Wait();
            Assert.IsFalse(moved);

        }


        [Test]
        public void SmaDeviationTest()
        {
            var data = new SortedMap<DateTime, double>();
            for (int i = 1; i < 101; i++)
            {
                data.Add(DateTime.UtcNow.Date.AddMinutes(i), i);
            }

            var sma = data.SMA(20, true);
            var deviation = (data/sma - 1);
            var deviationSm = deviation;
            var smaDirection = deviation.Map(Math.Sign);
            Assert.AreEqual(100, smaDirection.Count());
            Assert.AreEqual(100, deviation.Count());
            
        }

    }
}
