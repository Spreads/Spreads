// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


// TODO (low) old tests, use CompressionTests2, compare why this fails and ensure there was no VolksWagening with #2

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using NUnit.Framework;
//using System.Runtime.InteropServices;
//using Spreads.Collections;
//using Newtonsoft.Json.Bson;
//using Newtonsoft.Json;

//namespace Spreads.Extensions.Tests {

//    //[TestFixture]
//    public class CompressionTests {

//        private const int _small = 500;
//        private const int _big = 1000;

//        private double[] _doublesSmall = new double[_small];
//        private double[] _doublesBig = new double[_big];
//        private decimal[] _decsSmall = new decimal[_small];
//        private decimal[] _decsBig = new decimal[_big];
//        private long[] _longsSmall = new long[_small];
//        private long[] _longsBig = new long[_big];
//        private DateTime[] _datesSmall = new DateTime[_small];
//        private DateTime[] _datesBig = new DateTime[_big];
//        private Tick[] _tickSmall = new Tick[_small];
//        private Tick[] _tickBig = new Tick[_big];
//        private ComplexObject[] _complexSmall = new ComplexObject[10];
//        private string[] _stringSmall = new string[_small];
//        private Random _rng = new Random(0);
//        private KeyValuePair<DateTime, decimal>[] _kvpSmall = new KeyValuePair<DateTime, decimal>[_small];
//        private KeyValuePair<DateTime, decimal>[] _kvpBig = new KeyValuePair<DateTime, decimal>[_big];

//        [SetUp]
//        public void Init() {

//            var previous = 100.0;
//            for (var i = 0; i < _big; i++) {
//                var val = Math.Round(previous * (1 + (_rng.NextDouble() * 0.004 - 0.001)), 2);
//                if (i < _small) {
//                    _doublesSmall[i] = val;
//                    _decsSmall[i] = (decimal)val;
//                    _longsSmall[i] = i;
//                    _datesSmall[i] = DateTime.UtcNow.Date.AddDays(i);
//                    _kvpSmall[i] = new KeyValuePair<DateTime, decimal>(DateTime.Now.Date.AddDays(i), i);
//                    _tickSmall[i] = new Tick(DateTime.UtcNow.Date.AddSeconds(i), i, i);
//                    if (i < 10) _complexSmall[i] = ComplexObject.Create();
//                    _stringSmall[i] = _rng.NextDouble().ToString();
//                }
//                _doublesBig[i] = val;
//                _decsBig[i] = (decimal)val;
//                _longsBig[i] = i;
//                _datesBig[i] = DateTime.UtcNow.Date.AddDays(i);
//                _kvpBig[i] = new KeyValuePair<DateTime, decimal>(DateTime.Now.Date.AddDays(i), i);
//                _tickBig[i] = new Tick(DateTime.UtcNow.Date.AddSeconds(i), i, i);
//                var dt = DateTimeOffset.Now;
//                previous = val;
//            }

//            unsafe
//            {
//                fixed (Tick* destPtr = &_tickSmall[0])
//                {
//                    Console.WriteLine("Ticks pointer: " + ((IntPtr)destPtr));
//                }
//            }
//        }

//        [Test]
//        public void CouldSerilizeTypeToJson() {
//            var smt = new SortedMap<DateTime, double>().GetType();

//            var json = JsonConvert.SerializeObject(smt);
//            Console.WriteLine(json);
//            var smt2 = JsonConvert.DeserializeObject<Type>(json);

//            Assert.AreEqual(smt, smt2);
//        }

//        [Test]
//        public void CouldCompressAndDecompressComplexObject() {
//            bool write = true;
//            var sw = new Stopwatch();

//            sw.Start();
//            for (int rounds = 0; rounds < 1000; rounds++) {

//                var complexObj = ComplexObject.Create();
//                var bytes = Serializer.Serialize(complexObj);
//                if (write) {
//                    Console.WriteLine("Uncompressed size: " + (complexObj.TextValue.Length * 2 + 8000 + 16000));
//                    Console.WriteLine("Compressed size: " + bytes.Length);
//                    write = false;
//                }
//                var complexObj2 = Serializer.Deserialize<ComplexObject>(bytes);

