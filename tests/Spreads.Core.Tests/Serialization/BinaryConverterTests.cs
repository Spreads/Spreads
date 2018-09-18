// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class BinaryConverterTests
    {
        [JsonFormatter(typeof(Formatter))]
        public struct DummyStruct : IBinaryConverter<DummyStruct>, IEquatable<DummyStruct>
        {
            private int _value;

            public DummyStruct(int value)
            {
                _value = value;
            }

            public byte ConverterVersion => 1;

            public int SizeOf(DummyStruct value, out ArraySegment<byte> temporaryBuffer)
            {
                temporaryBuffer = default;
                return 4;
            }

            public int Write(DummyStruct value, DirectBuffer destination)
            {
                destination.WriteInt32(0, value._value);
                return 4;
            }

            public int Read(DirectBuffer source, out DummyStruct value)
            {
                var val = source.ReadInt32(0);
                value = new DummyStruct(val);
                return 4;
            }

            public bool Equals(DummyStruct other)
            {
                return _value == other._value;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is DummyStruct other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _value;
            }

            internal class Formatter : IJsonFormatter<DummyStruct>
            {
                public void Serialize(ref JsonWriter writer, DummyStruct value, IJsonFormatterResolver formatterResolver)
                {
                    writer.WriteBeginArray();
                    writer.WriteInt32(value._value);
                    writer.WriteEndArray();
                }

                public DummyStruct Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
                {
                    reader.ReadIsBeginArrayWithVerify();
                    var val = reader.ReadInt32();
                    reader.ReadIsEndArrayWithVerify();
                    return new DummyStruct(val);
                }
            }
        }

        [Test]
        public void BinaryConverterWorks()
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            var silent = true;

            var rm = BufferPool.Retain(100_000_000);
            var db = new DirectBuffer(rm.Span);

            var count = 5_000_000;
            var rounds = 10;

            var rng = new Random(0);

            var values = new DummyStruct[count];

            for (int i = 0; i < count; i++)
            {
                var value = new DummyStruct(i);
                values[i] = value;
            }

            var dest = db;
            int payloadSize = 0;

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Write", count))
                {
                    dest = db;
                    payloadSize = 0;
                    for (int i = 0; i < count; i++)
                    {
                        var value = values[i];
                        var size = BinarySerializer.SizeOf(in value, out var tmpBuffer, SerializationFormat.Binary);

                        var written = BinarySerializer.Write(in value, dest, tmpBuffer, SerializationFormat.Binary);

                        if (size != written)
                        {
                            Assert.Fail($"size {size} != written {written}");
                        }

                        dest = dest.Slice(written);

                        payloadSize += written;
                    }
                }
            }

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Read", count))
                {
                    var readSource = db.Slice(0, payloadSize);

                    for (int i = 0; i < count; i++)
                    {
                        var read = BinarySerializer.Read(readSource, out DummyStruct value1);
                        readSource = readSource.Slice(read);
                        var expected = values[i];
                        if (!value1.Equals(expected))
                        {
                            Assert.Fail($"value1 != values[i]");
                        }
                    }
                }
            }

            Benchmark.Dump();
        }


        [Test]
        public void CouldUseJsonWithBinaryConverter()
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            var silent = true;

            var rm = BufferPool.Retain(100_000_000);
            var db = new DirectBuffer(rm.Span);

            var count = 100_000;
            var rounds = 10;

            var rng = new Random(0);

            var values = new DummyStruct[count];

            for (int i = 0; i < count; i++)
            {
                var value = new DummyStruct(i);
                values[i] = value;
            }

            var dest = db;
            int payloadSize = 0;

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Write", count))
                {
                    dest = db;
                    payloadSize = 0;
                    for (int i = 0; i < count; i++)
                    {
                        var value = values[i];
                        var size = BinarySerializer.SizeOf(in value, out var tmpBuffer, SerializationFormat.Json);

                        var written = BinarySerializer.Write(in value, dest, tmpBuffer, SerializationFormat.Json);

                        if (size != written)
                        {
                            Assert.Fail($"size {size} != written {written}");
                        }

                        dest = dest.Slice(written);

                        payloadSize += written;
                    }
                }
            }

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Read", count))
                {
                    var readSource = db.Slice(0, payloadSize);

                    for (int i = 0; i < count; i++)
                    {
                        var read = BinarySerializer.Read(readSource, out DummyStruct value1);
                        readSource = readSource.Slice(read);
                        var expected = values[i];
                        if (!value1.Equals(expected))
                        {
                            Assert.Fail($"value1 != values[i]");
                        }
                    }
                }
            }

            Benchmark.Dump();
        }
    }
}