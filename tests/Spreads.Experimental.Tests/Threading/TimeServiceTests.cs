// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class TimeServiceTests
    {
        [Test]
        [Explicit("long running")]
        public void TimeServiceProducesUniqueValues()
        {
            var ptr = Marshal.AllocHGlobal(8);
            var ts = new TimeService(ptr, 1);

            var previous = ts.CurrentTime;
            for (int i = 0; i < 1_00_000_000; i++)
            {
                var current = ts.CurrentTime;
                if (current <= previous)
                {
                    Assert.Fail();
                }

                var delta = current.Nanos - previous.Nanos;
                if (delta > 1)
                {
                    Console.WriteLine($"{current.Nanos} - {delta.ToString("N")}");
                }

                if (i % 50_000_000 == 0)
                {
                    ts.UpdateTime();
                }
                previous = current;
            }
        }

        [Test]
        [Explicit("long running")]
        public void TimeServiceProducesUniqueValuesWithSpinner()
        {
            var ptr = Marshal.AllocHGlobal(8);
            var ts = new TimeService(ptr, 1);
            var cts = new CancellationTokenSource();
            ts.StartSpinUpdate(cts.Token, ThreadPriority.Lowest, spinCount: 150);
            Thread.Sleep(100);
            var previous = ts.CurrentTime;
            var count = 100_000_000;
            var deltas = new int[count];

            for (int i = 0; i < count; i++)
            {
                var current = ts.CurrentTime;
                if (current <= previous)
                {
                    Assert.Fail();
                }

                var delta = current.Nanos - previous.Nanos;
                if (delta < int.MaxValue)
                {
                    deltas[i] = (int)delta;
                }
                //if (delta > 1)
                //{
                //    Console.WriteLine($"{current.Nanos} - {delta.ToString("N")}");
                //}

                //if (i % 50_000_000 == 0)
                //{
                //    ts.UpdateTime();
                //}
                previous = current;
            }
            cts.Cancel();

            var nonOnes = deltas.Where(d => d > 1).ToArray();
            Array.Sort(nonOnes);

            Console.WriteLine("Average delta: " + nonOnes.Average());
            Console.WriteLine("Median delta: " + nonOnes[nonOnes.Length / 2]);
            Console.WriteLine($"NonOne count: {nonOnes.Length} {Math.Round(nonOnes.Length * 100.0 / count, 2)}%");

            Console.WriteLine("FINISHED");
        }

        [Test]
        [Explicit("long running")]
        public void TimeServiceGetterBenchmark()
        {
            var ptr = Marshal.AllocHGlobal(8);
            // TimeService.Start(ptr);
            var count = 1_000_000_000;
            using (Benchmark.Run("TimeServiceGetter", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var current = TimeService.Default.CurrentTime;
                }
            }
        }

        [Test]
        [Explicit("long running")]
        public void TimeServiceUpdateTime()
        {
            var ptr = Marshal.AllocHGlobal(8);
            // TimeService.Start(ptr);
            var count = 1_000;
            using (Benchmark.Run("TimeServiceGetter", count))
            {
                for (int i = 0; i < count; i++)
                {
                    var current = TimeService.Default.CurrentTime;
                    TimeService.Default.UpdateTime();
                    TimeService.Default.UpdateTime();
                }
            }
        }

        [Test]
        [Explicit("long running")]
        public void TimeServiceProducesIncreasingValuesWithSpinner()
        {
            var ptr = Marshal.AllocHGlobal(8);
            var ts = new TimeService(ptr, 10);
            var _cts = new CancellationTokenSource();
            ts.StartSpinUpdate(_cts.Token);
            Thread.Sleep(1000);

            const int count = 100_000_000;

            Action act = () =>
            {
                var previous = ts.CurrentTime;
                for (int i = 0; i < count; i++)
                {
                    var current = ts.CurrentTime;
                    if (current <= previous)
                    {
                        Assert.Fail($"Current time {current.Nanos} is <= previous {previous.Nanos}");
                    }

                    previous = current;
                }
            };

            var t1 = new Thread(() => act());
            var t2 = new Thread(() => act());
            var t3 = new Thread(() => act());
            var t4 = new Thread(() => act());
            t1.Start();
            t2.Start();
            t3.Start();
            t4.Start();

            t1.Join();
            t2.Join();
            t3.Join();
            t4.Join();

            _cts.Cancel();
        }
    }
}