//                Assert.IsTrue(complexObj.IntArray.SequenceEqual(complexObj2.IntArray));
//                Assert.AreEqual(complexObj.TextValue, complexObj2.TextValue);
//            }
//            sw.Stop();
//            Console.WriteLine("Elapsed: " + sw.ElapsedMilliseconds);
//            // scope, null map
//            {
//                var complexObj = ComplexObject.Create();
//                complexObj.SortedMap = null; // not root, JSON.NET deals with it
//                var bytes = Serializer.Serialize(complexObj);
//                if (write) {
//                    Console.WriteLine("Uncompressed size: " + (complexObj.TextValue.Length * 2 + 8000 + 16000));
//                    Console.WriteLine("Compressed size: " + bytes.Length);
//                    write = false;
//                }
//                var complexObj2 = Serializer.Deserialize<ComplexObject>(bytes);

//                Assert.IsTrue(complexObj.IntArray.SequenceEqual(complexObj2.IntArray));
//                Assert.AreEqual(complexObj.TextValue, complexObj2.TextValue);


//            }
//            // scope, null int array
//            {
//                var complexObj = ComplexObject.Create();
//                complexObj.SortedMap = null;
//                complexObj.IntArray = null;
//                complexObj.TextValue = "";
//                var bytes = Serializer.Serialize(complexObj);
//                if (write) {
//                    Console.WriteLine("Uncompressed size: " + (complexObj.TextValue.Length * 2 + 8000 + 16000));
//                    Console.WriteLine("Compressed size: " + bytes.Length);
//                    write = false;
//                }
//                var complexObj2 = Serializer.Deserialize<ComplexObject>(bytes);
//                Assert.IsTrue(complexObj2.IntArray == null);
//                Assert.AreEqual(complexObj.TextValue, complexObj2.TextValue);
//            }
//            //scope, empty int array
//            {
//                var complexObj = ComplexObject.Create();
//                complexObj.SortedMap = null;
//                complexObj.IntArray = new long[0];
//                complexObj.TextValue = "";
//                var bytes = Serializer.Serialize(complexObj);
//                if (write) {
//                    Console.WriteLine("Uncompressed size: " + (complexObj.TextValue.Length * 2 + 8000 + 16000));
//                    Console.WriteLine("Compressed size: " + bytes.Length);
//                    write = false;
//                }
//                var complexObj2 = Serializer.Deserialize<ComplexObject>(bytes);
//                Assert.IsTrue(complexObj.IntArray.SequenceEqual(complexObj2.IntArray));
//                Assert.AreEqual(complexObj.TextValue, complexObj2.TextValue);
//            }
//        }

//        [Test]
//        public void CouldCompressAndDecompressSeriesWithCustomObject() {
//            var sm = new SortedMap<DateTime, MyTestClass>();
//            for (int i = 0; i < 100; i++) {
//                var value = new MyTestClass()
//                {
//                    Text = "Text " + i,
//                    Number = i
//                };
//                sm.Add(DateTime.UtcNow.Date.AddSeconds(i + 1).ConvertToUtcWithUncpecifiedKind(""),
//                    value);
//            }
//            var bytes = Serializer.Serialize(sm);
//            var sm2 = Serializer.Deserialize<SortedMap<DateTime, MyTestClass>>(bytes);

//            Assert.AreEqual(sm.Count, sm2.Count);
//        }

//        [Test]
//        public void CouldCompressAndDecompressSeriesWithCustomSingleObject() {
//            var sm = new SortedMap<DateTime, MyTestClass>();
//            for (int i = 0; i < 2; i++) {
//                var value = new MyTestClass()
//                {
//                    Text = "Text " + i,
//                    Number = i
//                };
//                sm.Add(DateTime.UtcNow.Date.AddSeconds(i + 1).ConvertToUtcWithUncpecifiedKind(""),
//                    value);
//            }
//            var bytes = Serializer.Serialize(sm);
//            var sm2 = Serializer.Deserialize<SortedMap<DateTime, MyTestClass>>(bytes);

