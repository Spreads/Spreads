// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Spreads.Core.Tests.Blosc
{
    [TestFixture]
    public unsafe class BloscTests
    {
        public const long Iterations = 1_000;
        public const int itemCount = 20;

        [Test]
        public void CouldShuffleUnshuffle()
        {
            var bufferLen = 1_000_000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);
            var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

            var originalDB = new DirectBuffer(bufferLen, originalPtr);
            var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
            var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

            var itemCount = 4096 / 8;
            byte itemSize = 8;
            var srcLen = itemCount * itemSize;
            //var bytes = new byte[255 * itemCount];
            //var mem = (Memory<byte>) bytes;
            //var h = mem.Pin();
            //var ptr = h.Pointer;

            for (int i = 0; i < itemCount; i++)
            {
                for (int j = 0; j < itemSize; j++)
                {
                    originalDB[i * 255 + j] = (byte)(j + 1);
                }
            }

            var iterations = Iterations;

            var rounds = 10;
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Shuffle", iterations))
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        BinarySerializer.Shuffle(originalDB.Slice(0, srcLen), compressedDB, itemSize);
                    }
                }
                using (Benchmark.Run("Unshuffle", iterations))
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        BinarySerializer.Unshuffle(compressedDB.Slice(0, srcLen), decompressedDB, itemSize);
                    }
                }

                //using (Benchmark.Run("ShuffleXX", iterations))
                //{
                //    for (int i = 0; i < iterations; i++)
                //    {
                //        BinarySerializer.ShuffleXX(originalDB.Slice(0, srcLen), compressedDB, itemSize);
                //    }
                //}

                //using (Benchmark.Run("UnshuffleXX", iterations))
                //{
                //    for (int i = 0; i < iterations; i++)
                //    {
                //        BinarySerializer.UnshuffleXX(compressedDB.Slice(0, srcLen), decompressedDB, itemSize);
                //    }
                //}
            }
            Benchmark.Dump();

            Assert.IsTrue(originalDB.Slice(0, srcLen).Span.SequenceEqual(decompressedDB.Slice(0, srcLen).Span));
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TestValue
        {
            public string Str { get; set; }
            public string Str1 { get; set; }
            public int Num { get; set; }
            public int Num1 { get; set; }
            public int Num2 { get; set; }

            // public Decimal Dec { get; set; }

            public double Dbl { get; set; }
            public double Dbl1 { get; set; }

            public bool Boo { get; set; }
        }

        [Test]
        public void Lz4Benchmark()
        {
            // R2 has some strange very slow read perf on this data when count is small
            // R3 is balanced, good compr and fast enough

            var count = itemCount;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    //Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    //Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }

            var bufferLen = 1000_000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);
            var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

            var originalDB = new DirectBuffer(bufferLen, originalPtr);
            var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
            var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

            var originalLen = BinarySerializer.Write(values, ref originalDB, default, SerializationFormat.Json);
            Console.WriteLine("Original len: " + originalLen);

            for (int level = 1; level < 10; level++)
            {
                Settings.LZ4CompressionLevel = level;

                var compressedLen = BinarySerializer.Compress(originalDB.Slice(0, originalLen), compressedDB, CompressionMethod.Lz4);
                //Console.WriteLine("Compressed len: " + compressedLen);

                var decompressedLen = BinarySerializer.Decompress(compressedDB.Slice(0, compressedLen), decompressedDB, CompressionMethod.Lz4);
                //Console.WriteLine("Decompressed len: " + decompressedLen);

                Console.WriteLine($"Level: {Settings.LZ4CompressionLevel}, ratio: {1.0 * decompressedLen / compressedLen}");

                Assert.AreEqual(originalLen, decompressedLen);
            }

            Console.WriteLine("-------------------------------");

            var rounds = 10;
            var iterations = Iterations / itemCount;
            for (int r = 0; r < rounds; r++)
            {
                for (int level = 1; level < 10; level++)
                {
                    Settings.LZ4CompressionLevel = level;
                    int compressedLen = 0;
                    using (Benchmark.Run($"LZ4 W{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            compressedLen = BinarySerializer.Compress(originalDB.Slice(0, originalLen), compressedDB, CompressionMethod.Lz4);
                        }
                    }

                    using (Benchmark.Run($"LZ4 R{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            BinarySerializer.Decompress(compressedDB.Slice(0, compressedLen), decompressedDB, CompressionMethod.Lz4);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test]
        public void ZstdBenchmark()
        {
            // R2 has some strange very slow read perf on this data when count is small
            // R3 is balanced, good compr and fast enough

            var count = itemCount;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    //Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    // Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }

            var bufferLen = 1000_000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);
            var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

            var originalDB = new DirectBuffer(bufferLen, originalPtr);
            var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
            var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

            var originalLen = BinarySerializer.Write(values, ref originalDB, default, SerializationFormat.Json);
            Console.WriteLine("Original len: " + originalLen);

            for (int level = 1; level < 10; level++)
            {
                Settings.ZstdCompressionLevel = level;

                var compressedLen = BinarySerializer.Compress(originalDB.Slice(0, originalLen), compressedDB, CompressionMethod.Zstd);
                //Console.WriteLine("Compressed len: " + compressedLen);

                var decompressedLen = BinarySerializer.Decompress(compressedDB.Slice(0, compressedLen), decompressedDB, CompressionMethod.Zstd);
                //Console.WriteLine("Decompressed len: " + decompressedLen);

                Console.WriteLine($"Level: {Settings.ZstdCompressionLevel}, ratio: {1.0 * decompressedLen / compressedLen}");

                Assert.AreEqual(originalLen, decompressedLen);
            }

            Console.WriteLine("-------------------------------");

            var rounds = 10;
            var iterations = Iterations / itemCount;
            for (int r = 0; r < rounds; r++)
            {
                for (int level = 1; level < 5; level++)
                {
                    Settings.ZstdCompressionLevel = level;
                    int compressedLen = 0;
                    using (Benchmark.Run($"Zstd W{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            compressedLen = BinarySerializer.Compress(originalDB.Slice(0, originalLen), compressedDB, CompressionMethod.Zstd);
                        }
                    }

                    using (Benchmark.Run($"Zstd R{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            BinarySerializer.Decompress(compressedDB.Slice(0, compressedLen), decompressedDB, CompressionMethod.Zstd);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test]
        public void GZipBenchmark()
        {
            // R2 has some strange very slow read perf on this data when count is small
            // R3 is balanced, good compr and fast enough

            var count = itemCount;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    //Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    //Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }

            var bufferLen = 1000_000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);
            var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

            var originalDB = new DirectBuffer(bufferLen, originalPtr);
            var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
            var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

            var originalLen = BinarySerializer.Write(values, ref originalDB, default, SerializationFormat.Json);
            Console.WriteLine("Original len: " + originalLen);

            for (int level = 1; level < 10; level++)
            {
                Settings.ZlibCompressionLevel = level;

                var compressedLen = BinarySerializer.Compress(originalDB.Slice(0, originalLen), compressedDB, CompressionMethod.GZip);
                //Console.WriteLine("Compressed len: " + compressedLen);

                var decompressedLen = BinarySerializer.Decompress(compressedDB.Slice(0, compressedLen), decompressedDB, CompressionMethod.GZip);
                //Console.WriteLine("Decompressed len: " + decompressedLen);

                Console.WriteLine($"Level: {Settings.ZlibCompressionLevel}, ratio: {1.0 * decompressedLen / compressedLen}");

                Assert.AreEqual(originalLen, decompressedLen);
            }

            Console.WriteLine("-------------------------------");

            var rounds = 10;
            var iterations = Iterations / itemCount;
            for (int r = 0; r < rounds; r++)
            {
                for (int level = 1; level < 5; level++)
                {
                    Settings.ZlibCompressionLevel = level;
                    int compressedLen = 0;

                    using (Benchmark.Run($"Zlib W{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            compressedLen = BinarySerializer.Compress(originalDB.Slice(0, originalLen), compressedDB, CompressionMethod.GZip);
                        }
                    }

                    using (Benchmark.Run($"Zlib R{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            BinarySerializer.Decompress(compressedDB.Slice(0, compressedLen), decompressedDB, CompressionMethod.GZip);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test]
        public void CopyViaCalliBenchmark()
        {
            // R2 has some strange very slow read perf on this data when count is small
            // R3 is balanced, good compr and fast enough

            var count = itemCount;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    //Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    //Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }

            var bufferLen = 1000_000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);
            var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

            var originalDB = new DirectBuffer(bufferLen, originalPtr);
            var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
            var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

            var originalLen = BinarySerializer.Write(values, ref originalDB, default, SerializationFormat.Json);
            Console.WriteLine("Original len: " + originalLen);

            var rounds = 10;
            var iterations = 100 * Iterations / itemCount;
            for (int r = 0; r < rounds; r++)
            {
                for (int level = 1; level < 5; level++)
                {
                    Settings.ZlibCompressionLevel = level;
                    int compressedLen = 0;

                    using (Benchmark.Run($"Copy W{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            compressedLen = BinarySerializer.Compress(originalDB.Slice(0, originalLen), compressedDB, CompressionMethod.None);
                        }
                    }

                    using (Benchmark.Run($"Copy R{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            BinarySerializer.Decompress(compressedDB.Slice(0, compressedLen), decompressedDB, CompressionMethod.None);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test]
        public void DeflateBenchmark()
        {
            // R2 has some strange very slow read perf on this data when count is small
            // R3 is balanced, good compr and fast enough

            var count = itemCount;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    //Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    //Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }

            var bufferLen = 1000_000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);
            var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

            var originalDB = new DirectBuffer(bufferLen, originalPtr);
            var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
            var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

            var originalLen = BinarySerializer.Write(values, ref compressedDB, default, SerializationFormat.Json);
            Console.WriteLine("Original len: " + originalLen);

            for (int level = 1; level < 10; level++)
            {
                Settings.ZlibCompressionLevel = level;

                var compressedLen = BinarySerializer.WriteDeflate(originalDB.Slice(0, originalLen), compressedDB);
                //Console.WriteLine("Compressed len: " + compressedLen);

                var decompressedLen = BinarySerializer.ReadDeflate(compressedDB.Slice(0, compressedLen), decompressedDB);
                //Console.WriteLine("Decompressed len: " + decompressedLen);

                Console.WriteLine($"Level: {Settings.ZlibCompressionLevel}, ratio: {1.0 * decompressedLen / compressedLen}");

                Assert.AreEqual(originalLen, decompressedLen);
            }

            Console.WriteLine("-------------------------------");

            var rounds = 10;
            var iterations = Iterations / itemCount;
            for (int r = 0; r < rounds; r++)
            {
                for (int level = 1; level < 5; level++)
                {
                    Settings.ZlibCompressionLevel = level;
                    int compressedLen = 0;
                    using (Benchmark.Run($"Deflate W{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            compressedLen = BinarySerializer.WriteDeflate(originalDB.Slice(0, originalLen), compressedDB);
                        }
                    }

                    using (Benchmark.Run($"Deflate R{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            BinarySerializer.ReadDeflate(compressedDB.Slice(0, compressedLen), decompressedDB);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

#if NETCOREAPP2_1

        [Test]
        public void BrotliBenchmark()
        {
            // R2 has some strange very slow read perf on this data when count is small
            // R3 is balanced, good compr and fast enough

            var count = itemCount;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    //Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    //Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }

            var bufferLen = 1000000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);
            var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

            var originalDB = new DirectBuffer(bufferLen, originalPtr);
            var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
            var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

            var originalLen = BinarySerializer.Write(values, ref originalDB, default, SerializationFormat.Json);
            Console.WriteLine("Original len: " + originalLen);

            for (int level = 0; level < 10; level++)
            {
                Settings.BrotliCompressionLevel = level;

                var compressedLen = BinarySerializer.WriteBrotli(originalDB.Slice(0, originalLen), compressedDB);
                //Console.WriteLine("Compressed len: " + compressedLen);

                var decompressedLen = BinarySerializer.ReadBrotli(compressedDB, decompressedDB);
                //Console.WriteLine("Decompressed len: " + decompressedLen);

                Console.WriteLine($"Level: {Settings.BrotliCompressionLevel}, ratio: {1.0 * decompressedLen / compressedLen}");

                Assert.AreEqual(originalLen, decompressedLen);
            }

            Console.WriteLine("-------------------------------");

            var rounds = 10;
            var iterations = Iterations / itemCount;
            for (int r = 0; r < rounds; r++)
            {
                for (int level = 1; level < 5; level++)
                {
                    Settings.BrotliCompressionLevel = level;

                    using (Benchmark.Run($"W{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            BinarySerializer.WriteBrotli(originalDB.Slice(0, originalLen), compressedDB);
                        }
                    }

                    using (Benchmark.Run($"R{level}", originalLen * iterations, true))
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            BinarySerializer.ReadBrotli(compressedDB, decompressedDB);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

#endif

        [Test]
        public void ZlibDeflateStreamCompat()
        {
            var count = 10;

            var bufferLen = 1000000;
            var originalPtr = Marshal.AllocHGlobal(bufferLen);
            var compressedPtr = Marshal.AllocHGlobal(bufferLen);

            var compressedDB = new DirectBuffer(bufferLen, originalPtr);
            var destinationDB = new DirectBuffer(bufferLen, compressedPtr);

            var rms = RecyclableMemoryStream.Create(Spreads.Buffers.RecyclableMemoryStreamManager.Default);

            for (int i = 0; i < count; i++)
            {
                rms.WriteAsPtr((long)i);
            }

            var originalLen = rms.Length;
            var cbuffer = rms.SingleChunk;
            var cmem = (Memory<byte>)cbuffer;
            var ch = cmem.Pin();
            var cptr = ch.Pointer;
            Settings.ZlibCompressionLevel = 9;

            var writeSize =
                BinarySerializer.WriteGZip(new DirectBuffer(rms.Length, (IntPtr)cptr), compressedDB);

            var compressedStream = RecyclableMemoryStream.Create(Spreads.Buffers.RecyclableMemoryStreamManager.Default);
            using (var compressor = new GZipStream(compressedStream, CompressionLevel.Optimal, true))
            {
                rms.PositionInternal = 0;
                rms.CopyTo(compressor);
                compressor.Dispose();
            }

            var buffer = compressedStream.SingleChunk;
            var mem = (Memory<byte>)buffer;
            var h = mem.Pin();
            var ptr = h.Pointer;

            var readSize =
                BinarySerializer.ReadGZip(new DirectBuffer(compressedStream.Length, (IntPtr)ptr), destinationDB);
            var readSize2 =
                BinarySerializer.ReadGZip(compressedDB, destinationDB);

            var zlibCompressed = compressedDB.Span.ToArray();

            var ms = new MemoryStream(zlibCompressed);

            var decompressedStream =
                Spreads.Buffers.RecyclableMemoryStreamManager.Default.GetStream();

            using (var decompressor = new GZipStream(ms, CompressionMode.Decompress, true))
            {
                decompressor.CopyTo(decompressedStream);
                decompressor.Dispose();
            }

            var readSize3 = decompressedStream.Length;

            Assert.AreEqual(readSize, originalLen);
            Assert.AreEqual(readSize, readSize2);
            Assert.AreEqual(readSize, readSize3);

            Console.WriteLine(readSize);
        }
    }
}