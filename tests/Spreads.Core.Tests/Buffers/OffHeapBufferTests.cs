// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Linq;

#pragma warning disable 618

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class OffHeapBufferTests
    {
        [Test]
        public unsafe void CouldUseOffHeapBuffer()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618
            var rounds = 1_000;
            var count = 1_00_000;

            OffHeapBuffer<int> ob = default; // this just works
            // OffHeapBuffer<int> ob = new OffHeapBuffer<int>(count);

            using (Benchmark.Run("OB with grow", count * rounds))
            {
                ob.EnsureCapacity((count + 1) * 4);
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        // ob.EnsureCapacity((i + 1) * 4);
                        // Unsafe.WriteUnaligned(ob.Pointer + i, i);
                        // ob.DirectBuffer.Write(i * 4, i);
                        ob[i] = i;
                    }
                }
            }

            using (Benchmark.Run("OB via pointer", count * rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        // Unsafe.WriteUnaligned(ob.Pointer + i, i);
                        // ob.DirectBuffer.Write(i * 4, i);
                        ((int*)ob.Data)[i] = i;
                    }
                }
            }

            using (Benchmark.Run("Span read", count * rounds))
            {
                var sp = ob.Span;
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (sp[i] != i)
                        {
                            Assert.Fail($"sp[i] {sp[i]} != i {i}");
                        }
                    }
                }
            }

            ob.Dispose();
        }

        [Test]
        public void OffHeapPoolRentReturnPerformance()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false; // no difference
#pragma warning restore 618

            var sizes = new[] { Settings.LARGE_BUFFER_LIMIT/8, Settings.LARGE_BUFFER_LIMIT / 4, Settings.LARGE_BUFFER_LIMIT / 2, Settings.LARGE_BUFFER_LIMIT / 1, Settings.LARGE_BUFFER_LIMIT * 2 };
            var count = 1_000;

            var pool = new OffHeapMemoryPool<byte>(2);

            // warmup
            foreach (var size in sizes)
            {
                for (int i = 0; i < 1; i++)
                {
                    var offHeapMemory = pool.RentMemory(size);
                    pool.ReturnInternal(offHeapMemory);
                }
            }

            foreach (var size in sizes)
            {
                using (Benchmark.Run(size + " kb", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var offHeapMemory = pool.RentMemory(size);
                        pool.ReturnInternal(offHeapMemory);
                    }
                }
            }

            Benchmark.Dump();

            var objs = pool.InspectObjects().ToArray();

            Assert.AreEqual(2, objs.Count());
        }

        [Test]
        public void OffHeapPoolRetainDisposePerformance()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var sizesKb = new[] { 32, 64, 128, 256, 512, 1024, 2048, 4096 };
            var count = 1_000;
            var maxSize = 4096 * 1024;
            var pool = new OffHeapMemoryPool<byte>(2, maxSize);

            // warmup
            foreach (var size in sizesKb)
            {
                for (int i = 0; i < 1; i++)
                {
                    var offHeapMemory = pool.RentMemory(size * 1024);
                    pool.ReturnInternal(offHeapMemory);
                }
            }

            foreach (var size in sizesKb)
            {
                if (size * 1024 > maxSize)
                {
                    using (Benchmark.Run(size + " kb, KOPS", count))
                    {
                        for (int i = 0; i < count / 1_000; i++)
                        {
                            var rm = pool.RentMemory(size * 1024).Retain();
                            rm.Dispose();
                        }
                    }
                }
                else
                {
                    using (Benchmark.Run(size + " kb", count))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var rm = pool.RentMemory(size * 1024).Retain();
                            rm.Dispose();
                        }
                    }
                }
            }

            Benchmark.Dump();

            Assert.AreEqual(sizesKb.Select(x => x * 1024).Count(x => x >= Settings.LARGE_BUFFER_LIMIT), pool.InspectObjects().Count());
        }

        [Test]
        public void OffHeapPoolCannotDisposeRetained()
        {
            var pool = new OffHeapMemoryPool<byte>(1);
            var offHeapMemory = pool.RentMemory(32 * 1024);
            var rm = offHeapMemory.Retain();
            Assert.Throws<InvalidOperationException>(() => { pool.ReturnInternal(offHeapMemory); });
            rm.Dispose();
            pool.Dispose();
        }

        [Test]
        public void OffHeapPoolCouldRentReturnWithOverCapacity()
        {
            var pool = new OffHeapMemoryPool<byte>(2);
            var offHeapMemory = pool.RentMemory(32 * 1024);
            var offHeapMemory2 = pool.RentMemory(32 * 1024);
            var offHeapMemory3 = pool.RentMemory(32 * 1024);

            Assert.AreEqual(pool.PoolIdx, offHeapMemory3._poolIdx, "pool idx of newlly allocated");

            ((IDisposable)offHeapMemory).Dispose();
            ((IDisposable)offHeapMemory2).Dispose();
            ((IDisposable)offHeapMemory3).Dispose();

            Assert.Throws<ObjectDisposedException>(() => { pool.ReturnInternal(offHeapMemory); });
            Assert.Throws<ObjectDisposedException>(() => { pool.ReturnInternal(offHeapMemory2); });
            Assert.Throws<ObjectDisposedException>(() => { pool.ReturnInternal(offHeapMemory3); });
            Assert.AreEqual(2, pool.InspectObjects().Count());
        }

        [Test]
        public void OffHeapPoolCouldRentReturn()
        {
            var pool = new OffHeapMemoryPool<byte>(2);
            var offHeapMemory = pool.RentMemory(32 * 1024);
            var offHeapMemory2 = pool.RentMemory(32 * 1024);

            pool.ReturnInternal(offHeapMemory);
            pool.ReturnInternal(offHeapMemory2);
            Assert.AreEqual(2, pool.InspectObjects().Count());
            pool.Dispose();
        }

        [Test]
        public void OffHeapPoolCouldDisposeRented()
        {
            var pool = new OffHeapMemoryPool<byte>(2);
            var offHeapMemory = pool.RentMemory(32 * 1024);
            ((IDisposable)offHeapMemory).Dispose();

            Assert.Throws<ObjectDisposedException>(() => { pool.ReturnInternal(offHeapMemory); });

            Assert.AreEqual(1, pool.InspectObjects().Count());

            var rm = pool.RentMemory(32 * 1024).Retain();
            // ((IDisposable)offHeapMemory).Dispose();
            rm.Dispose();
            Assert.AreEqual(1, pool.InspectObjects().Count());

            pool.Dispose();
        }

        [Test]
        public void CouldDisposeNonPooledBuffer()
        {
            var pool = new OffHeapMemoryPool<byte>(2);

            var buffer = new OffHeapMemory<byte>(1000);

            Assert.Throws<InvalidOperationException>(() => { buffer.Unpin(); });

            Assert.Throws<InvalidOperationException>(() => { pool.ReturnInternal(buffer); });

            ((IDisposable)buffer).Dispose();

            Assert.Throws<ObjectDisposedException>(() => { ((IDisposable)buffer).Dispose(); });
        }
    }
}
