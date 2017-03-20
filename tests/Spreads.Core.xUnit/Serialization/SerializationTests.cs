// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Xunit;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Spreads.Blosc;

namespace Spreads.Core.Tests.Serialization
{
    public class SerializationTests
    {
        [Serialization(BlittableSize = 12)]
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BlittableStruct
        {
            public int Value1;
            public long Value2;
        }

        public class SimplePoco : IEquatable<SimplePoco>
        {
            public int Value1;
            public string Value2;

            public bool Equals(SimplePoco other)
            {
                return this.Value1 == other.Value1 && this.Value2 == other.Value2;
            }
        }

        [Fact]
        public void CouldNotPinDateTimeArray()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var dta = new DateTime[2];
                GCHandle.Alloc(dta, GCHandleType.Pinned);
            });
        }

        [Fact]
        public void CouldPinDecimalArray()
        {
            var dta = new decimal[2];
            var handle = GCHandle.Alloc(dta, GCHandleType.Pinned);
            handle.Free();
        }

        [Fact]
        public void CouldSerializeDateTimeArray()
        {
            var bytes = new byte[1000];
            var dta = new DateTime[2];
            dta[0] = DateTime.Today;
            dta[1] = DateTime.Today.AddDays(1);
            var len = BinarySerializer.Write(dta, bytes);
            Assert.Equal(8 + 8 * 2, len);
            DateTime[] dta2 = null;
            var len2 = BinarySerializer.Read(bytes, ref dta2);
            Assert.Equal(len, len2);
            Assert.True(dta.SequenceEqual(dta2));
        }

        [Fact]
        public void CouldSerializeIntArray()
        {
            var bytes = new byte[1000];
            var ints = new int[2];
            ints[0] = 123;
            ints[1] = 456;
            var len = BinarySerializer.Write(ints, bytes);
            Assert.Equal(8 + 4 * 2, len);
            int[] ints2 = null;
            var len2 = BinarySerializer.Read(bytes, ref ints2);
            Assert.Equal(len, len2);
            Assert.True(ints.SequenceEqual(ints2));
        }

        [Fact]
        public void CouldSerializeDecimalArray()
        {
            var bytes = new byte[1000];
            var decimals = new decimal[2];
            decimals[0] = 123;
            decimals[1] = 456;
            var len = BinarySerializer.Write(decimals, bytes);
            Assert.Equal(8 + 16 * 2, len);
            decimal[] decimals2 = null;
            var len2 = BinarySerializer.Read(bytes, ref decimals2);
            Assert.Equal(len, len2);
            Assert.True(decimals.SequenceEqual(decimals2));
        }

        [Fact]
        public void CouldSerializeStringArray()
        {
            var bytes = new byte[1000];
            var arr = new string[2];
            arr[0] = "123";
            arr[1] = "456";
            var len = BinarySerializer.Write(arr, bytes);

            string[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, ref arr2);
            Assert.Equal(len, len2);
            Assert.True(arr.SequenceEqual(arr2));
        }

        [Fact]
        public void CouldSerializeBlittableStructArray()
        {
            var bytes = new byte[1000];
            var arr = new BlittableStruct[2];
            arr[0] = new BlittableStruct
            {
                Value1 = 123,
                Value2 = 1230
            };
            arr[1] = new BlittableStruct
            {
                Value1 = 456,
                Value2 = 4560
            };
            var len = BinarySerializer.Write(arr, bytes);
            Assert.Equal(8 + 12 * 2, len);
            BlittableStruct[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, ref arr2);
            Assert.Equal(len, len2);
            Assert.True(arr.SequenceEqual(arr2));
        }

        [Fact]
        public void CouldSerializePocoArray()
        {
            var bytes = new byte[1000];
            var arr = new SimplePoco[2];
            arr[0] = new SimplePoco
            {
                Value1 = 123,
                Value2 = "1230"
            };
            arr[1] = new SimplePoco
            {
                Value1 = 456,
                Value2 = "4560"
            };
            var len = BinarySerializer.Write(arr, bytes);
            SimplePoco[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, ref arr2);
            Assert.Equal(len, len2);
            Assert.True(arr.SequenceEqual(arr2));
        }

        [Fact]
        public void CouldSerializeString()
        {
            var bytes = new byte[1000];
            var str = "This is string";
            var len = BinarySerializer.Write(str, bytes);
            string str2 = null;
            var len2 = BinarySerializer.Read(bytes, ref str2);
            Assert.Equal(len, len2);
            Assert.Equal(str, str2);
        }

        [Fact]
        public void JsonWorksWithArraySegment()
        {
            var ints = new int[4] { 1, 2, 3, 4 };
            var segment = new ArraySegment<int>(ints, 1, 2);
            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(segment);
            Console.WriteLine(serialized);
            var newInts = Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>(serialized);
            Assert.Equal(2, newInts[0]);
            Assert.Equal(3, newInts[1]);

            var bsonBytes = BinarySerializer.Json.Serialize(segment);
            var newInts2 = BinarySerializer.Json.Deserialize<int[]>(bsonBytes);

            Assert.Equal(2, newInts2[0]);
            Assert.Equal(3, newInts2[1]);
        }

        [Fact]
        public void CouldUseBloscCompression()
        {

            var rng = new Random();
            var ptr = Marshal.AllocHGlobal(1000000);
            var db = new DirectBuffer(1000000, ptr);
            var source = new decimal[10000];
            for (var i = 0; i < 10000; i++)
            {
                source[i] = i;
            }

            var len = CompressedArrayBinaryConverter<decimal>.Instance.Write(new ArraySegment<decimal>(source), ref db, 0, null,
                CompressionMethod.LZ4);

            Console.WriteLine($"Useful: {source.Length * 16}");
            Console.WriteLine($"Total: {len}");

            var destination = new ArraySegment<decimal>(new decimal[10000]);

            var len2 = CompressedArrayBinaryConverter<decimal>.Instance.Read(ptr, ref destination, false);

            Assert.True(source.SequenceEqual(destination.Array));

        }


        [Fact]
        public void CouldSerializeIMessage()
        {
            var ping = new PingMessage();
            var ms = BinarySerializer.Json.Serialize(ping);

            var ping2 = BinarySerializer.Json.Deserialize<IMessage>(ms);

            Assert.Equal("ping", ping2.Type);
            Assert.Equal(ping.Id, ping2.Id);
        }
    }
}