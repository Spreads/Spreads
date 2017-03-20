// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Xunit;
using Spreads.Collections.Concurrent;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    public class ObjectPoolsTests
    {
        public class DummyPoolable : IPoolable<DummyPoolable>
        {
            public int I { get; set; }

            public void Init()
            {
            }

            public void Release()
            {
            }
        }

        // https://github.com/dotnet/corefx/pull/14126
        [Fact(Skip = "Long running")]
        public void ComparePoolsPerformance()
        {
            int capacity = Environment.ProcessorCount * 2;
            var arrayPool = new ObjectPoolArray<DummyPoolable>(capacity);
            var bagPool = new ObjectPoolBag<DummyPoolable>(capacity);
            var queuePool = new ObjectPoolQueue<DummyPoolable>(capacity);
            //var mpmcQueuePool = new ObjectPoolMPMCQueue<DummyPoolable>(capacity);
            for (int round = 0; round < 10; round++)
            {
                TestPool(arrayPool);
                TestPool(bagPool);
                TestPool(queuePool);
                //TestPool(mpmcQueuePool);
                Console.WriteLine("----------");
            }
        }

        public void TestPool(IObjectPool<DummyPoolable> pool)
        {
            var sw = new Stopwatch();
            //while (true) {
            int gen0 = GC.CollectionCount(0);
            sw.Restart();
            Task.WaitAll(Enumerable.Range(0, Environment.ProcessorCount * 2).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 1000000; i++)
                {
                    var x1 = pool.Allocate();
                    var x2 = pool.Allocate();
                    Interlocked.MemoryBarrier();
                    //Thread.SpinWait(500);
                    pool.Free(x1);
                    pool.Free(x2);
                }
            })).ToArray());
            sw.Stop();
            Console.WriteLine(pool.GetType().Name + ": " + sw.Elapsed + " " + (GC.CollectionCount(0) - gen0) + " count:" + pool.Count);
            //}
        }
    }
}