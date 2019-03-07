// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Serialization.Experimental;
using Spreads.Serialization.Utf8Json;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

        public static Timestamp[] Tss = new[] { default(Timestamp), TimeService.Default.CurrentTime };

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
                return this.Value1 == other.Value1 && this.Value2 == other.Value2;
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

            vf.ConverterVersion = 3;
            Assert.AreEqual(3, vf.ConverterVersion);
        }

        [Test]
        public void CouldSerializeDoubles()
        {
            var count = 100000;
            for (int i = 0; i < count; i++)
            {
                var val = (double)i / (i + 1);

                var str = JsonSerializer.ToJsonString(val);

                var bytes = Encoding.UTF8.GetBytes(str);
                var mem = (Memory<byte>)bytes;
                var h = mem.Pin();
                var db = new DirectBuffer(bytes.Length, (byte*)h.Pointer);
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

        [Test]
        public void CouldSerializeOrderBag()
        {
            QuoteDecimal[] bids = new QuoteDecimal[10000];

            for (int i = 0; i < bids.Length; i++)
            {
                bids[i] = new QuoteDecimal(new SmallDecimal(15000000.45, 8), new SmallDecimal((double)i, 8));
            }

            QuoteDecimal[] asks = new QuoteDecimal[2];
            asks[0] = new QuoteDecimal(new SmallDecimal(123.89, 2), new SmallDecimal(0.00159748M, 8));
            asks[1] = new QuoteDecimal(new SmallDecimal(123.99M, 2), new SmallDecimal(0.00128948M, 8));

            var ob = new OrderBagDecimal(bids, asks);
            var ts = TimeService.Default.CurrentTime;

            var size = BinarySerializer.SizeOf(ob, out var segment, SerializationFormat.Json, ts);

            var rm = BufferPool.Retain(size);

            var written = BinarySerializer.Write(ob, rm.Memory, segment, SerializationFormat.Json, ts);

            Assert.AreEqual(size, written);

            var str = JsonSerializer.ToJsonString(ob);

            var ob2 = JsonSerializer.Deserialize<OrderBagDecimal>(str);

            Console.WriteLine(str);

            // var ob2 = JsonSerializer.Deserialize<OrderBagX>(str);

            rm.Dispose();
        }

        [Test]
        public unsafe void CouldSerializeTickDecimal()
        {
            var count = 1_00;
            var rounds = 1;
            var rm = BufferPool.Retain(512);
            var db = new DirectBuffer(rm.Length, (byte*)rm.Pointer);

            var formats = new[] { SerializationFormat.Binary, SerializationFormat.Json };

            for (int r = 0; r < rounds; r++)
            {
                foreach (var serializationFormat in formats)
                {
                    using (Benchmark.Run(serializationFormat.ToString(), count, true))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var tick = new TickDecimal((Timestamp)(long)(i + 1), new SmallDecimal((double)i, 8), new SmallDecimal((double)i, 8), i % 3 - 1, i);

                            var size = BinarySerializer.SizeOf(in tick, out var segment, serializationFormat);
                            if (serializationFormat == SerializationFormat.Binary && size != 32 + 4)
                            {
                                Assert.Fail("size != 32");
                            }

                            var written = BinarySerializer.Write(tick, ref db, segment, serializationFormat);

                            if (size != written)
                            {
                                Assert.Fail("size != written");
                            }

                            var readSource = db.Slice(0, written);
                            var readSize = BinarySerializer.Read<TickDecimal>(ref readSource, out var tick1);

                            if (readSize != written)
                            {
                                Assert.Fail("readSize != written");
                            }

                            if (!tick1.Equals(tick))
                            {
                                Assert.Fail("!tick1.Equals(tick)");
                            }
                        }
                    }
                }
            }
            Benchmark.Dump();

            var tick2 = new TickDecimal(TimeService.Default.CurrentTime, new SmallDecimal((double)123.45, 8), new SmallDecimal((double)56.789, 8), 123, 7894561253);
            var str = JsonSerializer.ToJsonString(tick2);

            Console.WriteLine(str);
            rm.Dispose();
        }

        public void CouldSerializeBlittable<T>(T value) where T : IEquatable<T>
        {
            var bytes = BufferPool<byte>.Rent(1000);
            var mem = (Memory<byte>)bytes;
            var h = mem.Pin();
            var db = new DirectBuffer(bytes.Length, (byte*)h.Pointer);

            foreach (var ts in Tss)
            {
                foreach (var format in Formats)
                {
                    var val = value;
                    var len = BinarySerializer.Write(val, ref db, format: format);
                    // for (int x = 0; x < 100; x++)
                    {
                        var len2 = BinarySerializer.Read(ref db, out T val2);
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

                    var len0 = BinarySerializer.SizeOf(val, out var segment, format, ts);

                    var len1 = BinarySerializer.Write(val, ref db, segment, format, ts);
                    if (len0 != len1)
                    {
                        Assert.Fail($"len0 {len0} != len1 {len1}");
                    }
                    //Assert.IsTrue(len1 > 8);
                    var len2 = BinarySerializer.Read(ref db, out T val2, out var ts2);
                    if (len1 != len2)
                    {
                        Assert.Fail($"len1 {len1} != len2 {len2}");
                    }

                    if (!val.Equals(val2))
                    {
                        Assert.Fail();
                    }

                    if (ts != ts2)
                    {
                        Assert.Fail();
                    }
                }
            }

            h.Dispose();
            BufferPool<byte>.Return(bytes);
        }

        [Test]
        public void CouldSerializeBlittables()
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            CouldSerializeBlittable(DateTime.Today);
            CouldSerializeBlittable(default(DateTime));
            CouldSerializeBlittable(DateTime.MinValue);
            CouldSerializeBlittable(DateTime.MaxValue);
            CouldSerializeBlittable(DateTime.Today.AddSeconds(1));

            CouldSerializeBlittable(TimeService.Default.CurrentTime);
            CouldSerializeBlittable(default(Timestamp));
            CouldSerializeBlittable((Timestamp)long.MaxValue);

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
                    CouldSerializeBlittable((Timestamp)long.MaxValue);

                    CouldSerializeBlittable(i);
                    CouldSerializeBlittable((short)i);
                    CouldSerializeBlittable((long)i);

                    CouldSerializeBlittable((double)x / (double)(x + 1)); // / (double)(x + 1));
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
                SerializationFormat.Json, SerializationFormat.JsonGZip, SerializationFormat.JsonLz4,
                SerializationFormat.JsonZstd
            };

            var lens = new[] { 1, 2, 10, 20, 50, 100, 200, 300, 400, 500, 600, 700, 1000, 10000 };
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
                foreach (var ts in Tss)
                {
                    Console.WriteLine($"With ts: {ts != default}");
                    foreach (var serializationFormat in formats)
                    {
                        var so = BinarySerializer.SizeOf(dta, out var segment, serializationFormat, ts);
                        var bytes = BufferPool<byte>.Rent(so);
                        var written = BinarySerializer.Write(dta, bytes, segment, serializationFormat, ts);

                        if (serializationFormat == SerializationFormat.Json)
                        {
                            uncompressed = written;
                        }

                        Console.WriteLine(
                            $"len: {len}, format: {serializationFormat}, written bytes: {written}, ratio: {Math.Round(uncompressed / written, 2)}");

                        if (so != written)
                        {
                            Assert.Fail($"so {so} != written {written}");
                        }

                        DateTime[] dta2 = null;
                        var read = BinarySerializer.Read(bytes, out dta2, out var ts2);
                        Assert.AreEqual(written, read);
                        Assert.IsTrue(dta.SequenceEqual(dta2));
                        if (ts != ts2)
                        {
                            Assert.Fail();
                        }
                    }
                }
            }
        }

        [Test]
        public void CouldSerializeTimestampArrayAsJson()
        {
            // not compressed even when requested

            var formats = new[] //
            {
                SerializationFormat.Json, SerializationFormat.JsonGZip, SerializationFormat.JsonLz4,
                SerializationFormat.JsonZstd
            };

            var lens = new[] { 1, 2, 10, 20, 50, 100, 200, 300, 400, 500, 600, 700, 1000, 10000, 100000 };
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
                    var so = BinarySerializer.SizeOf(dta, out var segment, format: serializationFormat);
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
            var len = BinarySerializer.Write(dta, bytes, format: SerializationFormat.Binary);
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
            var ints = new int[2];
            ints[0] = 123;
            ints[1] = 456;
            var len = BinarySerializer.Write(ints, bytes, format: SerializationFormat.Binary);
            Assert.AreEqual(8 + 4 + 4 * 2, len);
            int[] ints2 = null;
            var len2 = BinarySerializer.Read(bytes, out ints2);
            Assert.AreEqual(len, len2);
            Assert.IsTrue(ints.SequenceEqual(ints2));
        }

        [Test]
        public void CouldSerializeDecimalArray()
        {
            Assert.AreEqual(16, TypeHelper<decimal>.FixedSize);
            var bytes = new byte[1000];
            var decimals = new decimal[2];
            decimals[0] = 123;
            decimals[1] = 456;
            var len = BinarySerializer.Write(decimals, bytes, format: SerializationFormat.Binary);
            Assert.AreEqual(8 + 4 + 16 * 2, len);
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
            var len = BinarySerializer.Write(arr, bytes, format: SerializationFormat.Binary);
            Assert.AreEqual(8 + 4 + 12 * 2, len);
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
            var len = BinarySerializer.Write(str, bytes, timestamp: TimeService.Default.CurrentTime);
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

            var bsonBytes = JsonSerializer.Serialize(segment);
            var newInts2 = JsonSerializer.Deserialize<int[]>(bsonBytes);

            Assert.AreEqual(2, newInts2[0]);
            Assert.AreEqual(3, newInts2[1]);
        }

        [Test, Ignore("not implemented")]
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
                if (i != 2)
                {
                    sm.Add(DateTime.Today.AddHours(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
                }
            }
            var len = BinarySerializer.Write(sm, buffer, format: SerializationFormat.BinaryZstd);
            Console.WriteLine($"Useful: {sm.Count * 24.0}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 24.0))}");
            SortedMap<DateTime, decimal> sm2 = null;
            var len2 = BinarySerializer.Read(buffer, out sm2);

            Assert.AreEqual(len, len2);

            Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
            Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        }

        [Test, Ignore("SM not implemented")]
        public unsafe void CouldSerializeRegularSortedMapWithZstd()
        {
            var rng = new Random();

            var dest = (Memory<byte>)new byte[1000000];
            var buffer = dest;
            var handle = buffer.Pin();
            var ptr = (IntPtr)handle.Pointer;

            var sm = new SortedMap<DateTime, decimal>();
            for (var i = 0; i < 1000; i++)
            {
                sm.Add(DateTime.Today.AddSeconds(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
            }

            RetainedMemory<byte> tmp;
            var size = BinarySerializer.SizeOf(sm, out tmp);
            var len = BinarySerializer.Write(sm, dest, tmp);
            Console.WriteLine($"Useful: {sm.Count * 24}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 24.0))}");
            SortedMap<DateTime, decimal> sm2 = null;
            var len2 = BinarySerializer.Read(buffer, out sm2);

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
            RetainedMemory<byte> temp;
            var len = BinarySerializer.SizeOf(sm, out temp);
            var len2 = BinarySerializer.Write(sm, buffer, temp);
            Assert.AreEqual(len, len2);
            Console.WriteLine($"Useful: {sm.Count * 8}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 8.0))}");
            SortedMap<int, int> sm2 = null;
            var len3 = BinarySerializer.Read(buffer, out sm2);

            Assert.AreEqual(len, len3);

            Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
            Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        }

        [Test]
        public void CouldSerializeSortedMapWithStrings()
        {
            var rng = new Random();

            var dest = (Memory<byte>)new byte[10000000];
            var buffer = dest;
            var handle = buffer.Pin();
            var ptr = (IntPtr)handle.Pointer;

            var valueLens = 0;
            var sm = new SortedMap<int, string>();
            for (var i = 0; i < 100000; i++)
            {
                var str = i.ToString();
                valueLens += str.Length;
                sm.Add(i, str);
            }
            RetainedMemory<byte> temp;
            var len = BinarySerializer.SizeOf(sm, out temp);
            var len2 = BinarySerializer.Write(sm, buffer, temp);
            Assert.AreEqual(len, len2);
            var usefulLen = sm.Count * 4 + valueLens;
            Console.WriteLine($"Useful: {usefulLen}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (usefulLen))}");
            SortedMap<int, string> sm2 = null;
            var len3 = BinarySerializer.Read(buffer, out sm2);

            Assert.AreEqual(len, len3);

            Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
            Assert.IsTrue(sm2.Values.SequenceEqual(sm.Values));
        }

        [Test]
        public void CouldUseCompression()
        {
            // this also tests write without SizeOf, but only happy path
            // TODO unhappy path with corrupted data and small buffers

            var formats = new[]
            {
                SerializationFormat.Binary, SerializationFormat.BinaryZstd, SerializationFormat.BinaryLz4,
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
                    source[i] = source[i - 1] + Math.Round((decimal)rng.NextDouble() * 10, 4);
                }

                var len = BinarySerializer.Write(source, bytes, default,
                    serializationFormat);

                Console.WriteLine($"Useful: {source.Length * 16}");
                Console.WriteLine($"Total: {len}");

                decimal[] destination = null;

                var len2 = BinarySerializer.Read(bytes, out destination);

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
            var db = new DirectBuffer(1000, (byte*)ptr);
            var val = new Dummy()
            {
                Timestamp = TimeService.Default.CurrentTime,
                ValL = 123,
                ValS = "foo"
            };
            var ts = val.Timestamp;

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            var tss = new[] { default, ts };

            foreach (var timestamp in tss)
            {
                foreach (var serializationFormat in serializationFormats)
                {
                    var len = BinarySerializer.SizeOf(val, out var stream, serializationFormat, timestamp);
                    var len2 = BinarySerializer.Write(val, ref db, stream, serializationFormat,
                        timestamp);

                    Assert.AreEqual(len, len2);

                    var len3 = BinarySerializer.Read(ref db, out Dummy val2, out var ts2);

                    Assert.AreEqual(len, len3);
                    Assert.AreEqual(val.Timestamp, val2.Timestamp);
                    Assert.AreEqual(val.ValL, val2.ValL);
                    Assert.AreEqual(val.ValS, val2.ValS);
                    Assert.AreEqual(timestamp, ts2);
                }
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
            var ts = val.Timestamp;

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            var tss = new[] { default, ts };

            foreach (var timestamp in tss)
            {
                foreach (var serializationFormat in serializationFormats)
                {
                    var len = BinarySerializer.SizeOf(val, out var stream, serializationFormat, timestamp);
                    var len2 = BinarySerializer.Write(val, ref db, stream, serializationFormat,
                        timestamp);

                    Assert.AreEqual(len, len2);

                    var len3 = BinarySerializer.Read(ref db, out DummyBlittable val2, out var ts2);

                    Assert.AreEqual(len, len3);
                    Assert.AreEqual(val.Timestamp, val2.Timestamp);
                    Assert.AreEqual(val.ValL, val2.ValL);
                    Assert.AreEqual(timestamp, ts2);
                }
            }

            var str = JsonSerializer.ToJsonString(val);
            Console.WriteLine(str);

            var str2 = JsonSerializer.ToJsonString(val.Timestamp);
            Console.WriteLine(str2);

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeStringWithTimeStamp()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = "bar";
            var ts = TimeService.Default.CurrentTime;

            var serializationFormats = new[] { SerializationFormat.Json }; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            var tss = new[] { default, ts };

            foreach (var timestamp in tss)
            {
                foreach (var serializationFormat in serializationFormats)
                {
                    var len = BinarySerializer.SizeOf(val, out var stream, serializationFormat, timestamp);
                    var len2 = BinarySerializer.Write(val, ref db, stream, serializationFormat,
                        timestamp);

                    Assert.AreEqual(len, len2);

                    var len3 = BinarySerializer.Read(ref db, out string val2, out var ts2);

                    Assert.AreEqual(len, len3);
                    Assert.AreEqual(val, val2);
                    Assert.AreEqual(timestamp, ts2);
                }
            }
            rm.Dispose();
        }

        [Test]
        public void CouldSerializePrimitiveWithTimeStamp()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = "bar";
            var ts = TimeService.Default.CurrentTime;

            var serializationFormats = new[] { SerializationFormat.Json }; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            var tss = new[] { default, ts };

            foreach (var timestamp in tss)
            {
                foreach (var serializationFormat in serializationFormats)
                {
                    var len = BinarySerializer.SizeOf(val, out var stream, serializationFormat, timestamp);
                    var len2 = BinarySerializer.Write(val, ref db, stream, serializationFormat,
                        timestamp);

                    Assert.AreEqual(len, len2);

                    var len3 = BinarySerializer.Read(ref db, out string val2, out var ts2);

                    Assert.AreEqual(len, len3);
                    Assert.AreEqual(val, val2);
                    Assert.AreEqual(timestamp, ts2);
                }
            }
            rm.Dispose();
        }

        [Test]
        public void CouldSerializePrimitiveArrayWithTimeStamp()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new[] { 1, 2, 3 };
            var ts = TimeService.Default.CurrentTime;

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            var tss = new[] { default, ts };

            foreach (var timestamp in tss)
            {
                foreach (var serializationFormat in serializationFormats)
                {
                    var len = BinarySerializer.SizeOf(val, out var stream, serializationFormat, timestamp);
                    var len2 = BinarySerializer.Write(val, ref db, stream, serializationFormat,
                        timestamp);

                    Assert.AreEqual(len, len2);

                    var len3 = BinarySerializer.Read(ref db, out int[] val2, out var ts2);

                    Assert.AreEqual(len, len3);
                    Assert.IsTrue(val.SequenceEqual(val2));
                    Assert.AreEqual(timestamp, ts2);
                }
            }
            rm.Dispose();
        }

        [Test]
        public void CouldSerializeDummyArrayWithTimeStamp()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new[] { new Dummy() { ValL = 1 }, new Dummy() { ValL = 2 } };
            var ts = TimeService.Default.CurrentTime;

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            var tss = new[] { default, ts };

            foreach (var timestamp in tss)
            {
                foreach (var serializationFormat in serializationFormats)
                {
                    var len = BinarySerializer.SizeOf(val, out var stream, serializationFormat, timestamp);
                    var len2 = BinarySerializer.Write(val, ref db, stream, serializationFormat,
                        timestamp);

                    Assert.AreEqual(len, len2);

                    var len3 = BinarySerializer.Read(ref db, out Dummy[] val2, out var ts2);

                    Assert.AreEqual(len, len3);
                    Assert.IsTrue(val.SequenceEqual(val2));
                    Assert.AreEqual(timestamp, ts2);
                }
            }
            rm.Dispose();
        }

        [Test]
        public void CouldSerializeTaggedKeyValueWithTimeStamp()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new TaggedKeyValue<int, long>(10, 20, 1);
            var ts = TimeService.Default.CurrentTime;

            var serializationFormats = new[] { SerializationFormat.Binary }; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>()};

            var tss = new[] { ts, default };

            foreach (var timestamp in tss)
            {
                foreach (var serializationFormat in serializationFormats)
                {
                    db.Write(0, 0);

                    var len = BinarySerializerEx.SizeOf(val, out var tempBuf, serializationFormat);
                    Assert.AreEqual(13, len);

                    var len2 = BinarySerializerEx.Write(val, db, tempBuf, serializationFormat, timestamp);
                    Assert.AreEqual(4 + (timestamp == default ? 0 : 8) + 13, len2);

                    Assert.AreEqual(len + 4 + (timestamp == default ? 0 : 8), len2);

                    if (timestamp == default)
                    {
                        Assert.AreEqual(1, (int)db.Read<DataTypeHeaderEx>(0).VersionAndFlags.SerializationFormat);
                        Assert.AreEqual(1, db.Read<byte>(DataTypeHeaderEx.Size));
                        Assert.AreEqual(10, db.Read<int>(DataTypeHeaderEx.Size + 1));
                        Assert.AreEqual(20, db.Read<long>(DataTypeHeaderEx.Size + 1 + 4));
                    }
                    else
                    {
                        Assert.AreEqual(1, (int)db.Read<DataTypeHeaderEx>(0).VersionAndFlags.SerializationFormat);
                        Assert.AreEqual((long)timestamp, db.Read<long>(DataTypeHeaderEx.Size));
                        Assert.AreEqual(1, db.Read<byte>(DataTypeHeaderEx.Size + 8));
                        Assert.AreEqual(10, db.Read<int>(DataTypeHeaderEx.Size + 8 + 1));
                        Assert.AreEqual(20, db.Read<long>(DataTypeHeaderEx.Size + 8 + 1 + 4));
                    }

                    var len3 = BinarySerializerEx.Read(db, out TaggedKeyValue<int, long> val2, out var ts2);

                    Assert.AreEqual(len2, len3);

                    Assert.AreEqual(val, val2);

                    Assert.AreEqual(timestamp, ts2);
                }
            }
            rm.Dispose();
        }
    }
}