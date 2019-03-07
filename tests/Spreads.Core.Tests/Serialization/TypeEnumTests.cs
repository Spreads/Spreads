// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Serialization.Experimental;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class TypeEnumTests
    {
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct Symbol256xLong
        {
            public Symbol256 Symbol256;
            public long Long;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct Symbol128Wrapper
        {
            public Symbol128 Symbol128;
        }

        [Test]
        public void LargeFixedSize()
        {
            var header = TypeEnumHelper<Symbol256xLong>.DataTypeHeader;

            Assert.AreEqual(TypeEnumEx.FixedSize, header.TEOFS.TypeEnum);
            Assert.AreEqual(256 + 8, header.FixedSizeSize);

            var size = TypeEnumHelper<Symbol256xLong>.FixedSize;
            Assert.AreEqual(256 + 8, size);
        }

        [Test]
        public void MaxInlinedFixedSize()
        {
            var header = TypeEnumHelper<Symbol128Wrapper>.DataTypeHeader;

            Assert.AreEqual(128, header.TEOFS.Size);
            Assert.AreEqual(0, header.FixedSizeSize);

            var size = TypeEnumHelper<Symbol128Wrapper>.FixedSize;
            Assert.AreEqual(128, size);
        }

        [Test]
        public void Symbol256Size()
        {
            var header = TypeEnumHelper<Symbol256>.DataTypeHeader;

            Assert.AreEqual(TypeEnumEx.Symbol256, header.TEOFS.TypeEnum);
            Assert.AreEqual(256, header.FixedSizeSize);

            var size = TypeEnumHelper<Symbol256>.FixedSize;
            Assert.AreEqual(256, size);
        }

        [Test]
        public void CouldGetUnknownFixedSize()
        {
            for (int i = 1; i <= 128; i++)
            {
                var te = new TypeEnumOrFixedSize((byte)i);
                Assert.AreEqual(i, te.Size);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new TypeEnumOrFixedSize((byte)0);
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new TypeEnumOrFixedSize((byte)129);
            });
        }

        [Test, Explicit("bench")]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void SizeGetterBench()
        {
            var count = 2_000_000L;
            var sum = 0L;
            for (int r = 0; r < 20; r++)
            {
                using (Benchmark.Run("Size Get", count * 255))
                {
                    for (int _ = 0; _ < count; _++)
                    {
                        for (byte b = 0; b < 255; b++)
                        {
                            sum += TypeEnumHelper.GetSize(b);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test]
        public void ArrayVariantHeader()
        {
            var vhArr = TypeEnumHelper<int[]>.CreateTypeInfo().Header;
            Assert.AreEqual(TypeEnumEx.Array, vhArr.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vhArr.TEOFS1.TypeEnum);

            var vhJArr = TypeEnumHelper<int[][][][][][][][][]>.CreateTypeInfo().Header;
            Console.WriteLine(vhJArr.TEOFS);
            Console.WriteLine(vhJArr.TEOFS1);
            Console.WriteLine(vhJArr.TEOFS2);
            Console.WriteLine("----");

            Assert.AreEqual(TypeEnumEx.JaggedArray, vhJArr.TEOFS.TypeEnum);
            Assert.AreEqual(8, vhJArr.TEOFS2.Size);
            Assert.AreEqual(TypeEnumEx.Int32, vhJArr.TEOFS1.TypeEnum);

            var vhJArrKvp = TypeEnumHelper<KeyValuePair<int, int>[][][][][][][][][]>.CreateTypeInfo().Header;
            Console.WriteLine(vhJArrKvp.TEOFS);
            Console.WriteLine(vhJArrKvp.TEOFS1);
            Console.WriteLine(vhJArrKvp.TEOFS2);
            Assert.AreEqual(TypeEnumEx.JaggedArray, vhJArrKvp.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.CompositeType, vhJArrKvp.TEOFS1.TypeEnum);
            Assert.AreEqual(8, vhJArrKvp.TEOFS2.Size);
        }

        [Test]
        public void KnownScalarsVariantHeader()
        {
            TestType<sbyte>(TypeEnumEx.Int8);
            TestType<short>(TypeEnumEx.Int16);
            TestType<int>(TypeEnumEx.Int32);
            TestType<long>(TypeEnumEx.Int64);

            TestType<byte>(TypeEnumEx.UInt8);
            TestType<ushort>(TypeEnumEx.UInt16);
            TestType<uint>(TypeEnumEx.UInt32);
            TestType<ulong>(TypeEnumEx.UInt64);

            TestType<float>(TypeEnumEx.Float32);
            TestType<double>(TypeEnumEx.Float64);

            TestType<decimal>(TypeEnumEx.Decimal);
            TestType<SmallDecimal>(TypeEnumEx.SmallDecimal);

            TestType<bool>(TypeEnumEx.Bool);
            TestType<char>(TypeEnumEx.Utf16Char);
            TestType<UUID>(TypeEnumEx.UUID);

            TestType<DateTime>(TypeEnumEx.DateTime);
            TestType<Timestamp>(TypeEnumEx.Timestamp);

            TestType<Symbol>(TypeEnumEx.Symbol);
            TestType<Symbol32>(TypeEnumEx.Symbol32);
            TestType<Symbol64>(TypeEnumEx.Symbol64);
            TestType<Symbol128>(TypeEnumEx.Symbol128);
            TestType<Symbol256>(TypeEnumEx.Symbol256);

            void TestType<T>(TypeEnumEx expectedTe)
            {
                var h = TypeEnumHelper<T>.DataTypeHeader;
                Assert.AreEqual(expectedTe, h.TEOFS.TypeEnum);
                Assert.IsTrue(h.TEOFS1 == default && h.TEOFS2 == default);
                Assert.IsTrue(h.IsScalar);

                var teofs = new TypeEnumOrFixedSize(expectedTe);

                Assert.AreEqual(BinarySerializerEx.SizeOf<T>(default(T), out _, SerializationFormat.Binary), teofs.Size);

                Assert.AreEqual(Unsafe.SizeOf<T>(), h.TEOFS.Size);
                Assert.AreEqual(Unsafe.SizeOf<T>(), TypeEnumHelper<T>.FixedSize);
                if (typeof(T) != typeof(DateTime))
                {
                    Assert.AreEqual(TypeHelper<T>.PinnedSize, TypeEnumHelper<T>.FixedSize);
                }
            }
        }

        [Test]
        public void TupleTVariantHeader()
        {
            var vh = TypeEnumHelper<(int, int)>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Console.WriteLine("----");
            Assert.AreEqual(TypeEnumEx.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(8, TypeEnumHelper<(int, int)>.FixedSize);

            vh = TypeEnumHelper<Tuple<int, int>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Console.WriteLine("----");
            Assert.AreEqual(TypeEnumEx.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(8, TypeEnumHelper<(int, int)>.FixedSize);

            vh = TypeEnumHelper<KeyValuePair<int, int>>.CreateTypeInfo().Header;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Console.WriteLine("----");
            Assert.AreEqual(TypeEnumEx.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(8, TypeEnumHelper<KeyValuePair<int, int>>.FixedSize);

            // same but huge types, Tuple2T with Schema
            vh = TypeEnumHelper<KeyValuePair<KeyValuePair<int, int>[][][][][][][][][], KeyValuePair<int, int>[][][][][][][][][]>>.CreateTypeInfo().Header;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnumEx.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.CompositeType, vh.TEOFS1.TypeEnum);
        }

        [Test]
        public void Tuple2VariantHeader()
        {
            var vh = TypeEnumHelper<(int, long)>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnumEx.Tuple2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int64, vh.TEOFS2.TypeEnum);
            Assert.AreEqual(12, TypeEnumHelper<(int, long)>.FixedSize);

            vh = TypeEnumHelper<TaggedKeyValue<Timestamp, double>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnumEx.Tuple2Byte, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Timestamp, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Float64, vh.TEOFS2.TypeEnum);
            Assert.AreEqual(17, TypeEnumHelper<TaggedKeyValue<Timestamp, double>>.FixedSize);

            vh = TypeEnumHelper<KeyValuePair<int, long>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnumEx.Tuple2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int64, vh.TEOFS2.TypeEnum);

            vh = TypeEnumHelper<KeyValuePair<int, KeyValuePair<int, int>[][][][][][][][][]>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnumEx.Tuple2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnumEx.CompositeType, vh.TEOFS2.TypeEnum);
        }

        [Test]
        public void TupleSize()
        {
            Console.WriteLine(Unsafe.SizeOf<(byte, long)>());
            Console.WriteLine(TypeHelper<(byte, long)>.FixedSize);
            Console.WriteLine(Unsafe.SizeOf<TupleTest<byte, long>>());
            Console.WriteLine(TypeHelper<TupleTest<byte, long>>.FixedSize);

            Console.WriteLine(Unsafe.SizeOf<(byte, long, string)>());

            Console.WriteLine(TypeHelper<DateTime>.PinnedSize);
        }
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
