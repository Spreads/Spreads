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

namespace Spreads.Tests.Buffers
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
                const int count = 1000000;

                var sum = 0L;

                using (Benchmark.Run("Threadlocal", count))
                {
                    for (var i = 0; i < count; i++)
                    {
                        // var wrapper = BufferPool.StaticBuffer;
                        using (var wrapper = BufferPool.UseTempBuffer(BufferPool.StaticBufferSize))
                        {
                            wrapper.Memory.Span[0] = 123;
                            sum += wrapper.Memory.Span[0] + wrapper.Memory.Span[1];
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("GetBuffer via pool", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        using (var wrapper = BufferPool<byte>.RentOwnedPooledArray(BufferPool.StaticBufferSize + 1)) // BufferPool.UseTempBuffer(BufferPool.StaticBufferSize + 1
                        {
                            wrapper.Memory.Span[0] = 123;
                            sum += wrapper.Memory.Span[0] + wrapper.Memory.Span[1];
                        }
                    }
                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("Direct ArrayPool with StaticBufferSize", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        var buffer = BufferPool<byte>.Rent(BufferPool.StaticBufferSize);
                        buffer[0] = 123;
                        sum += buffer[0] + buffer[1];
                        BufferPool<byte>.Return(buffer, false);
                    }

                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("Direct ArrayPool with StaticBufferSize + 1", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        var buffer = BufferPool<byte>.Rent(BufferPool.StaticBufferSize + 1);
                        buffer[0] = 123;
                        sum += buffer[0] + buffer[1];
                        BufferPool<byte>.Return(buffer, false);
                    }

                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("GC StaticBufferSize", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        var buffer = new byte[BufferPool.StaticBufferSize];
                        buffer[0] = 123;
                        sum += buffer[0] + buffer[1];
                    }

                    Assert.IsTrue(sum > 0);
                }

                using (Benchmark.Run("GC StaticBufferSize + 1", count))
                {
                    sum = 0L;
                    for (var i = 0; i < count; i++)
                    {
                        var buffer = new byte[BufferPool.StaticBufferSize + 1];
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
    }
}