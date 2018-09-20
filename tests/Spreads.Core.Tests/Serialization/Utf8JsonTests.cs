// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Runtime.InteropServices;

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

            // public Decimal Dec { get; set; }

            //public double Dbl { get; set; }
            //public double Dbl1 { get; set; }

            public bool Boo { get; set; }
        }

        [Test, Explicit("long running")]
        public unsafe void CompareUtf8JsonWithBinarySerializer()
        {
            var rand = new Random(34151513);

            Settings.DoAdditionalCorrectnessChecks = false;
            var count = 1000_000;
            var bytes = new byte[100_000];
            var mem = (Memory<byte>)bytes;
            var h = mem.Pin();
            var db = new DirectBuffer(bytes.Length, (byte*)h.Pointer);

            var values = new Serialization.TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = Serialization.TestValue.Create(rand);
            }

            for (int r = 0; r < 20; r++)
            {
                using (Benchmark.Run("Spreads Write", count))
                {
                    var lenSum = 0L;

                    for (int i = 0; i < count; i++)
                    {
                        var writer = new Spreads.Serialization.Utf8Json.JsonWriter(bytes);
                        Spreads.Serialization.Utf8Json.JsonSerializer.Serialize(ref writer, values[i]);
                        //Spreads.Serialization.Utf8Json.JsonSerializer.Deserialize<TestValue>(bytes);
                        //lenSum += writer.CurrentOffset;
                    }
                    //Console.WriteLine("Spreads len " + lenSum);
                }

                using (Benchmark.Run("Spreads Write+Read", count))
                {
                    var lenSum = 0L;

                    for (int i = 0; i < count; i++)
                    {
                        //var writer = new Spreads.Serialization.Utf8Json.JsonWriter(bytes);
                        //Spreads.Serialization.Utf8Json.JsonSerializer.Serialize(ref writer, values[i]);
                        var val = Spreads.Serialization.Utf8Json.JsonSerializer.Deserialize<Serialization.TestValue>(db);
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

                using (Benchmark.Run("Utf8Json Write+Read", count))
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

        [Test, Explicit("long running")]
        public void SerializeToRetainedMemoryOverhead()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var count = 1000_000;
            
            for (int r = 0; r < 20; r++)
            {
                using (Benchmark.Run("To Rented Array", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var segment = Spreads.Serialization.Utf8Json.JsonSerializer.SerializeToRentedBuffer(i);
                        BufferPool<byte>.Return(segment.Array);
                    }
                }

                using (Benchmark.Run("To RM", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var rm = Spreads.Serialization.Utf8Json.JsonSerializer.SerializeToRetainedMemory(i);
                        rm.Dispose();
                    }
                }

                //using (Benchmark.Run("To RMS", count))
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        var rms = RecyclableMemoryStreamManager.Default.GetStream();
                //        Spreads.Serialization.Utf8Json.JsonSerializer.Serialize(rms, i);
                //        rms.Dispose();
                //    }
                //}

                using (Benchmark.Run("RM", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var rm = BufferPool.Retain(16);
                        rm.Dispose();
                    }
                }
            }
            Benchmark.Dump();
        }
    }
}