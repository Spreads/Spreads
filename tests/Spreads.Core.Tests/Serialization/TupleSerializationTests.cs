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
        public unsafe void CouldSerializeTuple2Nested()
        {
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            ((int, int), (int, int)) val = ((1, 2), (3, 4));

            var serializationFormats = new[] { SerializationFormat.Binary }; // Enum.GetValues(typeof(SerializationFormat)).Cast<SerializationFormat>();

            foreach (var preferredFormat in serializationFormats)
            {
                db.Write(0, 0);

                var sizeOf = BinarySerializer.SizeOf2(val, out var tempBuf, preferredFormat);
                //if (preferredFormat.IsBinary())
                //{
                //    Assert.AreEqual(10, tempBuf.temporaryBuffer.Span[9]);
                //    Assert.AreEqual(DataTypeHeader.Size + BinarySerializer.PayloadLengthSize + 4 + 4 + 3, sizeOf);
                //}

                var written = BinarySerializer.Write2(val, db, tempBuf, preferredFormat);

                Assert.AreEqual(sizeOf, written);

                var consumed = BinarySerializer.Read(db, out ((int, int), (int, int)) val2);

                Assert.AreEqual(written, consumed);

                Assert.AreEqual(val, val2);
            }

            rm.Dispose();
        }

        [Test, Explicit("bench")]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public unsafe void CouldSerializeTuple2NestedBench()
        {
            var count = 1_000_000;
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var format = SerializationFormat.Binary;

            Verify();

            void Verify()
            {
                ((int, int), ((int, int), (int, int))) val = default; // ((1,2), ((3,4),(5,6)));

                for (int i = 0; i < count / 1000; i++)
                {
                    var sizeOf = BinarySerializer.SizeOf(val, out var tempBuf, format);
                    var written = BinarySerializer.Write(val, db, tempBuf, format);

                    if (sizeOf != written)
                    {
                        Assert.Fail();
                    }

                    var consumed = BinarySerializer.Read(db, out ((int, int), ((int, int), (int, int))) val2);

                    if (written != consumed)
                    {
                        Assert.Fail();
                    }

                    if (val != val2)
                    {
                        Assert.Fail("val != val2");
                    }
                }
            }

            for (int _ = 0; _ < 5000; _++)
            {
                CouldSerializeTuple2NestedBench_Loop<((((int, int), (int, int)), ((int, int), (int, int))), (((int, int), (int, int)), ((int, int), (int, int))))>(count, default, format, db);
            }
            Benchmark.Dump();

            rm.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
        private static void CouldSerializeTuple2NestedBench_Loop<T>(int count, T val,
            SerializationFormat format, DirectBuffer db)
        {
            using (Benchmark.Run("Nested Tuple2", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var sizeOf = BinarySerializer.SizeOf2(val, out var tempBuf, format);
                    var written = BinarySerializer.Write2(val, db, tempBuf, format);

                    //if (sizeOf != written)
                    //{
                    //    Assert.Fail();
                    //}

                    var consumed = BinarySerializer.Read(db, out T val2);

                    //if (written != consumed)
                    //{
                    //    Assert.Fail();
                    //}
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
                if (preferredFormat.IsBinary())
                {
                    Assert.AreEqual(10, tempBuf.temporaryBuffer.Span[9]);
                    Assert.AreEqual(DataTypeHeader.Size + BinarySerializer.PayloadLengthSize + 4 + 4 + 3, sizeOf);
                }

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
            var ts = TimeService.Default.CurrentTime;

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
            var count = 30_000_000;
#else
            var count = 1_000;
#endif
            var rm = BufferPool.Retain(1000);
            var db = new DirectBuffer(rm);

            var val = new TaggedKeyValue<int, long>(10, 20, 1);

            var preferredFormat = SerializationFormat.Binary;

            for (int i = 0; i < count / 1000; i++)
            {
                var sizeOf = BinarySerializer.SizeOf(in val, out var payload, preferredFormat);
                var written = BinarySerializer.Write(in val, db, in payload, preferredFormat);
                if (sizeOf != written)
                {
                    Assert.Fail("DataTypeHeader.Size + sizeOf != written");
                }
                var consumed = BinarySerializer.Read(db, out TaggedKeyValue<int, long> val2,
                skipTypeInfoValidation: false);
                if (consumed <= 0)
                {
                    Assert.Fail("len3 <= 0");
                }

                if (written != consumed || val.Key != val2.Key || val.Value != val2.Value || val.Tag != val2.Tag)
                {
                    Assert.Fail();
                }
            }

            for (int _ = 0; _ < 50; _++)
            {
                using (Benchmark.Run("TKV roundtrip", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var sizeOf = BinarySerializer.SizeOf(in val, out var payload, preferredFormat);
                        var written = BinarySerializer.Write(in val, db, in payload, preferredFormat);
                        var consumed = BinarySerializer.Read(db, out TaggedKeyValue<int, long> val2, skipTypeInfoValidation: false);
                    }
                }
            }
            Benchmark.Dump();
            rm.Dispose();
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
