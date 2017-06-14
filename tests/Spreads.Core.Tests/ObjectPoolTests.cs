// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using NUnit.Framework;
using Spreads.Collections.Concurrent;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class ObjectPoolTests
    {
        public class DummyPoolable
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
        [Test]
        [Ignore]
        public void PoolPerformance()
        {
            int capacity = Environment.ProcessorCount * 2;
            var arrayPool = new ObjectPool<DummyPoolable>(() => new DummyPoolable(), capacity);
            for (int round = 0; round < 10; round++)
            {
                TestPool(arrayPool);
                Console.WriteLine("----------");
            }
        }

        internal void TestPool(ObjectPool<DummyPoolable> pool)
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
            Console.WriteLine(pool.GetType().Name + ": " + sw.Elapsed + " GC: " + (GC.CollectionCount(0) - gen0));
            //}
        }
    }
}