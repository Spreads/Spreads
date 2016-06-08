using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Algorithms;
using Spreads.Collections;
using Spreads.Storage;
using Spreads.Storage.Aeron;
using Spreads.Storage.Aeron.Logbuffer;
using Spreads.Storage.Aeron.Protocol;


namespace Spreads.Extensions.Tests.Algorithm {
    [TestFixture]
    public class RankerTests {

        [Test]
        public void CouldCalculateRankOfArray() {
            var rng = new System.Random();
            var values = new double[1000];
            for (int i = 0; i < values.Length; i++) {
                values[i] = rng.NextDouble();
            }

            var result = default(ArraySegment<KV<double, int>>);
            var sw = new Stopwatch();
            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(0);

            sw.Start();
            var sum = 0;
            const int loopCount = 10000;
            for (int i = 0; i < loopCount; i++) {
                result = Ranker<double>.SortRank(new ArraySegment<double>(values));
                sum += result.Array[0].Value;
            }
            sw.Stop();
            gc0 = GC.CollectionCount(0) - gc0;
            gc1 = GC.CollectionCount(0) - gc1;
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds} msec");
            Console.WriteLine($"GC0: {gc0}");
            Console.WriteLine($"GC1: {gc1}");
            Console.WriteLine($"{values.Length}-sized array per msec: {(double)loopCount / (sw.ElapsedMilliseconds) }");

            for (int i = 0; i < values.Length; i++) {
                Console.WriteLine($"{result.Array[i].Key} - {result.Array[i].Value}");
            }
            Console.WriteLine(sum);

        }

    }
}
