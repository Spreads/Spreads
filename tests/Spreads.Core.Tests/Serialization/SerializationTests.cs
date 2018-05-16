// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Blosc;
using Spreads.Collections;
using Spreads.Serialization;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
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

        [Test]
        [ExpectedException(typeof(System.ArgumentException))]
        public void CouldNotPinDateTimeArray()
        {
            var dta = new DateTime[2];
            GCHandle.Alloc(dta, GCHandleType.Pinned);
        }

        [Test]
        public void CouldPinDecimalArray()
        {
            var dta = new decimal[2];
            var handle = GCHandle.Alloc(dta, GCHandleType.Pinned);
            handle.Free();
        }

        [Test]
        public void CouldSerializeDateTimeArray()
        {
            var bytes = new byte[1000];
            var dta = new DateTime[2];
            dta[0] = DateTime.Today;
            dta[1] = DateTime.Today.AddDays(1);
            var len = BinarySerializer.Write(dta, bytes);
            Assert.AreEqual(8 + 8 * 2, len);
            DateTime[] dta2 = null;
            var len2 = BinarySerializer.Read(bytes, out dta2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(dta.SequenceEqual(dta2));
        }

        [Test]
        public void CouldSerializeIntArray()
        {
            var bytes = new byte[1000];
            var ints = new int[2];
            ints[0] = 123;
            ints[1] = 456;
            var len = BinarySerializer.Write(ints, bytes);
            Assert.AreEqual(8 + 4 * 2, len);
            int[] ints2 = null;
            var len2 = BinarySerializer.Read(bytes, out ints2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(ints.SequenceEqual(ints2));
        }

        [Test]
        public void CouldSerializeDecimalArray()
        {
            Assert.AreEqual(16, TypeHelper<decimal>.Size);
            var bytes = new byte[1000];
            var decimals = new decimal[2];
            decimals[0] = 123;
            decimals[1] = 456;
            var len = BinarySerializer.Write(decimals, bytes);
            Assert.AreEqual(8 + 16 * 2, len);
            decimal[] decimals2 = null;
            var len2 = BinarySerializer.Read(bytes, out decimals2);
            Assert.IsTrue(decimals.SequenceEqual(decimals2));
            Assert.AreEqual(len, len2);
        }

        [Test]
        public void CouldSerializeStringArray()
        {
            var bytes = new byte[1000];
            var arr = new string[2];
            arr[0] = "123";
            arr[1] = "456";
            var len = BinarySerializer.Write(arr, bytes);

            string[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, out arr2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(arr.SequenceEqual(arr2));
        }

        [Test]
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
            Assert.AreEqual(8 + 12 * 2, len);
            BlittableStruct[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, out arr2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(arr.SequenceEqual(arr2));
        }

        [Test]
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
            var len2 = BinarySerializer.Read(bytes, out arr2);
            Assert.IsTrue(arr.SequenceEqual(arr2), "Items are not equal");
            Assert.AreEqual(len, len2);
        }

        [Test]
        public void CouldSerializeString()
        {
            var bytes = new byte[1000];
            var str = "This is string";
            var len = BinarySerializer.Write(str, bytes);
            string str2 = null;
            var len2 = BinarySerializer.Read(bytes, out str2);
            Assert.AreEqual(len, len2);
            Assert.AreEqual(str, str2);
        }

        [Test]
        public void JsonWorksWithArraySegment()
        {
            var ints = new int[4] { 1, 2, 3, 4 };
            var segment = new ArraySegment<int>(ints, 1, 2);
            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(segment);
            Console.WriteLine(serialized);
            var newInts = Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>(serialized);
            Assert.AreEqual(2, newInts[0]);
            Assert.AreEqual(3, newInts[1]);

            var bsonBytes = BinarySerializer.Json.Serialize(segment);
            var newInts2 = BinarySerializer.Json.Deserialize<int[]>(bsonBytes);

            Assert.AreEqual(2, newInts2[0]);
            Assert.AreEqual(3, newInts2[1]);
        }

        [Test]
        public unsafe void CouldSerializeSortedMap()
        {
            SortedMap<DateTime, decimal>.Init();
            var rng = new Random();

            var dest = (Memory<byte>)new byte[1000000];
            var buffer = dest;
            var handle = buffer.Pin();
            var ptr = (IntPtr)handle.Pointer;

            var sm = new SortedMap<DateTime, decimal>();
            for (var i = 0; i < 10000; i++)
            {
                sm.Add(DateTime.Today.AddHours(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
            }
            var len = BinarySerializer.Write(sm, ref buffer, compression: CompressionMethod.Zstd);
            Console.WriteLine($"Useful: {sm.Count * 24.0}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 24.0))}");
            SortedMap<DateTime, decimal> sm2 = null;
            var len2 = BinarySerializer.Read(buffer, 0, out sm2);

            Assert.AreEqual(len, len2);

            Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
            Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        }

        [Test]
        public unsafe void CouldSerializeRegularSortedMapWithZstd()
        {
            BloscSettings.CompressionMethod = CompressionMethod.Zstd;
            var rng = new Random();

            var dest = (Memory<byte>)new byte[1000000];
            var buffer = dest;
            var handle = buffer.Pin();
            var ptr = (IntPtr)handle.Pointer;

            var sm = new SortedMap<DateTime, decimal>();
            for (var i = 0; i < 1; i++)
            {
                sm.Add(DateTime.Today.AddSeconds(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
            }

            MemoryStream tmp;
            var size = BinarySerializer.SizeOf(sm, out tmp);
            var len = BinarySerializer.Write(sm, ref buffer, 0, tmp);
            Console.WriteLine($"Useful: {sm.Count * 24}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 24.0))}");
            SortedMap<DateTime, decimal> sm2 = null;
            var len2 = BinarySerializer.Read(buffer, 0, out sm2);

            Assert.AreEqual(len, len2);

            Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
            Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        }

        [Test]
        public unsafe void CouldSerializeSortedMap2()
        {
            var rng = new Random();

            var dest = (Memory<byte>)new byte[1000000];
            var buffer = dest;
            var handle = buffer.Pin();
            var ptr = (IntPtr)handle.Pointer;

            var sm = new SortedMap<int, int>();
            for (var i = 0; i < 10000; i++)
            {
                sm.Add(i, i);
            }
            MemoryStream temp;
            var len = BinarySerializer.SizeOf(sm, out temp);
            var len2 = BinarySerializer.Write(sm, ref buffer, 0, temp);
            Assert.AreEqual(len, len2);
            Console.WriteLine($"Useful: {sm.Count * 8}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 8.0))}");
            SortedMap<int, int> sm2 = null;
            var len3 = BinarySerializer.Read(buffer, 0, out sm2);

            Assert.AreEqual(len, len3);

            Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
            Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        }

        [Test]
        public void CouldSerializeIMessage()
        {
            var ping = new PingMessage();
            var ms = BinarySerializer.Json.Serialize(ping);

            var ping2 = BinarySerializer.Json.Deserialize<IMessage>(ms);

            Assert.AreEqual("ping", ping2.Type);
            Assert.AreEqual(ping.Id, ping2.Id);
        }
    }
}