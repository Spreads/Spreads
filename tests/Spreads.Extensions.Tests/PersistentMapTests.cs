using System;
using System.Collections.Generic;
using System.Diagnostics;
using HdrHistogram;
using NUnit.Framework;
using Spreads.Collections.Direct;
using Spreads.Collections.Persistent;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class PersistentMapTests {

        [Test]
        public void CouldCRUDDirectDict() {

            var count =  500 * 1000000;
            var dd = new PersistentMapFixedLength<long, long>("../CouldCRUDDirectDict", count);
            //var dd = new Dictionary<DateTime, long>();

            var sw = new Stopwatch();
            dd.Clear();

            var histogram = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 10000, 3);
            for (int rounds = 0; rounds < 6; rounds++) {
                //dd.Clear();
                sw.Restart();
                var dtInit = DateTime.Today;
                for (int i = 0; i < count; i++) {
                    var startTick = sw.ElapsedTicks;
                    //dd[dtInit.AddTicks(i)] = i;
                    dd[i] = i;
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    if (rounds >= 1) histogram.RecordValue(nanos);
                }
                Assert.AreEqual(count, dd.Count);
                sw.Stop();

                Console.WriteLine($"Add elapsed msec: {sw.ElapsedMilliseconds}");
            }
            histogram.OutputPercentileDistribution(
                    printStream: Console.Out,
                    percentileTicksPerHalfDistance: 3,
                    outputValueUnitScalingRatio: OutputScalingFactor.None);
            var histogram2 = new LongHistogram(TimeSpan.TicksPerMillisecond * 100 * 10000, 3);
            for (int rounds = 0; rounds < 6; rounds++) {
                sw.Restart();
                var sum = 0L;
                for (int i = 0; i < count; i++)
                {
                    var startTick = sw.ElapsedTicks;
                    sum += dd[i];
                    var ticks = sw.ElapsedTicks - startTick;
                    var nanos = (long)(1000000000.0 * (double)ticks / Stopwatch.Frequency);
                    if (rounds >= 1) histogram2.RecordValue(nanos);
                }
                sw.Stop();
                Console.WriteLine($"Read elapsed msec: {sw.ElapsedMilliseconds} for sum {sum}");
            }
            histogram2.OutputPercentileDistribution(
                    printStream: Console.Out,
                    percentileTicksPerHalfDistance: 3,
                    outputValueUnitScalingRatio: OutputScalingFactor.None);
        }


        [Test]
        public unsafe void CouldRecoverFromFailuresScenario1() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoveFromFailures");
            dm.Clear();

            // scenario 1: fail during rewriting existing key

            for (int scenario = 11; scenario <= 13; scenario++) {
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
                //var snapshot = dm.entries[-1];
                //Assert.AreEqual(42, snapshot.key);
                //Assert.AreEqual(420, snapshot.value);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");

                // next read should recover value
                Assert.AreEqual(420, dm[42]);
                Assert.AreEqual(0, dm.recoveryFlags);
            }



            Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");
            Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
        }

        [Test]
        public unsafe void CouldRecoverFromFailuresScenario2() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoveFromFailures");
            dm.Clear();

            // scenario 2: fail during adding new key when free list > 0

            for (int scenario = 21; scenario <= 26; scenario++) {
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

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");
                long thisWasNotStored = 0;
                Assert.Throws<KeyNotFoundException>(() => {
                    // Item.Get should recover and undo set
                    thisWasNotStored = dm[43];
                });
                Assert.AreEqual(0, thisWasNotStored);
                Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
                Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");
            }


        }


        [Test]
        public unsafe void CouldRecoverFromFailuresScenario3() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoverFromFailuresScenario3");
            dm.Clear();
            dm[dm.Count + 1] = dm.Count + 1;
            // scenario 3: fail during adding new key when free list = 0

            for (int scenario = 31; scenario <= 35; scenario++) {
                Console.WriteLine($"Scenario {scenario}");
                Assert.IsTrue(dm.freeCount == 0);
                ChaosMonkey.Force = true;
                ChaosMonkey.Scenario = scenario;

                var initCount = dm.count;

                Assert.Throws<ChaosMonkeyException>(() => {
                    dm[initCount + 1] = initCount + 1;
                });

                // Now we must have recoverFlags set to (1 << 3)
                Assert.AreEqual(1 << 3, dm.recoveryFlags);

                Assert.AreEqual(initCount, dm.countCopy);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");

                long thisWasNotStored = 0;
                Assert.Throws<KeyNotFoundException>(() => {
                    // Item.Get should recover and undo set
                    thisWasNotStored = dm[initCount + 1];
                });

                Assert.AreEqual(initCount, dm.Count);
                Assert.AreEqual(0, thisWasNotStored);
                Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
                Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");
            }

        }


        [Test]
        public unsafe void CouldRecoverFromFailuresScenario4via2() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoveFromFailures");
            dm.Clear();

            // scenario 4 via 2: fail during adding new key when free list > 0

            for (int scenario = 41; scenario <= 43; scenario++) {
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
                Assert.AreEqual(1 << 2 | 1 << 4, dm.recoveryFlags);

                Assert.AreEqual(fl, dm.freeListCopy);
                Assert.AreEqual(fc, dm.freeCountCopy);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");
                long thisWasNotStored = 0;
                Assert.Throws<KeyNotFoundException>(() => {
                    // Item.Get should recover and undo set
                    thisWasNotStored = dm[43];
                });
                Assert.AreEqual(0, thisWasNotStored);
                Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
                Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");
            }

        }


        [Test]
        public unsafe void CouldRecoverFromFailuresScenario44() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoveFromFailures");
            dm.Clear();

            // scenario 44: write everything but fail to exit lock

            Console.WriteLine($"Scenario {44}");
            dm[42] = 420;
            Assert.IsTrue(dm.Remove(42));
            Assert.IsTrue(dm.freeCount > 0);
            ChaosMonkey.Force = true;
            ChaosMonkey.Scenario = 44;

            var fc = dm.freeCount;

            Assert.Throws<ChaosMonkeyException>(() => {
                dm[43] = 430;
            });

            Assert.AreEqual(0, dm.recoveryFlags);

            Assert.AreEqual(dm.freeCount, fc - 1);

            Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");
            long thisWasStored = 0;

            thisWasStored = dm[43];
            Assert.AreEqual(430, thisWasStored);
            Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
            Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");


        }

        [Test]
        public unsafe void CouldRecoverFromFailuresScenario4via3() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoverFromFailuresScenario3");
            dm.Clear();
            dm[dm.Count + 1] = dm.Count + 1;
            // scenario 4: fail during adding new key when free list = 0

            for (int scenario = 41; scenario <= 43; scenario++) {
                Console.WriteLine($"Scenario {scenario}");
                Assert.IsTrue(dm.freeCount == 0);
                ChaosMonkey.Force = true;
                ChaosMonkey.Scenario = scenario;

                var initCount = dm.count;

                Assert.Throws<ChaosMonkeyException>(() => {
                    dm[initCount + 1] = initCount + 1;
                });

                // Now we must have recoverFlags set to (1 << 3)
                Assert.AreEqual(1 << 3 | 1 << 4, dm.recoveryFlags);

                Assert.AreEqual(initCount, dm.countCopy);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");

                long thisWasNotStored = 0;
                Assert.Throws<KeyNotFoundException>(() => {
                    // Item.Get should recover and undo set
                    thisWasNotStored = dm[initCount + 1];
                });

                Assert.AreEqual(initCount, dm.Count);
                Assert.AreEqual(0, thisWasNotStored);
                Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
                Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");
            }

        }


        [Test]
        public unsafe void CouldRecoverFromFailuresScenario5() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoverFromFailuresScenario5");
            dm.Clear();
            dm[1] = 1;
            // scenario 3: fail during adding new key when free list = 0

            for (int scenario = 51; scenario <= 52; scenario++) {
                Console.WriteLine($"Scenario {scenario}");

                ChaosMonkey.Force = true;
                ChaosMonkey.Scenario = scenario;

                var initCount = dm.count;

                Assert.Throws<ChaosMonkeyException>(() => {
                    dm.Remove(1);
                });

                // Now we must have recoverFlags set to (1 << 3)
                Assert.AreEqual(1 << 5, dm.recoveryFlags);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");

                // recover
                Assert.AreEqual(1, dm[1]);


                Assert.AreEqual(initCount, dm.Count);
                Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
                Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");
            }

        }


        [Test]
        public unsafe void CouldRecoverFromFailuresScenario7via5() {
            if (!ChaosMonkey.Enabled) Assert.Inconclusive("Chaos monkey must be enabled for recovery tests.");

            var dm = new PersistentMapFixedLength<long, long>("../CouldRecoverFromFailuresScenario5");
            dm.Clear();
            dm[1] = 1;
            // scenario 3: fail during adding new key when free list = 0

            for (int scenario = 71; scenario <= 75; scenario++) {
                Console.WriteLine($"Scenario {scenario}");

                ChaosMonkey.Force = true;
                ChaosMonkey.Scenario = scenario;

                var initCount = dm.count;

                Assert.Throws<ChaosMonkeyException>(() => {
                    dm.Remove(1);
                });

                // Now we must have recoverFlags set to (1 << 3)
                Assert.AreEqual(1 << 5 | 1 << 7, dm.recoveryFlags);

                Assert.AreEqual(Process.GetCurrentProcess().Id, *(int*)dm._buckets._buffer._data, "Lock must be held by current process");

                // recover
                Assert.AreEqual(1, dm[1]);


                Assert.AreEqual(initCount, dm.Count);
                Assert.AreEqual(0, dm.recoveryFlags, "Must recover from all scenarios");
                Assert.AreEqual(0, *(int*)dm._buckets._buffer._data, "Lock must be released");
            }

        }
    }
}
