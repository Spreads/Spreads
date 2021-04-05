// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using Spreads.Utils;
using System;
using System.Runtime.CompilerServices;
using Spreads.DataTypes;

namespace Spreads.Core.Tests.Serialization
{
    // TODO add this to docs, this is a sample how to work with custom binary/Json serialization

    [Category("CI")]
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class IBinarySerializerTests
    {
        [BinarySerialization(serializerType: typeof(Serializer),  KnownTypeId = 123)]
        [JsonFormatter(typeof(Formatter))]
        public struct SampleStruct : IEquatable<SampleStruct>
        {
            public int Value;

            public SampleStruct(int value)
            {
                Value = value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(SampleStruct other)
            {
                return Value == other.Value;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is SampleStruct other && Equals(other);
            }

            public override int GetHashCode()
            {
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                return Value;
            }

            public byte ConverterVersion
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SizeOf(SampleStruct value, out ArraySegment<byte> temporaryBuffer)
            {
                temporaryBuffer = default;
                return 4;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Write(SampleStruct value, ref DirectBuffer destination)
            {
                destination.WriteInt32(0, value.Value);
                return 4;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Read(ref DirectBuffer source, out SampleStruct value)
            {
                var val = source.ReadInt32(0);
                value = new SampleStruct(val);
                return 4;
            }

            internal class Serializer : BinarySerializer<SampleStruct>
            {
                public override byte KnownTypeId => 0;

                public override short FixedSize => 4;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public override int SizeOf(in SampleStruct value, BufferWriter bufferWriter)
                {
                    return FixedSize;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public override int Write(in SampleStruct value, DirectBuffer destination)
                {
                    destination.WriteInt32(0, value.Value);
                    return 4;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public override int Read(DirectBuffer source, out SampleStruct value)
                {
                    var val = source.ReadInt32(0);
                    value = new SampleStruct(val);
                    return 4;
                }
            }

            internal class Formatter : IJsonFormatter<SampleStruct>
            {
                // NB performance penalty of `[` + `]` is very small, should prefer Json tuples over primitives (except for strings)

                public void Serialize(ref JsonWriter writer, SampleStruct value, IJsonFormatterResolver formatterResolver)
                {
                    writer.WriteBeginArray();
                    writer.WriteInt32(value.Value);
                    writer.WriteEndArray();
                }

                public SampleStruct Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
                {
                    reader.ReadIsBeginArrayWithVerify();
                    var val = reader.ReadInt32();
                    reader.ReadIsEndArrayWithVerify();
                    return new SampleStruct(val);
                }
            }
        }

        [Test]
        public void CouldUseCustomBinaryConverter()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var rm = BufferPool.Retain(100_000_000);
            var db = new DirectBuffer(rm);

            var count = 50_000;
            var rounds = 10;

            var values = new SampleStruct[count];

            for (int i = 0; i < count; i++)
            {
                var value = new SampleStruct(i);
                values[i] = value;
            }

            int payloadSize = 0;

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Write", count))
                {
                    var dest = db;
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
                        var read = BinarySerializer.Read(readSource, out SampleStruct value1);
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
            rm.Dispose();
        }

        [Test]
        public void CouldUseJsonWithCustomBinaryConverter()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var rm = BufferPool.Retain(100_000_000);
            var db = new DirectBuffer(rm);

            var count = 1_000;
            var rounds = 1;

            var values = new SampleStruct[count];

            for (int i = 0; i < count; i++)
            {
                var value = new SampleStruct(i);
                values[i] = value;
            }

            DirectBuffer dest;
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
                        var read = BinarySerializer.Read(readSource, out SampleStruct value1);
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
            rm.Dispose();
        }
    }
}