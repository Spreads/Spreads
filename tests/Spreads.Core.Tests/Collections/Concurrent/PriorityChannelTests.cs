// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Concurrent;
using Spreads.Utils;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Collections.Concurrent
{
    [TestFixture]
    public class PriorityChannelTests
    {
        [Test, Explicit("long running")]
        public void PriorityChannelBenchmark()
        {
            var count = 200_000_000;
            var pc = new PriorityChannel<ushort>();

            var wt = Task.Run(() =>
            {
                using (Benchmark.Run("Write", count, true))
                {
                    for (var i = 0; i < count; i++)
                    {
                        pc.TryAdd((ushort)(i % ushort.MaxValue)); //, i >> 3 == 0);
                    }
                }
            });

            

            var rt = Task.Run(() =>
            {
                using (Benchmark.Run("Read", count, true))
                {
                    var c = 0;

                    while (c < count)
                    {
                        pc.TryTake(out var i, out var isPriority);
                        //if (i >> 3 == 0 && !isPriority)
                        //{
                        //    Assert.Fail("i >> 8 == 0 && !isPriority");
                        //}

                        c++;
                    }
                }
            });

            wt.Wait();
            rt.Wait();

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void PriorityChannelBenchmarkSlowWriter()
        {
            var count = 10_000_000;
            var pc = new PriorityChannel<int>();

            var wt = Task.Run(() =>
            {
                using (Benchmark.Run("Write", count, true))
                {
                    for (var i = 0; i < count; i++)
                    {
                        pc.TryAdd(i); //, i >> 3 == 0);
                        Thread.SpinWait(1);
                    }
                }
            });

            var rt = Task.Run(() =>
            {
                using (Benchmark.Run("Read", count, true))
                {
                    var c = 0;

                    while (c < count)
                    {
                        pc.TryTake(out var i, out var isPriority);
                        //if (i >> 3 == 0 && !isPriority)
                        //{
                        //    Assert.Fail("i >> 8 == 0 && !isPriority");
                        //}

                        c++;
                    }
                }
            });
            rt.Wait();

            wt.Wait();

            Benchmark.Dump();
        }


        [Test, Explicit("long running")]
        public void PriorityChannelBenchmarkReadFirst()
        {
            var count = 100_000_000;
            var pc = new PriorityChannel<long>();

            var rt = Task.Run(() =>
            {
                using (Benchmark.Run("Read", count, true))
                {
                    var c = 0;

                    while (c < count)
                    {
                        pc.TryTake(out var i, out var isPriority);
                        //if (i >> 3 == 0 && !isPriority)
                        //{
                        //    Assert.Fail("i >> 8 == 0 && !isPriority");
                        //}

                        c++;
                    }
                }
            });

            Thread.Sleep(100);

            var wt = Task.Run(() =>
            {
                using (Benchmark.Run("Write", count, true))
                {
                    for (var i = 0; i < count; i++)
                    {
                        pc.TryAdd(i); //, i >> 3 == 0);
                    }
                }
            });

            
            rt.Wait();

            wt.Wait();

            Benchmark.Dump();
        }


        [Test, Explicit("long running")]
        public void ConcurrentQueueBenchmark()
        {
            var count = 100_000_000;
            var cq = new ConcurrentQueue<long>();

            var wt = Task.Run(() =>
            {
                using (Benchmark.Run("Write", count, true))
                {
                    for (var i = 0; i < count; i++)
                    {
                        cq.Enqueue(i);
                    }
                }
            });

            var rt = Task.Run(() =>
            {
                using (Benchmark.Run("Read", count, true))
                {
                    var c = 0;

                    while (c < count)
                    {
                        while (!cq.TryDequeue(out var i))
                        { }

                        c++;
                    }
                }
            });
            rt.Wait();
            wt.Wait();

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void BlockingCollectionBenchmark()
        {
            var count = 10_000_000;
            var cq = new BlockingCollection<int>();

            var wt = Task.Run(() =>
            {
                using (Benchmark.Run("Write", count, true))
                {
                    for (var i = 0; i < count; i++)
                    {
                        cq.Add(i);
                    }
                }
            });

            var rt = Task.Run(() =>
            {
                using (Benchmark.Run("Read", count, true))
                {
                    var c = 0;

                    while (c < count)
                    {
                        while (!cq.TryTake(out var i))
                        { }

                        c++;
                    }
                }
            });
            rt.Wait();
            wt.Wait();

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void SPSCQueueBenchmark()
        {
            var count = 100_000_000;
            var spscq = new SingleProducerSingleConsumerQueue<int>();

            var wt = Task.Run(() =>
            {
                using (Benchmark.Run("Write", count, true))
                {
                    for (var i = 0; i < count; i++)
                    {
                        spscq.Enqueue(i);
                    }
                }
            });

            var rt = Task.Run(() =>
            {
                using (Benchmark.Run("Read", count, true))
                {
                    var c = 0;

                    while (c < count)
                    {
                        while (!spscq.TryDequeue(out var i))
                        { }

                        c++;
                    }
                }
            });
            rt.Wait();

            wt.Wait();

            Benchmark.Dump();
        }
    }
}
