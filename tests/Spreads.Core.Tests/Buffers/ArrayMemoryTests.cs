// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Collections.Generic;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class ArrayMemoryTests
    {
        [Test]
        public void CannotDisposeRetained()
        {
            var memory = ArrayMemory<byte>.Create(32 * 1024);
            var rm = memory.Retain();
            Assert.Throws<InvalidOperationException>(() => { ((IDisposable)memory).Dispose(); });
            rm.Dispose();
        }

        [Test]
        public void CannotDoubleDispose()
        {
            var memory = ArrayMemory<byte>.Create(32 * 1024);
            ((IDisposable)memory).Dispose();
            Assert.Throws<ObjectDisposedException>(() => { ((IDisposable)memory).Dispose(); });
        }

        [Test, Explicit("long running")]
        public void RentReturnBenchmark()
        {
            var count = 100_000_000;

            using (Benchmark.Run("FullCycle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var memory = ArrayMemory<byte>.Create(32 * 1024);
                    ((IDisposable)memory).Dispose();
                }
            }
        }

        [Test, Explicit("long running")]
        public void RentReturnBenchmarkRetainablePool()
        {
            var count = 100_000_000;

            var pool = new RetainableMemoryPool<byte, ArrayMemory<byte>>(null, 16,
                1024 * 1024, 50, 2);

            for (int i = 0; i < 1000; i++)
            {
                var memory = pool.RentMemory(32 * 1024);
                //(memory.Pin(0)).Dispose();
                //if (memory.IsDisposed || memory.IsRetained)
                //{
                //    Assert.Fail();
                //}
                pool.Return(memory);
            }

            using (Benchmark.Run("FullCycle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var memory = pool.RentMemory(32 * 1024);
                    //(memory.Pin(0)).Dispose();
                    //if (memory.IsDisposed || memory.IsRetained)
                    //{
                    //    Assert.Fail();
                    //}
                    pool.Return(memory);
                }
            }

            pool.Dispose();
        }

        [Test, Explicit("long running")]
        public void RentReturnBenchmarkRetainablePoolOverCapacity()
        {
            var count = 1_000_000;
            var capacity = 25;
            var batch = capacity * 2;
            var pool = new RetainableMemoryPool<byte, ArrayMemory<byte>>(ArrayMemory<byte>.Create, 16,
                1024 * 1024, capacity, 0);

            var list = new List<ArrayMemory<byte>>(batch);

            using (Benchmark.Run("FullCycle", count * batch))
            {
                for (int i = 0; i < count; i++)
                {
                    for (int j = 0; j < batch; j++)
                    {
                        list.Add(pool.RentMemory(32 * 1024));
                    }

                    foreach (var arrayMemory in list)
                    {
                        pool.Return(arrayMemory);
                    }
                    list.Clear();
                }
            }

            pool.Dispose();
        }

        [Test, Explicit("long running")]
        public void RentReturnPinUnpinBenchmark()
        {
            var count = 10_000_000;

            using (Benchmark.Run("FullCyclePinUnpin", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var memory = ArrayMemory<byte>.Create(32 * 1024);
                    if (memory.Array.Length != 32 * 1024)
                    {
                        Assert.Fail("Length");
                    }
                    (memory.Pin(0)).Dispose();
                    if (memory.IsDisposed || memory.IsRetained)
                    {
                        Assert.Fail();
                    }
                }
            }
        }

        [Test, Explicit("long running")]
        public void RentReturnPinnedSlicesRetainablePool()
        {
            var maxBuffers = 128 / 64; // 2

            var pool = new RetainableMemoryPool<byte, ArrayMemory<byte>>(null, 32 * 1024,
                1024 * 1024, maxBuffers, 0);

            var list = new List<ArrayMemory<byte>>();

            using (Benchmark.Run("FullCycle"))
            {
                for (int i = 0; i < maxBuffers * 2; i++)
                {
                    list.Add(pool.RentMemory(64 * 1024));
                }

                for (int i = 0; i < maxBuffers; i++)
                {
                    ((IDisposable)list[i]).Dispose();
                }

                for (int i = 2; i < maxBuffers * 2; i++)
                {
                    pool.Return(list[i]);
                }
            }

            pool.Dispose();
        }

        [Test, Explicit("long running")]
        public void RentReturnPinnedSlicesRetainablePoolBadBehavior()
        {
            // Rent many then return many

            var maxBuffers = 32; // 2
            var buffersToTake = 128;

            var pool = new RetainableMemoryPool<byte, ArrayMemory<byte>>(null, 32 * 1024,
                1024 * 1024, maxBuffers, 0);

            var list = new List<ArrayMemory<byte>>();

            using (Benchmark.Run("FullCycle"))
            {
                for (int i = 0; i < buffersToTake; i++)
                {
                    list.Add(pool.RentMemory(64 * 1024));
                }

                for (int i = 0; i < buffersToTake; i++)
                {
                    ((IDisposable)list[i]).Dispose();
                }
            }

            Console.WriteLine("Disposing pool");
            pool.Dispose();
        }

        [Test, Explicit("long running")]
        public void RentReturnPinnedSlicesRetainablePoolBadBehaviorDropped()
        {
            // Rent many then return many

            var maxBuffers = 32; // 2
            var buffersToTake = 1_000_000;

            var pool = new RetainableMemoryPool<byte, ArrayMemory<byte>>(null, 32 * 1024,
                1024 * 1024, maxBuffers, 0);

            using (Benchmark.Run("FullCycle", buffersToTake))
            {
                for (int i = 0; i < buffersToTake; i++)
                {
                    var memory = pool.RentMemory(64 * 1024);
                    // pool.Return(memory);
                    //if (i % 100_000 == 0)
                    //{
                    //    Console.WriteLine(i);
                    //}
                    //if (i % 1000 == 0)
                    //{
                    //    GC.Collect(2, GCCollectionMode.Forced, true);
                    //    GC.WaitForPendingFinalizers();
                    //    GC.Collect(2, GCCollectionMode.Forced, true);
                    //    GC.WaitForPendingFinalizers();
                    //}
                }

                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }

            Console.WriteLine("Disposing pool");
            pool.Dispose();
        }
    }
}
