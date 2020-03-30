// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class BuffersTests
    {
        public static class LocalBuffers<T>
        {
            [ThreadStatic]
            private static T[] _threadStatic;

            private static ThreadLocal<T[]> _threadLocal = new ThreadLocal<T[]>(() => new T[10]);
            public static T[] ThreadStatic => _threadStatic ?? (_threadStatic = new T[10]);
            public static T[] ThreadLocal => _threadLocal.Value;
        }

        [Test, Explicit("long running")]
        public void ThreadStaticVsThreadLocal()
        {
            for (int r = 0; r < 10; r++)
            {
                const int count = 100000000;
                var sw = new Stopwatch();

                sw.Restart();
                var sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    var buffer = LocalBuffers<int>.ThreadStatic;
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"ThreadStatic {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    var buffer = LocalBuffers<int>.ThreadLocal;
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"ThreadLocal {sw.ElapsedMilliseconds}");

                Console.WriteLine("---------------------");
            }
        }

        [Test, Explicit("long running")]
        public void CouldGetArrayFromRetainedBuffer()
        {
            var retained = BufferPool.Retain(3456, false);
            Assert.AreEqual(4096, retained.Length);
            var mem = (ReadOnlyMemory<byte>)retained.Memory;
            if (!MemoryMarshal.TryGetArray(mem, out ArraySegment<byte> valuesSegment))
            {
                throw new NotSupportedException("Currently only arrays-backed OwnedMemory is supported");
            }
            Assert.IsTrue(valuesSegment.Count > 0);
        }

        [Test, Explicit("long running")]
        public void ThreadStaticBufferVsSharedPool()
        {
            //Case | MOPS | Elapsed | GC0 | GC1 | GC2 | Memory
            //    ------------------------------------------ -| --------:| ---------:| ------:| ------:| ------:| --------:
            //Direct ArrayPool with StaticBufferSize +1 | 37.04 | 27 ms | 0.0 | 0.0 | 0.0 | 0.008 MB
            //Threadlocal | 34.48 | 29 ms | 0.0 | 0.0 | 0.0 | 0.008 MB
            //    Direct ArrayPool with StaticBufferSize | 34.48 | 29 ms | 0.0 | 0.0 | 0.0 | 0.008 MB
            //    GetBuffer via pool                         | 12.20 | 82 ms | 0.0 | 0.0 | 0.0 | 0.008 MB
            //    GC StaticBufferSize + 1 | 1.49 | 673 ms | 2610.0 | 0.0 | 0.0 | 5.798 MB
            //    GC StaticBufferSize | 1.48 | 675 ms | 2610.0 | 0.0 | 0.0 | 5.795 MB
            for (int r = 0; r < 10; r++)
            {
                int count = (int)TestUtils.GetBenchCount(1_000_000);

                var sum = 0L;

                //using (Benchmark.Run("ThreadStatic", count))
                //{
                //    for (var i = 0; i < count; i++)
                //    {
                //        var wrapper = BufferPool.StaticBuffer;
                //        // using (var wrapper = BufferPool.StaticBufferMemory)
                //        {
                //            var s = wrapper.Memory.Span;
                //            s[0] = 123;
                //            sum += s[0] + s[1];
                //        }
                //    }
                //    Assert.IsTrue(sum > 0);
                //}

                using (Benchmark.Run("BP.Retain", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        using (var wrapper = BufferPool.Retain(Settings.LARGE_BUFFER_LIMIT))
                        {
                            var s = wrapper.Memory.Span;
                            s[0] = 123;
                            sum += s[0] + s[1];
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("BP.RetainTemp", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        using (var wrapper = BufferPool.RetainTemp(Settings.LARGE_BUFFER_LIMIT))
                        {
                            var s = wrapper.Memory.Span;
                            s[0] = 123;
                            sum += s[0] + s[1];
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("RMP", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        using (var wrapper = BufferPool<Byte>.MemoryPool.RentMemory(Settings.LARGE_BUFFER_LIMIT))
                        {
                            var s = wrapper.Memory.Span;
                            s[0] = 123;
                            sum += s[0] + s[1];
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("RM([])", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        var arr = BufferPool<byte>.Rent(Settings.LARGE_BUFFER_LIMIT);
                        using (var wrapper = new RetainedMemory<byte>(arr))
                        {
                            var s = wrapper.Memory.Span;
                            s[0] = 123;
                            sum += s[0] + s[1];
                        }
                        BufferPool<byte>.Return(arr);
                    }
                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("ArrayPool", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        var buffer = BufferPool<byte>.Rent(Settings.LARGE_BUFFER_LIMIT);
                        buffer[0] = 123;
                        sum += buffer[0] + buffer[1];
                        BufferPool<byte>.Return(buffer, false);
                    }

                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("GC", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        var buffer = new byte[Settings.LARGE_BUFFER_LIMIT];
                        buffer[0] = 123;
                        sum += buffer[0] + buffer[1];
                    }

                    Assert.IsTrue(sum > 0);
                }

            }

            Benchmark.Dump($"BufferPool benchmark");
        }

        [Test]
        public void HeaderLittleEndianTest()
        {
            var ptr = Marshal.AllocHGlobal(8);

            Marshal.WriteInt32(ptr, 255);

            var firstByte = Marshal.ReadByte(ptr);

            Assert.AreEqual(255, firstByte);
        }

        [Test, Explicit("long running")]
        public unsafe void InterlockedIncrVsAdd()
        {
            var ptr = (void*)Marshal.AllocHGlobal(8);

            const int count = 1000000000;
            var sw = new Stopwatch();

            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                *(int*)ptr = *(int*)ptr + 1;
            }
            sw.Stop();
            Console.WriteLine($"Pointer {sw.ElapsedMilliseconds}");
            *(int*)ptr = 0;
            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                Interlocked.Increment(ref (*(int*)ptr));
            }
            sw.Stop();
            Console.WriteLine($"Interlocked {sw.ElapsedMilliseconds}");
        }

        [Test, Explicit("long running")]
        public void SharedArrayPoolPerformance()
        {
            var sizesKb = new[] { 64, 128, 256, 512, 1024 };
            var count = 1_000_000;

            foreach (var size in sizesKb)
            {
                using (Benchmark.Run(size + " kb", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var array = BufferPool<byte>.Rent(size * 1024);
                        BufferPool<byte>.Return(array);
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void SharedArrayPoolPerformanceSingleSize()
        {
            var count = 10_000_000;

            using (Benchmark.Run("SharedPoolSingle", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var array = BufferPool<byte>.Rent(32 * 1024);
                    BufferPool<byte>.Return(array);
                }
            }

            Benchmark.Dump();
        }
    }
}