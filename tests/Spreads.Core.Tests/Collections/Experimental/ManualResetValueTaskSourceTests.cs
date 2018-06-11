// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Experimental;
using Spreads.Utils;
using System;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Collections.Experimental
{
    [TestFixture]
    public class ManualResetValueTaskSourceTests
    {
        [Test, Explicit("Benchmark")]
        public async Task TestVTS()
        {
            var count = 1_000_000;
            var vts = new TestVTS();

            //var _ = Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        await Task.Delay(10);
            //        vts.Notify();
            //    }

            //});
            for (int r = 0; r < 10; r++)
            {
                using (Benchmark.Run("mna", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        //var __ = Task.Run(async () =>
                        //{
                        //    var vt2 = vts.GetValueTask();
                        //    await vt2;
                        //});

                        await vts.GetValueTask();
                    }
                }
            }

            Benchmark.Dump();
        }

        [Test, Explicit("Benchmark")]
        public async Task ReuseInstanceWithResets_Success()
        {
            var count = 200000;
            Spreads.Collections.Experimental.IAsyncEnumerator<int> enumerator = (new CountAsyncEnumerable(count)).GetAsyncEnumerator();

            using (Benchmark.Run("mna", count))
            {
                try
                {
                    var start = DateTime.UtcNow.Ticks;
                    int total = 0;
                    while (await enumerator.MoveNextAsync())
                    {
                        await Task.Delay(0);
                        unchecked
                        {
                            total += enumerator.Current;
                        }

                        //Console.WriteLine(DateTime.UtcNow.Ticks - start);
                    }
                    //Assert.AreEqual(190, total);
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }
            }

            Benchmark.Dump();
        }
    }
}
