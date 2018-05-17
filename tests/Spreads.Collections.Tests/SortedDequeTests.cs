// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System;

namespace Spreads.Collections.Tests {


    [TestFixture]
    public class SortedDequeTests {

        private bool DequeIsSorted<T>(SortedDeque<T> deque) {
            bool isFirst = true;
            T prev = default(T);
            foreach (var e in deque) {
                if (isFirst) {
                    isFirst = false;
                    prev = e;
                } else {
                    if (deque._comparer.Compare(e, prev) < 0) return false;
                    prev = e;
                }
            }
            return true;
        }

        [Test]
        public void CouldRemoveFirstAddInTheMiddle() {
            var sd = new SortedDeque<int>();
            sd.Add(1);
            sd.Add(3);
            sd.Add(5);
            sd.Add(7);

            Assert.AreEqual(sd.First, 1);
            Assert.AreEqual(sd.Last, 7);

            var fst = sd.RemoveFirst();
            Assert.AreEqual(sd.First, 3);
            sd.Add(4);
            Assert.AreEqual(1, sd.ToList().IndexOf(4));
            Assert.AreEqual(2, sd.ToList().IndexOf(5));
            Assert.AreEqual(3, sd.ToList().IndexOf(7));

            var last = sd.RemoveLast();
            sd.Add(8);
            Assert.AreEqual(1, sd.ToList().IndexOf(4));
            Assert.AreEqual(2, sd.ToList().IndexOf(5));
            Assert.AreEqual(3, sd.ToList().IndexOf(8));

            sd.Add(6);
            Assert.AreEqual(1, sd.ToList().IndexOf(4));
            Assert.AreEqual(2, sd.ToList().IndexOf(5));
            Assert.AreEqual(3, sd.ToList().IndexOf(6));
            Assert.AreEqual(4, sd.ToList().IndexOf(8));
        }

        [Test]
        public void CouldAddBeyondInitialCapacity() {
            var sd = new SortedDeque<int>();
            for (int i = 0; i < 4; i++) {
                sd.Add(i);
            }

            for (int i = 0; i < 4; i++) {
                Assert.AreEqual(i, sd.ToList().IndexOf(i));
            }
        }


        [Test]
        public void CouldAddRemoveWithFixedSize() {
            var sd = new SortedDeque<int>();
            for (int i = 0; i < 4; i++) {
                sd.Add(i);
            }

            for (int i = 4; i < 100; i++) {
                sd.RemoveFirst();
                sd.Add(i);
                Assert.AreEqual(i - 3, sd.First);
                Assert.AreEqual(i, sd.Last);
            }
        }

        [Test]
        public void CouldAddRemoveIncreasing() {
            var sd = new SortedDeque<int>();
            for (int i = 0; i < 4; i++) {
                sd.Add(i);
            }

            for (int i = 4; i < 100; i++) {
                if (i % 2 == 0) sd.RemoveFirst();
                sd.Add(i);
                //Assert.AreEqual(i - 3, sd.First);
                Assert.AreEqual(i, sd.Last);
            }
        }




        [Test, Ignore("long running")]
        public void AddRemoveTest() {
            var rng = new System.Random();
            for (int r = 0; r < 1000; r++) {
                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 50; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }

                for (int i = 0; i < 1000; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.TryAdd(next);
                    }

                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());

