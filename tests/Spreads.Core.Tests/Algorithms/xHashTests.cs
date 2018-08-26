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
            var count = 50_000_000;
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

            var state = new XxHash.XxHashState32(0);
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    XxHash.Update(ref state, ptr, len);
                }
            }
            var digest = XxHash.Digest(state);
            Benchmark.Dump();
            Console.WriteLine(digest);
        }

        [Test, Explicit("long running")]
        public unsafe void xxHashIncrementalBenchmark2()
        {
            var rng = new Random(42);
            var len = 16;
            var mem = BufferPool.Retain(len);
            var ptr = (byte*)mem.Pointer;
            rng.NextBytes(mem.Span);
            var count = 100_000_000;
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

       
        [Test, Explicit("long running")]
        public unsafe void Crc32CHashIncrementalBenchmark()
        {
            var rng = new Random(42);
            var len = 16;
            var offset = 4;
            var mem = BufferPool.Retain(len + offset);
            var ptr = (byte*)mem.Pointer + offset;
            rng.NextBytes(mem.Span.Slice(offset));
            var count = 100_000_000;
            var sum = 0UL;

            var mod8 = (ulong) ptr % 8;

            uint digest = 0;
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    digest = Crc32C.CalculateCrc32C(ptr, len, digest);
                }
            }
            Benchmark.Dump();
            Console.WriteLine(digest);
        }

        [Test, Explicit("long running")]
        public unsafe void Crc32CManagedHashIncrementalBenchmark2()
        {
            var rng = new Random(42);
            var len = 16;
            var mem = BufferPool.Retain(len);
            var ptr = (byte*)mem.Pointer;
            rng.NextBytes(mem.Span);
            var count = 100_000_000;
            var sum = 0UL;

            uint digest = 0;
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    digest = Crc32C.CalculateCrc32CManaged(ptr, len, digest);
                }
            }
            Benchmark.Dump();
            Console.WriteLine(digest);
        }


        [Test, Explicit("long running")]
        public unsafe void Crc32CRalphHashIncrementalBenchmark2()
        {
            var rng = new Random(42);
            var len = 16;
            var arr = new byte[len];
            var memory = (Memory<byte>) arr;
            var handle = memory.Pin();
            var ptr = handle.Pointer;
            rng.NextBytes(memory.Span);
            var count = 100_000_000;
            var sum = 0UL;

            var crc32C = new Ralph.Crc32C.Crc32C();
            uint digest = 0;
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    crc32C.Update(arr, 0, arr.Length);
                    digest = crc32C.GetIntValue();
                }
            }
            Benchmark.Dump();
            Console.WriteLine(digest);
        }

        
    }
}
