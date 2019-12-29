// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BinarySerializer = Spreads.Serialization.BinarySerializer;

namespace Spreads.Core.Tests.Serialization
{
    [Category("CI")]
    [TestFixture]
    public class TypeEnumTests
    {
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        [BinarySerialization(blittableSize: 256 + 8)]
        private struct Symbol256xLong
        {
            public Symbol256 Symbol256;
            public long Long;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        [BinarySerialization(blittableSize: 128)]
        private struct Symbol128Wrapper
        {
            public Symbol128 Symbol128;
        }

        [Test]
        public void LargeFixedSize()
        {
            var header = TypeEnumHelper<Symbol256xLong>.DataTypeHeader;

            Assert.AreEqual(TypeEnum.FixedSize, header.TEOFS.TypeEnum);
            Assert.AreEqual(256 + 8, header.UserFixedSize);

            var size = TypeEnumHelper<Symbol256xLong>.FixedSize;
            Assert.AreEqual(256 + 8, size);
        }

        [Test]
        public void MaxInlinedFixedSize()
        {
            var header = TypeEnumHelper<Symbol128Wrapper>.DataTypeHeader;

            Assert.AreEqual(128, header.TEOFS.Size);
            Assert.AreEqual(0, header.UserFixedSize);

            var size = TypeEnumHelper<Symbol128Wrapper>.FixedSize;
            Assert.AreEqual(128, size);
        }

        [Test]
        public void Symbol128Size()
        {
            var header = TypeEnumHelper<Symbol128>.DataTypeHeader;

            Assert.AreEqual(TypeEnum.Symbol128, header.TEOFS.TypeEnum);
            Assert.AreEqual(128, header.FixedSize);

            var size = TypeEnumHelper<Symbol128>.FixedSize;
            Assert.AreEqual(128, size);
        }

        [Test]
        public void Symbol256Size()
        {
            var header = TypeEnumHelper<Symbol256>.DataTypeHeader;

            Assert.AreEqual(TypeEnum.Symbol256, header.TEOFS.TypeEnum);
            Assert.AreEqual(256, header.FixedSize);

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
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
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
            var vhArr = TypeEnumHelper<int[]>.CreateValidateTypeInfo().Header;
            Assert.AreEqual(TypeEnum.Array, vhArr.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Int32, vhArr.TEOFS1.TypeEnum);

            var vhJArr = TypeEnumHelper<int[][][][][][][][][]>.CreateValidateTypeInfo().Header;
            Console.WriteLine(vhJArr.TEOFS);
            Console.WriteLine(vhJArr.TEOFS1);
            Console.WriteLine(vhJArr.TEOFS2);
            Console.WriteLine("----");

            Assert.AreEqual(TypeEnum.JaggedArray, vhJArr.TEOFS.TypeEnum);
            Assert.AreEqual(8, vhJArr.TEOFS2.Size);
            Assert.AreEqual(TypeEnum.Int32, vhJArr.TEOFS1.TypeEnum);

            var vhJArrKvp = TypeEnumHelper<KeyValuePair<int, int>[][][][][][][][][]>.CreateValidateTypeInfo().Header;
            Console.WriteLine(vhJArrKvp.TEOFS);
            Console.WriteLine(vhJArrKvp.TEOFS1);
            Console.WriteLine(vhJArrKvp.TEOFS2);
            Assert.AreEqual(TypeEnum.JaggedArray, vhJArrKvp.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.CompositeType, vhJArrKvp.TEOFS1.TypeEnum);
            Assert.AreEqual(8, vhJArrKvp.TEOFS2.Size);
        }

