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
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Algorithms
{
    [TestFixture]
    public class VecHelpersTests
    {
        [Test, Explicit("long running")]
        public void BinarySearchBench()
        {
            var rounds = 5;
            var counts = new[] { 10, 100, 1000, 10_000, 100_000, 1_000_000 };
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
                                var idx = vec.DangerousBinarySearch(0, count, (Timestamp)i, KeyComparer<Timestamp>.Default);
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
                                var idx = VectorSearch.InterpolationSearch(ref vec.DangerousGetRef(0),
                                    count, (Timestamp)i);
                                if (idx != i)
                                {
                                    Console.WriteLine($"val {i} -> idx {idx}");
                                }
                            }
                        }
                    }
                }
            }
            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void BinarySearchLookupBench()
        {
            var counts = new[] { 10, 1000, 10_000, 100_000, 1_000_000 };
            var lookups = new[] { Lookup.GT, Lookup.GE, Lookup.EQ, Lookup.LE, Lookup.LT };
            foreach (var lookup in lookups)
            {
                foreach (var count in counts)
                {
                    var vec = new Vec<Timestamp>(Enumerable.Range(0, count).Select(x => (Timestamp)x).ToArray());

                    var mult = 10_000_000 / count;

                    using (Benchmark.Run($"{lookup} {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                var idx = vec.DangerousBinaryLookup(0, count, (Timestamp)i,
                                    KeyComparer<Timestamp>.Default,
                                    lookup);
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
            var counts = new[] { 100, 1000, 10000, 100000, 1000000 };

            foreach (var count in counts)
            {
                var vec = new Vec<long>(Enumerable.Range(0, count).Select(x => (long)x).ToArray());

                var mult = 10_000_000 / count;

                using (Benchmark.Run($"LeLookup {count}", count * mult))
                {
                    for (int m = 0; m < mult; m++)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var idx = BinaryLookupLe<long>(ref vec.DangerousGetRef(0),
                                count, (long)i, KeyComparer<long>.Default);
                            if (idx != i)
                            {
                                Console.WriteLine($"val {i} -> idx {idx}");
                            }
                        }
                    }
                }
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookupLe<T>(
            ref T vecStart, int length, T value, KeyComparer<T> comparer)
        // where T : IInt64Diffable<T>
        {
            var start = vecStart; // todo local?
            var totalRange = comparer.Diff(Unsafe.Add(ref vecStart, length - 1), start);
            var pow2 = (32 - IntUtil.NumberOfLeadingZeros(length - 1));
            // var rangePerValue = totalRange / BitUtil.FindNextPositivePowerOfTwo(length);
            var rangePerValue = totalRange >> pow2;
            var valueRange = comparer.Diff(value, start);
            // var searchStart = valueRange / rangePerValue;
            // var startIdx = (int)((valueRange / ((double)(totalRange))) * (length - 1));
            var startIdx = (int)((valueRange) * (length - 1)) >> ((32 - IntUtil.NumberOfLeadingZeros((int)totalRange - 1)));

            int c = comparer.Compare(value, Unsafe.Add(ref vecStart, startIdx));
        LOOP:

            if (c == 0)
            {
                return startIdx;
            }

            if (c > 0)
            {
                // startIdx is LE but the next one must be greater
                startIdx++;
                if (startIdx == length)
                {
                    return length - 1;
                }
                c = comparer.Compare(value, Unsafe.Add(ref vecStart, startIdx));
                if (c < 0) // next is GT
                {
                    return startIdx - 1;
                }

                goto LOOP;
            }

            startIdx--;
            if (startIdx >= 0)
            {
                goto LOOP;
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BinaryLookupLeTs(
                ref Timestamp vecStart, int length, Timestamp value)
        // where T : IInt64Diffable<T>
        {
            var start = vecStart; // todo local?
            var totalRange = Unsafe.Add(ref vecStart, length - 1).Nanos - vecStart.Nanos;
            var valueRange = value.Nanos - vecStart.Nanos;

            var startIdx = (int)(totalRange >> ((64 - IntUtil.NumberOfLeadingZeros(valueRange - 1))));

        //var pow2 = (32 - IntUtil.NumberOfLeadingZeros(length - 1));
        //// var rangePerValue = totalRange / BitUtil.FindNextPositivePowerOfTwo(length);
        //var rangePerValue = totalRange >> pow2;
        //var valueRange = comparer.Diff(value, start);
        //// var searchStart = valueRange / rangePerValue;
        //// var startIdx = (int)((valueRange / ((double)(totalRange))) * (length - 1));
        //var startIdx = ((valueRange) * (length - 1)) >> ((32 - IntUtil.NumberOfLeadingZeros((int)totalRange - 1)));

        // int c = comparer.Compare(value, Unsafe.Add(ref vecStart, startIdx));
        LOOP:

            if (value > Unsafe.Add(ref vecStart, startIdx))
            {
                // startIdx is LE but the next one must be greater
                startIdx++;
                if (startIdx == length)
                {
                    return length - 1;
                }
                if (value < Unsafe.Add(ref vecStart, startIdx)) // next is GT
                {
                    return startIdx - 1;
                }

                goto LOOP;
            }

            if (value == Unsafe.Add(ref vecStart, startIdx))
            {
                return startIdx;
            }

            startIdx--;
            if (startIdx >= 0)
            {
                goto LOOP;
            }

            return -1;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static int InterpolationSearchTimeStamp(
        //        ref Timestamp vecStart, int length, Timestamp value)
        //{
        //    return InterpolationSearch(ref Unsafe.As<Timestamp, long>(ref vecStart), length, (long)value);
        //    //int lo = 0;
        //    //int hi = length - 1;
        //    //// If length == 0, hi == -1, and loop will not be entered
        //    //while (lo <= hi)
        //    //{
        //    //    var l = hi - lo;
        //    //    var totalRange = 1 + Unsafe.Add(ref vecStart, hi).Nanos - Unsafe.Add(ref vecStart, lo).Nanos;
        //    //    var valueRange = 1 + value.Nanos - Unsafe.Add(ref vecStart, lo).Nanos;

        //    //    // division via double is much faster
        //    //    int i = (int)(l * (double)valueRange / totalRange);

        //    //    int c = value.CompareTo(Unsafe.Add(ref vecStart, i));
        //    //    if (c == 0)
        //    //    {
        //    //        return i;
        //    //    }
        //    //    else if (c > 0)
        //    //    {
        //    //        lo = i + 1;
        //    //    }
        //    //    else
        //    //    {
        //    //        hi = i - 1;
        //    //    }
        //    //}
        //    //return ~lo;
        //}

        
    }
}