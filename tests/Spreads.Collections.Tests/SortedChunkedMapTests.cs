// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Collections.Tests {

    [TestFixture]
    public class SortedChunkedMapTests {

        [SetUp]
        public void Init() {
        }

        [Test]
        public void CouldRemoveFirst() {
            var scm = new SortedChunkedMap<int, int>(50);
            for (int i = 0; i < 100; i++) {
                scm.Add(i, i);
            }

            scm.Remove(50);
            Assert.AreEqual(50, scm.outerMap.Last.Key);

            KeyValuePair<int, int> kvp;
            scm.RemoveFirst(out kvp);
            Assert.AreEqual(0, kvp.Value);
            Assert.AreEqual(1, scm.First.Value);
            Assert.AreEqual(0, scm.outerMap.First.Key);
        }

        [Test]
        public void CouldSetInsteadOfAddWithCorrectChunks() {
            var scm = new SortedChunkedMap<int, int>(50);
            for (int i = 0; i < 100; i++) {
                scm[i] = i;
            }

            Assert.AreEqual(2, scm.outerMap.Count());
        }

        [Test]
        public void CouldMoveAtGE() {
            var scm = new SortedChunkedMap<int, int>(50);
            for (int i = 0; i < 100; i++) {
                scm[i] = i;
            }

            var cursor = scm.GetCursor();

            cursor.MoveAt(-100, Lookup.GE);

            Assert.AreEqual(0, cursor.CurrentKey);
            Assert.AreEqual(0, cursor.CurrentValue);
            var shouldBeFalse = cursor.MoveAt(-100, Lookup.LE);
            Assert.IsFalse(shouldBeFalse);
        }

        [Test]
        public async void CouldReadReadOnlyChildWhileAddingToParent() {
            // TODO if we change the first element to -1 and add from 0, some weird assertion related to regular keys fails
            var total = 150;
            var scm = new SortedChunkedMap<int, int>(50);
            scm.IsSynchronized = true;
            scm.AddLast(1, 1);
            var cc = 1;
            var addTask = Task.Run(() => {
                for (int i = 2; i < total + 2; i++) {
                    cc++;
                    if (cc == 50) {
                        Console.WriteLine("Next bucket");
                    }
                    scm.Add(i, i);
                    //scm[i] = i;
                    Thread.Sleep(5);
                }
                scm.Complete(); // this will trigger a false return of MoveNextAsync()
            });


            //Thread.Sleep(5000000);

            var reader = scm.ReadOnly();
            Console.WriteLine("Writer IsReadOnly: {0}", scm.IsReadOnly);
            Console.WriteLine("Reader IsReadOnly: {0}", reader.IsReadOnly);
            var cnt = 0;
            using (var c = reader.GetCursor()) {
                var couldMove = await c.MoveNext(CancellationToken.None);
                while (couldMove) {
                    if (cnt % 100 == 0) Console.WriteLine("{0} - {1}", c.CurrentKey, c.CurrentValue);
                    cnt++;
                    couldMove = await c.MoveNext(CancellationToken.None);
                }
            }
            addTask.Wait();
            Thread.Sleep(200);
            Assert.AreEqual(cnt, total + 1);
            Console.WriteLine("Writer IsReadOnly: {0}", scm.IsReadOnly);
            Console.WriteLine("Reader IsReadOnly: {0}", reader.IsReadOnly);
            //(scm as IPersistentSeries<int, int>).Dispose();
        }

        private const int _small = 500;
        private const int _big = 100000;
        private Random _rng = new System.Random();

        public Dictionary<string, IReadOnlySeries<DateTime, double>> GetImplementation() {
            var implemetations = new Dictionary<string, IReadOnlySeries<DateTime, double>>();

            var scm_irregular_small = new SortedChunkedMap<DateTime, double>();
            var scm_irregular_big = new SortedChunkedMap<DateTime, double>();
            var scm_regular_small = new SortedChunkedMap<DateTime, double>();
            var scm_regular_big = new SortedChunkedMap<DateTime, double>();

            scm_irregular_small.Add(DateTime.Today.AddDays(-2), -2.0);
            scm_irregular_big.Add(DateTime.Today.AddDays(-2), -2.0);

            for (int i = 0; i < _big; i++) {
                if (i < _small) {
                    scm_irregular_small.Add(DateTime.Today.AddDays(i), i);
                    scm_regular_small.Add(DateTime.Today.AddDays(i), i);
                }

                scm_irregular_big.Add(DateTime.Today.AddDays(i), i);
                scm_regular_big.Add(DateTime.Today.AddDays(i), i);
            }

            implemetations.Add("scm_irregular_small", scm_irregular_small);
            implemetations.Add("scm_regular_small", scm_regular_small);
            implemetations.Add("scm_irregular_big", scm_irregular_big);
            implemetations.Add("scm_regular_big", scm_regular_big);
            return implemetations;
        }

        [Test]
        public void ContentEqualsToExpected() {
            var maps = GetImplementation();
            Assert.AreEqual(maps["scm_irregular_small"][DateTime.Today.AddDays(-2)], -2.0);
            Assert.AreEqual(maps["scm_irregular_big"][DateTime.Today.AddDays(-2)], -2.0);

            for (int i = 0; i < _big; i++) {
                if (i < _small) {
                    Assert.AreEqual(maps["scm_irregular_small"][DateTime.Today.AddDays(i)], i);
                    Assert.AreEqual(maps["scm_regular_small"][DateTime.Today.AddDays(i)], i);
                }
                Assert.AreEqual(maps["scm_irregular_big"][DateTime.Today.AddDays(i)], i);
                Assert.AreEqual(maps["scm_regular_big"][DateTime.Today.AddDays(i)], i);
            }
            Assert.IsTrue(GetImplementation().Count > 0);
        }

        [Test]
        public void CouldCreateSCM() {
            var scm_irregular_small = new SortedChunkedMap<DateTime, double>();

            scm_irregular_small.Add(DateTime.Today.AddDays(-2), -2.0);

            for (int i = 0; i < _small; i++) {
                scm_irregular_small.Add(DateTime.Today.AddDays(i), i);
            }
        }

        [Test]
        public void CouldAddMoreThanChunkSize() {
            var scm = new SortedChunkedMap<int, int>(1024);
            var cnt = 0;
            for (int i = 0; i < 10000; i = i + 2) {
                scm.Add(i, i);
                cnt++;
            }

            for (int i = 1; i < 10000; i = i + 2) {
                scm.Add(i, i);
                cnt++;
            }

            Assert.AreEqual(cnt, scm.Count);
            Assert.IsTrue(scm.outerMap.First.Value.Count > 1024);

            for (int i = 0; i < 10000; i++) {
                Assert.AreEqual(i, scm[i]);
            }
        }

        [Test]
        public void CouldCompareDates() {
            var dtc = KeyComparer.GetDefault<DateTime>();
            var neg = dtc.Compare(DateTime.Today.AddDays(-2), DateTime.Today);
            var pos = dtc.Compare(DateTime.Today.AddDays(2), DateTime.Today.AddDays(-2));

            Console.WriteLine(neg);
            Assert.IsTrue(neg < 0);

            Console.WriteLine(pos);
            Assert.IsTrue(pos > 0);
        }

        [Test]
        public void CouldRemoveMany() {
            var scm = new SortedChunkedMap<int, int>();
            scm.Add(1, 1);
            var removed = scm.RemoveMany(0, Lookup.GE);
            Assert.IsTrue(removed);
            Assert.IsTrue(scm.IsEmpty);
            Assert.IsTrue(scm.Count == 0);
        }

        [Test]
        public void CouldRemoveFirst2() {
            var scm = new SortedChunkedMap<int, int>();
            for (int i = 0; i < 10000; i++) {
                scm.Add(i, i);
            }
            Assert.AreEqual(10000, scm.Count, "Count is not equal");
            KeyValuePair<int, int> result;
            var removed = scm.RemoveFirst(out result);
            Assert.IsTrue(removed);
            Assert.IsTrue(!scm.IsEmpty);
            Assert.IsTrue(scm.Count == 9999);
            Assert.IsTrue(scm.First.Key == 1);
        }

        [Test]
        public void CouldMoveAtOnEmpty() {
            var scm = new SortedChunkedMap<int, int>();
            var c = scm.GetCursor();
            Assert.IsFalse(c.MoveAt(1, Lookup.GT));
            Assert.IsFalse(c.MoveAt(1, Lookup.GE));
            Assert.IsFalse(c.MoveAt(1, Lookup.EQ));
            Assert.IsFalse(c.MoveAt(1, Lookup.LE));
            Assert.IsFalse(c.MoveAt(1, Lookup.LT));
        }

        [Test]
        public void CouldMoveAtOnNonEmpty() {
            var scm = new SortedChunkedMap<int, int>();
            scm.Add(1, 1);
            var c = scm.GetCursor();
            Assert.IsFalse(c.MoveAt(1, Lookup.GT));
            Assert.IsTrue(c.MoveAt(1, Lookup.GE));
            Assert.IsTrue(c.MoveAt(1, Lookup.EQ));
            Assert.IsTrue(c.MoveAt(1, Lookup.LE));
            Assert.IsFalse(c.MoveAt(1, Lookup.LT));
        }

        /// <summary>
        /// MoveAt inside SCM was failing
        /// </summary>
        [Test]
        public void DemoAndStratIssue() {
            var series = new SortedChunkedMap<DateTime, double>[2];
            var scm1 = new SortedChunkedMap<DateTime, double>();
            var scm2 = new SortedChunkedMap<DateTime, double>();
            var today = DateTime.UtcNow.Date;
            for (int i = 0; i < 10000; i = i + 2) {
                scm1.Add(today.AddMilliseconds(i), i);
            }

            for (int i = 1; i < 10000; i = i + 2) {
                scm2.Add(today.AddMilliseconds(i), i);
            }

            series[0] = scm1;
            series[1] = scm2;

            // Zip on repeated used to throw
            var sm = series.Select(x => x.Repeat()).ToArray().Zip((k, vArr) => {
                if (Math.Abs(vArr.Sum(x => Math.Sign(x))) == vArr.Length) {
                    return vArr.Average();
                } else {
                    return 0.0;
                }
            }).ToSortedMap();
            Console.WriteLine(sm.Count);
        }

        [Test]
        public void AddExistingThrowsAndKeepsVersion() {
            var sm = new SortedChunkedMap<long, long>();
            sm.Add(1, 1);
            Assert.AreEqual(1, sm.Version);
            Assert.Throws<ArgumentException>(() => sm.Add(1, 1));
            Assert.AreEqual(1, sm.Version);
        }



        [Test]
        public void RemoveFirstLastIncrementsVersion() {
            var map = new SortedChunkedMap<long, long>();
            map.Add(1, 1);
            Assert.AreEqual(1, map.Version);
            map.Add(2, 2);
            Assert.AreEqual(2, map.Version);
            KeyValuePair<long, long> tmp;
            map.RemoveFirst(out tmp);
            Assert.AreEqual(3, map.Version);
            map.RemoveLast(out tmp);
            Assert.AreEqual(4, map.Version);
        }

        [Test]
        public void CouldRemoeAllFromBothEndsOnEmpty() {
            var map = new SortedChunkedMap<long, long>();
            map.Add(1, 1);
            Assert.True(map.RemoveMany(map.First.Key, Lookup.GE));
            Assert.AreEqual(0, map.Count);
            Assert.True(map.IsEmpty);
            map.Add(1, 1);
            Assert.True(map.RemoveMany(map.Last.Key, Lookup.LE));
            Assert.AreEqual(0, map.Count);
            Assert.True(map.IsEmpty);
        }

        private static Random rng = new Random();

        public static void Shuffle<T>(IList<T> list) {
            int n = list.Count;
            while (n > 1) {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        [Test]
        public void CouldAddRandomlyRemoveValues() {
            var map = new SortedChunkedMap<int, int>(50);
            var list = new List<int>();
            var count = 100000;
            for (int i = 0; i < count; i++) {
                list.Add(i);
            }
            for (int i = 0; i < count; i++) {
                map.Add(list[i], list[i]);
                Assert.AreEqual(i + 1, map.Version);
            }

            Shuffle(list);

            for (int i = 0; i < count; i++) {
                Assert.True(map.Remove(list[i]));
                Assert.AreEqual(i + count + 1, map.Version);
            }
            Assert.True(map.IsEmpty);
        }

        [Test]
        public void CouldRandomlyAddRemoveValues() {
            var map = new SortedChunkedMap<int, int>(50);
            var list = new List<int>();
            var count = 100000;
            for (int i = 0; i < count; i++) {
                list.Add(i);
            }
            Shuffle(list);
            for (int i = 0; i < count; i++) {
                map.Add(list[i], list[i]);
                Assert.AreEqual(i + 1, map.Version);
            }

            Shuffle(list);

            for (int i = 0; i < count; i++) {
                Assert.True(map.Remove(list[i]));
                Assert.AreEqual(i + count + 1, map.Version);
            }
            Assert.True(map.IsEmpty);
        }


        [Test]
        public void CouldRandomlyAddRemoveFirst() {
            var map = new SortedChunkedMap<int, int>(50);
            var list = new List<int>();
            var count = 10000;
            for (int i = 0; i < count; i++) {
                list.Add(i);
            }
            Shuffle(list);
            for (int i = 0; i < count; i++) {
                map.Add(list[i], list[i]);
                Assert.AreEqual(i + 1, map.Version);
            }

            for (int i = 0; i < count; i++) {
                KeyValuePair<int, int> tmp;
                Assert.True(map.RemoveFirst(out tmp));
                Assert.AreEqual(i + count + 1, map.Version);
            }
            Assert.True(map.IsEmpty);
        }

        [Test]
        public void CouldRandomlyAddRemoveLast() {
            var map = new SortedChunkedMap<int, int>(50);
            var list = new List<int>();
            var count = 10000;
            for (int i = 0; i < count; i++) {
                list.Add(i);
            }
            Shuffle(list);
            for (int i = 0; i < count; i++) {
                map.Add(list[i], list[i]);
                Assert.AreEqual(i + 1, map.Version);
            }

            for (int i = 0; i < count; i++) {
                KeyValuePair<int, int> tmp;
                Assert.True(map.RemoveLast(out tmp));
                Assert.AreEqual(i + count + 1, map.Version);
            }
            Assert.True(map.IsEmpty);
        }

        [Test]
        public void OuterMapCachesInnerMaps(SortedChunkedMap<int, int> scm)
        {
            scm.RemoveMany(int.MinValue, Lookup.GE);
            for (int i = 0; i < 10000; i++)
            {
                scm.Add(i,i);
            }
            var outer = scm.outerMap;
            var oc = outer.GetCursor();

            var first = outer.First;
            scm.Add(10001, 10001);
            Assert.True(oc.MoveFirst());
            Assert.ReferenceEquals(first.Value, oc.CurrentValue);


            var last = outer.Last;
            Assert.True(oc.MoveLast());
            Assert.ReferenceEquals(last.Value, oc.CurrentValue);

            // TODO other cases

            scm.RemoveMany(int.MinValue, Lookup.GE);
        }
    }
}
