using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Spreads.Buffers;

namespace Spreads.Core.Tests {


    [TestFixture]
    public class BuffersTests {



        [Test, Ignore]
        public void ThreadStaticBufferVsSharedPool() {
            for (int r = 0; r < 10; r++) {


                const int count = 1000000;
                var sw = new Stopwatch();

                sw.Restart();
                var sum = 0L;
                for (var i = 0; i < count; i++) {
                    using (var wrapper = RecyclableMemoryManager.GetBuffer(RecyclableMemoryManager.StaticBufferSize)) {
                        wrapper.Buffer[0] = 123;
                        sum += wrapper.Buffer[0] + wrapper.Buffer[1];
                    }
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"Threadlocal {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++) {
                    using (var wrapper = RecyclableMemoryManager.GetBuffer(RecyclableMemoryManager.StaticBufferSize + 1)) {
                        wrapper.Buffer[0] = 123;
                        sum += wrapper.Buffer[0] + wrapper.Buffer[1];
                    }
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"GetBuffer via pool {sw.ElapsedMilliseconds}");


                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++) {
                    var buffer = ArrayPool<byte>.Shared.Rent(RecyclableMemoryManager.StaticBufferSize);
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                    ArrayPool<byte>.Shared.Return(buffer, true);
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"Direct ArrayPool with StaticBufferSize {sw.ElapsedMilliseconds}");


                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++) {
                    var buffer = ArrayPool<byte>.Shared.Rent(RecyclableMemoryManager.StaticBufferSize + 1);
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                    ArrayPool<byte>.Shared.Return(buffer, true);
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"Direct ArrayPool with StaticBufferSize + 1 {sw.ElapsedMilliseconds}");


                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++) {
                    var buffer = new byte[RecyclableMemoryManager.StaticBufferSize];
                    buffer[0] = 123;
                    sum += buffer[0] + buffer[1];
                }
                Assert.IsTrue(sum > 0);
                sw.Stop();
                Console.WriteLine($"GC StaticBufferSize {sw.ElapsedMilliseconds}");


                sw.Restart();
                sum = 0L;
                for (var i = 0; i < count; i++) {
                    var buffer = new byte[RecyclableMemoryManager.StaticBufferSize + 1];
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
        public void HeaderLittleEndianTest() {
            var ptr = Marshal.AllocHGlobal(8);

            Marshal.WriteInt32(ptr, 255);

            var firstByte = Marshal.ReadByte(ptr);

            Assert.AreEqual(255, firstByte);

        }

        [Test]
        public unsafe void InterlockedIncrVsAdd() {
            var ptr = (void*)Marshal.AllocHGlobal(8);


            const int count = 1000000000;
            var sw = new Stopwatch();

            sw.Restart();
            for (int i = 0; i < count; i++) {
                *(int*)ptr = *(int*)ptr + 1;
            }
            sw.Stop();
            Console.WriteLine($"Pointer {sw.ElapsedMilliseconds}");
            *(int*)ptr = 0;
            sw.Restart();
            for (int i = 0; i < count; i++) {
                Interlocked.Increment(ref (*(int*)ptr));
            }
            sw.Stop();
            Console.WriteLine($"Interlocked {sw.ElapsedMilliseconds}");
        }

    }
}
