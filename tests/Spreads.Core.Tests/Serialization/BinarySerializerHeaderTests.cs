// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Serialization
{
    [Category("CI")]
    [TestFixture]
    public class BinarySerializerHeaderTests
    {
        [BinarySerialization((TypeEnum)123, serializerType: typeof(Serializer))]
        [JsonFormatter(typeof(Formatter))]
        public struct SampleStruct
        {
            public int Value;

            public SampleStruct(int value)
            {
                Value = value;
            }

            internal readonly struct Serializer : IBinarySerializer<SampleStruct>
            {
                public byte SerializerVersion
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get => 1;
                }

                public byte KnownTypeId => 0;

                public short FixedSize => 4;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int SizeOf(in SampleStruct value, out RetainedMemory<byte> temporaryBuffer)
                {
                    temporaryBuffer = default;
                    return 4;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int Write(in SampleStruct value, DirectBuffer destination)
                {
                    destination.WriteInt32(0, value.Value);
                    return 4;
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int Read(DirectBuffer source, out SampleStruct value)
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
        public void CouldSetTypeEnumInJson()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var rm = BufferPool.Retain(10_000);
            var db = new DirectBuffer(rm.Span);

            var value = new SampleStruct(42);

            var size = BinarySerializer.SizeOf(in value, out var tmpBuffer, SerializationFormat.Json);
            var written = BinarySerializer.Write(in value, db, tmpBuffer, SerializationFormat.Json);

            Assert.AreEqual(size, written);

            var typeEnumByte = db[1];

            Assert.AreEqual((byte)TypeEnum.UserType, typeEnumByte);
            rm.Dispose();
        }

        [Test]
        public void CouldSetTypeEnumInBinary()
        {
#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var rm = BufferPool.Retain(10_000);
            var db = new DirectBuffer(rm.Span);

            var value = new SampleStruct(42);

            var size = BinarySerializer.SizeOf(in value, out var tmpBuffer, SerializationFormat.Binary);
            var written = BinarySerializer.Write(in value, db, tmpBuffer, SerializationFormat.Binary);

            Assert.AreEqual(size, written);

            var typeEnumByte = db[1];

            Assert.AreEqual((byte)TypeEnum.UserType, typeEnumByte);
            rm.Dispose();
        }
    }
}
