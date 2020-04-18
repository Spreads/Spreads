// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Core.Tests.Collections;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Utils;

namespace Spreads.Core.Tests.Buffers
{
    // [Category("CI")]
    [TestFixture]
    public unsafe class RetainedVecTests
    {
        class Test
        {
            public object this[Range index]
            {
                get => null;
            }

            public object this[Index index]
            {
                get => null;
            }
        }

        [Test, Explicit("output")]
        public void SizeOfVectorStorage()
        {
            Test x = new Test();
            var y = x[1..^1];

            ObjectLayoutInspector.TypeLayout.PrintLayout<RetainedVec>();
        }

        [Test]
        public void Equality()
        {
            RetainedVec vs1 = default;
            RetainedVec vs2 = default;
            Assert.AreEqual(vs1, vs2);
            Assert.AreEqual(vs1.Length, 0);

            var count = 1000;
            var rm = BuffersTestHelper.CreateFilledRM(count);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            Assert.AreNotEqual(vs1, vs);

            var vsCopy = vs.Clone(0, vs.Length, true);
            var vsSlice = vs.Clone(0, vs.Length - 1, true);

            Assert.AreEqual(vs, vsCopy);
            Assert.AreNotEqual(vs, vsSlice);

            vs.Dispose();
        }

        [Test]
        public void CouldCreateVsAndReadElements()
        {
            var count = 1000;
            var rm = BuffersTestHelper.CreateFilledRM(count);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            Assert.AreEqual(rm.Length, vs.Length);
            long sum = 0L;
            for (int i = 0; i < rm.Length; i++)
            {
                var vi = vs.UnsafeReadUnaligned<long>(i);
                if (vi != i)
                {
                    Assert.Fail("vi != i");
                }

                sum += vs.UnsafeReadUnaligned<int>(i);
            }

            Console.WriteLine(sum);

            vs.Dispose();
        }

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

        //[Test]
        //public void CouldDisposeEmpty()
        //{
        //    VectorStorage.Empty.Dispose();
        //}

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void VectorStorageReadBench()
        {
            var count = (int) TestUtils.GetBenchCount(1_000_000, 100);
            var rounds = 10;
            var mult = 500;
            var rm = BuffersTestHelper.CreateFilledRM(count);

            var vs = RetainedVec.Create(rm, 0, rm.Length);

            Assert.AreEqual(rm.Length, vs.Length);

            int sum = 0;
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("VS Read", vs.Length * mult))
                {
                    for (int _ = 0; _ < mult; _++)
                    {
                        for (int i = 0; i < vs.Length; i++)
                        {
                            var vi = vs.UnsafeReadUnaligned<int>(i);
                            //if (vi != i)
                            //{
                            //    Assert.Fail("vi != i");
                            //}

                            unchecked
                            {
                                sum += vi;
                            }
                        }
                    }
                }
            }

            Benchmark.Dump();
            Console.WriteLine(sum);
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void SliceDisposeBenchmark()
        {
            // 6.3 MOPS
            var count = (int) TestUtils.GetBenchCount(1_000_000, 100);
            var rounds = 10;
            var bufferSize = 1000;
            var rm = BuffersTestHelper.CreateFilledRM(bufferSize);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            Assert.AreEqual(rm.Length, vs.Length);
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Slice/Dispose", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var vs1 = vs.Clone(0, vs.Length, externallyOwned: true);
                        vs1.Dispose();
                    }
                }
            }

            Benchmark.Dump();

            vs.Dispose();
        }
    }
}