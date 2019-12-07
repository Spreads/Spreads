//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.


//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Threading.Tasks;
//using NUnit.Framework;
//using Spreads.Serialization;

//namespace Spreads.Collections.Tests {

//    [TestFixture]
//    public class SortedMapTests {

//        [SetUp]
//        public void Init() {
//        }

//        [Test]
//        public void CouldEnumerateGrowingSM() {
//            var count = 1000000;
//            var sw = new Stopwatch();
//            sw.Start();
//            var sm = new SortedMap<DateTime, double>();
//            //sm.IsSynchronized = true;
//            var c = sm.GetCursor();

//            for (int i = 0; i < count; i++) {
//                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
//                c.MoveNext();
//                Assert.AreEqual(i, c.CurrentValue);
//            }
//            sw.Stop();
//            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
//            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

//        }


//        [Test]
//        public void CouldEnumerateChangingSM() {
//            var count = 1000000;
//            var sw = new Stopwatch();
//            sw.Start();
//            var sm = new SortedMap<DateTime, double>();
//            //sm.IsSynchronized = true;
//            var c = sm.GetCursor();

//            for (int i = 0; i < count; i++) {
//                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
//                var version = sm.Version;
//                //if (i > 10) {
//                //    sm.Add(DateTime.UtcNow.Date.AddSeconds(i - 10), i - 10 + 1);
//                //    Assert.IsTrue(sm.Version > version);
//                //}
//                c.MoveNext();
//                Assert.AreEqual(i, c.CurrentValue);
//            }
//            sw.Stop();
//            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds - 50);
//            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));

//        }

//        [Test]
//        public void CouldMoveAtGE() {
//            var sm = new SortedMap<int, int>(50);
//            for (int i = 0; i < 100; i++) {
//                sm[i] = i;
//            }
//            Assert.IsTrue(sm.IsRegular);
//            Assert.AreEqual(99, sm.rkLast);
//            var cursor = sm.GetCursor();

//            cursor.MoveAt(-100, Lookup.GE);

//            Assert.AreEqual(0, cursor.CurrentKey);
//            Assert.AreEqual(0, cursor.CurrentValue);

//            var shouldBeFalse = cursor.MoveAt(-100, Lookup.LE);
//            Assert.IsFalse(shouldBeFalse);

//        }

//        [Test]
//        public void CouldMoveAtFromDict() {

//            var sm = new SortedMap<int, int>(new Dictionary<int, int>()
//                {
//                    { 1, 1},
//                    //{ 2, 2},
//                    { 3, 3},
//                    //{ 4, 4},
//                    { 5, 5}
//                });

//            Assert.IsTrue(sm.IsRegular);
//            Assert.AreEqual(5, sm.rkLast);
//            var cursor = sm.GetCursor();

//            cursor.MoveAt(-100, Lookup.GE);

//            Assert.AreEqual(1, cursor.CurrentKey);
//            Assert.AreEqual(1, cursor.CurrentValue);

//            var shouldBeFalse = cursor.MoveAt(-100, Lookup.LE);
//            Assert.IsFalse(shouldBeFalse);

//        }


//        [Test]
//        public void CouldMoveAtLE() {
//            var scm = new SortedMap<long, long>();
//            for (long i = int.MaxValue; i < int.MaxValue * 4L; i = i + int.MaxValue) {
//                scm[i] = i;
//            }

//            var cursor = scm.GetCursor();

//            var shouldBeFalse = cursor.MoveAt(0, Lookup.LE);
//            Assert.IsFalse(shouldBeFalse);

//        }

//        //[Test]
//        //public void CouldSerializeSMWithSingleElement() {
//        //    var sm = new SortedMap<long, long>();
//        //    sm.Add(1, 1);
//        //    //Assert.Fail("TODO");
//        //    //var sm2 = BinarySerializer.Deserialize<SortedMap<long, long>>(Serialization.Serializer.Serialize(sm));
//        //    //Assert.AreEqual(1, sm2.Count);
//        //    //Assert.AreEqual(1, sm2.First.Value);
//        //    //Assert.AreEqual(1, sm2.First.Key);

//        //}


//        [Test]
//        public void IsSyncedIsSetAutomaticallyForCursor() {
//            var sm = new SortedMap<long, long>();
//            sm.Add(1, 1);

//            Task.Run(() => {
//                var c = sm.GetCursor();
//                c.MoveNext();
//            }).Wait();
//            Assert.IsTrue(sm.IsSynchronized);
//        }

//        [Test]
//        public void IsSyncedIsSetAutomaticallyForEnumerator() {
//            var sm = new SortedMap<long, long>();
//            sm.Add(1, 1);

//            Task.Run(() => {
//                foreach (var l in sm) {
//                    Assert.AreEqual(1, l.Value);
//                }
//            }).Wait();
//            Assert.IsTrue(sm.IsSynchronized);
//        }


//        [Test]
//        public void AddExistingThrowsAndKeepsVersion() {
//            var sm = new SortedMap<long, long>();
//            sm.Add(1, 1);
//            Assert.AreEqual(1, sm.Version);
//            Assert.Throws<ArgumentException>(() => sm.Add(1, 1));
//            Assert.AreEqual(1, sm.Version);
//        }

//    }
//}
