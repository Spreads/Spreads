// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class ValueTaskTests
    {
        [Test]
        public async Task CouldUseIDeltaMethods()
        {
            var tasks = new ValueTask<int>[4];

            tasks[0] = new ValueTask<int>(1);
            tasks[1] = new ValueTask<int>(Task.Run(async () =>
            {
                await Task.Delay(100);
                return 42;
            }));
            tasks[2] = new ValueTask<int>(Task.Run(async () =>
            {
                await Task.Delay(200);
                ThrowHelper.ThrowInvalidOperationException();
                return 0;
            }));

            tasks[3] = new ValueTask<int>(Task.Run<int>(async () => throw new OperationCanceledException()));

            await tasks.WhenAll();

            Assert.AreEqual(tasks[0].Result, 1);
            Assert.AreEqual(tasks[1].Result, 42);
            Assert.IsTrue(tasks[2].IsFaulted);
            Assert.IsTrue(tasks[3].IsCanceled);

            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = tasks[2].Result;
            });

            Assert.Throws<OperationCanceledException>(() =>
            {
                var _ = tasks[3].Result;
            });
        }

        public class DummyNotifier<T> : IAsyncNotifier<T>
        {
            public DummyNotifier()
            {
            }

            public bool IsCompleted => false;

            public ValueTask<T> Updated => new ValueTask<T>(default(T));
        }

        [Test, Explicit("")]
        public async Task SortedMapNotifierTest()
        {
            var count = 10_000_000; //_000_000; //_000_000;

            var sm1 = new Spreads.Collections.SortedMap<int, int>(count);

            var addTask = Task.Run(async () =>
            {
                await Task.Delay(100);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (i != 2)
                        {
                            sm1.TryAddLast(i, i);
                            Thread.SpinWait(100);
                        }
                    }

                    await sm1.Complete();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });

            var c = 0;

            // await addTask;

            Task.Run(async () =>
            {
                var rounds = 1;
                using (Benchmark.Run("SM.Updated", count * rounds))
                {
                    Thread.CurrentThread.Name = "MNA";
                    for (int r = 0; r < rounds; r++)
                    {
                        var cursor = sm1.GetCursor();

                        while (await cursor.MoveNextAsync())
                        {
                            c++;
                        }

                        Console.WriteLine($" Sync: {AsyncCursorCounters.SyncCount}, Async: {AsyncCursorCounters.AsyncCount}, Await: {AsyncCursorCounters.AwaitCount}");
                    }
                }

                Benchmark.Dump();
                Console.WriteLine(c);
            }).Wait();
        }

        [Test, Explicit("")]
        public async Task ReusableWhenAnyTest()
        {
            var count = 10_000;

            var sm1 = new Spreads.Collections.SortedMap<int, int>();
            var sm2 = new Spreads.Collections.SortedMap<int, int>();

            var whenAny = new Spreads.Collections.Experimental.ReusableWhenAny2(sm1, sm2); //  ReusableValueTaskWhenAny<int>();

            var _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                for (int i = 0; i < count; i++)
                {
                    await sm1.TryAddLast(i, i);
                }
            });

            var __ = Task.Run(async () =>
            {
                await Task.Delay(100);
                for (int i = 0; i < count; i++)
                {
                    await sm2.TryAddLast(i, i);
                }
            });

            var c = 0;
            using (Benchmark.Run("WhenAny", count))
            {
                Task.Run(async () =>
            {
                while (c < count)
                {
                    await whenAny.GetTask(); //  new WhenAnyAwiter<int>(t1, t2);
                    c++;
                    // Console.WriteLine(c);
                }
            }).Wait();
            }

            Benchmark.Dump();
        }
    }
}
