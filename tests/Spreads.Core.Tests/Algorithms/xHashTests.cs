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
            var rng = new Random();
            var len = 1024;
            var mem = BufferPool.Retain(len);
            var ptr = (IntPtr)mem.Pointer;
            rng.NextBytes(mem.Span);
            var count = 5_000_000;
            var sum = 0UL;
            using (Benchmark.Run("xxHash", count))
            {
                for (int i = 0; i < count; i++)
                {
                    sum += XxHash.CalculateHash(ptr, len);
                }
            }
            Benchmark.Dump();
            Console.WriteLine(sum);
        }
    }
}
