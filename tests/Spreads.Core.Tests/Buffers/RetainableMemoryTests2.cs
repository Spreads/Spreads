// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class RetainableMemoryTests2
    {
        [Test]
        public void CannotDisposeRetained()
        {
            var memory = ArrayMemory<byte>.Create(32 * 1024);
            memory.IsPoolable.ShouldBe(false);
            memory.IsPooled.ShouldBe(false);
            memory.IsDisposed.ShouldBe(false);
            var rm = memory.Retain();
            Assert.Throws<InvalidOperationException>(() => { memory.Dispose(); });
            rm.Dispose();
            memory.IsDisposed.ShouldBe(true);
        }

        [Test]
        public void CannotDoubleDispose()
        {
            var memory = ArrayMemory<byte>.Create(32 * 1024);
            memory.IsPoolable.ShouldBe(false);
            memory.IsPooled.ShouldBe(false);
            memory.IsDisposed.ShouldBe(false);
            memory.Dispose();
            memory.IsDisposed.ShouldBe(true);
            Assert.Throws<ObjectDisposedException>(() => { memory.Dispose(); });
        }

        [Test]
        public void CannotDisposeFromPoolRetained()
        {
            var memory = BufferPool<byte>.MemoryPool.RentMemory(1024);
            var rm = memory.Retain();
            Assert.Throws<InvalidOperationException>(() => { memory.Dispose(); });
            memory.IsPooled.ShouldBe(false, "Memory should not be pooled after failed Dispose()");
            rm.Dispose();
            memory.IsPooled.ShouldBe(true);
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
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void CreateDisposeBenchmark()
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
                    var memory = ArrayMemory<byte>.Create(32);
                    (memory).Dispose();
                }
            }
        }

        [Test]
        public void RefCountOfPooled()
        {
            var pool = new RetainableMemoryPool<Byte>(RetainableMemoryPool<Byte>.DefaultFactory);

            var buf = (PrivateMemory<byte>)pool.Rent(100);

            buf.IsPoolable.ShouldBe(true);

            Console.WriteLine($"rented: {buf.ReferenceCount}");

            buf.Dispose();

            buf.IsDisposed.ShouldBe(true);
            buf.IsPooled.ShouldBe(true);

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
         , Explicit("bench")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void RentReturnDefaultRetainablePoolBench()
        {
            var count = TestUtils.GetBenchCount(1_000_000, 1_0);

            var pool = new RetainableMemoryPool<Byte>(RetainableMemoryPool<Byte>.DefaultFactory);

            for (int i = 0; i < 1000; i++)
            {
                var memory = pool.RentMemory(32 * 1024);
                memory.Increment();
                memory.Decrement();
                // memory.Dispose();
                //(memory.Pin(0)).Dispose();
                //if (memory.IsDisposed || memory.IsRetained)
                //{
                //    Assert.Fail();
                //}
                // pool.Return(memory);
            }

            for (int _ = 0; _ < 20; _++)
            {
                using (Benchmark.Run("RentReturn", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var memory = pool.RentMemory(32 * 1024);
                        //(memory.Pin(0)).Dispose();
                        //if (memory.IsDisposed || memory.IsRetained)
                        //{
                        //    Assert.Fail();
                        //}
                        memory.Dispose();
                    }
                }
            }
            Benchmark.Dump();
            pool.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void RentReturnArrayPoolBench()
        {
#if !DEBUG
            var count = 10_000_000;
#else
            var count = 1_000;
#endif

            for (int _ = 0; _ < 20; _++)
            {
                using (Benchmark.Run("RentReturn", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var array = BufferPool<byte>.Rent(32 * 1024);
                        BufferPool<byte>.Return(array, false);
                    }
                }
            }
            Benchmark.Dump();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void ConcurrentRentReturn()
        {
#if !DEBUG
            var count = 100_000_000;
#else
            var count = 1_000;
#endif
            var pool = new RetainableMemoryPool<Byte>(RetainableMemoryPool<Byte>.DefaultFactory);

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
                    memory.Dispose();

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
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void RentReturnBenchmarkRetainablePoolOverCapacity()
        {
#if !DEBUG
            var count = 10_000;
#else
            var count = 1_0;
#endif
            var capacity = 25;
            var batch = capacity * 2;
            var pool = new RetainableMemoryPool<Byte>(RetainableMemoryPool<Byte>.DefaultFactory);

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
                        arrayMemory.Dispose();
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
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
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
                    var memory = ArrayMemory<byte>.Create(32 * 1024);
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
        public void CouldDisposePoolWithFreeSpace()
        {
            var maxBuffers = 10;

            var pool =  new RetainableMemoryPool<Byte>(RetainableMemoryPool<Byte>.DefaultFactory);

            using (Benchmark.Run("FullCycle"))
            {
                for (int i = 0; i < maxBuffers / 2; i++)
                {
                    var memory = pool.RentMemory(64 * 1024);
                    memory.Dispose();
                    // pool.ReturnInternal(memory);
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

            var pool = new RetainableMemoryPool<Byte>(RetainableMemoryPool<Byte>.DefaultFactory);

            var list = new List<RetainableMemory<byte>>();

            using (Benchmark.Run("FullCycle"))
            {
                for (int i = 0; i < buffersToTake; i++)
                {
                    list.Add(pool.RentMemory(64 * 1024));
                }

                for (int i = 0; i < buffersToTake; i++)
                {
                    list[i].Dispose();
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

            var pool = new RetainableMemoryPool<Byte>(RetainableMemoryPool<Byte>.DefaultFactory);

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
