// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class ArrayMemoryTests
    {
        [Test]
        public void CannotDisposeRetained()
        {
            var memory = ArrayMemory<byte>.Create(32 * 1024, pin: true);
            var rm = memory.Retain();
            Assert.Throws<InvalidOperationException>(() => { ((IDisposable)memory).Dispose(); });
            rm.Dispose();
        }

        [Test]
        public void CannotDoubleDispose()
        {
            var memory = ArrayMemory<byte>.Create(32 * 1024, pin: true);
            ((IDisposable)memory).Dispose();
            Assert.Throws<ObjectDisposedException>(() => { ((IDisposable)memory).Dispose(); });
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public unsafe void GcHandleTest()
        {
#if !DEBUG
            var count = 100_000_000;
#else
            var count = 1_000;
#endif
            var bytes = new byte[10000];

            // if we work with Memory abstraction GCHandle is no-go
            // either move to off-heap or pre-pin, handle is 19MOPS, slower than RMP
            // fixed is OK with 250+ MOPS in this test

            using (Benchmark.Run("GCHandle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    h.Free();
                }
            }

            var sum = 0.0;
            using (Benchmark.Run("Fixed", count))
            {
                for (int i = 0; i < count; i++)
                {
                    fixed (byte* ptr = &bytes[0])
                    {
                        sum += *ptr;
                    }
                }
            }

            Console.WriteLine(sum);
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void RentReturnBenchmark()
        {
#if !DEBUG
            var count = 100_000_000;
#else
            var count = 1_000;
#endif

            using (Benchmark.Run("FullCycle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var memory = ArrayMemory<byte>.Create(32, pin: false);
                    ((IDisposable)memory).Dispose();
                }
            }
        }

        [Test]
        public void RefCountOfPooled()
        {
            var pool = new RetainableMemoryPool<byte>(null, 16,
                1024 * 1024, 50, 2);

            var buf = (ArrayMemory<byte>)pool.Rent(100);

            Assert.IsTrue(buf.IsPoolable);

            Console.WriteLine($"rented: {buf.ReferenceCount}");

            pool.ReturnInternal(buf);

            Assert.IsTrue(buf.IsDisposed);
            Assert.IsTrue(buf.IsPooled);

            Console.WriteLine($"returned: {buf.ReferenceCount}");
            Console.WriteLine($"pooled: {buf.IsPooled}");

            Assert.Throws<ObjectDisposedException>(() =>
            {
                var _ = buf.Retain();
            });

            pool.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void RentReturnBenchmarkRetainablePool()
        {
#if !DEBUG
            var count = 10_000_000;
#else
            var count = 1_000;
#endif

            var pool = new RetainableMemoryPool<byte>(null, 16,
                1024 * 1024, 50, 2);

            for (int i = 0; i < 1000; i++)
            {
                var memory = pool.RentMemory(32 * 1024);
                memory.Increment();
                memory.Decrement();
                // ((IDisposable)memory).Dispose();
                //(memory.Pin(0)).Dispose();
                //if (memory.IsDisposed || memory.IsRetained)
                //{
                //    Assert.Fail();
                //}
                // pool.Return(memory);
            }

            for (int _ = 0; _ < 20; _++)
            {
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
                        pool.ReturnInternal(memory, false);
                    }
                }
            }
            Benchmark.Dump();
            pool.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void ConcurrentRentReturn()
        {
#if !DEBUG
            var count = 100_000_000;
#else
            var count = 1_000;
#endif
            var pool = new RetainableMemoryPool<byte>(null, 16,
                1024 * 1024, 50, 2);

            var tasks = new List<Task>();
            var taskCount = 1; // 6x2 cores

            var mre = new ManualResetEventSlim(false);

            Action action = () =>
            {
                mre.Wait();
                for (int i = 0; i < count; i++)
                {
                    // ReSharper disable once AccessToDisposedClosure
                    var memory = pool.RentMemory(32 * 1024);

                    //(memory.Pin(0)).Dispose();
                    //if (memory.IsDisposed || memory.IsRetained)
                    //{
                    //    Assert.Fail();
                    //}

                    // ReSharper disable once AccessToDisposedClosure
                    pool.ReturnInternal(memory);

                    //if (i % 1000000 == 0)
                    //{
                    //    Console.WriteLine(i);
                    //}
                }
            };

            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(action));
            }

            //Console.WriteLine("Set affinity and press enter");
            //Console.ReadLine();

            using (Benchmark.Run("FullCycle", count * taskCount))
            {
                mre.Set();

                Task.WhenAll(tasks).Wait();
            }

            pool.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void RentReturnBenchmarkRetainablePoolOverCapacity()
        {
#if !DEBUG
            var count = 10_000;
#else
            var count = 1_000;
#endif
            var capacity = 25;
            var batch = capacity * 2;
            var pool = new RetainableMemoryPool<byte>((p, l) =>
                {
                    var am = ArrayMemory<byte>.Create(l, pin: true);
                    // Attach pool
                    am._poolIdx = p.PoolIdx;
                    return am;
                }, 16,
                1024 * 1024, capacity, 0);

            var list = new List<RetainableMemory<byte>>(batch);

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
                        pool.ReturnInternal(arrayMemory);
                    }
                    list.Clear();
                }
            }

            pool.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void RentReturnPinUnpinBenchmark()
        {
#if !DEBUG
            var count = 10_000_000;
#else
            var count = 1_000;
#endif

            using (Benchmark.Run("FullCyclePinUnpin", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var memory = ArrayMemory<byte>.Create(32 * 1024, pin: true);
                    if (memory.Array.Length != 32 * 1024)
                    {
                        Assert.Fail("Length");
                    }
                    (memory.Pin(0)).Dispose();
                    if (memory.IsRetained)
                    {
                        Assert.Fail();
                    }
                }
            }
        }

        [Test]
        public void RentReturnPinnedSlicesRetainablePool()
        {
            var maxBuffers = 128 / 64; // 2

            var pool = new RetainableMemoryPool<byte>(null, 32 * 1024,
                1024 * 1024, maxBuffers, 0);

            var list = new List<RetainableMemory<byte>>();

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
                    pool.ReturnInternal(list[i]);
                }
            }

            pool.Dispose();
        }

        [Test]
        public void CouldDisposePoolWithFreeSpace()
        {
            var maxBuffers = 10;

            var pool = new RetainableMemoryPool<byte>(null, 32 * 1024,
                1024 * 1024, maxBuffers, 0);

            using (Benchmark.Run("FullCycle"))
            {
                for (int i = 0; i < maxBuffers / 2; i++)
                {
                    var memory = pool.RentMemory(64 * 1024);
                    pool.ReturnInternal(memory);
                }
            }

            pool.Dispose();
        }

        [Test]
        public void RentReturnPinnedSlicesRetainablePoolBadBehavior()
        {
            // Rent many then return many

            var maxBuffers = 32; // 2
            var buffersToTake = 128;

            var pool = new RetainableMemoryPool<byte>(null, 32 * 1024,
                1024 * 1024, maxBuffers, 0);

            var list = new List<RetainableMemory<byte>>();

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

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void RentReturnPinnedSlicesRetainablePoolBadBehaviorDropped()
        {
            // Rent many then return many

            var maxBuffers = 32; // 2

#if !DEBUG
            var buffersToTake = 1_00_000;
#else
            var buffersToTake = 1_000;
#endif

            var pool = new RetainableMemoryPool<byte>(null, 32 * 1024,
                1024 * 1024, maxBuffers, 0);

            using (Benchmark.Run("FullCycle", buffersToTake))
            {
                for (int i = 0; i < buffersToTake; i++)
                {
                    // ReSharper disable once UnusedVariable
                    var memory = pool.RentMemory(64 * 1024);
                    // pool.Return(memory);
                    //if (i % 100_000 == 0)
                    //{
                    //    Console.WriteLine(i);
                    //}
                    if (i % 1000 == 0)
                    {
                        GC.Collect(2, GCCollectionMode.Forced, true);
                        GC.WaitForPendingFinalizers();
                        GC.Collect(2, GCCollectionMode.Forced, true);
                        GC.WaitForPendingFinalizers();
                    }
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
