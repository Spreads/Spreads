// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Runtime.InteropServices;
using Spreads.Buffers;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class Utf8JsonTests
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TestValue
        {
            public string Str { get; set; }
            public string Str1 { get; set; }
            public int Num { get; set; }
            public int Num1 { get; set; }
            public int Num2 { get; set; }

            //public Decimal Dec { get; set; }
            public double Dbl { get; set; }

            public double Dbl1 { get; set; }
            public bool Boo { get; set; }
        }

        [Test, Explicit("long running")]
        public unsafe void CompareUtf8JsonWithBinarySerializer()
        {
            var count = 100_000;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }

            for (int r = 0; r < 20; r++)
            {
                using (Benchmark.Run("Utf8Json.NuGet", count))
                {
                    var lenSum = 0L;

                    for (int i = 0; i < count; i++)
                    {
                        var stream = RecyclableMemoryStreamManager.Default.GetStream(null, 1000);
                        Utf8Json.JsonSerializer.Serialize(stream, values[i]);
                        Utf8Json.JsonSerializer.Deserialize<TestValue>(stream);
                        lenSum += stream.Length;
                        stream.Dispose();
                        //var str = Encoding.UTF8.GetString(stream.ToArray());
                        //Console.WriteLine(str);
                    }
                    // Console.WriteLine("Utf8Json " + lenSum);
                }

                using (Benchmark.Run("Utf8Json.Spreads", count))
                {
                    var lenSum = 0L;

                    for (int i = 0; i < count; i++)
                    {
                        var stream = Spreads.Serialization.Utf8Json.JsonSerializer.SerializeWithOffset(values[i], 0);
                        Spreads.Serialization.Utf8Json.JsonSerializer.Deserialize<TestValue>(stream);
                        lenSum += stream.Length;
                        stream.Dispose();
                        //var str = Encoding.UTF8.GetString(stream.ToArray());
                        //Console.WriteLine(str);
                    }
                    // Console.WriteLine("Utf8Json " + lenSum);
                }

                using (Benchmark.Run("Spreads BinarySerializer", count))
                {
                    var lenSum = 0L;
                    var bytes = new byte[1000];
                    fixed (byte* ptr = &bytes[0])
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var size = BinarySerializer.SizeOf(values[i], out var stream, SerializationFormat.Json);
                            BinarySerializer.WriteUnsafe(values[i], (IntPtr)ptr, stream, SerializationFormat.Json);
                            BinarySerializer.Read<TestValue>((IntPtr)ptr, out var value);
                            lenSum += size;
                        }
                    }
                }
            }
            Benchmark.Dump();
        }
    }
}