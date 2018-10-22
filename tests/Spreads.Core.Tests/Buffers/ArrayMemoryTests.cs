// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests.Buffers
{
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

        [Test]
        public void RentReturnBenchmark()
        {
            var count = 100_000_000;

            using (Benchmark.Run("FullCycle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var memory = ArrayMemory<byte>.Create(32 * 1024);
                    ((IDisposable) memory).Dispose();
                }
            }
        }

        [Test]
        public void RentReturnBenchmarkRetainablePool()
        {
            var count = 100_000_000;

            var pool = new RetainableMemoryPool<byte, ArrayMemory<byte>>(ArrayMemory<byte>.Create, 16,
                1024 * 1024, 50, 2);

            using (Benchmark.Run("FullCycle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var memory = pool.Rent(32 * 1024);
                    pool.Return(memory);
                }
            }

            pool.DisposeBuckets();
        }

        [Test]
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
    }
}
