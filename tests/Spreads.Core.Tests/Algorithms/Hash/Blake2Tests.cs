// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NSec.Cryptography;
using NUnit.Framework;
using Spreads.Algorithms.Hash.BLAKE2b;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Blake2b = Spreads.Algorithms.Hash.Blake2b;

//using System.Runtime.Intrinsics;
//using System.Runtime.Intrinsics.X86;

namespace Spreads.Core.Tests.Algorithms
{
    [Category("CI")]
    [TestFixture]
    public class Blake2Tests
    {
        [Test]
        public void CouldHash()
        {
            var hashLength = 32;

#if NETCOREAPP3_0
            NSec.Cryptography.Blake2b b = default;
            if (hashLength == 32)
            {
                b = HashAlgorithm.Blake2b_256;
            }
            else if (hashLength == 64)
            {
                b = HashAlgorithm.Blake2b_512;
            }
            else
            {
                Assert.Fail("hashLength != 32 || 64");
            }
#endif
            var rng = new Random();
            var size = 48;

            var rmb = BufferPool.Retain(size);
            var bytes = new DirectBuffer(rmb);

            var hash0 = new byte[hashLength];
            var hash1 = new byte[hashLength];

            var count = TestUtils.GetBenchCount(1000_000);
            var rounds = 10;

            // warm up
            for (int i = 0; i < count / 10; i++)
            {
                Blake2b.ComputeAndWriteHash(hashLength, bytes, hash0);
            }
#if NETCOREAPP3_0
            for (int i = 0; i < count / 10; i++)
            {
                b.Hash(bytes.Span, hash1);
            }
#endif

            // var ctx = Blake2b.CreateIncrementalHasher(32);

            for (int r = 0; r < rounds; r++)
            {
                rng.NextBytes(rmb.GetSpan());

                using (Benchmark.Run("Spreads (MBsec)", count * size, false))
                {
                    for (int i = 0; i < count; i++)
                    {
                        Blake2b.ComputeAndWriteHash(hashLength, bytes, hash0);
                    }
                }
#if NETCOREAPP3_0
                using (Benchmark.Run("Libsodium (MBsec)", count * size, false))
                {
                    for (int i = 0; i < count; i++)
                    {
                        b.Hash(bytes.Span, hash1);
                    }
                }

                Assert.IsTrue(hash0.SequenceEqual(hash1));
#endif
            }

            Benchmark.Dump();
            rmb.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void CouldHashIncrementalBench()
        {
            Console.WriteLine("Context size: " + Unsafe.SizeOf<Blake2bContext>());

            var steps = TestUtils.GetBenchCount(50_000, 2);
            var incrSize = 64;
            var rng = new Random(42);
            var byteLength = incrSize * steps;

            var rmb = BufferPool.Retain((int)byteLength);
            var bytes = new DirectBuffer(rmb);

            rng.NextBytes(rmb.GetSpan());

            var hashLength = 32;

            NSec.Cryptography.Blake2b b = default;
            if (hashLength == 32)
            {
                b = HashAlgorithm.Blake2b_256;
            }
            else if (hashLength == 64)
            {
                b = HashAlgorithm.Blake2b_512;
            }
            else
            {
                Assert.Fail("hashLength != 32 || 64");
            }

            // var size = 32L;

            var hash0 = new byte[hashLength];
            var hash1 = new byte[hashLength];

            var rounds = 50;

            // warm up
            {
                Blake2b.ComputeAndWriteHash(hashLength, bytes, hash0);
            }

            {
                b.Hash(bytes.Span, hash1);
            }

            for (int r = 0; r < rounds; r++)
            {
                using (var stat = Benchmark.Run("Spreads (MBsec)", byteLength, false))
                {
                    // for (int rr = 0; rr < 100; rr++)
                    {
                        var ctx = Blake2b.CreateIncrementalHasher(hashLength);
                        var sp = bytes.Span;
                        for (int i = 0; i < steps; i++)
                        {
                            var slice = bytes.Slice(i * incrSize, incrSize);
                            ctx.UpdateHash(slice, hash0);
                            
                        }
                    }
                }

                //using (var stat = Benchmark.Run("Libsodium (MBsec)", byteLength, false))
                //{
                //    var sp = bytes.Span;
                //    for (int i = 0; i < steps; i++)
                //    {
                //        var span = sp.Slice(0, i * incrSize + incrSize);
                //        b.Hash(span, hash1);
                //    }
                //}

                //Assert.IsTrue(hash0.SequenceEqual(hash1), "Different hash vs LibSodium");
            }

            Benchmark.Dump($"Incremental hashing by {incrSize} bytes {steps} times");
            rmb.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void CouldHashChainedBench()
        {
            Console.WriteLine("Context size: " + Unsafe.SizeOf<Blake2bContext>());

            var steps = TestUtils.GetBenchCount(5_000, 2);
            var incrSize = 64;
            var rng = new Random(42);
            var byteLength = incrSize * steps;

            var rmb = BufferPool.Retain((int)byteLength);
            var bytes = new DirectBuffer(rmb);
            
            rng.NextBytes(rmb.GetSpan());

            var hashLength = 32;

            // var size = 32L;

            var hash0 = new byte[hashLength];
            var hash1 = new byte[hashLength];

            var rounds = 50;

            for (int r = 0; r < rounds; r++)
            {
                using (var stat = Benchmark.Run("Spreads (MBsec)", byteLength * 100, false))
                {
                    for (int rr = 0; rr < 100; rr++)
                    {
                        for (int i = 0; i < steps; i++)
                        {
                            var span = bytes.Slice(i * incrSize, incrSize);

                            Blake2b.ComputeAndWriteHash(hashLength, hash0, span, hash0);
                        }
                    }
                }
            } 

            Benchmark.Dump($"Chained hashing by {incrSize} bytes {steps} times");
            rmb.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void CouldHashIncrementalCorrectness()
        {
            Console.WriteLine("Context size: " + Unsafe.SizeOf<Blake2bContext>());

            var steps = TestUtils.GetBenchCount(20_000, 10);
            var incrSize = 1;
            var rng = new Random(42);
            var byteLength = incrSize * steps;

            var rmb = BufferPool.Retain((int)byteLength);
            var bytes = new DirectBuffer(rmb);
            
            rng.NextBytes(rmb.GetSpan());

            Console.WriteLine("Filled bytes");
            var hashLength = 32;

            NSec.Cryptography.Blake2b b = default;
            if (hashLength == 32)
            {
                b = HashAlgorithm.Blake2b_256;
            }
            else if (hashLength == 64)
            {
                b = HashAlgorithm.Blake2b_512;
            }
            else
            {
                Assert.Fail("hashLength != 32 || 64");
            }

            var hash0 = new byte[hashLength];
            var hash1 = new byte[hashLength];
            var hash2 = new byte[hashLength];

            var rounds = 1;

            for (int r = 0; r < rounds; r++)
            {
                var ctx = Blake2b.CreateIncrementalHasher(hashLength);

                var sp = bytes.Span;

                for (int i = 0; i < steps; i++)
                {
                    var incrSpan = bytes.Slice(i * incrSize, incrSize);
                    var runningBuff = bytes.Slice(0, i * incrSize + incrSize);
                    var runningSpan = sp.Slice(0, i * incrSize + incrSize);

                    // Incremental Spreads
                    ctx.UpdateHash(incrSpan, hash0);

                    // Running Spreads
                    Blake2b.ComputeAndWriteHash(hashLength, runningBuff, hash1);

                    // Libsodium
                    b.Hash(runningSpan, hash2);

                    Assert.IsTrue(hash0.SequenceEqual(hash1), "hash0.SequenceEqual(hash1)");
                    Assert.IsTrue(hash0.SequenceEqual(hash2), "hash0.SequenceEqual(hash2)");

                    if (i % 10000 == 0)
                    {
                        Console.WriteLine($"Round {r}, Iteration {i}");
                    }
                }
            }
            rmb.Dispose();
        }
    }
}