//            Assert.AreEqual(sm.Count, sm2.Count);
//        }



//        [Test]
//        public void MarshalDoesRoundsDateTime() {
//            for (int i = 0; i < 1000; i++) {
//                var now = new Tick(DateTime.Now.AddSeconds(i), i, i);
//                var now2 = now;

//                var ticks = new Tick[1];
//                unsafe
//                {
//                    fixed (Tick* ptr = &ticks[0])
//                    {
//                        Marshal.StructureToPtr(now2, (IntPtr)ptr, false);
//                        now2 = (Tick)Marshal.PtrToStructure((IntPtr)ptr, typeof(Tick));
//                        Assert.AreEqual(now.DateTime, now2.DateTime);
//                    }
//                }
//            }
//        }


//        [Test]
//        public void MarshalDoesntRoundsDateTime() {
//            for (int i = 0; i < 1000; i++) {
//                var now = new Tick(DateTime.Now.AddSeconds(i), i, i);
//                var now2 = now;

//                var ticks = new Tick[1];
//                ticks[0] = now2;
//                unsafe
//                {
//                    fixed (Tick* ptr = &ticks[0])
//                    {
//                        //Marshal.StructureToPtr(now2, (IntPtr)ptr, false);
//                        now2 = (Tick)Marshal.PtrToStructure((IntPtr)ptr, typeof(Tick));
//                        Assert.AreEqual(now.DateTime, now2.DateTime);
//                    }
//                }
//            }
//        }


//        [Test]
//        public void CouldCompressKVP()
//        {

//            var kvp = new KeyValuePair<DateTime, decimal>(DateTime.Now.Date, 123M);

//            var bytes = Serializer.Serialize(kvp);

//            var kvp2 = Serializer.Deserialize<KeyValuePair<DateTime, decimal>>(bytes);
//            Console.WriteLine(kvp.Key.Kind);
//            Console.WriteLine(kvp2.Key.Kind);
//            Assert.AreEqual(kvp.Key.ToUniversalTime(), kvp2.Key.ToUniversalTime());

//        }


//        [Test]
//        public void CouldCompressAndDecompress() {
//            //for (int round = 0; round < 100; round++) {
//            CompressSmallBig();
//            //}
//        }

//        [Test]
//        public void CouldCompressAndDecompressDynamicResolution() {
//            Console.WriteLine("float: " + _small);
//            CompressDynamicResolution<double>(_doublesSmall);

//            Console.WriteLine("float: " + _big);
//            CompressDynamicResolution<double>(_doublesBig);

//            Console.WriteLine("decimal: " + _small);
//            CompressDynamicResolution<decimal>(_decsSmall);

//            Console.WriteLine("decimal: " + _big);
//            CompressDynamicResolution<decimal>(_decsBig);

//            Console.WriteLine("long: " + _small);
//            CompressDynamicResolution<long>(_longsSmall);

//            Console.WriteLine("long: " + _big);
//            CompressDynamicResolution<long>(_longsBig);

//            Console.WriteLine("datetime: " + _small);
//            CompressDynamicResolution<DateTime>(_datesSmall);

//            Console.WriteLine("datetime: " + _big);
//            CompressDynamicResolution<DateTime>(_datesBig);

//            Console.WriteLine("tick: " + _small);
//            CompressDynamicResolution<Tick>(_tickSmall);

//            Console.WriteLine("tick: " + _big);
//            CompressDynamicResolution<Tick>(_tickBig);

//            Console.WriteLine("KVP: " + _small);
//            CompressDynamicResolution<KeyValuePair<DateTime, decimal>>(_kvpSmall);

//            Console.WriteLine("KVP: " + _big);
//            CompressDynamicResolution<KeyValuePair<DateTime, decimal>>(_kvpBig);

