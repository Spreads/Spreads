// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Linq;
using System.Threading;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class SpinningThreadpoolTests
    {
        public class Completable
        {
            private readonly SpinningThreadPool _tp;
            public static long TotalCount;
            private bool recursive;

            public Completable(SpinningThreadPool tp)
            {
                _tp = tp;
            }

            public void ExecuteCompletion()
            {
                // for (int i = 0; i < 10; i++)
                {
                    Interlocked.Increment(ref TotalCount);
                }
            }
        }

        [Test, Explicit("Benchmark")]
        public void ThreadPoolPerformanceBenchmark()
        {
            var count = 50_000_000;

            var items = Enumerable.Range(1, 100).Select(x => (Action)(new Completable(SpinningThreadPool.Default)).ExecuteCompletion).ToArray();

            using (Benchmark.Run("SpinningThreadPool", count))
            {
                for (int i = 0; i < count; i++)
                {
                    SpinningThreadPool.Default.UnsafeQueueCompletableItem(items[i % 100], true);
                    // Thread.SpinWait(1);
                }

                SpinningThreadPool.Default.Dispose();
                SpinningThreadPool.Default.WaitForThreadsExit();
            }

            Console.WriteLine($"Total: {Completable.TotalCount}");
            foreach (var item in items)
            {
            }
            Benchmark.Dump();
        }
    }
}