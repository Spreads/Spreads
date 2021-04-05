// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BinarySerializer = Spreads.Serialization.BinarySerializer;
using SerializationFormat = Spreads.Serialization.SerializationFormat;

namespace Spreads.Core.Tests.Serialization
{
    [Category("Serialization")]
    [Category("CI")]
    [TestFixture]
    public unsafe class SerializationTests
    {
        public static SerializationFormat[] Formats =
            Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>().ToArray();

        [BinarySerialization(blittableSize: 12)]
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
                return Value1 == other.Value1 && Value2 == other.Value2;
            }
        }

        [Test]
        public void CouldPinDecimalArray()
        {
            var dta = new decimal[2];
            var handle = GCHandle.Alloc(dta, GCHandleType.Pinned);
            handle.Free();
        }

        [Test]
        public void VersionAndFlags()
        {
            var vf = new VersionAndFlags();
            vf.SerializationFormat = SerializationFormat.JsonGZip;

            Assert.AreEqual(SerializationFormat.JsonGZip, vf.SerializationFormat);
            Assert.AreEqual(CompressionMethod.GZip, vf.CompressionMethod);
        }

        [Test]
        public void CouldSerializeDoubles()
        {
            var count = 100000;
            for (int i = 0; i < count; i++)
            {
                var val = (double) i / (i + 1);

                var str = JsonSerializer.ToJsonString(val);

                var bytes = Encoding.UTF8.GetBytes(str);
                var mem = (Memory<byte>) bytes;
                var h = mem.Pin();
                var db = new DirectBuffer(bytes.Length, (byte*) h.Pointer);
                var reader = new JsonReader(db);

                var des = JsonSerializer.Deserialize<double>(ref reader);

                if (reader.GetCurrentOffsetUnsafe() != bytes.Length)
                {
                    Console.WriteLine($"reader.GetCurrentOffsetUnsafe() {reader.GetCurrentOffsetUnsafe()} != bytes.Length {bytes.Length}");
                }

                if (des != val) // Math.Abs(des - val) > 0.00000000000001)
                {
                    Console.WriteLine($"des {des} != val {val}");
                }
            }
        }

        public void CouldSerializeBlittable<T>(T value) where T : IEquatable<T>
        {
            var bytes = BufferPool<byte>.Rent(1000);
            var mem = (Memory<byte>) bytes;
            var h = mem.Pin();
            var db = new DirectBuffer(bytes.Length, (byte*) h.Pointer);

            foreach (var format in Formats)
            {
                var val = value;
                var len0 = BinarySerializer.SizeOf(in val, out var pl, format);
                var len = BinarySerializer.Write(val, db, pl, format);
                // for (int x = 0; x < 100; x++)
                {
                    var len2 = BinarySerializer.Read(db, out T val2);

                    if (len != len2)
                    {
                        Assert.Fail($"len {len} != len2 {len2}");
                    }

                    if (!val.Equals(val2))
                    {
                        Assert.Fail();
                    }
                }
            }

            foreach (var format in Formats)
            {
                var val = value;

                var len0 = BinarySerializer.SizeOf(in val, out var segment, format);

                var len1 = BinarySerializer.Write(in val, db, segment, format);

                if (len0 != len1)
                {
                    Assert.Fail($"len0 {len0} != len1 {len1}");
                }

                //Assert.IsTrue(len1 > 8);
                var len2 = BinarySerializer.Read(db, out T val2);
                if (len1 != len2)
                {
                    Assert.Fail($"len1 {len1} != len2 {len2}");
                }

                if (!val.Equals(val2))
                {
                    Assert.Fail();
                }
            }

            h.Dispose();
            BufferPool<byte>.Return(bytes);
        }

        [Test]
        public void CouldSerializeBlittables()
        {
            Settings.DoAdditionalCorrectnessChecks = true;

            CouldSerializeBlittable(0L);
            CouldSerializeBlittable(DateTime.Today);
            CouldSerializeBlittable(default(DateTime));
            CouldSerializeBlittable(DateTime.MinValue);
            CouldSerializeBlittable(DateTime.MaxValue);
            CouldSerializeBlittable(DateTime.Today.AddSeconds(1));

            CouldSerializeBlittable(TimeService.Default.CurrentTime);
            CouldSerializeBlittable(default(Timestamp));
            CouldSerializeBlittable(new Timestamped<int>(DateTime.UtcNow, 123));
            CouldSerializeBlittable((Timestamp) long.MaxValue);

            CouldSerializeBlittable(0.0);

            var count = 10;
            using (Benchmark.Run("CouldSerializeBlittables", count * (2 * 2 * 8) * 12))
            {
                for (int i = 0; i < count; i++)
                {
                    var x = i; //  % 100;

                    CouldSerializeBlittable(DateTime.Today);
                    CouldSerializeBlittable(default(DateTime));
                    CouldSerializeBlittable(DateTime.MinValue);
                    CouldSerializeBlittable(DateTime.MaxValue);
                    CouldSerializeBlittable(DateTime.Today.AddSeconds(x));

                    CouldSerializeBlittable(TimeService.Default.CurrentTime);
                    CouldSerializeBlittable(default(Timestamp));
                    CouldSerializeBlittable((Timestamp) long.MaxValue);

                    CouldSerializeBlittable(i);
                    CouldSerializeBlittable((short) i);
                    CouldSerializeBlittable((long) i);

                    CouldSerializeBlittable((double) x / (double) (x + 1)); // / (double)(x + 1));
                    // CouldSerializeBlittable((decimal)x / (decimal)(x + 1)); // / (double)(x + 1));
                }

                //CouldSerializeBlittable(default(double));
                //CouldSerializeBlittable(double.MinValue);
                // CouldSerializeBlittable(double.MaxValue);
            }
        }

        [Test]
        public void CouldSerializeDateTimeArrayAsJson()
        {
            // not compressed even when requested

            var formats = new[]
            {
                SerializationFormat.Json,
                SerializationFormat.JsonGZip,
                SerializationFormat.JsonLz4,
                SerializationFormat.JsonZstd
            };

            var lens = new[] {1, 2, 10, 20, 50, 100, 200, 300, 400, 500, 600, 700, 1000, 10000};
            var rng = new Random(42);
            foreach (var len in lens)
            {
                var dta = new DateTime[len];
                for (int l = 0; l < len; l++)
                {
                    dta[l] = DateTime.Today.ToUniversalTime().AddMinutes(l).AddMilliseconds(rng.Next(0, 1000));
                }

                var uncompressed = 0.0;
                Console.WriteLine("---------------");

                foreach (var serializationFormat in formats)
                {
                    var sizeOf = BinarySerializer.SizeOf(dta, out var segment, serializationFormat);
                    var bytes = BufferPool<byte>.Rent(4 + sizeOf);
                    var written = BinarySerializer.Write(dta, bytes, segment, serializationFormat);

                    if (serializationFormat == SerializationFormat.Json)
                    {
                        uncompressed = written;
                    }

                    Console.WriteLine(
                        $"len: {len}, format: {serializationFormat}, written bytes: {written}, ratio: {Math.Round(uncompressed / written, 2)}");

                    if (sizeOf != written)
                    {
                        Assert.Fail($"sizeOf {sizeOf} != written {written}");
                    }

                    DateTime[] dta2 = null;
                    var read = BinarySerializer.Read(bytes, out dta2);
                    Assert.AreEqual(written, read);
                    Assert.IsTrue(dta.SequenceEqual(dta2));
                }
            }
        }

        [Test]
        public void CouldSerializeTimestampArrayAsJson()
        {
            // not compressed even when requested

            var formats = new[] //
            {
                SerializationFormat.Json,
                SerializationFormat.JsonGZip,
                SerializationFormat.JsonLz4,
                SerializationFormat.JsonZstd
            };

            var lens = new[] {1, 2, 10, 20, 50, 100, 200, 300, 400, 500, 600, 700, 1000, 10000, 100000};
            var rng = new Random(42);
            foreach (var len in lens)
            {
                var dta = new Timestamp[len];
                for (int l = 0; l < len; l++)
                {
                    dta[l] = DateTime.Today.ToUniversalTime().AddMinutes(1).AddMilliseconds(rng.Next(0, 1000));
                }

                var uncompressed = 0.0;
                Console.WriteLine("---------------");
                foreach (var serializationFormat in formats)
                {
                    var so = BinarySerializer.SizeOf(dta, out var segment, preferredFormat: serializationFormat);
                    var bytes = BufferPool<byte>.Rent(so);
                    var written = BinarySerializer.Write(dta, bytes, segment, format: serializationFormat);

                    if (serializationFormat == SerializationFormat.Json)
                    {
                        uncompressed = written;
                    }

                    Console.WriteLine($"len: {len}, format: {serializationFormat}, written bytes: {written}, ratio: {Math.Round(uncompressed / written, 2)}");

                    if (so != written)
                    {
                        Assert.Fail($"so {so} != written {written}");
                    }

                    Timestamp[] dta2 = null;
                    var read = BinarySerializer.Read(bytes, out dta2);
                    Assert.AreEqual(written, read);
                    Assert.IsTrue(dta.SequenceEqual(dta2));
                }
            }
        }

        [Test]
        public void CouldSerializeDateTimeArray()
        {
            var bytes = new byte[1000];
            var dta = new DateTime[2];
            dta[0] = DateTime.Today;
            dta[1] = DateTime.Today.AddDays(1);
            var len0 = BinarySerializer.SizeOf(in dta, out var pl, SerializationFormat.Binary);
            var len = BinarySerializer.Write(dta, bytes, pl, format: SerializationFormat.Binary);
            Assert.AreEqual(8 + 4 + 8 * 2, len);
            DateTime[] dta2 = null;
            var len2 = BinarySerializer.Read(bytes, out dta2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(dta.SequenceEqual(dta2));
        }

        [Test]
        public void CouldSerializeIntArray()
        {
            var bytes = new byte[1000];
            var value = new int[2];
            value[0] = 123;
            value[1] = 456;
            var len0 = BinarySerializer.SizeOf(in value, out var pl, SerializationFormat.Binary);
            var len = BinarySerializer.Write(in value, bytes, pl, format: SerializationFormat.Binary);
            Assert.AreEqual(len0, len);
            Assert.AreEqual(8 + 4 + 4 * 2, len);
            int[] ints2 = null;
            var len2 = BinarySerializer.Read(bytes, out ints2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(value.SequenceEqual(ints2));
        }

        [Test]
        public void CouldSerializeDecimalArray()
        {
            Assert.AreEqual(16, TypeHelper<decimal>.FixedSize);
            var bytes = new byte[1000];
            var value = new decimal[2];
            value[0] = 123;
            value[1] = 456;
            var len0 = BinarySerializer.SizeOf(in value, out var pl, SerializationFormat.Binary);
            var len = BinarySerializer.Write(value, bytes, pl, format: SerializationFormat.Binary);
            Assert.AreEqual(8 + 4 + 16 * 2, len);
            decimal[] decimals2 = null;
            var len2 = BinarySerializer.Read(bytes, out decimals2);
            Assert.IsTrue(value.SequenceEqual(decimals2));
            Assert.AreEqual(len, len2);
        }

        [Test]
        public void CouldSerializeStringArray()
        {
            var bytes = new byte[1000];
            var value = new string[2];
            value[0] = "123";
            value[1] = "456";
            var len0 = BinarySerializer.SizeOf(in value, out var pl);
            var len = BinarySerializer.Write(value, bytes, pl);

            string[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, out arr2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(value.SequenceEqual(arr2));
        }

        [Test]
        public void CouldSerializeBlittableStructArray()
        {
            var bytes = new byte[1000];
            var value = new BlittableStruct[2];
            value[0] = new BlittableStruct
            {
                Value1 = 123,
                Value2 = 1230
            };
            value[1] = new BlittableStruct
            {
                Value1 = 456,
                Value2 = 4560
            };
            var len0 = BinarySerializer.SizeOf(in value, out var pl, SerializationFormat.Binary);
            var len = BinarySerializer.Write(value, bytes, pl, format: SerializationFormat.Binary);
            Assert.AreEqual(8 + 4 + 12 * 2, len);
            BlittableStruct[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, out arr2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(value.SequenceEqual(arr2));
        }

        [Test]
        public void CouldSerializePocoArray()
        {
            var bytes = new byte[1000];
            var value = new SimplePoco[2];
            value[0] = new SimplePoco
            {
                Value1 = 123,
                Value2 = "1230"
            };
            value[1] = new SimplePoco
            {
                Value1 = 456,
                Value2 = "4560"
            };
            var len0 = BinarySerializer.SizeOf(in value, out var pl);
            var len = BinarySerializer.Write(value, bytes, pl);
            SimplePoco[] arr2 = null;
            var len2 = BinarySerializer.Read(bytes, out arr2);
            Assert.IsTrue(value.SequenceEqual(arr2), "Items are not equal");
            Assert.AreEqual(len, len2);
        }

        [Test]
        public void JsonWorksWithArraySegment()
        {
            var ints = new int[4] {1, 2, 3, 4};
            var segment = new ArraySegment<int>(ints, 1, 2);
            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(segment);
            Console.WriteLine(serialized);
            var newInts = Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>(serialized);
            Assert.AreEqual(2, newInts[0]);
            Assert.AreEqual(3, newInts[1]);

            var bsonBytes = JsonSerializer.Serialize(segment);
            var newInts2 = JsonSerializer.Deserialize<int[]>(bsonBytes);

            Assert.AreEqual(2, newInts2[0]);
            Assert.AreEqual(3, newInts2[1]);
        }

        //[Test, Ignore("not implemented")]
        //public unsafe void CouldSerializeSortedMap()
        //{
        //    Series<DateTime, decimal>.Init();
        //    var rng = new Random();

        //    var dest = (Memory<byte>)new byte[1000000];
        //    var buffer = dest;
        //    var handle = buffer.Pin();
        //    var ptr = (IntPtr)handle.Pointer;

        //    var sm = new Series<DateTime, decimal>();
        //    for (var i = 0; i < 10000; i++)
        //    {
        //        if (i != 2)
        //        {
        //            sm.Add(DateTime.Today.AddHours(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
        //        }
        //    }
        //    var len = BinarySerializer.Write(sm, buffer, format: SerializationFormat.BinaryZstd);
        //    Console.WriteLine($"Useful: {sm.Count * 24.0}");
        //    Console.WriteLine($"Total: {len}");
        //    // NB interesting that with converting double to decimal savings go from 65% to 85%,
        //    // even calculated from (8+8) base size not decimal's 16 size
        //    Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 24.0))}");
        //    Series<DateTime, decimal> sm2 = null;
        //    var len2 = BinarySerializer.Read(buffer, out sm2);

        //    Assert.AreEqual(len, len2);

        //    Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
        //    Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        //}

        // [Test, Ignore("SM not implemented")]
        // public unsafe void CouldSerializeRegularSortedMapWithZstd()
        // {
        //     var rng = new Random();
        //
        //     var dest = (Memory<byte>)new byte[1000000];
        //     var buffer = dest;
        //     var handle = buffer.Pin();
        //     var ptr = (IntPtr)handle.Pointer;
        //
        //     var sm = new MutableSeries<DateTime, decimal>();
        //     for (var i = 0; i < 1000; i++)
        //     {
        //         sm.Add(DateTime.Today.AddSeconds(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
        //     }
        //
        //     var sizeOf = BinarySerializer.SizeOf(sm, out var tmp);
        //     var written = BinarySerializer.Write(sm, dest.Span, tmp);
        //     Assert.AreEqual(sizeOf, written);
        //     Console.WriteLine($"Useful: {sm.RowCount * 24}");
        //     Console.WriteLine($"Total: {written}");
        //     // NB interesting that with converting double to decimal savings go from 65% to 85%,
        //     // even calculated from (8+8) base size not decimal's 16 size
        //     Console.WriteLine($"Savings: {1.0 - ((written * 1.0) / (sm.RowCount * 24.0))}");
        //     Series<DateTime, decimal> sm2 = null;
        //     var len2 = BinarySerializer.Read(buffer.Span, out sm2);
        //
        //     Assert.AreEqual(written, len2);
        //
        //     Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
        //     Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        // }
        //
        // [Test, Ignore("Old SM not supported")]
        // public unsafe void CouldSerializeSortedMap2()
        // {
        //     var rng = new Random();
        //
        //     var dest = (Memory<byte>)new byte[1000000];
        //     var buffer = dest;
        //     var handle = buffer.Pin();
        //     var ptr = (IntPtr)handle.Pointer;
        //
        //     var sm = new MutableSeries<int, int>();
        //     for (var i = 0; i < 10000; i++)
        //     {
        //         sm.Add(i, i);
        //     }
        //
        //     var len = BinarySerializer.SizeOf(sm, out var temp);
        //     var len2 = BinarySerializer.Write(sm, buffer.Span, temp);
        //     Assert.AreEqual(len, len2);
        //     Console.WriteLine($"Useful: {sm.RowCount * 8}");
        //     Console.WriteLine($"Total: {len}");
        //     // NB interesting that with converting double to decimal savings go from 65% to 85%,
        //     // even calculated from (8+8) base size not decimal's 16 size
        //     Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.RowCount * 8.0))}");
        //     Series<int, int> sm2 = null;
        //     var len3 = BinarySerializer.Read(buffer.Span, out sm2);
        //
        //     Assert.AreEqual(len, len3);
        //
        //     Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
        //     Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        // }

        // [Test, Ignore("Old SM not supported")]
        // public void CouldSerializeSortedMapWithStrings()
        // {
        //     var rng = new Random();
        //
        //     var dest = (Memory<byte>)new byte[10000000];
        //     var buffer = dest;
        //     var handle = buffer.Pin();
        //     var ptr = (IntPtr)handle.Pointer;
        //
        //     var valueLens = 0;
        //     var sm = new MutableSeries<int, string>();
        //     for (var i = 0; i < 100000; i++)
        //     {
        //         var str = i.ToString();
        //         valueLens += str.Length;
        //         sm.Add(i, str);
        //     }
        //
        //     var len = BinarySerializer.SizeOf(sm, out var temp);
        //     var len2 = BinarySerializer.Write(sm, buffer.Span, temp);
        //     Assert.AreEqual(len, len2);
        //     var usefulLen = ((int)sm.RowCount * 4) + valueLens;
        //     Console.WriteLine($"Useful: {usefulLen}");
        //     Console.WriteLine($"Total: {len}");
        //     // NB interesting that with converting double to decimal savings go from 65% to 85%,
        //     // even calculated from (8+8) base size not decimal's 16 size
        //     Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (usefulLen))}");
        //     Series<int, string> sm2 = null;
        //     var len3 = BinarySerializer.Read(buffer.Span, out sm2);
        //
        //     Assert.AreEqual(len, len3);
        //
        //     Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
        //     Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        // }

        [Test]
        public void CouldUseCompression()
        {
            // this also tests write without SizeOf, but only happy path
            // TODO unhappy path with corrupted data and small buffers

            var formats = new[]
            {
                SerializationFormat.BinaryZstd, SerializationFormat.Binary, SerializationFormat.BinaryLz4,
                SerializationFormat.BinaryGZip,
                SerializationFormat.Json, SerializationFormat.JsonGZip, SerializationFormat.JsonLz4,
                SerializationFormat.JsonZstd
            };
            foreach (var serializationFormat in formats)
            {
                Console.WriteLine(serializationFormat);

                var count = 10000;
                var rng = new Random(42);
                Memory<byte> bytes = new byte[count * 16 + 20];
                var source = new decimal[count];
                source[0] = 123.4567M;
                for (var i = 1; i < count; i++)
                {
                    source[i] = source[i - 1] + Math.Round((decimal) rng.NextDouble() * 10, 4);
                }

                var len = BinarySerializer.Write(source, bytes.Span, default,
                    serializationFormat);

                Console.WriteLine($"Useful: {source.Length * 16}");
                Console.WriteLine($"Total: {len}");

                decimal[] destination = null;

                var len2 = BinarySerializer.Read(bytes.Span, out destination);

                Console.WriteLine("len2: " + len2);
                Console.WriteLine(destination.Length.ToString());

                Assert.True(source.SequenceEqual(destination));
            }
        }

        public struct Dummy
        {
            public long ValL;
            public string ValS;
            public Timestamp Timestamp;
        }

        [Test]
        public void CouldSerializeTimestampAsAFieldDummyStruct()
        {
            var ptr = Marshal.AllocHGlobal(1000);
            var db = new DirectBuffer(1000, (byte*) ptr);
            var val = new Dummy()
            {
                Timestamp = TimeService.Default.CurrentTime,
                ValL = 123,
                ValS = "foo"
            };
            var ts = val.Timestamp;

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var serializationFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var stream, serializationFormat);
                var written = BinarySerializer.Write(val, db, stream, serializationFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out Dummy val2);

                Assert.AreEqual(sizeOf, consumed);
                Assert.AreEqual(val.Timestamp, val2.Timestamp);
                Assert.AreEqual(val.ValL, val2.ValL);
                Assert.AreEqual(val.ValS, val2.ValS);
            }

            var str = JsonSerializer.ToJsonString(val);
            Console.WriteLine(str);

            var str2 = JsonSerializer.ToJsonString(val.Timestamp);
            Console.WriteLine(str2);
        }

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        public struct DummyBlittable
        {
            public long ValL;
            public Timestamp Timestamp;
        }

        [Test]
        public void CouldSerializeTimestampAsAFieldDummyBlittableStruct()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new DummyBlittable()
            {
                Timestamp = TimeService.Default.CurrentTime,
                ValL = 123,
            };

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>().ToArray();

            foreach (var serializationFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var stream, serializationFormat);
                var written = BinarySerializer.Write(val, db, stream, serializationFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out DummyBlittable val2);

                Assert.AreEqual(sizeOf, consumed);
                Assert.AreEqual(val.Timestamp, val2.Timestamp);
                Assert.AreEqual(val.ValL, val2.ValL);
            }

            var str = JsonSerializer.ToJsonString(val);
            Console.WriteLine(str);

            var str2 = JsonSerializer.ToJsonString(val.Timestamp);
            Console.WriteLine(str2);

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeString()
        {
            var bytes = new byte[1000];
            var value = "This is string";

            var len0 = BinarySerializer.SizeOf(in value, out var pl);

            // Assert.AreEqual(DataTypeHeader.Size + BinarySerializer.PayloadLengthSize + value.Length, len0);

            var len = BinarySerializer.Write(value, bytes, pl);

            var len2 = BinarySerializer.Read(bytes, out string str2);
            Assert.AreEqual(len, len0);
            Assert.AreEqual(len, len2);
            Assert.AreEqual(value, str2);
        }

        [Test]
        public void CouldSerializeString2()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = "bar";

            var serializationFormats = new[] {SerializationFormat.Json}; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>(); // TODO

            foreach (var serializationFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var stream, serializationFormat);
                var written = BinarySerializer.Write(val, db, stream, serializationFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out string val2);

                Assert.AreEqual(sizeOf, consumed);
                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test]
        public void CouldSerializePrimitive()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = "bar";

            var serializationFormats = new[] {SerializationFormat.Json}; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var serializationFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(in val, out var stream, serializationFormat);
                var written = BinarySerializer.Write(in val, db, stream, serializationFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out string val2);

                Assert.AreEqual(sizeOf, consumed);
                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test]
        public void CouldSerializePrimitiveArrayWithTimeStamp()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new[] {1, 2, 3};
            var ts = TimeService.Default.CurrentTime;

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>().ToArray();

            foreach (var serializationFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var stream, serializationFormat);
                var written = BinarySerializer.Write(val, db, stream, serializationFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out int[] val2);

                Assert.AreEqual(sizeOf, consumed);
                Assert.IsTrue(val.SequenceEqual(val2));
            }

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeDummyArray()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new[] {new Dummy() {ValL = 1}, new Dummy() {ValL = 2}};
            var ts = TimeService.Default.CurrentTime;

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>().ToArray();

            foreach (var serializationFormat in serializationFormats)
            {
                var len = BinarySerializer.SizeOf(val, out var stream, serializationFormat);
                var len2 = BinarySerializer.Write(val, db, stream, serializationFormat);

                Assert.AreEqual(len, len2);

                var len3 = BinarySerializer.Read(db, out Dummy[] val2);

                Assert.AreEqual(len2, len3);
                Assert.IsTrue(val.SequenceEqual(val2));
            }

            rm.Dispose();
        }
    }
}