//            //Console.WriteLine("complex: " + _small);
//            //CompressDynamicResolution<ComplexObject>(_complexSmall);
//        }

//        public void CompressSmallBig() {
//            Console.WriteLine("float: " + _small);
//            CompressDiffMethods<double>(_doublesSmall);

//            Console.WriteLine("float: " + _big);
//            CompressDiffMethods<double>(_doublesBig);

//            Console.WriteLine("decimal: " + _small);
//            CompressDiffMethods<decimal>(_decsSmall);

//            Console.WriteLine("decimal: " + _big);
//            CompressDiffMethods<decimal>(_decsBig);

//            Console.WriteLine("long: " + _small);
//            CompressDiffMethods<long>(_longsSmall);

//            Console.WriteLine("long: " + _big);
//            CompressDiffMethods<long>(_longsBig);

//            Console.WriteLine("datetime: " + _small);
//            CompressDiffMethods<DateTime>(_datesSmall);

//            Console.WriteLine("datetime: " + _big);
//            CompressDiffMethods<DateTime>(_datesBig);

//            Console.WriteLine("tick: " + _small);
//            CompressDiffMethods<Tick>(_tickSmall);

//            Console.WriteLine("tick: " + _big);
//            CompressDiffMethods<Tick>(_tickBig);

//            Console.WriteLine("string: " + _small);
//            CompressDiffMethods<string>(_stringSmall);

//            Console.WriteLine("complex: " + _small);
//            CompressDiffMethods<ComplexObject>(_complexSmall);

//            Console.WriteLine("KVP: " + _small);
//            CompressDiffMethods<KeyValuePair<DateTime, decimal>>(_kvpSmall);

//            Console.WriteLine("KVP: " + _big);
//            CompressDiffMethods<KeyValuePair<DateTime, decimal>>(_kvpBig);
//        }

//        public void CompressDiffMethods<T>(T[] input) {
//            Console.WriteLine("-------shuffle on-------");
//            Compress<T>(input, CompressionMethod.blosclz, true);
//            Compress<T>(input, CompressionMethod.lz4, true);
//            //Compress<T>(input, CompressionMethod.snappy, true);
//            //Compress<T>(input, CompressionMethod.lz4hc, true);
//            //Compress<T>(input, CompressionMethod.zlib, true);
//            //Console.WriteLine("-------shuffle off-------");
//            //Compress<T>(input, CompressionMethod.blosclz, false);
//            //Compress<T>(input, CompressionMethod.lz4, false);
//            ////Compress<T>(input, CompressionMethod.lz4hc, false);
//            //Compress<T>(input, CompressionMethod.zlib, false);
//        }

//        internal void Compress<T>(T[] input, CompressionMethod method, bool shuffle) {
//            int typeSize = 0;
//            try {
//                typeSize = Marshal.SizeOf(input[0]);
//            } catch {
//                if (typeof(T) == typeof(DateTime)) {
//                    typeSize = 8;
//                } else if (typeof(T) == typeof(string)) {
//                    typeSize = (input[input.Length - 1] as string).Length;
//                } else if (typeof(T) == typeof(ComplexObject)) {
//                    typeSize = ComplexObject.Create().TextValue.Length + 16000 + 8000;
//                }
//            }

//            Console.Write("  m: " + method);
//            Console.WriteLine(" s: " + shuffle);
//            Console.WriteLine("    nbytes: " + input.Length * typeSize);

//            byte[] compr = new byte[0];
//            var sw = new Stopwatch();
//            sw.Start();
//            var rounds = 500;
//            for (int i = 0; i < rounds; i++) {
//                compr = Serializer.CompressArray<T>(input, level: 9,
//                    shuffle: shuffle, method: method, diff: true);
//            }
//            sw.Stop();
//            //var js = new SpreadsJsonSerializer();
//            //var json = js.SerializeToJson(input);
//            //Console.WriteLine(json);
//            //var fromJson = js.DeserializeFromJson<T[]>(@"[""0"", ""1""]");

