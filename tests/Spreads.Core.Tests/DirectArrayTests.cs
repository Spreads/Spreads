using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Experimental.Collections.Generic;

namespace Spreads.Core.Tests {
    [TestFixture]
    public class DirectArrayTests {
        [Test]
        public void CouldCreateAndGrowDirectArray() {
            var da = new DirectArray<long>("CouldCreateAndGrowDirectArray", 100);
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
        public void CouldCRUDDirectDict() {
            var dd = new DirectMap<long, long>("../CouldCRUDDirectDict");
            //var dd = new Dictionary<long, long>();
            dd.Clear();
            var count = 1000000;
            var sw = new Stopwatch();
            
            for (int rounds = 0; rounds < 20; rounds++) {
                sw.Restart();
                
                for (int i = 0; i < count; i++) {
                    dd[i] = i;
                }
                Assert.AreEqual(count, dd.Count);
                sw.Stop();
                Console.WriteLine($"Add elapsed msec: {sw.ElapsedMilliseconds}");
            }

            for (int rounds = 0; rounds < 10; rounds++) {
                sw.Restart();
                var cnt = 0;
                foreach (var kvp in dd) {
                    //Assert.AreEqual(kvp.Key, kvp.Value);
                    cnt++;
                }
                Assert.AreEqual(count, cnt);
                sw.Stop();
                Console.WriteLine($"Read elapsed msec: {sw.ElapsedMilliseconds}");
            }
        }
    }
}
