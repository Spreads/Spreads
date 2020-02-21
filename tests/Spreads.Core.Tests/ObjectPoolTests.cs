// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ObjectLayoutInspector;
using Spreads.Collections.Concurrent;
using Spreads.Utils;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class ObjectPoolTests
    {
        public class DummyPoolable
        {
        }


        [Test]
        [Explicit("output")]
        public void CorePoolsLayout()
        {
            TypeLayout.PrintLayout<ObjectPoolCore<DummyPoolable>>();
            TypeLayout.PrintLayout<LockedObjectPoolCore<DummyPoolable>>();
            TypeLayout.PrintLayout<ObjectPool<DummyPoolable>.RightPaddedObjectPoolCore>();
            TypeLayout.PrintLayout<LockedObjectPool<DummyPoolable>.RightPaddedLockedObjectPoolCore>();
        }

        [Test]
        [Explicit("bench")]
        public void PoolPerformance()
        {
            const int perCoreCapacity = 20;
            int capacity = Environment.ProcessorCount * perCoreCapacity;
            Func<DummyPoolable> dummyFactory = () => new DummyPoolable();
            var objectPool = new ObjectPoolCore<DummyPoolable>(dummyFactory, capacity);
            var lockedObjectPool = new LockedObjectPoolCore<DummyPoolable>(dummyFactory, capacity);
            var perCoreObjectPool = new ObjectPool<DummyPoolable>(dummyFactory, perCoreCapacity);
            var perCoreLockedObjectPool = new LockedObjectPool<DummyPoolable>(dummyFactory, perCoreCapacity);
            for (int round = 0; round < 50; round++)
            {
                PoolBenchmark(objectPool, "objectPool");
                PoolBenchmark(lockedObjectPool, "lockedObjectPool");
                PoolBenchmark(perCoreObjectPool, "perCoreObjectPool");
                PoolBenchmark(perCoreLockedObjectPool, "perCoreLockedObjectPool");
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        internal void PoolBenchmark<T>(T pool, string testCase) where T : IObjectPool<DummyPoolable>
        {
            var count = 5_000_000;
            var threads = Environment.ProcessorCount;
            using (Benchmark.Run(testCase, count * 2 * threads))
            {
                Task.WaitAll(Enumerable.Range(0, threads).Select(_ => Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var x1 = pool.Rent();
                        var x2 = pool.Rent();
                        pool.Return(x1);
                        pool.Return(x2);
                    }
                }, TaskCreationOptions.LongRunning)).ToArray());
            }
        }
    }
}