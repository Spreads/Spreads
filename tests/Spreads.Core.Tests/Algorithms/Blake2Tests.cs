// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NSec.Cryptography;
using NUnit.Framework;
using Spreads.Algorithms.Hash;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Blake2b = Spreads.Algorithms.Hash.Blake2b;

namespace Spreads.Core.Tests.Algorithms
{
    [TestFixture]
    public class Blake2Tests
    {
        [Test]
        public void CouldHash()
        {
            var hashLength = 32;

#if NETCOREAPP2_1
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
            var size = 128;

            var rmb = BufferPool.Retain(size);
            var bytes = new DirectBuffer(rmb);

            rmb.TryGetArray(out var segment);

            var hash0 = new byte[hashLength];
            var hash1 = new byte[hashLength];

            var count = 1000_000;
            var rounds = 10;

            // warm up
            for (int i = 0; i < count / 10; i++)
            {
                Blake2b.ComputeAndWriteHash(hashLength, bytes, hash0);
            }
#if NETCOREAPP2_1
            for (int i = 0; i < count / 10; i++)
            {
                b.Hash(bytes.Span, hash1);
            }
#endif

            // var ctx = Blake2b.CreateIncrementalHasher(32);

            for (int r = 0; r < rounds; r++)
            {
                rng.NextBytes(segment.Array);

                using (Benchmark.Run("Spreads (MBsec)", count * size, false))
                {
                    for (int i = 0; i < count; i++)
                    {
                        Blake2b.ComputeAndWriteHash(hashLength, bytes, hash0);
                    }
                }
#if NETCOREAPP2_1
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
        }

        [Test, Explicit("long running")]
        public void CouldHashIncrementalBench()
        {
            var steps = 50_000;
            var incrSize = 64;
            var rng = new Random(42);
            var byteLength = incrSize * steps;

            var rmb = BufferPool.Retain(byteLength);
            var bytes = new DirectBuffer(rmb);
            rmb.TryGetArray(out var segment);
            rng.NextBytes(segment.Array);

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

            var rounds = 10;

            // warm up
            {
                Blake2b.ComputeAndWriteHash(hashLength, bytes, hash0);
            }

            {
                b.Hash(bytes.Span, hash1);
            }

            for (int r = 0; r < rounds; r++)
            {
                using (var stat = Benchmark.Run("Spreads (MBsec)", byteLength * 100, false))
                {
                    for (int rr = 0; rr < 100; rr++)
                    {
                        var ctx = Blake2b.CreateIncrementalHasher(hashLength);
                        var sp = bytes.Span;
                        for (int i = 0; i < steps; i++)
                        {
                            var span = bytes.Slice(i * incrSize, incrSize);
                            ctx.Update(span);
                            Blake2bContext.TryFinish(ref ctx, hash0, out var len);
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

                //Assert.IsTrue(hash0.SequenceEqual(hash1));
            }

            Benchmark.Dump($"Incremental hashing by {incrSize} bytes {steps} times");
        }

        [Test, Explicit("long running")]
        public void CouldHashIncrementalCorrectness()
        {
            Console.WriteLine("Context size: " + Unsafe.SizeOf<Blake2bContext>());

            var steps = 200_000;
            var incrSize = 1;
            var rng = new Random(42);
            var byteLength = incrSize * steps;

            var rmb = BufferPool.Retain(byteLength);
            var bytes = new DirectBuffer(rmb);
            rmb.TryGetArray(out var segment);
            rng.NextBytes(segment.Array);

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
                    ctx.Update(incrSpan);
                    Blake2bContext.TryFinish(ref ctx, hash0, out var _);

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
        }
    }
}
