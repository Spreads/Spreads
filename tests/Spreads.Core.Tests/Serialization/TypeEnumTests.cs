// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization.Experimental;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class TypeEnumTests
    {
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
                            sum += TypeEnumOrFixedSize.GetSize(b);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test]
        public void ArrayVariantHeader()
        {
            var vhArr = TypeEnumHelper<int[]>.CreateVariantHeader();
            Assert.AreEqual(TypeEnumEx.Array, vhArr.TEOFS.TypeEnum);

            Assert.AreEqual(TypeEnumEx.Int32, vhArr.TEOFS1.TypeEnum);

            var vhJArr = TypeEnumHelper<int[][][][][][][][][]>.CreateVariantHeader();
            Console.WriteLine(vhJArr.TEOFS);
            Console.WriteLine(vhJArr.TEOFS1);
            Console.WriteLine(vhJArr.TEOFS2);
            Console.WriteLine(vhJArr.TEOFS3);
            Assert.AreEqual(TypeEnumEx.JaggedArray, vhJArr.TEOFS.TypeEnum);
            Assert.AreEqual(8, vhJArr.TEOFS1.Size);
            Assert.AreEqual(TypeEnumEx.Int32, vhJArr.TEOFS2.TypeEnum);

            var vhJArrKvp = TypeEnumHelper<KeyValuePair<int, int>[][][][][][][][][]>.CreateVariantHeader();
            Console.WriteLine(vhJArrKvp.TEOFS);
            Console.WriteLine(vhJArrKvp.TEOFS1);
            Console.WriteLine(vhJArrKvp.TEOFS2);
            Console.WriteLine(vhJArrKvp.TEOFS3);
            Assert.AreEqual(TypeEnumEx.JaggedArray, vhJArrKvp.TEOFS.TypeEnum);
            Assert.AreEqual(8, vhJArrKvp.TEOFS1.Size);
            Assert.AreEqual(TypeEnumEx.Tuple2T, vhJArrKvp.TEOFS2.TypeEnum);
            Assert.AreEqual(TypeEnumEx.Int32, vhJArrKvp.TEOFS3.TypeEnum);
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
                var h = TypeEnumHelper<T>.VariantHeader;
                Assert.AreEqual(expectedTe, h.TEOFS.TypeEnum);
                Assert.IsTrue(h.TEOFS1 == default && h.TEOFS3 == default && h.TEOFS3 == default);
                Assert.AreEqual(Unsafe.SizeOf<T>(), h.TEOFS.Size);
            }
        }
    }
}
