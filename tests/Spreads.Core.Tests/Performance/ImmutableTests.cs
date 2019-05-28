// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.FSharp.Collections;
using NUnit.Framework;
using ObjectLayoutInspector;
using Spreads.Collections.Experiemntal;
using Spreads.Utils;
using System;
using System.Linq;

namespace Spreads.Core.Tests.Performance
{
    [TestFixture]
    public class ImmutableTests
    {
        //[Test]
        //public void ObjObj()
        //{
        //    Console.WriteLine("OLD NODE");
        //    MapTree<object, object> x = default;

        //    TypeLayout.PrintLayout<MapTree<object, object>.MapNode>();
        //    Console.WriteLine("OLD LEAF");
        //    TypeLayout.PrintLayout<MapTree<object, object>.MapOne>();
        //}

        [Test]
        public void FSharpMap()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rounds = 20;
            var im = new FSharpMap<long, long>(Enumerable.Empty<Tuple<long, long>>());

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Add", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        im = im.Add(i, i);
                    }
                }

                using (Benchmark.Run("Get", count))
                {
                    var sum = 0L;
                    for (int i = 0; i < count; i++)
                    {
                        sum += im[i];
                    }
                }
            }

            Benchmark.Dump();

            // base line after resurrecting the old code
            //Case | MOPS | Elapsed | GC0 | GC1 | GC2 | Memory
            //    ---- | --------:| ---------:| ------:| ------:| ------:| --------:
            //Get | 5.75 | 174 ms | 3.0 | 0.0 | 0.0 | 4.961 MB
            //Add | 1.14 | 877 ms | 148.0 | 4.0 | 0.0 | 47.646 MB
        }

        [Test]
        public void Performance()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rounds = 20;
            var im = ImmutableSortedMap<long, long>.Empty;

            for (int r = 0; r < rounds; r++)
            {

                using (Benchmark.Run("Add", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        im = im.Add(i, i);
                    }
                }

                using (Benchmark.Run("Get", count))
                {
                    var sum = 0L;
                    for (int i = 0; i < count; i++)
                    {
                        sum += im[i];
                    }
                }
            }

            Benchmark.Dump();

            //// FSharpMap
            //Case | MOPS | Elapsed | GC0 | GC1 | GC2 | Memory
            //    ---- | --------:| ---------:| ------:| ------:| ------:| --------:
            //Get | 7.25 | 138 ms | 0.0 | 0.0 | 0.0 | 0.000 MB
            //Add | 1.17 | 858 ms | 144.0 | 4.0 | 0.0 | 42.731 MB

            //// base line after resurrecting the old code
            //Case | MOPS | Elapsed | GC0 | GC1 | GC2 | Memory
            //    ---- | --------:| ---------:| ------:| ------:| ------:| --------:
            //Get | 5.75 | 174 ms | 3.0 | 0.0 | 0.0 | 4.961 MB
            //Add | 1.14 | 877 ms | 148.0 | 4.0 | 0.0 | 47.646 MB

            //// using KeyComparer instead of IComparer
            //Case | MOPS | Elapsed | GC0 | GC1 | GC2 | Memory
            //    ---- | --------:| ---------:| ------:| ------:| ------:| --------:
            //Get | 9.17 | 109 ms | 0.0 | 0.0 | 0.0 | 0.000 MB
            //Add | 1.14 | 876 ms | 144.0 | 4.0 | 0.0 | 42.745 MB

            //// after inlining inner MapTree.find. Public Map.Item has no attribute, JIT decides (in this test Item getter is completely inlined)
            //Case | MOPS | Elapsed | GC0 | GC1 | GC2 | Memory
            //    ---- | --------:| ---------:| ------:| ------:| ------:| --------:
            //Get | 9.62 | 104 ms | 0.0 | 0.0 | 0.0 | 0.000 MB
            //Add | 1.17 | 858 ms | 144.0 | 4.0 | 0.0 | 42.743 MB

            //// changed MapTree layout
            //Case | MOPS | Elapsed | GC0 | GC1 | GC2 | Memory
            //    ---- | --------:| ---------:| ------:| ------:| ------:| --------:
            //Get | 19.23 | 52 ms | 0.0 | 0.0 | 0.0 | 0.000 MB
            //Add | 2.06 | 486 ms | 144.0 | 4.0 | 0.0 | 43.275 MB
        }
    }
}
