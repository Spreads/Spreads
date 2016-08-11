using System;
using System.Diagnostics;
using NUnit.Framework;
using Spreads.Algorithms;


namespace Spreads.Extensions.Tests.Algorithm {
    [TestFixture]
    public class RankerTests {

        [Test, Ignore]
        public void CouldCalculateRankOfArray() {
            var rng = new Random();
            var values = new double[50];
            for (int i = 0; i < values.Length; i++) {
                values[i] = rng.NextDouble();
            }

            var result = default(ArraySegment<int>);
            var sw = new Stopwatch();
            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(0);
            GC.Collect(3, GCCollectionMode.Forced, true);
            sw.Start();
            const int loopCount = 1000000;
            for (int i = 0; i < loopCount; i++) {
                result = Ranker<double>.SortRank(new ArraySegment<double>(values));
            }
            sw.Stop();
            GC.Collect(3, GCCollectionMode.Forced, true);
            gc0 = GC.CollectionCount(0) - gc0;
            gc1 = GC.CollectionCount(0) - gc1;
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds} msec");
            Console.WriteLine($"GC0: {gc0}");
            Console.WriteLine($"GC1: {gc1}");
            Console.WriteLine($"{values.Length}-sized array per msec: {(double)loopCount / (sw.ElapsedMilliseconds) }");

            for (int i = 0; i < values.Length; i++) {
                Console.WriteLine($"{values[i]} - {result.Array[i]}");
            }
            // avoid GC-ing this objects
            Console.WriteLine(rng.NextDouble());
            Ranker<double>.SortRank(new ArraySegment<double>(values));
        }

    }
}
