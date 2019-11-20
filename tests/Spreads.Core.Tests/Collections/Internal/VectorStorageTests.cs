// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections.Internal;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Spreads.DataTypes;

namespace Spreads.Core.Tests.Collections.Internal
{
    // [Category("CI")]
    [TestFixture]
    public unsafe class VectorStorageTests
    {
        [Test, Explicit("output")]
        public void SizeOfVectorStorage()
        {
            ObjectLayoutInspector.TypeLayout.PrintLayout<VecStorage>();
        }

        [Test]
        public void Equality()
        {
            VecStorage vs1 = default;
            VecStorage vs2 = default;
            Assert.AreEqual(vs1, vs2);
            Assert.AreEqual(vs1.Vec.Length, 0);

            var count = 1000;
            var arr = Enumerable.Range(0, count).ToArray();
            var r = ArrayMemory<int>.Create(arr, 0, arr.Length, externallyOwned: true, pin: true);
            var vs = VecStorage.Create(r, 0, r.Length);

            Assert.AreNotEqual(vs1, vs);

            var vsCopy = vs.Slice(0, vs.Vec.Length, true);
            var vsSlice = vs.Slice(0, vs.Vec.Length - 1, true);

            Assert.AreEqual(vs, vsCopy);
            Assert.AreNotEqual(vs, vsSlice);

            vs.Dispose();
        }

        [Test]
        public void CouldCreateVsAndReadElements()
        {
            var count = 1000;
            var arr = Enumerable.Range(0, count).ToArray();
            var r = ArrayMemory<int>.Create(arr, 0, arr.Length, externallyOwned: true, pin: true);
            var vs = VecStorage.Create(r, 0, r.Length);

            Assert.AreEqual(arr.Length, vs.Vec.Length);
            long sum = 0L;
            for (int i = 0; i < arr.Length; i++)
            {
                var vi = vs.Vec.DangerousGet<int>(i);
                if (vi != i)
                {
                    Assert.Fail("vi != i");
                }
                sum += vs.Vec.DangerousGet<int>(i);
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

            arr[0] = new SmallDecimal(1000 * 1.0, 4);

            for (int i = 1; i < count; i++)
            {
                arr[i] = arr[i - 1] + new SmallDecimal((double)arr[i - 1] * (0.02 + -0.04 * rng.NextDouble()), 4);
            }
            // arr = Enumerable.Range(0, count).Select(x => new SmallDecimal(1000 + (double)x + (double)Math.Round(0.1 * rng.NextDouble(), 5), precision:3)).ToArray();

            var r = ArrayMemory<SmallDecimal>.Create(arr, 0, arr.Length, externallyOwned: true, pin: true);
            var vs = VecStorage.Create(r, 0, r.Length);

            var vsT = new VecStorage<SmallDecimal>(vs);

            var payload = count * Unsafe.SizeOf<double>() + 4;

            foreach (SerializationFormat format in ((SerializationFormat[])Enum.GetValues(typeof(SerializationFormat))).OrderBy(e => e.ToString()))
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

                var len2 = BinarySerializer.Read(destinationDb, out VecStorage<SmallDecimal> value);
                Assert.AreEqual(destination.Length, destinationDb.Length);

                Assert.AreEqual(len, len2);
                Assert.AreEqual(vs.Vec.Length, value.Storage.Vec.Length);


                for (int i = 0; i < count; i++)
                {
                    SmallDecimal left;
                    SmallDecimal right;
                    if ((left = vs.Vec.DangerousGetRef<SmallDecimal>(i)) != (right = value.Storage.Vec.DangerousGetRef<SmallDecimal>(i)))
                    {
                        Console.WriteLine("Not equals");
                    }
                }

                Assert.IsTrue(vs.Vec.Slice(0, vs.Vec.Length).AsSpan<SmallDecimal>().SequenceEqual(value.Storage.Vec.Slice(0, value.Storage.Vec.Length).AsSpan<SmallDecimal>()));

                Console.WriteLine($"{format} len: {len:N0} x{Math.Round((double)payload/len, 2)}");

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

        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        [Test, Explicit("long running")]
        public void VectorStorageReadBench()
        {
            var count = 1_000_000;
            var rounds = 10;
            var mult = 500;
            var arr = Enumerable.Range(0, count).ToArray();

            var mem = ArrayMemory<int>.Create(arr, 0, arr.Length, externallyOwned: true, pin: true);

            var vs = VecStorage.Create(mem, 0, mem.Length);

            Assert.AreEqual(arr.Length, vs.Vec.Length);

            int sum = 0;
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("VS Read", vs.Vec.Length * mult))
                {
                    for (int _ = 0; _ < mult; _++)
                    {
                        for (int i = 0; i < vs.Vec.Length; i++)
                        {
                            var vi = vs.Vec.DangerousGet<int>(i);
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

        [Test, Explicit("long running")]
        public void SliceDisposeBenchmark()
        {
            // 6.3 MOPS
            var count = 1_000_000;
            var rounds = 10;
            var arrSize = 1000;
            var arr = Enumerable.Range(0, arrSize).ToArray();
            var mem = ArrayMemory<int>.Create(arr, 0, arr.Length, externallyOwned: true, pin: true);
            var vs = VecStorage.Create(mem, 0, mem.Length);

            Assert.AreEqual(arr.Length, vs.Vec.Length);
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Slice/Dispose", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var vs1 = vs.Slice(0, vs.Vec.Length, externallyOwned: true);
                        vs1.Dispose();
                    }
                }
            }

            Benchmark.Dump();

            vs.Dispose();

        }
    }
}
