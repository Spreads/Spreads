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
            var rmp = RetainableMemoryPool<byte>.Default;

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
            var prevRent = BuffersStatistics.RentReturnedBeforeYield.Value;
            var prevReturn = BuffersStatistics.ReturnReturnedBeforeYield.Value;
            var prevSCRent = BuffersStatistics.SameCoreRentContention.Value;
            var prevSCReturn = BuffersStatistics.SameCoreReturnContention.Value;
            var prevRentLoop = BuffersStatistics.RentLoop.Value;
            var prevReturnLoop = BuffersStatistics.ReturnLoop.Value;

            var count = TestUtils.GetBenchCount(10_000_000, 1000_000);
            var threadCount = Environment.ProcessorCount;
            using (Benchmark.Run(testCase, count * 2 * threadCount))
            {
                Task.WaitAll(Enumerable.Range(0, threadCount).Select(_ => Task.Factory.StartNew(() =>
                {
                    var size = 8 * 1024;
                    
                    var x0 = pool.RentMemory(size);
                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var x1 = pool.RentMemory(size);

                            var x2 = pool.RentMemory(size);
                            // if(x1 == x2)
                            //     ThrowHelper.FailFast("WTF!");
                            x1.Dispose();
                            x2.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"EXCEPTION: {ex}");
                    }

                    x0.Dispose();
                }, TaskCreationOptions.LongRunning)).ToArray());
            }

            // Console.WriteLine(
            //     $"Rent: {BuffersStatistics.RentReturnedBeforeYield.Value - prevRent:N0} Return: {BuffersStatistics.ReturnReturnedBeforeYield.Value - prevReturn:N0} " +
            //     $"SCRent: {BuffersStatistics.SameCoreRentContention.Value - prevSCRent:N0} SCReturn: {BuffersStatistics.SameCoreReturnContention.Value - prevSCReturn:N0}" +
            //     $"RentLoop: {BuffersStatistics.RentLoop.Value - prevRentLoop:N0} ReturnLoop: {BuffersStatistics.ReturnLoop.Value - prevReturnLoop:N0}");

            // Console.WriteLine(pool.InspectObjects().Count());
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        internal void ArrayPoolBenchmark<T>()
        {
            var pool = ArrayPool<T>.Shared;
            var count = TestUtils.GetBenchCount(10_000_000, 100_000);
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