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
            //use the second Core/Processor for the test
            //Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);
            //prevent "Normal" Processes from interrupting Threads
            //Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            //prevent "Normal" Threads from interrupting this thread
            //Thread.CurrentThread.Priority = ThreadPriority.Highest;

            var dd = new DirectMap<long, long>("../CouldCRUDDirectDict");
            //var dd = new Dictionary<long, long>();
            
            var count = 1000000;
            var sw = new Stopwatch();

            var histogram = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 1000, 3);
            for (int rounds = 0; rounds < 5; rounds++) {
                dd.Clear();
                sw.Restart();

                for (int i = 0; i < count; i++) {
                    var startTick = sw.ElapsedTicks;
                    dd[i] = i;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long) (1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    if (rounds > 1) histogram.RecordValue(nanos);
                }
                Assert.AreEqual(count, dd.Count);
                sw.Stop();

                Console.WriteLine($"Add elapsed msec: {sw.ElapsedMilliseconds}");
            }
            histogram.OutputPercentileDistribution(
                    printStream: Console.Out,
                    percentileTicksPerHalfDistance: 3,
                    outputValueUnitScalingRatio: OutputScalingFactor.None);
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
