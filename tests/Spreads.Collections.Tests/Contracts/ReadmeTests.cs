//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using NUnit.Framework;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Spreads.Collections.Tests.Contracts
//{
//    [TestFixture]
//    public class ReadmeTests
//    {
//        private readonly SortedMap<int, int> _upper = new SortedMap<int, int> { { 2, 2 }, { 4, 4 } };
//        private readonly SortedMap<int, int> _lower = new SortedMap<int, int> { { 1, 10 }, { 3, 30 }, { 5, 50 } };

//        [Test]
//        public void ZipNFromLogoAndReadmeIsEmpty()
//        {
//            var sum = (_upper + _lower);
//            Assert.AreEqual(0, sum.Count());
//        }

//        [Test]
//        public void ZipNFromLogoAndReadmeRepeatWorks()
//        {
//            var sum = (_upper.Repeat() + _lower);
//            Assert.AreEqual(2, sum.Count());
//            Assert.AreEqual(32, sum[3]);
//            Assert.AreEqual(54, sum[5]);
//        }

//        [Test]
//        public void ZipNFromLogoAndReadmeRepeatFillWorks()
//        {
//            var sum = (_upper.Repeat() + _lower.Fill(42));

//            // Item[] like any moves are defined only on observed keys
//            Assert.AreEqual(4, sum.Count());
//            Assert.AreEqual(44, sum[2]);
//            Assert.AreEqual(32, sum[3]);
//            Assert.AreEqual(46, sum[4]);
//            Assert.AreEqual(54, sum[5]);

//            // To get a value from continuous series at non-existing key, we should use TryGetValue method
//            Assert.IsTrue(sum.TryFindAt(6, Lookup.EQ, out var v));
//            Assert.AreEqual(46, v.Value);

//            // Try find is series mirrow to cursor moves: we cannot move exactly to 6 here
//            KeyValuePair<int, int> kvp;
//            Assert.IsFalse(sum.TryFindAt(6, Lookup.EQ, out kvp));
//            Assert.IsFalse(sum.TryFindAt(6, Lookup.GE, out kvp));

//            // But we can move to the last key 5 if we try find a kvp at a key that is less or equal to 6
//            Assert.IsTrue(sum.TryFindAt(6, Lookup.LE, out kvp));
//            Assert.IsTrue(sum.TryFindAt(6, Lookup.LT, out kvp));
//            Assert.AreEqual(5, kvp.Key);
//            Assert.AreEqual(54, kvp.Value);

//            // Or to the first key from another side
//            Assert.IsFalse(sum.TryFindAt(0, Lookup.EQ, out kvp));
//            Assert.IsFalse(sum.TryFindAt(1, Lookup.EQ, out kvp));
//            Assert.IsFalse(sum.TryFindAt(0, Lookup.LE, out kvp));

//            Assert.IsTrue(sum.TryFindAt(0, Lookup.GE, out kvp));
//            Assert.AreEqual(2, kvp.Key);
//            Assert.AreEqual(44, kvp.Value);

//            Assert.IsTrue(sum.TryFindAt(1, Lookup.GE, out kvp));
//            Assert.AreEqual(2, kvp.Key);
//            Assert.AreEqual(44, kvp.Value);
//        }

//        [Test]
//        public void ZipNFromLogoAndReadmeRepeatCouldMoveCursorCorrectly()
//        {
//            var upper = new SortedMap<int, int> { { 2, 2 }, { 4, 4 } };
//            var lower = new SortedMap<int, int> { { 1, 10 }, { 3, 30 }, { 5, 50 } };
//            var sum = (upper.Repeat() + lower);
//            var cursor = sum.GetCursor();

//            Assert.AreEqual(32, sum[3]);
//            Assert.AreEqual(54, sum[5]);

//            Assert.IsFalse(cursor.MoveAt(1, Lookup.EQ));
//            Assert.IsTrue(cursor.MoveAt(1, Lookup.GE));
//            Assert.AreEqual(3, cursor.CurrentKey);
//            Assert.AreEqual(32, cursor.CurrentValue);

//            // move forward

//            Assert.IsTrue(cursor.MoveNext());
//            Assert.AreEqual(5, cursor.CurrentKey);
//            Assert.AreEqual(54, cursor.CurrentValue);

//            // finished
//            Assert.IsFalse(cursor.MoveNext());

//            //// move back

//            Assert.IsTrue(cursor.MovePrevious());
//            Assert.AreEqual(3, cursor.CurrentKey);
//            Assert.AreEqual(32, cursor.CurrentValue);

//            // async moves
//            Assert.IsTrue(cursor.MoveNextAsync(CancellationToken.None).Result);
//            Assert.AreEqual(5, cursor.CurrentKey);
//            Assert.AreEqual(54, cursor.CurrentValue);

//            var moved = false;
//            var t = Task.Run(async () =>
//            {
//                moved = await cursor.MoveNextAsync(CancellationToken.None);
//            });

//            // add new value
//            lower.Add(6, 60);
//            t.Wait();
//            Assert.IsTrue(moved);
//            Assert.AreEqual(6, cursor.CurrentKey);
//            Assert.AreEqual(4 + 60, cursor.CurrentValue);

