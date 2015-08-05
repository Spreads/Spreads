using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;
using Spreads;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class GenerationTests {

        private SortedMapGenerator<int> gen;

        [Test]
        public void CouldGenerateSubMillisecondSeries()
        {
            
            gen = new SortedMapGenerator<int>(TimeSpan.TicksPerMillisecond*10, (cnt,prev) => cnt);
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
    }
}
