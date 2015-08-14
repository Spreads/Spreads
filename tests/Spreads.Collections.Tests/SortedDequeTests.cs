using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Threading.Tasks;
using System.Threading;

namespace Spreads.Collections.Tests {

    [TestFixture]
    public class SortedDequeTests {

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
        public void CouldAddBehindInitialCapacity() {
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




        [Test]
        public void AddRemoveTest() {
            var rng = new System.Random();
            for (int r = 0; r < 1000; r++) {
                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 50; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.Add(next);
                    }
                }

                for (int i = 0; i < 1000; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.Add(next);
                    }

                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());

                    var first = sd.RemoveFirst();
                    set.Remove(first);
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                }

            }
        }


        [Test]
        public void RemoveTest() {
            for (int r = 0; r < 10000; r++) {


                var rng = new System.Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.Add(next);
                    }
                }
                while (sd.Count > 0) {
                    var first = sd.RemoveFirst();
                    set.Remove(first);
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                }
            }
        }


        [Test]
        public void AddTest() {
            for (int r = 0; r < 10000; r++) {

                var rng = new System.Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++) {
                    for (var next = rng.Next(1000); !set.Contains(next);) {
                        set.Add(next);
                        sd.Add(next);
                    }
                    Assert.AreEqual(set.Count, sd.Count);
                    Assert.AreEqual(set.Sum(), sd.Sum());
                }
            }
        }

    }
}