//            // when all sources are marked as immutable/complete, MNA must return false
//            var t2 = Task.Run(async () =>
//            {
//                moved = await cursor.MoveNextAsync(CancellationToken.None);
//            });
//            upper.Complete();
//            lower.Complete();
//            t2.Wait();
//            Assert.IsFalse(moved);
//        }

//        [Test]
//        public void ZipNFromLogoAndReadmeRepeatFillCouldMoveCursorCorrectly()
//        {
//            var upper = new SortedMap<int, int> { { 2, 2 }, { 4, 4 } };
//            var lower = new SortedMap<int, int> { { 1, 10 }, { 3, 30 }, { 5, 50 } };
//            var sum = (upper.Repeat() + lower.Fill(42));
//            var cursor = sum.GetCursor();

//            Assert.IsFalse(cursor.MoveAt(1, Lookup.EQ));
//            Assert.IsTrue(cursor.MoveAt(1, Lookup.GE));
//            Assert.AreEqual(2, cursor.CurrentKey);
//            Assert.AreEqual(44, cursor.CurrentValue);

//            // move forward

//            Assert.IsTrue(cursor.MoveNext());
//            Assert.AreEqual(3, cursor.CurrentKey);
//            Assert.AreEqual(32, cursor.CurrentValue);

//            Assert.IsTrue(cursor.MoveNext());
//            Assert.AreEqual(4, cursor.CurrentKey);
//            Assert.AreEqual(46, cursor.CurrentValue);

//            Assert.IsTrue(cursor.MoveNext());
//            Assert.AreEqual(5, cursor.CurrentKey);
//            Assert.AreEqual(54, cursor.CurrentValue);

//            // finished
//            Assert.IsFalse(cursor.MoveNext());

//            //// move back

//            Assert.IsTrue(cursor.MovePrevious());
//            Assert.AreEqual(4, cursor.CurrentKey);
//            Assert.AreEqual(46, cursor.CurrentValue);

//            Assert.IsTrue(cursor.MovePrevious());
//            Assert.AreEqual(3, cursor.CurrentKey);
//            Assert.AreEqual(32, cursor.CurrentValue);

//            Assert.IsTrue(cursor.MovePrevious());
//            Assert.AreEqual(2, cursor.CurrentKey);
//            Assert.AreEqual(44, cursor.CurrentValue);

//            // async moves
//            Assert.IsTrue(cursor.MoveNextAsync(CancellationToken.None).Result);
//            Assert.AreEqual(3, cursor.CurrentKey);
//            Assert.AreEqual(32, cursor.CurrentValue);

//            Assert.IsTrue(cursor.MoveNextAsync(CancellationToken.None).Result);
//            Assert.AreEqual(4, cursor.CurrentKey);
//            Assert.AreEqual(46, cursor.CurrentValue);

//            Assert.IsTrue(cursor.MoveNextAsync(CancellationToken.None).Result);
//            Assert.AreEqual(5, cursor.CurrentKey);
//            Assert.AreEqual(54, cursor.CurrentValue);

//            var moved = false;
//            var t = Task.Run(async () =>
//            {
//                moved = await cursor.MoveNextAsync(CancellationToken.None);
//            });

//            // add new value
//            upper.Add(6, 6);
//            t.Wait();
//            Assert.IsTrue(moved);
//            Assert.AreEqual(6, cursor.CurrentKey);
//            Assert.AreEqual(6 + 42, cursor.CurrentValue);

//            // when all sources are marked as immutable/complete, MNA must return false
//            var t2 = Task.Run(async () =>
//            {
//                moved = await cursor.MoveNextAsync(CancellationToken.None);
//            });
//            upper.Complete();
//            lower.Complete();
//            t2.Wait();
//            Assert.IsFalse(moved);
//        }

//        [Test]
//        public void SmaDeviationTest()
//        {
//            var count = 8193;
//            var data = new SortedMap<int, double>();
//            for (int i = 0; i < count; i++)
//            {
//                data.Add(i, i);
//            }

//            var dc = data.GetCursor();
//            dc.MoveFirst();
//            var dc2 = dc.Clone();
//            Assert.IsFalse(dc.MovePrevious());
//            Assert.IsFalse(dc2.MovePrevious());
//            //Assert.AreEqual(8192, dc.CurrentKey);

//            var sma = data.SMA(2, true);
//            var sma2 = data.Window(2, true).Map(w => w.Values.Average());
//            var ii = 0;
//            foreach (var kvp in sma2)
//            {
//                //Assert.AreEqual(kvp.Value, ii);
//                ii++;
//            }
//            Assert.AreEqual(count, ii);
//            //var smaSm = sma.ToSortedMap();
//            //Assert.AreEqual(count, smaSm.Count());

//            //var deviation = (data/sma - 1);
//            //var deviationSm = deviation;
//            //var smaDirection = deviation.Map(Math.Sign);
//            //Assert.AreEqual(count, smaDirection.Count());
//            //Assert.AreEqual(count, deviation.Count());
//        }
//    }
//}
