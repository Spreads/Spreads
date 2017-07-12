// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Spreads.Buffers;

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

        [Test, Ignore]
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

        [Test, Ignore]
        public void ThreadStaticBufferVsSharedPool()
        {
            for (int r = 0; r < 10; r++)
            {
                const int count = 1000000;
                var sw = new Stopwatch();

                sw.Restart();
                var sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    using (var wrapper = BufferPool.UseTempBuffer(BufferPool.StaticBufferSize))
                    {
                        wrapper.Buffer.Span[0] = 123;
                        sum += wrapper.Buffer.Span[0] + wrapper.Buffer.Span[1];
                    }
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"Threadlocal {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    using (var wrapper = BufferPool.UseTempBuffer(BufferPool.StaticBufferSize + 1))
                    {
                        wrapper.Buffer.Span[0] = 123;
                        sum += wrapper.Buffer.Span[0] + wrapper.Buffer.Span[1];
                    }
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"GetBuffer via pool {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    var buffer = BufferPool<byte>.Rent(BufferPool.StaticBufferSize);
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                    BufferPool<byte>.Return(buffer, true);
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"Direct ArrayPool with StaticBufferSize {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    var buffer = BufferPool<byte>.Rent(BufferPool.StaticBufferSize + 1);
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                    BufferPool<byte>.Return(buffer, true);
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"Direct ArrayPool with StaticBufferSize + 1 {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    var buffer = new byte[BufferPool.StaticBufferSize];
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"GC StaticBufferSize {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++)
                {
                    var buffer = new byte[BufferPool.StaticBufferSize + 1];
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"GC StaticBufferSize + 1 {sw.ElapsedMilliseconds}");

                Console.WriteLine("---------------------");
            }
        }

        [Test]
        public void HeaderLittleEndianTest()
        {
            var ptr = Marshal.AllocHGlobal(8);

            Marshal.WriteInt32(ptr, 255);

            var firstByte = Marshal.ReadByte(ptr);

            Assert.AreEqual(255, firstByte);
        }

        [Test]
        [Ignore]
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