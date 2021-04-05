// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Serialization;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class VectorStorageSerialization
    {
        [Test]
        public void CouldSerializeVectorStorage()
        {
            var rng = new Random(42);
            var count = 100_000;
            var arr = new SmallDecimal[count];

            var r = PrivateMemory<SmallDecimal>.Create(count);
            var vec = r.GetVec();
            vec[0] = new SmallDecimal(1000 * 1.0, 4);
            for (int i = 1; i < count; i++)
            {
                vec[i] = vec[i - 1] + new SmallDecimal((double) vec[i - 1] * (0.02 + -0.04 * rng.NextDouble()), 4);
            }

            var vs = RetainedVec.Create(r, 0, r.Length);

            var vsT = new RetainedVec<SmallDecimal>(vs);

            var payload = count * Unsafe.SizeOf<double>() + 4;

            foreach (SerializationFormat format in ((SerializationFormat[]) Enum.GetValues(typeof(SerializationFormat))).OrderBy(e => e.ToString()))
            {
                var len = BinarySerializer.SizeOf(in vsT, out var rm, format);

                var destination = BufferPool.Retain(len);
                var destinationDb = new DirectBuffer(destination);

                var len1 = BinarySerializer.Write(in vsT, destinationDb, rm, format);
                Assert.AreEqual(destination.Length, destinationDb.Length);

                Assert.AreEqual(len, len1);

                var flags = destinationDb.Read<VersionAndFlags>(0);
                Assert.AreEqual(format, flags.SerializationFormat);
                var header = destinationDb.Read<DataTypeHeader>(0);
                Assert.AreEqual(TypeEnum.Array, header.TEOFS.TypeEnum);
                Assert.AreEqual(TypeEnum.SmallDecimal, header.TEOFS1.TypeEnum);
                Assert.AreEqual(Unsafe.SizeOf<SmallDecimal>(), header.TEOFS1.Size);

                var len2 = BinarySerializer.Read(destinationDb, out RetainedVec<SmallDecimal> value);
                Assert.AreEqual(destination.Length, destinationDb.Length);

                Assert.AreEqual(len, len2);
                Assert.AreEqual(vs.Length, value.Storage.Length);

                for (int i = 0; i < count; i++)
                {
                    SmallDecimal left;
                    SmallDecimal right;
                    if ((left = vs.UnsafeReadUnaligned<SmallDecimal>(i)) != (right = value.Storage.UnsafeReadUnaligned<SmallDecimal>(i)))
                    {
                        Console.WriteLine("Not equals");
                    }
                }

                Assert.IsTrue(vs.Clone(0, vs.Length).GetSpan<SmallDecimal>().SequenceEqual(value.Storage.Clone(0, value.Storage.Length).GetSpan<SmallDecimal>()));

                Console.WriteLine($"{format} len: {len:N0} x{Math.Round((double) payload / len, 2)}");

                destination.Dispose();
                value.Storage.Dispose();
            }

            vs.Dispose();
        }
    }
}
