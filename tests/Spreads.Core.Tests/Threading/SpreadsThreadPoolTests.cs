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
    public class SpinningThreadPoolTests
    {
        public class Completable : IThreadPoolWorkItem
        {
            private readonly SpreadsThreadPool _tp;
            public static long TotalCount;
            private bool recursive;

            public Completable(SpreadsThreadPool tp)
            {
                _tp = tp;
            }

            public void Execute()
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
            var count = 100_000_000;

            var items = Enumerable.Range(1, 100).Select(x => new Completable(SpreadsThreadPool.Default)).ToArray();
            var tp = SpreadsThreadPool.Default;
            var th = new Thread(() =>
            {
                using (Benchmark.Run("SpinningThreadPool", count))
                {
                    for (var i = 0L; i < count; i++)
                    {
                        tp.UnsafeQueueCompletableItem(items[i % 100], true);
                        //Thread.SpinWait(1);
                    }

                    tp.Dispose();
                    tp.WaitForThreadsExit();
                }
            });

            th.Priority = ThreadPriority.Normal;
            th.Start();

            th.Join();

            Console.WriteLine($"Total: {Completable.TotalCount}");
            var c = 0;
            foreach (var item in items)
            {
                if (item != null)
                {
                    c++;
                }
            }

            Console.WriteLine(c);
            Benchmark.Dump();
        }
    }
}