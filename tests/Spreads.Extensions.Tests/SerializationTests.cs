// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Newtonsoft.Json;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Spreads.Extensions.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void CouldSerializeSortedMapWithJsonNet()
        {
            var sm = new SortedMap<DateTime, double>();
            for (int i = 0; i < 10; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddDays(i), i);
            }

            var str = JsonConvert.SerializeObject(sm);
            Console.WriteLine(str);
            var sm2 = JsonConvert.DeserializeObject<SortedMap<DateTime, double>>(str);
            Assert.IsTrue(sm.SequenceEqual(sm2));
        }

        [Test]
        public void CouldSerializeSortedMapWithBinary()
        {
            SortedMap<DateTime, double>.Init();
            var sm = new SortedMap<DateTime, double>();
            for (int i = 0; i < 10; i++)
            {
                sm.Add(DateTime.UtcNow.Date.AddDays(i), i);
            }
            MemoryStream tmp;
            var len = BinarySerializer.SizeOf(sm, out tmp);
            Console.WriteLine(len);
            var dest = BufferPool.PreserveMemory(len);
            var len2 = BinarySerializer.Write(sm, ref dest, 0, tmp);
            Assert.AreEqual(len, len2);

            SortedMap<DateTime, double> sm2 = null;
            BinarySerializer.Read<SortedMap<DateTime, double>>(dest, 0, out sm2);

            Assert.IsTrue(sm.SequenceEqual(sm2));
        }

        [Test]
        public void CouldSerializeSortedMap()
        {
            SortedMap<DateTime, decimal>.Init();
            var rng = new Random();
            var buffer = new byte[100000];
            var sm = new SortedMap<DateTime, decimal>();
            for (var i = 0; i < 10000; i++)
            {
                sm.Add(DateTime.Today.AddHours(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
            }
            var len = BinarySerializer.Write(sm, buffer, compression: CompressionMethod.Zstd);
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
        public void CouldSerializeRegularSortedMapWithZstd()
        {
            SortedMap<DateTime, decimal>.Init();
            //BloscSettings.CompressionMethod = "zstd";
            var rng = new Random();
            var buffer = new byte[10000];
            var sm = new SortedMap<DateTime, decimal>();
            for (var i = 0; i < 1; i++)
            {
                sm.Add(DateTime.Today.AddSeconds(i), (decimal)Math.Round(i + rng.NextDouble(), 2));
            }

            MemoryStream tmp;
            var size = BinarySerializer.SizeOf(sm, out tmp, CompressionMethod.Zstd);
            var len = BinarySerializer.Write(sm, buffer, 0, tmp);
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
        public void CouldSerializeSortedMap2()
        {
            SortedMap<int, int>.Init();
            var rng = new Random();
            var buffer = new byte[100000];
            var sm = new SortedMap<int, int>();
            for (var i = 0; i < 10000; i++)
            {
                sm.Add(i, i);
            }
            MemoryStream temp;
            var len = BinarySerializer.SizeOf(sm, out temp, CompressionMethod.LZ4);
            var len2 = BinarySerializer.Write(sm, buffer, 0, temp, CompressionMethod.LZ4);
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
        public void CouldSerializeIDiffableSortedMap()
        {
            //SortedMap<DateTime, MarketDepth2>.Init();
            var rng = new Random();
            var ptr = Marshal.AllocHGlobal(100000);
            var buffer = new byte[100000];
            var sm = new SortedMap<DateTime, MarketDepth2>();
            for (var i = 1; i <= 10000; i++)
            {
                sm.Add(DateTime.Today.AddHours(i),
                    new MarketDepth2(i, i, i, i, i, i, i, i, i, i, i, i));
            }
            var len = BinarySerializer.Write(sm, buffer, compression: CompressionMethod.Zstd);
            Console.WriteLine($"Useful: {sm.Count * 24.0}");
            Console.WriteLine($"Total: {len}");
            // NB interesting that with converting double to decimal savings go from 65% to 85%,
            // even calculated from (8+8) base size not decimal's 16 size
            Console.WriteLine($"Savings: {1.0 - ((len * 1.0) / (sm.Count * 24.0))}");
            SortedMap<DateTime, MarketDepth2> sm2 = null;
            var len2 = BinarySerializer.Read(buffer, 0, out sm2);

            Assert.AreEqual(len, len2);

            Assert.IsTrue(sm2.Keys.SequenceEqual(sm.Keys));
            Assert.IsTrue(sm2.Values.Select(x => x.LargeAskPrice).SequenceEqual(sm.Values.Select(x => x.LargeAskPrice)));
        }
    }
}