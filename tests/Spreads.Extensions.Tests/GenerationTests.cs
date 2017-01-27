// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Threading;
using FSharp.Charting;
using Microsoft.FSharp.Core;
using Spreads.Generation;

namespace Spreads.Extensions.Tests {

    [TestFixture]
    public class GenerationTests {
        private SortedMapGenerator<int> gen;

        [Test]
        public void CouldGenerateSubMillisecondSeries() {
            gen = new SortedMapGenerator<int>(TimeSpan.TicksPerMillisecond * 10, (cnt, prev) => cnt);
            var sm = gen.Generate();

            //Task.Run(async () => {
            //    var cur = sm.GetCursor();
            //    while (await cur.MoveNext(CancellationToken.None)) {
            //        Console.WriteLine("New record: {0} - {1}", cur.CurrentKey, cur.CurrentValue);
            //    }
            //});

            Thread.Sleep(1500);
            Console.WriteLine("Generated number of values: {0}", sm.Count);
            //gen = null;
        }

        [Test]
        public void CouldGenerateRandomWalk() {
            var sm = RandomWalk.Generate(DateTime.Today.AddYears(-1), DateTime.Today, TimeSpan.FromDays(1), 0.20, 0.15);

            Thread.Sleep(1500);
            Console.WriteLine($"Last: {sm.Last.Value}");
            Console.WriteLine("Generated number of values: {0}", sm.Count);
            //gen = null;
        }
    }
}
