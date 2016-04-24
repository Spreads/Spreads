using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Collections;

namespace Spreads.Core.Tests {
    [TestFixture]
    public class DirectArrayTests {
        [Test]
        public void CouldCreateAndGrowDirectArray() {
            var da = new DirectArray<long>("CouldCreateAndGrowDirectArray", 100);
            da[42] = 24;
            da.Grow(200);
            Assert.AreEqual(24, da[42]);
            var count = 10000000;
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


    }
}
