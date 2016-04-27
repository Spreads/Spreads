using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HdrHistogram;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Experimental.Collections.Generic;

namespace Spreads.Core.Tests {
    [TestFixture]
    public class DirectArrayTests {
        [Test]
        public void CouldCreateAndGrowDirectArray() {
            var da = new DirectArray<long>("../DirectArrayTests.CouldCreateAndGrowDirectArray", 100);
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
            var t = Task.Run(() => {
                var da2 = new DirectArray<long>("../DirectArrayTests.CouldGrowParallel", 100);
                //da.Grow(300);
                da2[43] = 43;
            });
            Thread.Sleep(1000);
            var da = new DirectArray<long>("../DirectArrayTests.CouldGrowParallel", 100);
            
            da[42] = 42;
            //da.Grow(200);
            t.Wait();
        }

    }
}
