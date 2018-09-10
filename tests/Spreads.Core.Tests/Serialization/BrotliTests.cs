// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Runtime.InteropServices;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class BrotliTests
    {
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

        //[Test, Explicit("long running")]
        //public void BrotliBenchmark()
        //{
        //    // R2 has some strange very slow read perf on this data when count is small
        //    // R3 is balanced, good compr and fast enough

        //    var count = 100;
        //    var values = new TestValue[count];
        //    for (int i = 0; i < count; i++)
        //    {
        //        values[i] = new TestValue()
        //        {
        //            // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
        //            Dbl = (double)i + 1 / (double)(i + 1),
        //            Dbl1 = (double)i + 1 / (double)(i + 1),
        //            Num = i,
        //            Num1 = i,
        //            Num2 = i,
        //            Str = i.ToString(),
        //            Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
        //            Boo = i % 2 == 0
        //        };
        //    }

        //    var bufferLen = 1000000;
        //    var originalPtr = Marshal.AllocHGlobal(bufferLen);
        //    var compressedPtr = Marshal.AllocHGlobal(bufferLen);
        //    var decompressedPtr = Marshal.AllocHGlobal(bufferLen);

        //    var originalDB = new DirectBuffer(bufferLen, originalPtr);
        //    var compressedDB = new DirectBuffer(bufferLen, compressedPtr);
        //    var decompressedDB = new DirectBuffer(bufferLen, decompressedPtr);

        //    var originalLen = BinarySerializer.WriteUnsafe(values, originalPtr, null, SerializationFormat.Json);
        //    Console.WriteLine("Original len: " + originalLen);

        //    for (int level = 0; level < 10; level++)
        //    {
        //        Settings.BrotliCompressionLevel = level;

        //        var compressedLen = BinarySerializer.WriteBrotli(originalDB.Slice(0, originalLen), compressedDB);
        //        //Console.WriteLine("Compressed len: " + compressedLen);

        //        var decompressedLen = BinarySerializer.ReadBrotli(compressedDB, decompressedDB);
        //        //Console.WriteLine("Decompressed len: " + decompressedLen);

        //        Console.WriteLine($"Level: {Settings.BrotliCompressionLevel}, ratio: {1.0 * decompressedLen / compressedLen}");

        //        Assert.AreEqual(originalLen, decompressedLen);
        //    }

        //    Console.WriteLine("-------------------------------");

        //    var rounds = 10;
        //    var iterations = 1000;
        //    for (int r = 0; r < rounds; r++)
        //    {
        //        for (int level = 0; level < 5; level++)
        //        {
        //            Settings.BrotliCompressionLevel = level;

        //            using (Benchmark.Run($"W{level}", originalLen * iterations, true))
        //            {
        //                for (int i = 0; i < iterations; i++)
        //                {
        //                    BinarySerializer.WriteBrotli(originalDB.Slice(0, originalLen), compressedDB);
        //                }
        //            }

        //            using (Benchmark.Run($"R{level}", originalLen * iterations, true))
        //            {
        //                for (int i = 0; i < iterations; i++)
        //                {
        //                    BinarySerializer.ReadBrotli(compressedDB, decompressedDB);
        //                }
        //            }
        //        }
        //    }

        //    Benchmark.Dump();
        //}

        //[Test, Explicit("long running")]
        //public void DetectBrotliVsDeflate()
        //{
        //    var count = 10;

        //    var bufferLen = 1000000;
        //    var originalPtr = Marshal.AllocHGlobal(bufferLen);
        //    var compressedPtr = Marshal.AllocHGlobal(bufferLen);

        //    var originalDB = new DirectBuffer(bufferLen, originalPtr);
        //    var compressedDB = new DirectBuffer(bufferLen, compressedPtr);

        //    for (int i = 0; i < count; i++)
        //    {
        //        var originalLen = BinarySerializer.WriteUnsafe(i, originalPtr, null, SerializationFormat.JsonDeflate);

        //        Console.WriteLine($"{originalDB.Span[8]} - {originalDB.Span[9]}");
        //    }

        //    for (int i = 0; i < count; i++)
        //    {
        //        var len = BinarySerializer.WriteUnsafe(i, originalPtr, null, SerializationFormat.Json);
        //        BinarySerializer.WriteBrotli(originalDB.Slice(0, len), compressedDB);
        //        Console.WriteLine($"{compressedDB.Span[0]} - {compressedDB.Span[1]} - {compressedDB.Span[2]}");

        //    }
        //}
    }
}