//            var cspeed = 1000.0 * ((double)(input.Length * typeSize * rounds) / (1024.0 * 1024)) /
//                         ((double)sw.ElapsedMilliseconds); // MB/s
//            var celapsed = sw.ElapsedMilliseconds;
//            var saving = -Math.Round(100.0 * (((double)compr.Length) / ((double)(input.Length * typeSize)) - 1.0), 1);
//            Console.WriteLine("    cbytes: " + compr.Length + " saving: " + saving);

//            sw.Restart();
//            T[] decompr = new T[0];
//            for (int i = 0; i < rounds; i++) {
//                decompr = Serializer.DecompressArray<T>(compr, diff: true);
//            }
//            sw.Stop();
//            var delapsed = sw.ElapsedMilliseconds;
//            var dspeed = 1000.0 * ((double)(input.Length * typeSize * rounds) / (1024.0 * 1024)) /
//                         ((double)sw.ElapsedMilliseconds); // MB/s

//            Assert.AreEqual(decompr.Length, input.Length);
//            for (int i = 0; i < decompr.Length; i++)
//            {
//	            Assert.IsTrue(decompr[i].Equals(input[i]));
//                if (!decompr[i].Equals(input[i])) {
//                    Console.WriteLine("In: " + input[i] + "; out: " + decompr[i]);
//                }
//            }
//            //Assert.IsTrue(decompr.SequenceEqual(input));

//            Console.WriteLine("    >: " + Math.Round(cspeed, 2) + "MB/s; save/sec: " +
//                              Math.Round((saving * input.Length * typeSize * rounds / (100 * 1024 * 1024)) / (celapsed / 1000.0), 2));
//            Console.WriteLine("    <: " + Math.Round(dspeed, 2) + "MB/s; save/sec: " +
//                              Math.Round((saving * input.Length * typeSize * rounds / (100 * 1024 * 1024)) / (delapsed / 1000.0), 2));

//        }


//        public void CompressDynamicResolution<T>(T[] input) {
//            int typeSize = 0;
//            try {
//                typeSize = Marshal.SizeOf(input[0]);
//            } catch {
//                if (typeof(T) == typeof(DateTime)) {
//                    typeSize = 8;
//                } else if (typeof(T) == typeof(string)) {
//                    typeSize = (input[input.Length - 1] as string).Length;
//                } else if (typeof(T) == typeof(ComplexObject)) {
//                    typeSize = ComplexObject.Create().TextValue.Length + 16000 + 8000;
//                }
//            }

//            byte[] compr = new byte[0];
//            var sw = new Stopwatch();
//            sw.Start();
//            var rounds = 500;
//            for (int i = 0; i < rounds; i++) {
//                compr = Serializer.Serialize(input);
//            }
//            sw.Stop();
//            var cspeed = 1000.0 * ((double)(input.Length * typeSize * rounds) / (1024.0 * 1024)) /
//                         ((double)sw.ElapsedMilliseconds); // MB/s
//            var celapsed = sw.ElapsedMilliseconds;
//            var saving = -Math.Round(100.0 * (((double)compr.Length) / ((double)(input.Length * typeSize)) - 1.0), 1);
//            Console.WriteLine("    cbytes: " + compr.Length + " saving: " + saving);

//            sw.Restart();
//            T[] decompr = new T[0];
//            for (int i = 0; i < rounds; i++) {
//                decompr = Serializer.Deserialize<T[]>(compr);
//            }
//            sw.Stop();
//            var delapsed = sw.ElapsedMilliseconds;
//            var dspeed = 1000.0 * ((double)(input.Length * typeSize * rounds) / (1024.0 * 1024)) /
//                         ((double)sw.ElapsedMilliseconds); // MB/s

//            Assert.AreEqual(decompr.Length, input.Length);
//            Assert.IsTrue(decompr.SequenceEqual(input));

