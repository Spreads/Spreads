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
using Spreads.Buffers;
using Spreads.Collections.Concurrent;
using Spreads.Utils;

namespace Spreads.Core.Tests.Collections.Concurrent
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
            TypeLayout.PrintLayout<MPMCPoolCore<DummyPoolable>>();
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
            var mpmcPool = new MPMCPoolCore<DummyPoolable>(dummyFactory, capacity);
            var objectPool = new ObjectPoolCore<DummyPoolable>(dummyFactory, capacity);
            var lockedObjectPool = new LockedObjectPoolCore<DummyPoolable>(dummyFactory, capacity);
            var perCoreMPMCPool = new MPMCPool<DummyPoolable>(dummyFactory, perCoreCapacity);
            var perCoreObjectPool = new ObjectPool<DummyPoolable>(dummyFactory, perCoreCapacity);
            var perCoreLockedObjectPool = new LockedObjectPool<DummyPoolable>(dummyFactory, perCoreCapacity);
            var threads = new int[] {1}; //{1, 2, 4, 6, 8, 12, 24};
            foreach (var t in threads)
            {
                for (int round = 0; round < 20; round++)
                {
                    // MPMCBenchmark();
                    // PoolBenchmark(mpmcPool, "MPMC", t);
                    // PoolBenchmark(objectPool, "OP", t);
                    // PoolBenchmark(lockedObjectPool, "LOP", t);
                    // PoolBenchmark(perCoreMPMCPool, "pcMPMC", t);
                    PoolBenchmark(perCoreObjectPool, "pcOP", t);
                    // PoolBenchmark(perCoreLockedObjectPool, "pcLOP", t);
                }
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        internal void PoolBenchmark<T>(T pool, string testCase, int threads) where T : IObjectPool<DummyPoolable>
        {
            var count = 1_000_000;
            testCase = testCase + "_" + threads;
            using (Benchmark.Run(testCase, count  * threads))
            {
                Task.WaitAll(Enumerable.Range(0, threads).Select(i => Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var x1 = pool.Rent();
                        // var x2 = pool.Rent();
                        pool.Return(x1);
                        // pool.Return(x2);
                    }
                }, TaskCreationOptions.LongRunning)).ToArray());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        internal void MPMCBenchmark()
        {
            var count = 2_000_000;
            var threads = 4;
            var oddQueue = new MPMCQueue(128);
            var evenQueue = new MPMCQueue(128);
            using (Benchmark.Run("MPMCQueue", count * threads))
            {
                Task.WaitAll(Enumerable.Range(0, threads).Select(i =>
                {
                    for (int j = 0; j < 4; j++)
                    {
                        oddQueue.Enqueue(new object());
                        evenQueue.Enqueue(new object());
                    }

                    if (i % 2 == 0)
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            for (int i = 0; i < count; i++)
                            {
                                object x1;
                                do
                                {
                                    x1 = evenQueue.Dequeue();
                                } while (x1 == null);

                                while (!oddQueue.Enqueue(x1))
                                {
                                }
                            }
                        }, TaskCreationOptions.LongRunning);
                    }
                    else
                    {
                        return Task.Factory.StartNew(() =>
                        {
                            for (int i = 0; i < count; i++)
                            {
                                object x1;
                                do
                                {
                                    x1 = oddQueue.Dequeue();
                                } while (x1 == null);

                                while (!evenQueue.Enqueue(x1))
                                {
                                }
                            }
                        }, TaskCreationOptions.LongRunning);
                    }
                }).ToArray());
            }
        }

        [Test]
        [Explicit("bench")]
        public void PoolUnbalancedRentReturn()
        {
            const int perCoreCapacity = 50;
            Func<DummyPoolable> dummyFactory = () => new DummyPoolable();
            var perCoreLockedObjectPool =
                new LockedObjectPool<DummyPoolable>(dummyFactory, perCoreCapacity, allocateOnEmpty: false);

            var queues = Enumerable.Range(0, 4)
                .Select(x => new SingleProducerSingleConsumerQueue<DummyPoolable>()).ToArray();

            var cts = new CancellationTokenSource();
            var totalCount = 0L;

            Task[] producers = new Task[queues.Length];
            Task[] consumers = new Task[queues.Length];

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < queues.Length; i++)
            {
                var queue = queues[i];
                producers[i] = Task.Factory.StartNew(() =>
                {
                    var count = 0L;

                    while (!cts.IsCancellationRequested)
                    {
                        var item = perCoreLockedObjectPool.Rent();
                        if (item != null)
                        {
                            queue.Enqueue(item);
                            count++;
                        }
                    }

                    Interlocked.Add(ref totalCount, count);
                });

                consumers[i] = Task.Factory.StartNew(() =>
                {
                    var count = 0L;
                    while (!cts.IsCancellationRequested)
                    {
                        if (queue.TryDequeue(out var item))
                        {
                            perCoreLockedObjectPool.Return(item);
                            count++;
                        }
                    }

                    Interlocked.Add(ref totalCount, count);
                });
            }

            Thread.Sleep(5_000);
            cts.Cancel();
            Task.WaitAll(producers);
            Task.WaitAll(consumers);
            sw.Stop();

            Console.WriteLine(
                $"MOPS: {(totalCount / 1000000.0) / (sw.ElapsedMilliseconds / 1000.0):N2}, Total count: {totalCount:N0}, elapsed: {sw.ElapsedMilliseconds:N0}");
        }
    }
}