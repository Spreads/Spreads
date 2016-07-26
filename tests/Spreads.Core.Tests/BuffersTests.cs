using System;
using System.Diagnostics;
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

                Console.WriteLine("---------------------");
            }
        }





    }
}
