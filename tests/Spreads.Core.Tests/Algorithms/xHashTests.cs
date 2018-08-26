// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Algorithms.Hash;
using Spreads.Buffers;
using Spreads.Utils;
using System;

namespace Spreads.Tests.Algorithms
{
    [TestFixture]
    public class xHashTests
    {
        [Test, Explicit("long running")]
        public unsafe void xxHashBenchmark()
        {
            var rng = new Random(42);
            var len = 10;
            var mem = BufferPool.Retain(len);
            var ptr = (byte*)mem.Pointer;
            rng.NextBytes(mem.Span);
            var count = 200_000_000;
            uint hash = 0;
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    hash = XxHash.CalculateHash(ptr, len);
                }
            }
            Benchmark.Dump();
            Console.WriteLine(hash);
        }


        [Test, Explicit("long running")]
        public unsafe void xxHashIncrementalBenchmark()
        {
            var rng = new Random(42);
            var len = 10;
            var mem = BufferPool.Retain(len);
            var ptr = (byte*)mem.Pointer;
            rng.NextBytes(mem.Span);
            var count = 50_000_000;
            var sum = 0UL;

            
            uint digest = 0;
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var state = new XxHash.XxHashState32(digest);
                    XxHash.Update(ref state, ptr, len);
                    digest = XxHash.Digest(state);
                }
            }
            Benchmark.Dump();
            Console.WriteLine(digest);
        }

        [Test, Explicit("long running")]
        public unsafe void xxHashIncrementalBenchmark2()
        {
            var rng = new Random(42);
            var len = 10;
            var mem = BufferPool.Retain(len);
            var ptr = (byte*)mem.Pointer;
            rng.NextBytes(mem.Span);
            var count = 50_000_000;
            var sum = 0UL;


            uint digest = 0;
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    digest = XxHash.CalculateHash(ptr, len, digest);
                }
            }
            Benchmark.Dump();
            Console.WriteLine(digest);
        }
    }
}
