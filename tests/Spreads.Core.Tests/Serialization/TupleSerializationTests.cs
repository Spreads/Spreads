// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Spreads.Core.Tests.Serialization
{
    [Category("Serialization")]
    [Category("CI")]
    [TestFixture]
    public class TupleSerializationTests
    {
        //[Test]
        //public void ExpressionTest()
        //{
        //    Expression<Func<(int, long), long>> ex = ExpressionMethod(x => x.Item1 + x.Item2);

        //}

        //public Expression<Func<(int, long), long>> ExpressionMethod(Func<(int, long), long> ex)
        //{
        //    return (Expression<Func<(int, long), long>>)ex;
        //}

        [Test]
        public unsafe void CouldSerializeTuple2TFixed()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (int, int) val = (10, 20);

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                db.Write(0, 0);

                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                if (preferredFormat.IsBinary())
                {
                    Assert.AreEqual(12, sizeOf);
                }

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);
                if (preferredFormat == SerializationFormat.Json)
                {
                    Console.WriteLine(Encoding.UTF8.GetString(db.Data + 8, written - 8));
                }
                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out (int, int) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test]
        public unsafe void CouldSerializeTuple2WithDateTime()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (DateTime, int) val = (DateTime.Now, 20);

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                if (preferredFormat.IsBinary())
                {
                    Assert.AreEqual(16, sizeOf);
                }

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);
                if (preferredFormat == SerializationFormat.Json)
                {
                    Console.WriteLine(Encoding.UTF8.GetString(db.Data + 8, written - 8));
                }
                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out (DateTime, int) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeTuple2Nested()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            ((int, int), (int, int)) val = ((1, 2), (3, 4));

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                db.Write(0, 0);

                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                //if (preferredFormat.IsBinary())
                //{
                //    Assert.AreEqual(10, tempBuf.temporaryBuffer.Span[9]);
                //    Assert.AreEqual(DataTypeHeader.Size + BinarySerializer.PayloadLengthSize + 4 + 4 + 3, sizeOf);
                //}

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out ((int, int), (int, int)) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void CouldSerializeTuple2NestedBench()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var format = SerializationFormat.Binary;

            var classThatWarmsUpStuff = new TupleBenchmarkRunnerWithWarmUp<((int, int), (int, int))>();
            for (int _ = 0; _ < TestUtils.GetBenchCount(5000, 1); _++)
            {
                classThatWarmsUpStuff.Bench_Loop(count, default, format, db);
            }
            Benchmark.Dump();

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeTuple3()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (Timestamp, int, double) val = (TimeService.Default.CurrentTime, 42, 3.1415);

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out (Timestamp, int, double) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void CouldSerializeTuple3Bench()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var format = SerializationFormat.Json;

            var classThatWarmsUpStuff = new TupleBenchmarkRunnerWithWarmUp<(Timestamp, SmallDecimal, double)>();
            for (int _ = 0; _ < TestUtils.GetBenchCount(5000, 1); _++)
            {
                classThatWarmsUpStuff.Bench_Loop(count, default, format, db);
            }
            Benchmark.Dump();

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeTuple4()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (Timestamp, SmallDecimal, double, DateTime) val = (TimeService.Default.CurrentTime, 42, 3.1415, DateTime.UtcNow);

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                if (preferredFormat.IsBinary())
                {
                    Assert.AreEqual(4 + 32, sizeOf);
                }

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out (Timestamp, SmallDecimal, double, DateTime) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeTuple4WithTuple3AndTuple2()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            // name do not participate in serialization
            ((long i8, int i4, short i2) ints, (double f64, float f32) floats, decimal dec, DateTime dt) val = ((1, 2, 3), (4, 5), 6, DateTime.UtcNow);

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                if (preferredFormat.IsBinary())
                {
                    Assert.AreEqual(4 + 50, sizeOf);
                }

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out ((long, int, short), (double, float), decimal, DateTime) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void CouldSerializeTuple4Bench()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var format = SerializationFormat.Binary;

            var classThatWarmsUpStuff = new TupleBenchmarkRunnerWithWarmUp<(Timestamp, SmallDecimal, double, long)>();
            for (int _ = 0; _ < TestUtils.GetBenchCount(5000, 1); _++)
            {
                classThatWarmsUpStuff.Bench_Loop(count, default, format, db);
            }
            Benchmark.Dump();

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeTuple5()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (decimal, SmallDecimal, double, DateTime, int) val = (10M, 42, 3.1415, DateTime.UtcNow, 123);

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                if (preferredFormat.IsBinary())
                {
                    Assert.AreEqual(4 + 44, sizeOf);
                }

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out (decimal, SmallDecimal, double, DateTime, int) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void CouldSerializeTuple5Bench()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var format = SerializationFormat.Binary;

            var classThatWarmsUpStuff = new TupleBenchmarkRunnerWithWarmUp<(decimal, long, (int, short), (short, byte), long)>();
            for (int _ = 0; _ < TestUtils.GetBenchCount(5000, 1); _++)
            {
                classThatWarmsUpStuff.Bench_Loop(count, default, format, db);
            }
            Benchmark.Dump();

            rm.Dispose();
        }

        private class TupleBenchmarkRunnerWithWarmUp<T>
        {
            static TupleBenchmarkRunnerWithWarmUp()
            {
                // Example of how to warmup serializer once for a container that
                // will use serialization of type T.
                // This does not work when calling warmup from static field initializer.
                BinarySerializer.WarmUp<T>();
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
            public void Bench_Loop(long count, T val,
                SerializationFormat format, DirectBuffer db)
            {
                using (Benchmark.Run(typeof(T).Name, count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        // ReSharper disable UnusedVariable
                        var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, format);
                        var written = BinarySerializer.Write(val, db, tempBuf, format);
                        var consumed = BinarySerializer.Read(db, out T val2);
                        // ReSharper restore UnusedVariable
                    }
                }
            }
        }

        [Test]
        public unsafe void CouldSerializeTuple2TVariable()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (int, string) val = (10, "asd");

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                db.Write(0, 0);

                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                //if (preferredFormat.IsBinary())
                //{
                //    Assert.AreEqual(10, tempBuf.bufferWriter.WrittenSpan[9]);
                //    Assert.AreEqual(DataTypeHeader.Size + BinarySerializer.PayloadLengthSize + 4 + 4 + 3, sizeOf);
                //}

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);
                if (preferredFormat == SerializationFormat.Json)
                {
                    Console.WriteLine(Encoding.UTF8.GetString(db.Data + 8, written - 8));
                }
                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out (int, string) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test]
        public unsafe void CouldSerializeTuple2()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            (int, long) val = (10, 20L);

            var serializationFormats = Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                db.Write(0, 0);

                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, preferredFormat);
                if (preferredFormat.IsBinary())
                {
                    Assert.AreEqual(16, sizeOf);
                }

                var written = BinarySerializer.Write(val, db, tempBuf, preferredFormat);
                if (preferredFormat == SerializationFormat.Json)
                {
                    Console.WriteLine(Encoding.UTF8.GetString(db.Data + 8, written - 8));
                }
                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out (int first, long second) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test]
        public void CouldSerializeTaggedKeyValue()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new TaggedKeyValue<int, long>(10, 20, 1);

            var serializationFormats = new[] { SerializationFormat.Binary }; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>()};

            foreach (var serializationFormat in serializationFormats)
            {
                db.Write(0, 0);

                var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, serializationFormat);
                Assert.AreEqual(17, sizeOf);

                var written = BinarySerializer.Write(val, db, tempBuf, serializationFormat);
                Assert.AreEqual(sizeOf, written);

                Assert.AreEqual(sizeOf, written);

                Assert.AreEqual(1, (int)db.Read<DataTypeHeader>(0).VersionAndFlags.SerializationFormat);
                Assert.AreEqual(1, db.Read<byte>(DataTypeHeader.Size));
                Assert.AreEqual(10, db.Read<int>(DataTypeHeader.Size + 1));
                Assert.AreEqual(20, db.Read<long>(DataTypeHeader.Size + 1 + 4));

                var consumed = BinarySerializer.Read(db, out TaggedKeyValue<int, long> val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void CouldSerializeTaggedKeyValueBench()
        {
#if !DEBUG
            var count = 100_000_000;
#else
            var count = 1_000;
#endif
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new TaggedKeyValue<int, long>(10, 20, 1);

            var preferredFormat = SerializationFormat.Binary;

            // 5X performance difference due to warmup!
            BinarySerializer.WarmUp<TaggedKeyValue<int, long>>();

            for (int _ = 0; _ < 50; _++)
            {
                CouldSerializeTaggedKeyValueBench_Loop(count, val, preferredFormat, db);
            }
            Benchmark.Dump();
            rm.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
        private static void CouldSerializeTaggedKeyValueBench_Loop(int count, TaggedKeyValue<int, long> val, SerializationFormat preferredFormat, DirectBuffer db)
        {
            using (Benchmark.Run("TKV roundtrip", count))
            {
                for (int i = 0; i < count; i++)
                {
                    // ReSharper disable UnusedVariable
                    var sizeOf = BinarySerializer.SizeOf(in val, out var payload, preferredFormat);
                    var written = BinarySerializer.Write(in val, db, in payload, preferredFormat);
                    var consumed = BinarySerializer.Read(db, out TaggedKeyValue<int, long> val2, skipTypeInfoValidation: false);
                    // ReSharper restore UnusedVariable
                }
            }
        }

        [Test, Explicit("output")]
        public void TupleSize()
        {
            Console.WriteLine(Unsafe.SizeOf<(byte, long)>());
            Console.WriteLine(TypeHelper<(byte, long)>.FixedSize);
            Console.WriteLine(Unsafe.SizeOf<TupleTest<byte, long>>());
            Console.WriteLine(TypeHelper<TupleTest<byte, long>>.FixedSize);

            Console.WriteLine(Unsafe.SizeOf<(byte, long, string)>());

            Console.WriteLine(TypeHelper<DateTime>.PinnedSize);
        }

        // ReSharper disable  InconsistentNaming
        [BinarySerialization(preferBlittable: true)]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TupleTest<A, B> //, C, D, E, F
        {
            public A a;
            public B b;
            //public C c;
            //public D d;
            //public E e;
            //public F f;

            //public Test(ValueTuple<A, B, C, D, E, F> tuple)
            //{
            //    (a, b, c, d, e, f) = tuple;

            //}

            public TupleTest((A a, B b) tuple) //, C c, D d, E e, F f
            {
                (a, b) = tuple; //, c, d, e, f
            }
        }
    }
}
