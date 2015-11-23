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
            var total = 100;
            var scm = new SortedChunkedMap<int, int>(50);
            scm.IsSynchronized = true;
            scm.AddLast(1, 1);
            var addTask = Task.Run(() => {
                for (int i = 2; i < total + 2; i++) {
                    scm.Add(i, i);
                    //scm[i] = i;
                    Thread.Sleep(5);
                }
                scm.IsMutable = false; // this will trigger a false return of MoveNextAsync()
            });

            var reader = scm.ReadOnly();
            Console.WriteLine("Writer IsMutable: {0}", scm.IsMutable);
            Console.WriteLine("Reader IsMutable: {0}", reader.IsMutable);
            var cnt = 0;
            using (var c = reader.GetCursor()) {
                var couldMove = await c.MoveNext(CancellationToken.None);
                while (couldMove) {
                    Console.WriteLine("{0} - {1}", c.CurrentKey, c.CurrentValue);
                    cnt++;
                    couldMove = await c.MoveNext(CancellationToken.None);
                }
            }
            addTask.Wait();
            Assert.AreEqual(cnt, total + 1);
            Console.WriteLine("Writer IsMutable: {0}", scm.IsMutable);
            Console.WriteLine("Reader IsMutable: {0}", reader.IsMutable);
            //(scm as IPersistentOrderedMap<int, int>).Dispose();
        }


    }
}
