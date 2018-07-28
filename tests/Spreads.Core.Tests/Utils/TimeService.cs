// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Utils;

namespace Spreads.Core.Tests.Utils
{
    [TestFixture]
    public class TimeServiceTests
    {
        [Test]
        [Explicit("long running")]
        public void TimeServiceProducesUniqueValues()
        {
            // multi calls are ok
            TimeService.Start();
            TimeService.Stop();
            TimeService.Stop();
            TimeService.Start();
            TimeService.Start();

            var ptr = Marshal.AllocHGlobal(8);
            TimeService.Stop();
            TimeService.Start(ptr);


            var previous = TimeService.CurrentTime;
            for (int i = 0; i < 1_00_000_000; i++)
            {
                var current = TimeService.CurrentTime;
                if (current <= previous)
                {
                    Assert.Fail();
                }

                var delta = current.Nanos - previous.Nanos;
                if (delta > 1)
                {
                    Console.WriteLine($"{current.Nanos} - {delta}");
                }

                if (i % 50_000_000 == 0)
                {
                    TimeService.UpdateTime();
                }
                previous = current;
            }
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
                    var current = TimeService.CurrentTime;
                }
            }
            
        }
    }
}
