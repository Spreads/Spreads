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

            Assert.AreEqual(2, scm.outerMap.Count);
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
            var total = 1000;
            var scm = new SortedChunkedMap<int, int>(50);
            scm.IsSynchronized = true;
            scm.AddLast(1, 1);
            var addTask = Task.Run(() => {
                for (int i = 2; i < total + 2; i++) {
                    scm.Add(i, i);
                    //scm[i] = i;
                    Thread.Sleep(5);
                }
                scm.Complete(); // this will trigger a false return of MoveNextAsync()
            });

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
            Assert.AreEqual(cnt, total + 1);
            Console.WriteLine("Writer IsReadOnly: {0}", scm.IsReadOnly);
            Console.WriteLine("Reader IsReadOnly: {0}", reader.IsReadOnly);
            //(scm as IPersistentOrderedMap<int, int>).Dispose();
        }


    }



    // TODO (low) merge this with above
    [TestFixture]
    public class SCMTests {

        private const int _small = 500;
        private const int _big = 100000;
        private Random _rng = new System.Random();

        [SetUp]
        public void Init() {
        }

        public Dictionary<string, IReadOnlyOrderedMap<DateTime, double>> GetImplementation() {
            var implemetations = new Dictionary<string, IReadOnlyOrderedMap<DateTime, double>>();

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
        public void CouldRemoveFirst() {
            var scm = new SortedChunkedMap<int, int>();
            for (int i = 0; i < 10000; i++) {
                scm.Add(i, i);
            }
            KeyValuePair<int, int> result;
            var removed = scm.RemoveFirst(out result);
            Assert.IsTrue(removed);
            Assert.IsTrue(!scm.IsEmpty);
            Assert.IsTrue(scm.Count == 9999);
            Assert.IsTrue(scm.First.Key == 1);
        }


    }
}
