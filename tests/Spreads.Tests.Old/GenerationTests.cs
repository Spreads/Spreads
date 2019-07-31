// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FSharp.Charting;
using Microsoft.FSharp.Core;
using Spreads.Collections;
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
            //    while (await cur.MoveNextAsync(CancellationToken.None)) {
            //        Console.WriteLine("New record: {0} - {1}", cur.CurrentKey, cur.CurrentValue);
            //    }
            //});

            Thread.Sleep(1500);
            Console.WriteLine("Generated number of values: {0}", sm.Count);
            //gen = null;
        }

        //[Test]
        //public void CouldGenerateRandomWalk() {
        //    var sw = new Stopwatch();
        //    sw.Restart();
        //    var acc = new SortedMap<int, double>();

        //    SortedMap<DateTime, double> sm = null;
        //    for (int i = 0; i < 10000; i++) {
        //        sm = RandomWalk.Generate(DateTime.Today.AddYears(-1), DateTime.Today, TimeSpan.FromDays(1), 0.5, 3.65, 365.0);
        //        acc.Add(i, sm.Last.Value);
        //    }
        //    sw.Stop();
        //    Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
            
        //    // NB internalsvisibleto make it so easy to use values instead of Values
        //    // hopefully not an issue outside tests
        //    Console.WriteLine($"Average: {acc.Values.Average()}");
        //    Console.WriteLine($"StDev: {acc.StDev(int.MaxValue, true).Last.Value}");
        //    Console.WriteLine("Generated number of values: {0}", sm.Count);
        //    //gen = null;
        //}
    }
}
