// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Utils;

namespace Spreads.Core.Tests.Performance
{
    [TestFixture]
    public class InterlockedVsFences
    {
        private const int workload = 0;
        private volatile bool _concurrent;
        private long _value;

        [Test]
        public void InterlockedVsFencesTest()
        {
            // Without concurrent reads interlocked is OKish at c.200Mops, but fences are still 4x faster
            // With concurrent reads Interlocked case drops by 5x, while fences perf remains the same on Intel x86.
            _concurrent = true;
            Task.Factory.StartNew(() =>
            {
                var sum = 0L;
                while (_concurrent)
                {
                    sum += Volatile.Read(ref _value);
                    for (int i = 0; i < workload; i++)
                    {
                        Dummy();
                    }
                }

                Console.WriteLine(sum);
            }, TaskCreationOptions.LongRunning);


            var rounds = 10;
            var iterations = TestUtils.GetBenchCount(_concurrent ? 10_000_000: 100_000_000, 100_000);

            for (int r = 0; r < rounds; r++)
            {
                long sum1, sum2 = 0L;
                using (Benchmark.Run("Interlocked", iterations + r))
                {
                    sum1 = IncrementInterlocked(iterations + r);
                }

                using (Benchmark.Run("Fences", iterations + r))
                {
                    sum2 = IncrementFences(iterations + r);
                }

                Assert.AreEqual(sum1, sum2);
            }

            _concurrent = false;

            Benchmark.Dump();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization| MethodImplOptions.NoInlining)]
#endif
        private long IncrementInterlocked(long count)
        {
            var sum = 0L;
            _value = 0;
            for (int i = 0; i < count; i++)
            {
                sum += Interlocked.Increment(ref _value);
            }

            return sum;
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization| MethodImplOptions.NoInlining)]
#endif
        private long IncrementFences(long count)
        {
            var sum = 0L;
            _value = 0;
            for (int i = 0; i < count; i++)
            {
                Volatile.Write(ref _value, Volatile.Read(ref _value) + 1L);
                
                // Uncommenting this degrades performance closer to the interlocked case
                // Interlocked.MemoryBarrier();
                
                sum += Volatile.Read(ref _value);
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Dummy()
        {

        }
    }
}