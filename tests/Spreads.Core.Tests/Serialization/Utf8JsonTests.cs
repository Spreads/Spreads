// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization.Utf8Json;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Spreads.Core.Tests.Serialization
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct LongUnion
    {
        [FieldOffset(0)]
        public int Int1;

        [FieldOffset(1)]
        public int Int2;

        [FieldOffset(0)]
        public float Float;

        [FieldOffset(0)]
        public double Double;

        [FieldOffset(0)]
        public ulong Long;
    }

    [TestFixture]
    public class Utf8JsonTests
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TestValue
        {
            // public string Str { get; set; }
            //public string Str1 { get; set; }
            public int Num { get; set; }

            public int Num1 { get; set; }
            public int Num2 { get; set; }

            //public Decimal Dec { get; set; }

            //public double Dbl { get; set; }
            //public double Dbl1 { get; set; }

            public bool Boo { get; set; }
        }
        #if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        #endif
        [Test, Explicit("long running")]
        public unsafe void CompareUtf8JsonWithBinarySerializer()
        {
            var rand = new Random(34151513);

            Settings.DoAdditionalCorrectnessChecks = false;
            var count = 5000_000;
            var bytes = new byte[100_000];
            var mem = (Memory<byte>)bytes;
            var h = mem.Pin();
            var db = new DirectBuffer(bytes.Length, (byte*)h.Pointer);

            var values = new Serialization.TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = Serialization.TestValue.Create(rand, withDouble: false);
            }

            for (int i = 0; i < 100; i++)
            {
                var writer = new JsonWriter(bytes);
                JsonSerializer.Serialize(ref writer, values[i]);
                var val = JsonSerializer.Deserialize<Serialization.TestValue>(db);

                var writer2 = new Utf8Json.JsonWriter(bytes);
                Utf8Json.JsonSerializer.Serialize(ref writer2, values[i]);
                var val2 = Utf8Json.JsonSerializer.Deserialize<Serialization.TestValue>(bytes);
            }
            bytes.AsSpan().Clear();

            for (int r = 0; r < 20; r++)
            {
                CompareUtf8JsonWithBinarySerializer_SW(count, bytes, values);

                CompareUtf8JsonWithBinarySerializer_SR(count, db);

                CompareUtf8JsonWithBinarySerializer_UW(count, bytes, values);

                CompareUtf8JsonWithBinarySerializer_UR(count, bytes);

                //using (Benchmark.Run("Spreads BinarySerializer", count))
                //{
                //    var lenSum = 0L;

                //    for (int i = 0; i < count; i++)
                //    {
                //        var size = BinarySerializer.SizeOf(values[i], out var stream, SerializationFormat.Json);
                //        BinarySerializer.Write(values[i], bytes, stream, SerializationFormat.Json);
                //        BinarySerializer.Read<TestValue>(bytes, out var value);
                //        lenSum += size;
                //    }
                //}
            }
            Benchmark.Dump();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif

        private static void CompareUtf8JsonWithBinarySerializer_UR(int count, byte[] bytes)
        {
            using (Benchmark.Run("Utf8Json Read", count))
            {
                var lenSum = 0L;

                for (int i = 0; i < count; i++)
                {
                    //var writer = new Utf8Json.JsonWriter(bytes);
                    //Utf8Json.JsonSerializer.Serialize(ref writer, values[i]);
                    var val = Utf8Json.JsonSerializer.Deserialize<Serialization.TestValue>(bytes);
                    //if (val.Num != values[i].Num)
                    //{
                    //    Console.WriteLine($"val.Num {val.Num} != values[i].Num {values[i].Num} for i={i}");
                    //    // Assert.Fail("val.Num != values[i].Num");
                    //}
                    //lenSum += writer.CurrentOffset;
                }

                // Console.WriteLine("Utf8Json len " + lenSum);
            }
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif

        private static void CompareUtf8JsonWithBinarySerializer_UW(int count, byte[] bytes, Serialization.TestValue[] values)
        {
            using (Benchmark.Run("Utf8Json Write", count))
            {
                var lenSum = 0L;

                for (int i = 0; i < count; i++)
                {
                    var writer = new Utf8Json.JsonWriter(bytes);
                    Utf8Json.JsonSerializer.Serialize(ref writer, values[i]);
                    //Utf8Json.JsonSerializer.Deserialize<TestValue>(bytes);
                    //lenSum += writer.CurrentOffset;
                }

                // Console.WriteLine("Utf8Json len " + lenSum);
            }
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif

        private static void CompareUtf8JsonWithBinarySerializer_SR(int count, DirectBuffer db)
        {
            using (Benchmark.Run("Spreads Read", count))
            {
                var lenSum = 0L;

                for (int i = 0; i < count; i++)
                {
                    //var writer = new Spreads.Serialization.Utf8Json.JsonWriter(bytes);
                    //Spreads.Serialization.Utf8Json.JsonSerializer.Serialize(ref writer, values[i]);
                    var val = JsonSerializer.Deserialize<Serialization.TestValue>(db);
                    //if (val.Num != values[i].Num)
                    //{
                    //    // var val1 = Utf8Json.JsonSerializer.Deserialize<TestValue>(bytes);
                    //    Console.WriteLine($"val.Num {val.Num} != values[i].Num {values[i].Num} for i={i}");
                    //    // Assert.Fail("val.Num != values[i].Num");
                    //}
                    //lenSum += writer.CurrentOffset;
                }

                // Console.WriteLine("Spreads len " + lenSum);
            }
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif

        private static void CompareUtf8JsonWithBinarySerializer_SW(int count, byte[] bytes, Serialization.TestValue[] values)
        {
            using (Benchmark.Run("Spreads Write", count))
            {
                var lenSum = 0L;

                for (int i = 0; i < count; i++)
                {
                    var writer = new JsonWriter(bytes);
                    JsonSerializer.Serialize(ref writer, values[i]);

                    //Spreads.Serialization.Utf8Json.JsonSerializer.Deserialize<TestValue>(bytes);
                    //lenSum += writer.CurrentOffset;
                }

                //Console.WriteLine("Spreads len " + lenSum);
            }
        }

        [Test, Explicit("long running")]
        public void SerializeToRetainedMemoryOverhead()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var count = 1_0_000;

            for (int r = 0; r < 10; r++)
            {
                //using (Benchmark.Run("To Rented Array", count))
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        var segment = Spreads.Serialization.Utf8Json.JsonSerializer.SerializeToRentedBuffer(i);
                //        BufferPool<byte>.Return(segment.Array);
                //    }
                //}

                //using (Benchmark.Run("To RM", count))
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        var rm = Spreads.Serialization.Utf8Json.JsonSerializer.SerializeToRetainedMemory(i);
                //        rm.Dispose();
                //    }
                //}

                //using (Benchmark.Run("To RMS", count))
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        var rms = RecyclableMemoryStreamManager.Default.GetStream();
                //        Spreads.Serialization.Utf8Json.JsonSerializer.Serialize(rms, i);
                //        rms.Dispose();
                //    }
                //}

                var rm = BufferPool.Retain(16);
                rm.Dispose();

                using (Benchmark.Run("RM", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        rm = BufferPool.Retain(16);
                        rm.Dispose();
                    }
                }
            }
            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void CouldReadStream()
        {
            var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("[1]"));
            var result1 = JsonSerializer.Deserialize<int[]>(stream1);

            var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("["));
            Assert.Throws<JsonParsingException>(() =>
            {
                var result2 = JsonSerializer.Deserialize<int[]>(stream2);
            });
        }

        public class BinaryDataWrapper
        {
            public byte[] Data { get; set; }
        }

        [Test]
        public void ByteArrayBenchmark()
        {
            BinaryDataWrapper wrapper = new BinaryDataWrapper();
            wrapper.Data = Enumerable.Range(1, 512 * 1024).Select(x => unchecked((byte)x)).ToArray();

            const int N = 1000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                byte[] newtonSoftBytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(wrapper, Newtonsoft.Json.Formatting.Indented));
            }
            sw.Stop();
            GC.Collect();
            Console.WriteLine($"Did serialize with Json.NET in {sw.Elapsed.TotalSeconds:F3}s");

            sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                byte[] bytes = Utf8Json.JsonSerializer.Serialize(wrapper);
            }
            sw.Stop();
            GC.Collect();
            Console.WriteLine($"Did serialize with UTF8Json in {sw.Elapsed.TotalSeconds:F3}s");

            sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                byte[] bytes = JsonSerializer.Serialize(wrapper);
            }
            sw.Stop();
            GC.Collect();
            Console.WriteLine($"Did serialize with Spreads.UTF8Json in {sw.Elapsed.TotalSeconds:F3}s");

#if NETCOREAPP3_0

            sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(wrapper);
            }
            sw.Stop();
            GC.Collect();
            Console.WriteLine($"Did serialize with S.T.Json in {sw.Elapsed.TotalSeconds:F3}s");

#endif
        }
    }
}
