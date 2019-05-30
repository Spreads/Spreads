// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.FSharp.Collections;
using NUnit.Framework;
using Spreads.Collections.Experiemntal;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using ObjectLayoutInspector;

namespace Spreads.Core.Tests.Performance
{
    [TestFixture]
    public class ImmutableTests
    {
        [Test]
        public void Layout()
        {
            TypeLayout.PrintLayout<MapTreeNode<long,byte>>();
        }

        [Test]
        public void FSharpMap()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rounds = 100;
            var im = new FSharpMap<long, long>(Enumerable.Empty<Tuple<long, long>>());
            im = FSharpMap_Add(count);
            for (int r = 0; r < rounds; r++)
            {
                FSharpMap_Get(count, im);
            }

            Benchmark.Dump();

            // After more correct benchmark (separate methods)
            // Case                |    MOPS |  Elapsed |   GC0 |   GC1 |   GC2 |  Memory
            //----                 |--------:|---------:|------:|------:|------:|--------:
            //Get                  |    6.76 |   148 ms |   0.0 |   0.0 |   0.0 | 0.000 MB
            //Add                  |    1.01 |   990 ms | 146.2 |   6.5 |   1.8 | 46.226 MB
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static FSharpMap<long, long> FSharpMap_Add(long count)
        {
            FSharpMap<long, long> im;
            im = new FSharpMap<long, long>(Enumerable.Empty<Tuple<long, long>>());
            using (Benchmark.Run("Add", count))
            {
                for (int i = 0; i < count; i++)
                {
                    im = im.Add(i, i);
                }
            }

            return im;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void FSharpMap_Get(long count, FSharpMap<long, long> im)
        {
            using (Benchmark.Run("Get", count))
            {
                var sum = 0L;
                for (int i = 0; i < count; i++)
                {
                    sum += im[i];
                }
            }
        }

        [Test]
        public void SCIImmutableSortedDictionary()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rounds = 100;
            var im = ImmutableSortedDictionary<long, long>.Empty;
            im = ISD_Add(count);
            for (int r = 0; r < rounds; r++)
            {
                ISD_Get(count, im);
            }

            Benchmark.Dump();

            // After more correct benchmark (separate methods)
            // Case                |    MOPS |  Elapsed |   GC0 |   GC1 |   GC2 |  Memory
            //----                 |--------:|---------:|------:|------:|------:|--------:
            //Get                  |   11.63 |    86 ms |   0.0 |   0.0 |   0.0 | 0.001 MB
            //Add                  |    1.18 |   846 ms | 146.3 |   7.1 |   1.9 | 58.328 MB
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static ImmutableSortedDictionary<long, long> ISD_Add(long count)
        {
            ImmutableSortedDictionary<long, long> im;
            im = ImmutableSortedDictionary<long, long>.Empty;
            using (Benchmark.Run("Add", count))
            {
                for (int i = 0; i < count; i++)
                {
                    im = im.Add(i, i);
                }
            }

            return im;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void ISD_Get(long count, ImmutableSortedDictionary<long, long> im)
        {
            using (Benchmark.Run("Get", count))
            {
                var sum = 0L;
                for (int i = 0; i < count; i++)
                {
                    sum += im[i];
                }
            }
        }

        private static TV Find<TK, TV>(KeyComparer<TK> comparer, TK k, MapTree<TK, TV> m)
        {
            while (true)
            {
                if (m == null)
                {
                    throw new KeyNotFoundException();
                }

                var c = comparer.Compare(k, m.Key);

                // Common for MapOne and MapNode
                if (c == 0)
                {
                    // do not return from loop.
                    // Even though https://github.com/dotnet/coreclr/issues/9692
                    // closed/fixed in 2017 in this case it's very visible
                    break;
                }

                // This is pure isinst without casting.
                // Then using Unsafe for cast without a local var.
                m = m as MapTreeNode<TK, TV>;
                if (m != null)
                {
                    m = c < 0
                        ? Unsafe.As<MapTreeNode<TK, TV>>(m).Left
                        : Unsafe.As<MapTreeNode<TK, TV>>(m).Right;
                }

                // m == null for MapOne case after `as` and we go to KeyNotFoundException
            }
            return m.Value;
        }

        private static TV Find2<TK, TV>(KeyComparer<TK> comparer, TK k, MapTree<TK, TV> m)
        {
            while (m != null)
            {
                var c = comparer.Compare(k, m.Key);

                if (c != 0)
                {
                    // This is pure isinst without casting.
                    // Then using Unsafe for cast without a local var.
                    m = m as MapTreeNode<TK, TV>;
                    if (m != null)
                    {
                        m = c < 0
                            ? Unsafe.As<MapTreeNode<TK, TV>>(m).Left
                            : Unsafe.As<MapTreeNode<TK, TV>>(m).Right;
                    }

                    // m == null for MapOne case after `as` and we go to KeyNotFoundException
                }
                else
                {
                    // Common for MapOne and MapNode
                    return m.Value;
                }
            }
            throw new KeyNotFoundException();
        }

        private static TV FindFSharp<TK, TV>(KeyComparer<TK> comparer, TK k, MapTree<TK, TV> m)
        {
            while (m != null)
            {
                int c = comparer.Compare(k, m.Key);
                if (c == 0)
                {
                    return m.Value;
                }
                KeyComparer<TK> keyComparer = comparer;
                TK k2 = k;
                m = ((!(m is MapTreeNode<TK, TV>)) ? null : ((c >= 0) ? Unsafe.As<MapTreeNode<TK, TV>>(m).Right : Unsafe.As<MapTreeNode<TK, TV>>(m).Left));
                k = k2;
                comparer = keyComparer;
            }
            throw new KeyNotFoundException();
        }

        private static TV FindFSharpExLoc<TK, TV>(KeyComparer<TK> comparer, TK k, MapTree<TK, TV> m)
        {
            while (m != null)
            {
                int c = comparer.Compare(k, m.Key);
                if (c == 0)
                {
                    return m.Value;
                }
                KeyComparer<TK> keyComparer = comparer;
                // TK k2 = k;
                m = ((!(m is MapTreeNode<TK, TV>)) ? null : ((c >= 0) ? Unsafe.As<MapTreeNode<TK, TV>>(m).Right : Unsafe.As<MapTreeNode<TK, TV>>(m).Left));
                // k = k2;
                // comparer = keyComparer;
            }
            throw new KeyNotFoundException();
        }

        [Test]
        public void Performance()
        {
            var count = TestUtils.GetBenchCount(1_000_000);
            var rounds = 200;
            var im = ImmutableSortedMap<long, long>.Empty;

            im = Perf_Add(count);

            for (int r = 0; r < rounds; r++)
            {
                // Perf_GetOriginal(count, im);
                Perf_Get(count, im);
                //Perf_GetLoopFSharp(count, im);

                //Perf_GetLoopFSharpExLoc(count, im);

                // Perf_GetLoop(count, im);
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

            // After more correct benchmark (separate methods)
            // Case                |    MOPS |  Elapsed |   GC0 |   GC1 |   GC2 |  Memory
            //------------         |--------:|---------:|------:|------:|------:|--------:
            //Get C# loop          |   14.71 |    68 ms |   0.0 |   0.0 |   0.0 | 0.000 MB
            //Get                  |   12.82 |    78 ms |   0.0 |   0.0 |   0.0 | 0.000 MB
            //Add                  |    1.85 |   541 ms | 150.0 |   4.0 |   2.0 | 45.356 MB
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static ImmutableSortedMap<long, long> Perf_Add(long count)
        {
            ImmutableSortedMap<long, long> im;
            im = ImmutableSortedMap<long, long>.Empty;
            using (Benchmark.Run("Add", count))
            {
                for (int i = 0; i < count; i++)
                {
                    im = im.Add(i, i);
                }
            }

            return im;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void Perf_Get(long count, ImmutableSortedMap<long, long> im)
        {
            using (Benchmark.Run("Get", count))
            {
                var sum = 0L;
                for (int i = 0; i < count; i++)
                {
                    var x = im[i];
                    sum += x;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void Perf_GetOriginal(long count, ImmutableSortedMap<long, long> im)
        {
            using (Benchmark.Run("Get Original", count))
            {
                var sum = 0L;
                for (int i = 0; i < count; i++)
                {
                    var x = im.FindOriginal(i);
                    sum += x;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void Perf_GetLoop_Return_After_Loop(long count, ImmutableSortedMap<long, long> im)
        {
            using (Benchmark.Run("Get C# loop RAL", count))
            {
                var sum = 0L;
                var c = KeyComparer<long>.Default;
                for (int i = 0; i < count; i++)
                {
                    var x = Find(c, i, im.Tree);
                    sum += x;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void Perf_GetLoop(long count, ImmutableSortedMap<long, long> im)
        {
            using (Benchmark.Run("Get C# loop", count))
            {
                var sum = 0L;
                var c = KeyComparer<long>.Default;
                for (int i = 0; i < count; i++)
                {
                    var x = Find2(c, i, im.Tree);
                    sum += x;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void Perf_GetLoopFSharp(long count, ImmutableSortedMap<long, long> im)
        {
            using (Benchmark.Run("Get F# IL->C#", count))
            {
                var sum = 0L;
                var c = KeyComparer<long>.Default;
                for (int i = 0; i < count; i++)
                {
                    var x = FindFSharp(c, i, im.Tree);
                    sum += x;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
        private static void Perf_GetLoopFSharpExLoc(long count, ImmutableSortedMap<long, long> im)
        {
            using (Benchmark.Run("Get F# IL->C# ex loc", count))
            {
                var sum = 0L;
                var c = KeyComparer<long>.Default;
                for (int i = 0; i < count; i++)
                {
                    var x = FindFSharpExLoc(c, i, im.Tree);
                    sum += x;
                }
            }
        }
    }
}
