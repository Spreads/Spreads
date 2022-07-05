// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Shouldly;
using Spreads.Buffers;
using Spreads.Core.Tests.Collections;
using Spreads.Utils;

namespace Spreads.Core.Tests.Buffers
{
    // [Category("CI")]
    [TestFixture]
    public unsafe class RetainedVecTests
    {
#if NETCOREAPP
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
#endif

        [Test]
        public void Equality()
        {
            RetainedVec vs1 = default;
            RetainedVec vs2 = default;
            vs2.ShouldBe(vs1);
            vs1.Length.ShouldBe(0);

            var count = 1000;
            var rm = BuffersTestHelper.CreateFilledRM(count);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            vs.ShouldNotBe(vs1);

            var vsCopy = vs.Clone(0, vs.Length, true);
            var vsSlice = vs.Clone(0, vs.Length - 1, true);

            vsCopy.ShouldBe(vs);
            vsSlice.ShouldNotBe(vs);

            vs.Dispose();
        }

        [Test]
        public void CouldCreateVsAndReadElements()
        {
            var count = 1000;
            var rm = BuffersTestHelper.CreateFilledRM(count);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            vs.Length.ShouldBe(rm.Length);
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
            var count = (int)TestUtils.GetBenchCount(1_000_000, 100);
            var rounds = 10;
            var mult = 500;
            var rm = BuffersTestHelper.CreateFilledRM(count);

            var vs = RetainedVec.Create(rm, 0, rm.Length);

            vs.Length.ShouldBe(rm.Length);

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
            var count = (int)TestUtils.GetBenchCount(1_000_000, 100);
            var rounds = 10;
            var bufferSize = 1000;
            var rm = BuffersTestHelper.CreateFilledRM(bufferSize);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            vs.Length.ShouldBe(rm.Length);
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
