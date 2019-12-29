// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Threading;
using Spreads.Utils;

// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class AtomicCounterTests
    {
        [Test]
        // ReSharper disable once InconsistentNaming
        public void CouldCreateACPoolAndAcquireRelease()
        {
            var pool = new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(4));

            var originalSpan = pool._pinnedSpan.Span.ToArray();

            Assert.IsTrue(pool.TryAcquireCounter(out var ac));

            Assert.AreEqual(1, ac.Increment());
            Assert.AreEqual(0, ac.Decrement());
            Assert.AreEqual(1, ac.Increment());

            Assert.Throws<InvalidOperationException>(() => ac.Dispose());

            Assert.AreEqual(0, ac.Decrement());

            Assert.Throws<InvalidOperationException>(() => pool.ReleaseCounter(ac));

            ac.Dispose();

            Assert.Throws<ObjectDisposedException>(() => ac.Increment());

            pool.ReleaseCounter(ac);

            var finalSpan = pool._pinnedSpan.Span.ToArray();

            Assert.IsTrue(originalSpan.SequenceEqual(finalSpan));

            pool.Dispose();
        }

        [Test]
        public void CannotAcquireSmallCapacity()
        {
            var pool = new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(4));

            var originalSpan = pool._pinnedSpan.Span.ToArray();

            Assert.IsTrue(pool.TryAcquireCounter(out var ac1));
            Assert.IsTrue(pool.TryAcquireCounter(out var ac2));
            Assert.IsFalse(pool.TryAcquireCounter(out var ac3));

            ac1.Dispose();
            ac2.Dispose();

            Assert.IsFalse(ac3.IsValid);

            pool.ReleaseCounter(ac2);
            pool.ReleaseCounter(ac1);

            var finalSpan = pool._pinnedSpan.Span.ToArray();

            Assert.IsTrue(originalSpan.SequenceEqual(finalSpan));

            // now release in different order
            Assert.IsTrue(pool.TryAcquireCounter(out ac1));
            Assert.IsTrue(pool.TryAcquireCounter(out ac2));
            ac1.Dispose();
            ac2.Dispose();
            pool.ReleaseCounter(ac1);
            pool.ReleaseCounter(ac2);

            var finalSpan2 = pool._pinnedSpan.Span.ToArray();
            Assert.IsFalse(originalSpan.SequenceEqual(finalSpan2));

            pool.Dispose();
        }

        [Test]
        public void CouldRecoverCorruptedFreeList()
        {
            var pool = new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(4));

            var originalSpan = pool._pinnedSpan.Span.ToArray();

            Assert.IsTrue(pool.TryAcquireCounter(out var ac1));
            Assert.IsTrue(pool.TryAcquireCounter(out var ac2));

            ac1.Dispose();
            ac2.Dispose();

            pool.ReleaseCounter(ac2);
            pool.ReleaseCounter(ac1);

            var finalSpan = pool._pinnedSpan.Span.ToArray();

            Assert.IsTrue(originalSpan.SequenceEqual(finalSpan));

            // now release in different order
            Assert.IsTrue(pool.TryAcquireCounter(out ac1));
            Assert.IsTrue(pool.TryAcquireCounter(out ac2));
            ac1.Dispose();
            ac2.Dispose();
            pool.ReleaseCounter(ac1);
            pool.ReleaseCounter(ac2);

            var finalSpan2 = pool._pinnedSpan.Span.ToArray();
            Assert.IsFalse(originalSpan.SequenceEqual(finalSpan2));

            pool.Dispose();
        }

        [Test, Explicit("long running")]
        public void AcquireIncrementDecrementDisposeReleaseBenchmark()
        {
            // this is relevant for non-pooled object (new when pool is small then dispose if pool is full)

            var pool = new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(4));

            var count = 50_000_000;

            using (Benchmark.Run("FullCycle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    if (!pool.TryAcquireCounter(out var ac))
                    {
                        Assert.Fail();
                    }

                    ac.Increment();
                    ac.Decrement();
                    ac.Dispose();
                    pool.ReleaseCounter(ac);
                }
            }

            pool.Dispose();
        }

        private int _field;

        #if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        #endif
        [Test, Explicit("long running")]
        public void IncrementDecrementBenchmark()
        {
            // this is relevant for non-pooled object (new when pool is small then dispose if pool is full)

            var pool = new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(4));

            var count = 5_000_000;

            if (!pool.TryAcquireCounter(out var ac))
            {
                Assert.Fail();
            }

            using (Benchmark.Run("Increment", count))
            {
                for (int i = 0; i < count; i++)
                {
                    ac.Increment();
                }
            }

            using (Benchmark.Run("Decrement", count))
            {
                for (int i = 0; i < count; i++)
                {
                    ac.Decrement();
                }
            }

            using (Benchmark.Run("Increment field", count))
            {
                for (int i = 0; i < count; i++)
                {
                    Interlocked.Increment(ref _field);
                }
            }
            using (Benchmark.Run("Decrement field", count))
            {
                for (int i = 0; i < count; i++)
                {
                    Interlocked.Decrement(ref _field);
                }
            }

            ac.Dispose();
            pool.ReleaseCounter(ac);
            pool.Dispose();
        }

        [Test, Explicit("long running")]
        public void IncrementDecrementBenchmarkManyTimes()
        {
            var rounds = 20;

            for (int r = 0; r < rounds; r++)
            {
                IncrementDecrementBenchmark();
            }
        }

        ////////////////////////////// SERVICE ///////////////////////////////////////

        [Test, Explicit("with static side effects")]
        // ReSharper disable once InconsistentNaming
        public void ServiceCouldCreateACPoolAndAcquireRelease()
        {
            Settings.AtomicCounterPoolBucketSize = 4;
            var ac = AtomicCounterService.AcquireCounter();
            var ac1 = AtomicCounterService.AcquireCounter();

            Assert.IsTrue(ac.IsValid);

            Assert.AreEqual(1, ac.Increment());
            Assert.AreEqual(0, ac.Decrement());
            Assert.AreEqual(1, ac.Increment());

            Assert.Throws<InvalidOperationException>(() => ac.Dispose());

            Assert.AreEqual(0, ac.Decrement());

            Assert.Throws<InvalidOperationException>(() => AtomicCounterService.ReleaseCounter(ac));

            ac.Dispose();
            ac1.Dispose();

            Assert.Throws<ObjectDisposedException>(() => ac.Increment());
            Assert.Throws<ObjectDisposedException>(() => ac1.Increment());

            AtomicCounterService.ReleaseCounter(ac);
        }

        [Test, Explicit("with static side effects")]
        public void ServiceCouldGrowBucketsWithoutResize()
        {
            // 2 counters per bucket
            Settings.AtomicCounterPoolBucketSize = 4;

            // 4 buckets initially
            var count = 2 * 4;

            var counters = new List<AtomicCounter>();

            for (int i = 0; i < count; i++)
            {
                counters.Add(AtomicCounterService.AcquireCounter());
            }

            foreach (var atomicCounter in counters)
            {
                atomicCounter.Dispose();
                AtomicCounterService.ReleaseCounter(atomicCounter);
            }

            Assert.AreEqual(4, AtomicCounterService.Buckets.Length);
            Assert.AreEqual(4, AtomicCounterService.Buckets.Where(x => x != null).Count());
        }

        [Test, Explicit("long running")]
        public void ServiceCouldGrowBucketsWithResize()
        {
            // 2 counters per bucket
            Settings.AtomicCounterPoolBucketSize = 4;

            // 4 buckets initially
            var count = 100;

            var counters = new List<AtomicCounter>();

            for (int i = 0; i < count; i++)
            {
                counters.Add(AtomicCounterService.AcquireCounter());
            }

            foreach (var atomicCounter in counters)
            {
                atomicCounter.Dispose();
                AtomicCounterService.ReleaseCounter(atomicCounter);
            }

            Assert.AreEqual(64, AtomicCounterService.Buckets.Length);
            Assert.AreEqual(50, AtomicCounterService.Buckets.Where(x => x != null).Count());
            Assert.AreEqual(50 * 2, AtomicCounterService.Buckets.Where(x => x != null).Select(x => x.FreeCount).Sum());
        }

        [Test, Explicit("with static side effects")]
        public void ServiceCouldGrowBucketsWithResizeManyTimes()
        {
            var rounds = 100;
            for (int i = 0; i < rounds; i++)
            {
                ServiceCouldGrowBucketsWithResize();
            }
        }

        [Test, Explicit("long running")]
        public void ServiceBenchmark()
        {
            var count = 10000;

            Settings.AtomicCounterPoolBucketSize = count;

            var rounds = 5000;
            var counters = new List<AtomicCounter>();
            using (Benchmark.Run("Service FullCycle", count * (long)rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        counters.Add(AtomicCounterService.AcquireCounter());
                    }

                    for (int i = counters.Count - 1; i >= 0; i--)
                    {
                        var atomicCounter = counters[i];
                        atomicCounter.Dispose();
                        AtomicCounterService.ReleaseCounter(atomicCounter);
                    }

                    //foreach (var atomicCounter in counters)
                    //{
                    //    atomicCounter.Dispose();
                    //    AtomicCounterService.ReleaseCounter(atomicCounter);
                    //}

                    counters.Clear();
                }
            }

            Assert.AreEqual(4, AtomicCounterService.Buckets.Length);
            Assert.AreEqual(1, AtomicCounterService.Buckets.Where(x => x != null).Count());
        }

        [Test, Explicit("long running")]
        public void ServiceBenchmarkNoRelease()
        {
            var count = 100000;

            Settings.AtomicCounterPoolBucketSize = count;

            var rounds = 50;
            var counters = new List<AtomicCounter>();
            using (Benchmark.Run("Service FullCycle", count * (long)rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        counters.Add(AtomicCounterService.AcquireCounter());
                    }

                    counters.Clear();
                }
            }
        }
    }
}