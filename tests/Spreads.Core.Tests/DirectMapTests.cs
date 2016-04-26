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
    public class DirectMapTests {

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
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
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


        [Test]
        public unsafe void CouldRecoverFromFailuresScenario1() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");
            
            var dm = new DirectMap<long, long>("../CouldRecoveFromFailures");
            dm.Clear();

            // scenario 1: fail during rewriting existing key

            for (int scenario = 11; scenario <= 13; scenario++)
            {
                Console.WriteLine($"Scenario {scenario}");

                dm[42] = 420;
                // next set to the key 42 will throw
                ChaosMonkey.Force = true;
                ChaosMonkey.Scenario = scenario;
                Assert.Throws<ChaosMonkeyException>(() => {
                    dm[42] = 430;
                });
                // Now we must have recoverFlags set to (1 << 1)
                Assert.AreEqual(1 << 1, dm.recoveryFlags);
                var snapshot = dm.entries[-1];
                Assert.AreEqual(42, snapshot.key);
                Assert.AreEqual(420, snapshot.value);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm.buckets.Slot0, "Lock must be held by current process");

                // next read should recover value
                Assert.AreEqual(420, dm[42]);
                Assert.AreEqual(0, dm.recoveryFlags);
            }


            // scenario 2: fail during rewriting existing key


            Assert.AreEqual(0, *(int *) dm.buckets.Slot0, "Lock must be released");
            Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
        }

        [Test]
        public unsafe void CouldRecoverFromFailuresScenario2()
        {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new DirectMap<long, long>("../CouldRecoveFromFailures");
            dm.Clear();

            // scenario 2: fail during adding new key when free list > 0

            for (int scenario = 21; scenario <= 26; scenario++)
            {
                Console.WriteLine($"Scenario {scenario}");
                dm[42] = 420;
                Assert.IsTrue(dm.Remove(42));
                Assert.IsTrue(dm.freeCount > 0);
                ChaosMonkey.Force = true;
                ChaosMonkey.Scenario = scenario;

                var fl = dm.freeList;
                var fc = dm.freeCount;

                Assert.Throws<ChaosMonkeyException>(() => {
                    dm[43] = 430;
                });

                // Now we must have recoverFlags set to (1 << 2)
                Assert.AreEqual(1 << 2, dm.recoveryFlags);

                Assert.AreEqual(fl, dm.freeListCopy);
                Assert.AreEqual(fc, dm.freeCountCopy);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm.buckets.Slot0, "Lock must be held by current process");
                long thisWasNotStored = 0;
                Assert.Throws<KeyNotFoundException>(() =>
                {
                    // Item.Get should recover and undo set
                    thisWasNotStored = dm[43];
                });
                Assert.AreEqual(0, thisWasNotStored);
                Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
                Assert.AreEqual(0, *(int*)dm.buckets.Slot0, "Lock must be released");
            }

            
        }
    }
}
