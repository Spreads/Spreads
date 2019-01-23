// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Algorithms;
using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Native;
using Spreads.Utils;
using System;
using System.Linq;

namespace Spreads.Core.Tests.Algorithms
{
    [Category("CI")]
    [TestFixture]
    public class VecSearchTests
    {
        [Test]
        public void WorksOnEmpty()
        {
            var arr = new int[0];

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(1);
            Assert.AreEqual(-1, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(1);
            Assert.AreEqual(-1, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.LE);
            Assert.AreEqual(-1, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.EQ);
            Assert.AreEqual(-1, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.GE);
            Assert.AreEqual(-1, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.GT);
            Assert.AreEqual(-1, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.LE);
            Assert.AreEqual(-1, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.EQ);
            Assert.AreEqual(-1, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.GE);
            Assert.AreEqual(-1, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.GT);
            Assert.AreEqual(-1, idxBGt);
        }

        [Test]
        public void WorksOnSingle()
        {
            var arr = new int[] { 1 };

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(1);
            Assert.AreEqual(0, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(1);
            Assert.AreEqual(0, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.EQ);
            Assert.AreEqual(0, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.GT);
            Assert.AreEqual(-2, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.EQ);
            Assert.AreEqual(0, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.GE);
            Assert.AreEqual(0, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.GT);
            Assert.AreEqual(-2, idxBGt);
        }

        [Test]
        public void WorksOnFirst()
        {
            var arr = new int[] { 1, 2, 3, 4, 5, 6 };

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(1);
            Assert.AreEqual(0, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(1);
            Assert.AreEqual(0, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.EQ);
            Assert.AreEqual(0, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(1, Lookup.GT);
            Assert.AreEqual(1, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.EQ);
            Assert.AreEqual(0, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.GE);
            Assert.AreEqual(0, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(1, Lookup.GT);
            Assert.AreEqual(1, idxBGt);
        }

        [Test]
        public void WorksOnLast()
        {
            var arr = new int[] { 1, 2 };

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(2);
            Assert.AreEqual(1, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(2);
            Assert.AreEqual(1, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.EQ);
            Assert.AreEqual(1, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GT);
            Assert.AreEqual(-3, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.EQ);
            Assert.AreEqual(1, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GT);
            Assert.AreEqual(-3, idxBGt);
        }

        [Test]
        public void WorksOnExistingMiddle()
        {
            var arr = new int[] { 1, 2, 4 };

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(2);
            Assert.AreEqual(1, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(2);
            Assert.AreEqual(1, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.EQ);
            Assert.AreEqual(1, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GT);
            Assert.AreEqual(2, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.EQ);
            Assert.AreEqual(1, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GT);
            Assert.AreEqual(2, idxBGt);
        }

        [Test]
        public void WorksOnNonExistingMiddle()
        {
            var arr = new int[] { 1, 4 };

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(2);
            Assert.AreEqual(-2, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(2);
            Assert.AreEqual(-2, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.EQ);
            Assert.AreEqual(-2, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GT);
            Assert.AreEqual(1, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.EQ);
            Assert.AreEqual(-2, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GT);
            Assert.AreEqual(1, idxBGt);
        }

        [Test]
        public void WorksAfterEnd()
        {
            var arr = new int[] { 0, 1 };

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(2);
            Assert.AreEqual(-3, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(2);
            Assert.AreEqual(-3, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LT);
            Assert.AreEqual(1, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.EQ);
            Assert.AreEqual(-3, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GE);
            Assert.AreEqual(-3, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(2, Lookup.GT);
            Assert.AreEqual(-3, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LT);
            Assert.AreEqual(1, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.EQ);
            Assert.AreEqual(-3, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GE);
            Assert.AreEqual(-3, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(2, Lookup.GT);
            Assert.AreEqual(-3, idxBGt);
        }

        [Test]
        public void WorksBeforeStart()
        {
            var arr = new int[] { 0, 1, 2, 3, 4, 5 };

            var idxI = ((ArrayVec<int>)arr).InterpolationSearch(-1);
            Assert.AreEqual(-1, idxI);

            var idxB = ((ArrayVec<int>)arr).BinarySearch(-1);
            Assert.AreEqual(-1, idxB);

            var idxILt = ((ArrayVec<int>)arr).InterpolationLookup(-1, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = ((ArrayVec<int>)arr).InterpolationLookup(-1, Lookup.LE);
            Assert.AreEqual(-1, idxILe);

            var idxILq = ((ArrayVec<int>)arr).InterpolationLookup(-1, Lookup.EQ);
            Assert.AreEqual(-1, idxILq);

            var idxIGe = ((ArrayVec<int>)arr).InterpolationLookup(-1, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            var idxIGt = ((ArrayVec<int>)arr).InterpolationLookup(-1, Lookup.GT);
            Assert.AreEqual(0, idxIGt);

            var idxBLt = ((ArrayVec<int>)arr).BinaryLookup(-1, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = ((ArrayVec<int>)arr).BinaryLookup(-1, Lookup.LE);
            Assert.AreEqual(-1, idxBLe);

            var idxBEq = ((ArrayVec<int>)arr).BinaryLookup(-1, Lookup.EQ);
            Assert.AreEqual(-1, idxBEq);

            var idxBGe = ((ArrayVec<int>)arr).BinaryLookup(-1, Lookup.GE);
            Assert.AreEqual(-0, idxBGe);

            var idxBGt = ((ArrayVec<int>)arr).BinaryLookup(-1, Lookup.GT);
            Assert.AreEqual(-0, idxBGt);
        }

        [Test, Explicit("long running")]
        public void SearchBench()
        {
            var rounds = 5;
            var counts = new[] { 1, 10, 100, 1000, 10_000, 100_000, 1_000_000 };
            for (int r = 0; r < rounds; r++)
            {
                foreach (var count in counts)
                {
                    var vec = new Vec<Timestamp>(Enumerable.Range(0, count).Select(x => (Timestamp)x).ToArray());

                    var mult = 10_000_000 / count;

                    using (Benchmark.Run("Binary" + count, count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable 618
                                var idx = vec.DangerousBinarySearch(0, count, (Timestamp)i, KeyComparer<Timestamp>.Default);
#pragma warning restore 618
                                if (idx < 0)
                                {
                                    ThrowHelper.FailFast(String.Empty);
                                }
                            }
                        }
                    }

                    using (Benchmark.Run($"Interpolation {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable 618
                                var idx = vec.DangerousInterpolationSearch(0, count, (Timestamp)i, KeyComparer<Timestamp>.Default);
#pragma warning restore 618
                                if (idx < 0)
                                {
                                    ThrowHelper.FailFast(String.Empty);
                                }

                                //var idx = VectorSearch.InterpolationSearch(ref vec.DangerousGetRef(0),
                                //    count, (Timestamp)i);
                                //if (idx != i)
                                //{
                                //    Console.WriteLine($"val {i} -> idx {idx}");
                                //}
                            }
                        }
                    }
                }
            }
            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void LookupBench()
        {
            var counts = new[] { 1, 10, 100, 1000, 10_000, 100_000, 1_000_000 };
            var lookups = new[] { Lookup.GT, Lookup.GE, Lookup.EQ, Lookup.LE, Lookup.LT };
            foreach (var lookup in lookups)
            {
                foreach (var count in counts)
                {
                    var vec = new Vec<Timestamp>(Enumerable.Range(0, count).Select(x => (Timestamp)x).ToArray());

                    var mult = 10_000_000 / count;

                    using (Benchmark.Run($"Binary {lookup} {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable CS0618 // Type or member is obsolete
                                var idx = vec.DangerousBinaryLookup(0, count, (Timestamp)i, lookup);
#pragma warning restore CS0618 // Type or member is obsolete
                                if (idx < 0
                                    && !(i == 0 && lookup == Lookup.LT
                                         ||
                                         i == count - 1 && lookup == Lookup.GT
                                         )
                                    )
                                {
                                    throw new InvalidOperationException($"LU={lookup}, i={i}, idx={idx}");
                                }
                                else if (idx >= 0)
                                {
                                    if (lookup.IsEqualityOK() && idx != i
                                        ||
                                        lookup == Lookup.LT && idx != i - 1
                                        ||
                                        lookup == Lookup.GT && idx != i + 1
                                        )
                                    {
                                        throw new InvalidOperationException($"LU={lookup}, i={i}, idx={idx}");
                                    }
                                }
                            }
                        }
                    }

                    using (Benchmark.Run($"Interpolation {lookup} {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable CS0618 // Type or member is obsolete
                                var idx = vec.DangerousInterpolationLookup(0, count, (Timestamp)i,
                                    lookup);
#pragma warning restore CS0618 // Type or member is obsolete
                                if (idx < 0
                                    && !(i == 0 && lookup == Lookup.LT
                                         ||
                                         i == count - 1 && lookup == Lookup.GT
                                        )
                                )
                                {
                                    throw new InvalidOperationException($"LU={lookup}, i={i}, idx={idx}");
                                }
                                else if (idx >= 0)
                                {
                                    if (lookup.IsEqualityOK() && idx != i
                                        ||
                                        lookup == Lookup.LT && idx != i - 1
                                        ||
                                        lookup == Lookup.GT && idx != i + 1
                                    )
                                    {
                                        throw new InvalidOperationException($"LU={lookup}, i={i}, idx={idx}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void SearchIrregularBench()
        {
            Random rng;
            var rounds = 10;
            var counts = new[] { 50, 100, 256, 512, 1000, 2048, 4096, 10_000, 100_000 };
            for (int r = 0; r < rounds; r++)
            {
                foreach (var count in counts)
                {
                    rng = new Random(r);

                    var step = rng.Next(10, 100);
                    var dev = 0; // step / rng.Next(2, 8);

                    var vec = new Vec<Timestamp>(Enumerable.Range(0, count)
                        .Select(i => (Timestamp)i).ToArray());

                    for (int i = 1; i < vec.Length; i++)
                    {
                        vec[i] = vec[i - 1] + (Timestamp)step + (Timestamp)rng.Next(-dev, dev); //  (Timestamp)(vec[i].Nanos * 1000 - 2 + rng.Next(0, 4)); //
                    }

                    int[] binRes = new int[vec.Length];
                    int[] interRes = new int[vec.Length];

                    for (int i = 0; i < count; i++)
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        var br = vec.DangerousBinarySearch(0, count, (Timestamp)(vec[i].Nanos));
                        var ir = vec.DangerousInterpolationSearch(0, count, (Timestamp)(vec[i].Nanos));
                        if (br != ir)
                        {
                            Console.WriteLine($"[{count}] binRes {br} != interRes {ir} at {i}");
                            ir = vec.DangerousInterpolationSearch(0, count, (Timestamp)(i * 1000));
                            Assert.Fail();
                        }

                        if (br >= 0)
                        {
                            // Console.WriteLine("found");
                        }
#pragma warning restore CS0618 // Type or member is obsolete
                    }

                    var mult = 5_000_000 / count;

                    using (Benchmark.Run($"Bin      {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable CS0618 // Type or member is obsolete
                                binRes[i] = vec.DangerousBinarySearch(0, count, (Timestamp)(i * step));
#pragma warning restore CS0618 // Type or member is obsolete
                            }
                        }
                    }

                    using (Benchmark.Run($"Interpol {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                //if (count == 10)
                                //{
                                //    Console.WriteLine("Catch me");
                                //}

#pragma warning disable CS0618 // Type or member is obsolete
                                interRes[i] = vec.DangerousInterpolationSearch(0, count, (Timestamp)(i * step));
#pragma warning restore CS0618 // Type or member is obsolete
                            }
                        }
                    }

                    for (int i = 0; i < count; i++)
                    {
                        if (binRes[i] != interRes[i])
                        {
                            Console.WriteLine($"[{count}] binRes[i] {binRes[i]} != interRes[i] {interRes[i]} at {i}");
                            Assert.Fail();
                        }
                    }

                    Assert.IsTrue(binRes.SequenceEqual(interRes));
                }
            }
            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void IndexOfBench()
        {
            var rounds = 20;
            var counts = new[] { 10 };
            for (int r = 0; r < rounds; r++)
            {
                foreach (var count in counts)
                {
                    var vec = new Vec<Timestamp>(Enumerable.Range(0, count).Select(x => (Timestamp)x).ToArray());

                    var mult = 5_000_000 / count;

                    using (Benchmark.Run("IndexOf " + count, count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (long i = 0; i < count; i++)
                            {
                                var idx = VectorSearch.IndexOf(ref vec.GetRef(0), (Timestamp)i, count);
                                if (idx < 0)
                                {
                                    Console.WriteLine($"val {i} -> idx {idx}");
                                    // ThrowHelper.FailFast(String.Empty);
                                }
                            }
                        }
                    }
                }
            }
            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void LeLookupBench()
        {
            var counts = new[] { 10, 100, 1000, 10000, 100000, 1000000 };

            foreach (var count in counts)
            {
                var vec = new Vec<long>(Enumerable.Range(0, count).Select(x => (long)x).ToArray());

                var mult = 50_000_000 / count;

                using (Benchmark.Run($"LeLookup {count}", count * mult))
                {
                    for (int m = 0; m < mult; m++)
                    {
                        for (int i = 0; i < count; i++)
                        {
#pragma warning disable 618
                            var idx = vec.DangerousInterpolationLookup(0, count, (long)i, Lookup.LE);
#pragma warning restore 618
                            if (idx != i)
                            {
                                Console.WriteLine($"val {i} -> idx {idx}");
                                Assert.Fail();
                            }
                        }
                    }
                }
            }

            Benchmark.Dump();
        }
    }
}