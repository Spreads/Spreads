// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Linq;
using System.Threading;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class AtomicCounterTests
    {
        [Test]
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

        private int field;

        [Test, Explicit("long running")]
        public void IncrementDecrementBenchmark()
        {
            // this is relevant for non-pooled object (new when pool is small then dispose if pool is full)

            var pool = new AtomicCounterPool<OffHeapBuffer<int>>(new OffHeapBuffer<int>(4));

            var count = 500_000_000;

            var local = 0;

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
            

            //using (Benchmark.Run("Increment field", count))
            //{
            //    for (int i = 0; i < count; i++)
            //    {
            //        Interlocked.Increment(ref field);
            //    }
            //}
            //using (Benchmark.Run("Decrement field", count))
            //{
            //    for (int i = 0; i < count; i++)
            //    {
            //        Interlocked.Decrement(ref field);
            //    }
            //}

            ac.Dispose();
            pool.ReleaseCounter(ac);
            pool.Dispose();
        }
    }
}