//            Console.WriteLine("    >: " + Math.Round(cspeed, 2) + "MB/s; save/sec: " +
//                              Math.Round((saving * input.Length * typeSize * rounds / (100 * 1024 * 1024)) / (celapsed / 1000.0), 2));
//            Console.WriteLine("    <: " + Math.Round(dspeed, 2) + "MB/s; save/sec: " +
//                              Math.Round((saving * input.Length * typeSize * rounds / (100 * 1024 * 1024)) / (delapsed / 1000.0), 2));

//        }

//        [Test]
//        public void TestMemory() {
//            CompressManyTimesSilent<long>(_longsSmall, CompressionMethod.lz4, true);
//        }

//        internal void CompressManyTimesSilent<T>(T[] input, CompressionMethod method, bool shuffle) {
//            int typeSize = 0;
//            try {
//                typeSize = Marshal.SizeOf(input[0]);
//            } catch {
//                if (typeof(T) == typeof(DateTime)) {
//                    typeSize = 8;
//                }
//            }

//            var sw = new Stopwatch();

//            GC.Collect(3, GCCollectionMode.Forced, true);
//            var startMem = GC.GetTotalMemory(true);
//            byte[] compr = new byte[0];
//            T[] decompr = new T[0];
//            sw.Start();
//            var rounds = 100000;
//            for (int i = 0; i < rounds; i++) {
//                compr = Serializer.CompressArray<T>(input, level: 9, shuffle: shuffle, method: method);
//                decompr = Serializer.DecompressArray<T>(compr);
//            }
//            GC.Collect(3, GCCollectionMode.Forced, true);
//            var endMem = GC.GetTotalMemory(true);
//            sw.Stop();
//            var delapsed = sw.ElapsedMilliseconds;
//            var dspeed = 1000.0 * ((double)(input.Length * typeSize * rounds) / (1024.0 * 1024)) /
//                         ((double)sw.ElapsedMilliseconds); // MB/s

//            Assert.AreEqual(decompr.Length, input.Length);
//            Assert.IsTrue(decompr.SequenceEqual(input));

//            Console.WriteLine("Elapsed: " + sw.ElapsedMilliseconds);
//            Console.WriteLine("Memory change: " + (endMem - startMem));
//        }


//        [Test]
//        public void CouldSerializePrimitives() {
//            var sw = new Stopwatch();
//            sw.Start();
//            double d;
//            byte[] bytes;
//            for (int i = 0; i < 10000000; i++) {
//                bytes = Serializer.SerializeImpl(1.0);
//                unsafe
//                {
//                    fixed (byte* srcPtr = &bytes[0])
//                    {
//                        d = Serializer.DeserializeImpl<double>((IntPtr)srcPtr, bytes.Length, 0.0);
//                    }
//                }
//            }
//            sw.Stop();
//            Console.WriteLine("Double: " + sw.ElapsedMilliseconds);

//            //sw.Restart();
//            //DateTime dt = DateTime.Now;
//            //for (int i = 0; i < 10000000; i++) {
//            //    dt = srlzr.Deserialize<DateTime>(srlzr.Serialize(dt));
//            //}
//            //sw.Stop();
//            //Console.WriteLine("Double: " + sw.ElapsedMilliseconds);
//        }

//        [Test]
//        public void CouldSerializePrimitivesDynamicResolution() {
//            // dynamic 20-25% faster *in this keys*
//            var sw = new Stopwatch();
//            sw.Start();
//            double d;
//            byte[] bytes;
//            for (int i = 0; i < 10000000; i++) {
//                bytes = Serializer.Serialize(1.0);
//                d = Serializer.Deserialize<double>(bytes);
//            }
//            sw.Stop();
//            Console.WriteLine("Double: " + sw.ElapsedMilliseconds);

//            //sw.Restart();
//            //DateTime dt = DateTime.Now;
//            //for (int i = 0; i < 10000000; i++) {
//            //    dt = srlzr.Deserialize<DateTime>(srlzr.Serialize(dt));
//            //}
//            //sw.Stop();
//            //Console.WriteLine("Double: " + sw.ElapsedMilliseconds);
//        }


//        [Test]
//        public void CouldSerializeCollectionsDynamicResolution() {
//            //var sw = new Stopwatch();
//            //sw.Start();

