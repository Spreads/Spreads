// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NSec.Cryptography;
using NUnit.Framework;
using Spreads.Algorithms.Hash.BLAKE2b;
using Spreads.Buffers;
using Spreads.Utils;
using Blake2b = Spreads.Algorithms.Hash.Blake2b;

namespace Spreads.Core.Tests.Algorithms.Hash
{
    [Category("CI")]
    [TestFixture]
    public class Blake2Tests
    {
        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void CouldHash()
        {
            var hashLength = 32;

#if NET5_0
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
#if NET5_0
            for (int i = 0; i < count / 10; i++)
            {
                b.Hash(bytes.Span, hash1);
            }
#endif

            // var ctx = Blake2b.CreateIncrementalHasher(32);

            for (int r = 0; r < rounds; r++)
            {
                rng.NextBytes(rmb.GetSpan());

                CouldHash_Spreads(count, size, hashLength, bytes, hash0);

                // CouldHash_B2Fast_CWH(count, size, bytes, hashLength, hash0);
                //
                // CouldHash_B2Fast_Init(count, size, bytes, hashLength, hash0);
                //
                // CouldHash_B2FastUnsafe(count, size, bytes, hashLength, hash0);

#if NET5_0
                CouldHash_LibSodium(count, size, b, bytes, hash1);

                Assert.IsTrue(hash0.SequenceEqual(hash1));
#endif
            }

            Benchmark.Dump();
            rmb.Dispose();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void CouldHash_Spreads(long count, int size, int hashLength, DirectBuffer bytes, byte[] hash0)
        {

            using (Benchmark.Run("Spreads (MBsec)", count * size, false))
            {
                for (int i = 0; i < count; i++)
                {
                    Blake2b.ComputeAndWriteHash(hashLength, bytes, hash0);
                }
            }
        }

        // [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        // private static void CouldHash_B2Fast_CWH(long count, int size, DirectBuffer bytes, int hashLength, byte[] hash0)
        // {
        //     using (Benchmark.Run("B2Fast CWH (MBsec)", count * size, false))
        //     {
        //         var input = bytes.Span;
        //         for (int i = 0; i < count; i++)
        //         {
        //             Blake2Fast.Blake2b.ComputeAndWriteHash(hashLength, input, hash0);
        //         }
        //     }
        // }
        //
        // [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        // private static void CouldHash_B2Fast_Init(long count, int size, DirectBuffer bytes, int hashLength, byte[] hash0)
        // {
        //
        //     using (Benchmark.Run("B2Fast Init (MBsec)", count * size, false))
        //     {
        //         var input = bytes.Span;
        //         for (int i = 0; i < count; i++)
        //         {
        //             var hs = default(Blake2bHashState);
        //             hs.Init(hashLength);
        //             hs.Update(input);
        //             hs.Finish(hash0);
        //         }
        //     }
        // }

        // [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        // private static void CouldHash_B2FastUnsafe(long count, int size, DirectBuffer bytes, int hashLength, byte[] hash0)
        // {
        //
        //     using (Benchmark.Run("B2Fast Unsafe (MBsec)", count * size, false))
        //     {
        //         var input = bytes.Span;
        //         for (int i = 0; i < count; i++)
        //         {
        //             var hs = default(Blake2bHashState);
        //             hs.UnsafeInit(hashLength);
        //             hs.UnsafeUpdate(input);
        //             hs.UnsafeFinish(hash0);
        //         }
        //     }
        // }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void CouldHash_LibSodium(long count, int size, NSec.Cryptography.Blake2b b, DirectBuffer bytes, byte[] hash1)
        {

            using (Benchmark.Run("Libsodium (MBsec)", count * size, false))
            {
                for (int i = 0; i < count; i++)
                {
                    b.Hash(bytes.Span, hash1);
                }
            }
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

            var rmb = BufferPool.Retain((int) byteLength);
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

                // using (var stat = Benchmark.Run("B2Fast (MBsec)", byteLength, false))
                // {
                //     // for (int rr = 0; rr < 100; rr++)
                //     {
                //         var ctx = Blake2Fast.Blake2b.CreateIncrementalHasher(hashLength);
                //         var input = bytes.Span;
                //         for (int i = 0; i < steps; i++)
                //         {
                //             var slice = input.Slice(i * incrSize, incrSize);
                //             ctx.Update(slice);
                //             var ctxCopy = ctx;
                //             ctxCopy.Finish(hash0);
                //         }
                //     }
                // }
                //
                // using (var stat = Benchmark.Run("B2Fast Unsafe (MBsec)", byteLength, false))
                // {
                //     // for (int rr = 0; rr < 100; rr++)
                //     {
                //         // var ctx = Blake2Fast.Blake2b.CreateIncrementalHasher(hashLength);
                //         var input = bytes.Span;
                //         for (int i = 0; i < steps; i++)
                //         {
                //             var slice = input.Slice(i * incrSize, incrSize);
                //             // ctx.UnsafeUpdate(slice);
                //             var ctxCopy = ctx;
                //             ctxCopy.UnsafeFinish(hash0);
                //         }
                //     }
                // }

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

            var rmb = BufferPool.Retain((int) byteLength);
            var bytes = new DirectBuffer(rmb);

            rng.NextBytes(rmb.GetSpan());

            var hashLength = 32;

            // var size = 32L;

            var hash0 = new byte[hashLength];
            var hash1 = new byte[hashLength];

            var rounds = 25;

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

                // Chained_B2Fast_CWH(byteLength, steps, bytes, incrSize, hashLength, hash0);
                //
                // Chained_B2Fast_Init(byteLength, steps, bytes, incrSize, hashLength, hash0);
                //
                // Chained_B2Fast_Unsafe(byteLength, steps, bytes, incrSize, hashLength, hash0);
            }

            Benchmark.Dump($"Chained hashing by {incrSize} bytes {steps} times");
            rmb.Dispose();
        }

