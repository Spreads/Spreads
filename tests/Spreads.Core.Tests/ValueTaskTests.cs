// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System;
using System.Threading.Channels;
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

        [Test, Ignore("not working")]
        public async Task ReusableWhenAnyTest()
        {
            var count = 1000;

            var ch1 = Channel.CreateBounded<int>(new BoundedChannelOptions(10) { SingleReader = false, AllowSynchronousContinuations = true, FullMode = BoundedChannelFullMode.Wait });
            var ch2 = Channel.CreateBounded<int>(new BoundedChannelOptions(10) { SingleReader = false, AllowSynchronousContinuations = true, FullMode = BoundedChannelFullMode.Wait });

            // var whenAny = new ReusableValueTaskWhenAny<int>();

            var _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                for (int i = 0; i < count; i++)
                {
                    await ch1.Writer.WriteAsync(i);
                }
            });

            var __ = Task.Run(async () =>
            {
                await Task.Delay(100);
                for (int i = 0; i < count; i++)
                {
                    await ch2.Writer.WriteAsync(i);
                }
            });

            var c = 0;
            using (Benchmark.Run("WhenAny", count))
            {
                Task.Run(async () =>
            {
                while (c < count)
                {
                    var t1 = ch1.Reader.ReadAsync();
                    var t2 = ch2.Reader.ReadAsync();
                    await new WhenAnyAwiter<int>(t1, t2);
                    if (t1.IsCompleted)
                    {
                        var x = t1.Result;
                    }
                    if (t2.IsCompleted)
                    {
                        var x = t2.Result;
                    }
                    c++;
                    // Console.WriteLine(c);
                }
            }).Wait();
            }

            Benchmark.Dump();
        }
    }
}