//            //SortedList<DateTime, double> sl = new SortedList<DateTime, double>();
//            //byte[] bytes;
//            //for (int i = 0; i < 1000; i++) {
//            //    sl.Add(DateTime.Today.AddDays(i), i);
//            //}
//            //bytes = Serializer.Serialize(sl);

//            //sl = Serializer.Deserialize<SortedList<DateTime, double>>(bytes);
//            //sw.Stop();
//            //Console.WriteLine("Sorted list: " + sw.ElapsedMilliseconds);


//            var sw = new Stopwatch();
//            sw.Start();

//            SortedMap<int, int> sl = new SortedMap<int, int>();
//            byte[] bytes;
//            for (int i = 0; i < 1000; i++) {
//                sl.Add(i, i);
//            }
//            bytes = Serializer.Serialize(sl);

//            sl = Serializer.Deserialize<SortedMap<int, int>>(bytes);
//            sw.Stop();
//            Console.WriteLine("Sorted list: " + sw.ElapsedMilliseconds);

//        }

//        [Test]
//        public void CouldSerializeSortedMap() {
//            var sw = new Stopwatch();


//            SortedMap<DateTime, double> sortedMap = new SortedMap<DateTime, double>();
//            byte[] bytes = null;
//            var rng = new System.Random();
//            for (int i = 0; i < 1000; i++) {
//                sortedMap.Add(DateTime.Today.AddDays(i), Math.Round(i + rng.NextDouble(), 2));
//            }
//			sortedMap.Add(DateTime.Today.AddDays(1002), Math.Round(1002 + rng.NextDouble(), 2));

//			sw.Start();
//            for (int i = 0; i < 5000; i++) {
//                bytes = Serializer.Serialize(sortedMap);
//                sortedMap = Serializer.Deserialize<SortedMap<DateTime, double>>(bytes);
//            }
//            sw.Stop();
//            Console.WriteLine("Elapsed msecs: " + sw.ElapsedMilliseconds);
//            Console.WriteLine("Uncompressed size: 16000");
//            Console.WriteLine("Compressed size: " + bytes.Length);
//        }

//        [Test]
//        public void CouldSerializeSortedMapWithStrings() {
//            {
//                var sw = new Stopwatch();
//                SortedMap<DateTime, string> sortedMap = new SortedMap<DateTime, string>();
//                byte[] bytes = null;
//                var rng = new System.Random();
//                for (int i = 0; i < 1000; i++) {
//                    sortedMap.Add(DateTime.Today.AddDays(i), Math.Round(i + rng.NextDouble(), 2).ToString());
//                }
//                sw.Start();
//                for (int i = 0; i < 1000; i++) {
//                    bytes = Serializer.Serialize(sortedMap);
//                    sortedMap = Serializer.Deserialize<SortedMap<DateTime, string>>(bytes);
//                }
//                sw.Stop();
//                Console.WriteLine("Elapsed msecs: " + sw.ElapsedMilliseconds);
//                Console.WriteLine("Uncompressed size: 16000");
//                Console.WriteLine("Compressed size: " + bytes.Length);
//            }
//            {
//                SortedMap<DateTime, string> sortedMap = new SortedMap<DateTime, string>();
//                var bytes = Serializer.Serialize(sortedMap);
//                sortedMap = Serializer.Deserialize<SortedMap<DateTime, string>>(bytes);
//            }
//        }

//        [Test]
//        public void CouldSerializeManySortedMaps() {
//            var sw = new Stopwatch();
//            sw.Start();