                    var first = sd.RemoveFirst();
                    set.Remove(first);
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                    Assert.IsTrue(DequeIsSorted(sd));
                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd) {
                        if (c == 0) {
                            c++;
                            prev = e;
                        } else {
                            Assert.IsTrue(e > prev);
                            prev = e;
                        }
                    }

                }

            }
        }


        [Test]
        public void AddRemoveTestWithRemoveElement() {
            var rng = new System.Random();
            for (int r = 0; r < 1000; r++) {
                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 50; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }

                for (int i = 0; i < 1000; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.TryAdd(next);
                    }

                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());

                    var first = sd.First;
                    sd.Remove(first);
                    set.Remove(first);
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                    Assert.IsTrue(DequeIsSorted(sd));
                }

            }
        }

        [Test, Ignore("long running")]
        public void RemoveTest() {
            for (int r = 0; r < 10000; r++) {


                var rng = new System.Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }
                while (sd.Count > 0) {
                    var first = sd.RemoveFirst();
                    set.Remove(first);
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                    Assert.IsTrue(DequeIsSorted(sd));
                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd) {
                        if (c == 0) {
                            c++;
                            prev = e;
                        } else {
                            Assert.IsTrue(e > prev);
                            prev = e;
                        }
                    }
                }
            }
        }

        [Test]
        public void CouldRemoveInTheMiddle() {
            for (int r = 0; r < 1000; r++) {

                var rng = new System.Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }
                while (sd.Count > 0) {
                    var midElement = sd._buffer[((sd._firstOffset + sd._count / 2) % sd._buffer.Length)];
                    sd.Remove(midElement);
                    set.Remove(midElement);
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                    Assert.IsTrue(DequeIsSorted(sd));
                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd) {
                        if (c == 0) {
                            c++;
                            prev = e;
                        } else {
                            Assert.IsTrue(e > prev);
                            prev = e;
                        }
                    }
                }
            }
        }


        [Test]
        public void CouldRemoveInTheMiddleSplit() {

            var rng = new System.Random();

            var sd = new SortedDeque<int>();
            var set = new HashSet<int>();
            for (int i = 0; i < 100; i++) {
                if (i % 2 == 0) {
                    set.Add(i);
                    sd.TryAdd(i);
                } else {
                    set.Add(-i);
                    sd.TryAdd(-i);
                }
            }
            {
                var midElement = sd._buffer[0]; //[.___    ____]
                sd.Remove(midElement);
                set.Remove(midElement);
                Assert.AreEqual(set.Count, sd.Count);
                Assert.AreEqual(set.Sum(), sd.Sum());
                Assert.IsTrue(DequeIsSorted(sd));
                var c = 0;
                var prev = 0;
                foreach (var e in sd) {
                    if (c == 0) {
                        c++;
                        prev = e;
                    } else {
                        Assert.IsTrue(e > prev);
                        prev = e;
                    }
                }
            }
            {
                var midElement = sd._buffer[sd._buffer.Length - 1]; //[___    ____.]
                sd.Remove(midElement);
                set.Remove(midElement);
                Assert.AreEqual(set.Count, sd.Count);
                Assert.AreEqual(set.Sum(), sd.Sum());
                Assert.IsTrue(DequeIsSorted(sd));
                var c = 0;
                var prev = 0;
                foreach (var e in sd) {
                    if (c == 0) {
                        c++;
                        prev = e;
                    } else {
                        Assert.IsTrue(e > prev);
                        prev = e;
                    }
                }
            }
        }

        [Test, Ignore("long running")]
        public void AddTest() {
            for (int r = 0; r < 10000; r++) {

                var rng = new System.Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                    Assert.IsTrue(DequeIsSorted(sd));
                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd) {
                        if (c == 0) {
                            c++;
                            prev = e;
                        } else {
                            Assert.IsTrue(e > prev);
                            prev = e;
                        }
                    }
                }
            }
        }


        [Test]
        public void AddSequentialTest() {
            var sd = new SortedDeque<int>();
            for (var r = 0; r < 1000; r++) {
                sd.TryAdd(r);
            }
        }


        [Test]
        public void AddReverseSequentialTest() {
            var sd = new SortedDeque<int>();
            for (var r = 1000; r >= 0; r--) {
                sd.TryAdd(r);
            }
        }

        [Test]
        public void AddDatesTest() {
            var sd = new SortedDeque<DateTime>();
            DateTime dt;

            dt = DateTime.Parse("10 / 2 / 2015 10:29:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 2 / 2015 2:06:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 1 / 2015 11:30:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 2 / 2015 10:30:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 1 / 2015 11:31:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 2 / 2015 2:07:00 PM");
            sd.Add(dt);
            Assert.AreEqual(6, sd.Count);
        }

        [Test,Ignore("long running")]
        public void CouldCompareDatesManyTimes() {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var now = DateTime.UtcNow;
            var later = DateTime.UtcNow.AddSeconds(1.0);
            for (int i = 0; i < 100000000; i++) {
                if (now > later) {
                    throw new ApplicationException("no way");
                }
                //later = later.AddTicks(1);
            }

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        [Test]
        public void CouldAddKVsWithSimilarKey() {
            var sd = new SortedDeque<int, int>(2, new KVPComparer<int, int>(KeyComparer<int>.Default, KeyComparer<int>.Default));
            for (int i = 0; i < 3; i++) {
                sd.Add(new KeyValuePair<int, int>(1, i));
            }
            var expected = 1 * 3;
            int[] values = new int[3];
            foreach (var item in sd)
            {
                values[item.Value] = item.Key;
            }
            var actual = values.Sum();
            Console.WriteLine(actual);
            Assert.AreEqual(expected, actual);
        }


    }
}
