// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class SerializedMemoryIteratorTests
    {
        public Random Random { get; set; } = new Random();

        [Test]
        public void CouldIterateOverSerializedJson()
        {
            var count = 1_00_000;

            var values = new double[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = Random.NextDouble();
            }

            var bytes = JsonSerializer.Serialize(values);

            using (Benchmark.Run("SerializedMemoryIterator JSON", count))
            {
                var iterator = new SerializedMemoryIterator(bytes, false);

                var cnt = 0;
                foreach (var db in iterator)
                {
                    var value = JsonSerializer.Deserialize<double>(db.Span.ToArray());
                    if (Math.Abs(values[cnt] - value) > 0.000000001)
                    {
                        Assert.Fail("Values are not equal");
                    }
                    cnt++;
                }
            }
            Benchmark.Dump();
        }

        [Test]
        public void CouldIterateOverSerializedBinary()
        {
            var count = 10_000_000;

            var values = new double[count];
            var bytes = new byte[count * 8 + 8];
            var offset = 0;
            for (int i = 0; i < count; i++)
            {
                values[i] = Random.NextDouble();
            }

            BinarySerializer.Write(values, ref bytes, null, SerializationFormat.Binary);

            using (Benchmark.Run("SerializedMemoryIterator Binary", count))
            {
                var iterator = new SerializedMemoryIterator(bytes, true);

                var cnt = 0;
                foreach (var db in iterator)
                {
                    var value = db.ReadDouble(0);
                    // BinarySerializer.Read<double>(db.Data, out var value);
                    if (Math.Abs(values[cnt] - value) > 0.000000001)
                    {
                        Assert.Fail("Values are not equal");
                    }
                    cnt++;
                }
            }
            Benchmark.Dump();
        }


        public class DoubleWrapper
        {
            public double Value { get; set; }
        }

        [Test]
        public void CouldIterateOverSerializedBinaryJson()
        {
            // Terminology is bad now: binary transport format vs binary serialization format
            // Transport format adds header but payload could still be serializaed as JSON

            var count = 100_000;

            var values = new DoubleWrapper[count];
            var bytes = new byte[count * 100 + 8];
            var offset = 0;
            for (int i = 0; i < count; i++)
            {
                values[i] = new DoubleWrapper() {Value = Random.NextDouble()};
            }

            BinarySerializer.Write(values, ref bytes, null, SerializationFormat.Binary);

            using (Benchmark.Run("SerializedMemoryIterator Binary", count))
            {
                var iterator = new SerializedMemoryIterator(bytes, true);

                var cnt = 0;
                foreach (var db in iterator)
                {
                    var value = JsonSerializer.Deserialize<DoubleWrapper>(db.Span.ToArray());
                    if (Math.Abs(values[cnt].Value - value.Value) > 0.000000001)
                    {
                        Assert.Fail("Values are not equal");
                    }
                    cnt++;
                }
            }
            Benchmark.Dump();
        }
    }
}