//            SortedMap<DateTime, double> sortedMap = new SortedMap<DateTime, double>();
//            byte[] bytes = null;
//            var rng = new System.Random();
//            for (int i = 0; i < 1000; i++) {
//                sortedMap.Add(DateTime.UtcNow.Date.AddSeconds(i), Math.Round(i + rng.NextDouble(), 2));
//                bytes = Serializer.Serialize(sortedMap);
//                var sortedMap2 = Serializer.Deserialize<SortedMap<DateTime, double>>(bytes);
//                Assert.AreEqual(sortedMap.Count, sortedMap2.Count);
//                unsafe
//                {
//                    fixed (DateTime* ptr1 = &sortedMap.keys[0])
//                    fixed (DateTime* ptr2 = &sortedMap2.keys[0])
//                    {
//                        Assert.IsTrue(BytesExtensions.UnsafeCompare((IntPtr)ptr1, (IntPtr)ptr2, sortedMap.keys.Length * 8));

//                    }
//                    fixed (double* ptr1 = &sortedMap.values[0])
//                    fixed (double* ptr2 = &sortedMap2.values[0])
//                    {
//                        Assert.IsTrue(BytesExtensions.UnsafeCompare((IntPtr)ptr1, (IntPtr)ptr2, sortedMap.size * 8));

//                    }
//                }

//                //Assert.IsTrue(sortedMap.Keys.SequenceEqual(sortedMap2.Keys));
//                //Assert.IsTrue(sortedMap.Values.SequenceEqual(sortedMap2.Values));
//            }
//            sw.Stop();
//            Console.WriteLine("Elapsed msecs: " + sw.ElapsedMilliseconds);
//            Console.WriteLine("Uncompressed size: 16000");
//            Console.WriteLine("Compressed size: " + bytes.Length);
//        }


//        public struct SomeDataStructure {
//            public string name;
//            public double value;

//        }

//        [Test]
//        public void CouldSerializeStructureDynamicResolution() {
//            var sw = new Stopwatch();
//            sw.Start();

//            byte[] bytes;
//            for (int i = 0; i < 1000; i++) {
//                var str = new SomeDataStructure()
//                {
//                    name = i.ToString(),
//                    value = i
//                };
//                bytes = Serializer.Serialize(str);
//                var str2 = Serializer.Deserialize<SomeDataStructure>(bytes);
//                Assert.AreEqual(str.value, str2.value);
//            }
//            sw.Stop();
//            Console.WriteLine("Sorted list: " + sw.ElapsedMilliseconds);
//        }

//        [Test]
//        public void CouldS() {
//            for (int i = 0; i < 1000; i++) {
//                var now = new Tick(DateTime.Now.AddSeconds(i), i, i);
//                var now2 = now;

//                var ticks = new Tick[1];
//                ticks[0] = now2;
//            }
//        }


//        [Test]
//        public void CouldCompressDecimalViaPointer()
//        {
//            var bytes = Serializer.SerializeImpl(10M);
//            Console.WriteLine(bytes.Length);
//        }


//        [Test]
//        public void DateTimeBinary()
//        {

//            var dates = new DateTime[10000000];
//            var longs1 = new long[10000000];
//            var longs2 = new long[10000000];

//            for (int i = 0; i < 10000000; i++)
//            {
//                dates[i] = DateTime.SpecifyKind(DateTime.Today.AddMilliseconds(i), DateTimeKind.Utc);
//            }

//            var sw = new Stopwatch();
//            sw.Start();

//            for (int i = 0; i < 10000000; i++)
//            {
//                longs1[i] = dates[i].ToBinary();
//            }

//            sw.Stop();
//            Console.WriteLine("ToBinary: " + sw.ElapsedMilliseconds);


//            sw.Restart();

//            for (int i = 0; i < 10000000; i++)
//            {
//                var ticks = dates[i].Ticks;
//                var kind = dates[i].Kind;
//                longs2[i] = ((Int64) ticks | ((Int64) kind << 62));
//            }

//            sw.Stop();
//            Console.WriteLine("BitShift: " + sw.ElapsedMilliseconds);


//            for (int i = 0; i < 10000000; i++) {
//                Assert.AreEqual(longs1[i], (long)longs2[i]);
//            }


//	        var dt1 = DateTime.Now;
//	        var dt2 = DateTime.FromBinary(dt1.ToBinary());
//	        Assert.AreEqual(dt1, dt2);
//        }
//    }

//}
