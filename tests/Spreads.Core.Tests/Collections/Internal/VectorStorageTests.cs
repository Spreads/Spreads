// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections.Internal;
using Spreads.Utils;

namespace Spreads.Core.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public unsafe class VectorStorageTests
    {
        [Test]
        public void CouldCreateVsAndReadElements()
        {
            var count = 1000;
            var arr = Enumerable.Range(0, count).ToArray();
            var r = ArrayMemory<int>.Create(arr, 0, arr.Length, externallyOwned: true, pin: true);
            var vs = VectorStorage.Create(r, 0, r.Length);

            Assert.AreEqual(arr.Length, vs.Length);
            long sum = 0L;
            for (int i = 0; i < arr.Length; i++)
            {
                var vi = vs.DangerousGet<int>(i);
                if (vi != i)
                {
                    Assert.Fail("vi != i");
                }
                sum += vs.DangerousGet<int>(i);
            }

            Console.WriteLine(sum);

            vs.Dispose();

            Assert.IsTrue(vs.IsDisposed);
        }

        [Test]
        public void CouldDisposeEmpty()
        {
            VectorStorage.Empty.Dispose();
        }

        [Test, Explicit("long running")]
        public void VectorStorageReadBench()
        {
            var count = 1_000_000;
            var rounds = 10;
            var mult = 500;
            var arr = Enumerable.Range(0, count).ToArray();

            var mem = ArrayMemory<int>.Create(arr, 0, arr.Length, externallyOwned: true, pin: true);

            var stride = 2;

            var vs = VectorStorage.Create(mem, (stride - 1), mem.Length - (stride - 1), stride);

            Assert.AreEqual(arr.Length / stride, vs.Length);

            int sum = 0;
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("VS Read", vs.Length * mult))
                {
                    var vector = vs.GetVector<int>();
                    for (int _ = 0; _ < mult; _++)
                    {
                        for (int i = 0; i < vs.Length; i++)
                        {
                            var vi = vector[i];
                            if (vi != i * stride + (stride - 1))
                            {
                                Assert.Fail("vi != i * 2");
                            }

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
            var vs = VectorStorage.Create(mem, 0, mem.Length);

            Assert.AreEqual(arr.Length, vs.Length);
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Slice/Dispose", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var vs1 = vs.Slice(0, vs.Vec.Length, 1, externallyOwned: true);
                        vs1.Dispose();
                    }
                }
            }

            Benchmark.Dump();

            vs.Dispose();

            Assert.IsTrue(vs.IsDisposed);
        }
    }
}
