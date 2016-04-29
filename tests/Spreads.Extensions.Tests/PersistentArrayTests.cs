using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Collections.Persistent;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class PersistentArrayTests {
        [Test]
        public void CouldCreateAndGrowDirectArray() {
            var da = new PersistentArray<long>("../PersistentArrayTests.CouldCreateAndGrowDirectArray", 100);
            da[42] = 24;
            da.Grow(200);
            Assert.AreEqual(24, da[42]);
            var count = 1000000;
            da.Grow(count);
            for (int rounds = 0; rounds < 10; rounds++) {
                var sw = new Stopwatch();
                sw.Start();
                for (int i = 0; i < count; i++) {
                    da[i] = i;
                }
                sw.Stop();
                Console.WriteLine($"Elapsed msec: {sw.ElapsedMilliseconds}");
            }
        }


        [Test]
        public void CouldGrowParallel() {
            var da2 = new PersistentArray<long>("../PersistentArrayTests.CouldGrowParallel", 10000);
            var t = Task.Run(() => {
                da2.Grow(3000);
                da2[43] = 43;
            });
            Thread.Sleep(1000);
            var da = new PersistentArray<long>("../PersistentArrayTests.CouldGrowParallel", 10000);
            da[42] = 42;
            da.Grow(300000);
            Assert.AreEqual(43, da2[43]);
            t.Wait();
            da2.Grow(3000000);
            Assert.AreEqual(42, da[42]);
        }

    }
}