        [Test]
        public void KnownScalarsVariantHeader()
        {
            TestType<sbyte>(TypeEnum.Int8);
            TestType<short>(TypeEnum.Int16);
            TestType<int>(TypeEnum.Int32);
            TestType<long>(TypeEnum.Int64);

            TestType<byte>(TypeEnum.UInt8);
            TestType<ushort>(TypeEnum.UInt16);
            TestType<uint>(TypeEnum.UInt32);
            TestType<ulong>(TypeEnum.UInt64);

            TestType<float>(TypeEnum.Float32);
            TestType<double>(TypeEnum.Float64);

            TestType<decimal>(TypeEnum.Decimal);
            TestType<SmallDecimal>(TypeEnum.SmallDecimal);

            TestType<bool>(TypeEnum.Bool);
            TestType<char>(TypeEnum.Utf16Char);
            TestType<UUID>(TypeEnum.UUID);

            TestType<DateTime>(TypeEnum.DateTime);
            TestType<Timestamp>(TypeEnum.Timestamp);

            TestType<Symbol>(TypeEnum.Symbol);
            TestType<Symbol32>(TypeEnum.Symbol32);
            TestType<Symbol64>(TypeEnum.Symbol64);
            TestType<Symbol128>(TypeEnum.Symbol128);
            TestType<Symbol256>(TypeEnum.Symbol256);

            void TestType<T>(TypeEnum expectedTe)
            {
                var h = TypeEnumHelper<T>.DataTypeHeader;
                Assert.AreEqual(expectedTe, h.TEOFS.TypeEnum);
                Assert.IsTrue(h.TEOFS1 == default && h.TEOFS2 == default);
                Assert.IsTrue(h.IsScalar);

                var teofs = new TypeEnumOrFixedSize(expectedTe);

                Assert.AreEqual(BinarySerializer.SizeOf<T>(default(T), out _, SerializationFormat.Binary), DataTypeHeader.Size + teofs.Size);

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
            Assert.AreEqual(TypeEnum.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(8, TypeEnumHelper<(int, int)>.FixedSize);

            vh = TypeEnumHelper<Tuple<int, int>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Console.WriteLine("----");
            Assert.AreEqual(TypeEnum.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(8, TypeEnumHelper<(int, int)>.FixedSize);

            vh = TypeEnumHelper<KeyValuePair<int, int>>.CreateValidateTypeInfo().Header;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Console.WriteLine("----");
            Assert.AreEqual(TypeEnum.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(8, TypeEnumHelper<KeyValuePair<int, int>>.FixedSize);

            // same but huge types, Tuple2T with Schema
            vh = TypeEnumHelper<KeyValuePair<KeyValuePair<int, int>[][][][][][][][][], KeyValuePair<int, int>[][][][][][][][][]>>.CreateValidateTypeInfo().Header;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnum.TupleT2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.CompositeType, vh.TEOFS1.TypeEnum);
        }

        [Test]
        public void Tuple2VariantHeader()
        {
            var vh = TypeEnumHelper<(int, long)>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnum.Tuple2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnum.Int64, vh.TEOFS2.TypeEnum);
            Assert.AreEqual(12, TypeEnumHelper<(int, long)>.FixedSize);

            vh = TypeEnumHelper<TaggedKeyValue<Timestamp, double>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnum.Tuple2Byte, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Timestamp, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnum.Float64, vh.TEOFS2.TypeEnum);
            Assert.AreEqual(17, TypeEnumHelper<TaggedKeyValue<Timestamp, double>>.FixedSize);

            vh = TypeEnumHelper<KeyValuePair<int, long>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnum.Tuple2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnum.Int64, vh.TEOFS2.TypeEnum);

            vh = TypeEnumHelper<KeyValuePair<int, KeyValuePair<int, int>[][][][][][][][][]>>.DataTypeHeader;
            Console.WriteLine(vh.TEOFS);
            Console.WriteLine(vh.TEOFS1);
            Console.WriteLine(vh.TEOFS2);
            Assert.AreEqual(TypeEnum.Tuple2, vh.TEOFS.TypeEnum);
            Assert.AreEqual(TypeEnum.Int32, vh.TEOFS1.TypeEnum);
            Assert.AreEqual(TypeEnum.CompositeType, vh.TEOFS2.TypeEnum);
        }
    }
}
