// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class RetainableMemoryPoolTests
    {
        [Test]
        [Explicit("bench")]
        public void PoolPerformance()
        {
            const int perCoreCapacity = 20;

            var rmp = new RetainableMemoryPool<byte>(maxBuffersPerBucketPerCore: perCoreCapacity);

            for (int round = 0; round < 20; round++)
            {
                RmpBenchmark(rmp, "rmp");
                ArrayPoolBenchmark<byte>();
            }

            Benchmark.Dump();
            Console.WriteLine($"NativeAllocated: {BuffersStatistics.AllocatedNativeMemory.Value}");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        internal void RmpBenchmark<T>(RetainableMemoryPool<T> pool, string testCase)
        {
            var count = 10_000_000;
            var threads = Environment.ProcessorCount;
            using (Benchmark.Run(testCase, count * 2 * threads))
            {
                Task.WaitAll(Enumerable.Range(0, threads).Select(_ => Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var x1 = pool.RentMemory(64 * 1024);
                        var x2 = pool.RentMemory(64 * 1024);
                        x1.Dispose();
                        x2.Dispose();
                    }
                }, TaskCreationOptions.LongRunning)).ToArray());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        internal void ArrayPoolBenchmark<T>()
        {
            var pool = ArrayPool<T>.Shared;
            var count = 10_000_000;
            var threads = Environment.ProcessorCount;
            using (Benchmark.Run("array_pool", count * 2 * threads))
            {
                Task.WaitAll(Enumerable.Range(0, threads).Select(_ => Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var x1 = pool.Rent(64 * 1024);
                        var x2 = pool.Rent(64 * 1024);
                        pool.Return(x1);
                        pool.Return(x2);
                    }
                }, TaskCreationOptions.LongRunning)).ToArray());
            }
        }
    }
}