        // [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        // private static void Chained_B2Fast_CWH(long byteLength, long steps, DirectBuffer bytes, int incrSize, int hashLength, byte[] hash0)
        // {
        //
        //     using (var stat = Benchmark.Run("B2Fast CWH (MBsec)", byteLength * 100, false))
        //     {
        //         for (int rr = 0; rr < 100; rr++)
        //         {
        //             for (int i = 0; i < steps; i++)
        //             {
        //                 var span = bytes.Span.Slice(i * incrSize, incrSize);
        //
        //                 Blake2Fast.Blake2b.ComputeAndWriteHash(hashLength, hash0, span, hash0);
        //             }
        //         }
        //     }
        // }

        // [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        // private static void Chained_B2Fast_Init(long byteLength, long steps, DirectBuffer bytes, int incrSize, int hashLength, byte[] hash0)
        // {
        //
        //     using (var stat = Benchmark.Run("B2Fast Init (MBsec)", byteLength * 100, false))
        //     {
        //         for (int rr = 0; rr < 100; rr++)
        //         {
        //             for (int i = 0; i < steps; i++)
        //             {
        //                 var span = bytes.Span.Slice(i * incrSize, incrSize);
        //
        //                 var hs = default(Blake2bHashState);
        //                 hs.Init(hashLength, hash0);
        //                 hs.Update(span);
        //                 hs.Finish(hash0);
        //             }
        //         }
        //     }
        // }

        // [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        // private static void Chained_B2Fast_Unsafe(long byteLength, long steps, DirectBuffer bytes, int incrSize, int hashLength, byte[] hash0)
        // {
        //     using (var stat = Benchmark.Run("B2Fast Unsafe (MBsec)", byteLength * 100, false))
        //     {
        //         for (int rr = 0; rr < 100; rr++)
        //         {
        //             for (int i = 0; i < steps; i++)
        //             {
        //                 var span = bytes.Span.Slice(i * incrSize, incrSize);
        //
        //                 var hs = default(Blake2bHashState);
        //                 hs.UnsafeInit(hashLength, hash0);
        //                 hs.UnsafeUpdate(span);
        //                 hs.UnsafeFinish(hash0);
        //             }
        //         }
        //     }
        // }

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

            var rmb = BufferPool.Retain((int) byteLength);
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
            var hash0_B2F = new byte[hashLength];
            var hash1 = new byte[hashLength];
            var hash1_B2F = new byte[hashLength];
            var hash2 = new byte[hashLength];

            var rounds = 1;

            for (int r = 0; r < rounds; r++)
            {
                var ctx = Blake2b.CreateIncrementalHasher(hashLength);
                // var ctx_B2F = Blake2Fast.Blake2b.CreateIncrementalHasher(hashLength);

                var sp = bytes.Span;

                for (int i = 0; i < steps; i++)
                {
                    var incrSpan = bytes.Slice(i * incrSize, incrSize);
                    var runningBuff = bytes.Slice(0, i * incrSize + incrSize);
                    var runningSpan = sp.Slice(0, i * incrSize + incrSize);

                    // Incremental Spreads
                    ctx.UpdateHash(incrSpan, hash0);

                    // Incremental B2F
                    // ctx_B2F.Update(incrSpan.Span);
                    // var copy = ctx_B2F;
                    // copy.Finish(hash0_B2F);

                    // Running Spreads
                    Blake2b.ComputeAndWriteHash(hashLength, runningBuff, hash1);

                    // Running B2F
                    // Blake2Fast.Blake2b.ComputeAndWriteHash(hashLength, runningBuff.Span, hash1_B2F);

                    // Libsodium
                    b.Hash(runningSpan, hash2);

                    Assert.IsTrue(hash0.SequenceEqual(hash1), "hash0.SequenceEqual(hash1)");
                    Assert.IsTrue(hash0.SequenceEqual(hash2), "hash0.SequenceEqual(hash2)");

                    Assert.IsTrue(hash0_B2F.SequenceEqual(hash1_B2F), "hash0_B2F.SequenceEqual(hash1_B2F)");
                    Assert.IsTrue(hash0_B2F.SequenceEqual(hash2), "hash0_B2F.SequenceEqual(hash2)");

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