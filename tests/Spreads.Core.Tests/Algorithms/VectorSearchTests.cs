// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
#if HAS_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using NUnit.Framework;
using Shouldly;
using Spreads.Algorithms;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.DataTypes;
using Spreads.Utils;

// ReSharper disable HeapView.BoxingAllocation

namespace Spreads.Core.Tests.Algorithms
{
    public readonly struct TestStruct : IInt64Diffable<TestStruct>
    {
        private readonly long _value;

        public TestStruct(long value)
        {
            _value = value;
        }

        public static implicit operator TestStruct(long value)
        {
            return new(value);
        }

        public static implicit operator long(TestStruct value)
        {
            return value._value;
        }

        public int CompareTo(TestStruct other)
        {
            // ReSharper disable once ImpureMethodCallOnReadonlyValueField
            return _value.CompareTo(other);
        }

        public TestStruct Add(long diff)
        {
            return new(_value + diff);
        }

        public long Diff(TestStruct other)
        {
            return _value - other._value;
        }
    }

    [Category("CI")]
    [TestFixture]
    public class VecSearchTests
    {
#if HAS_INTRINSICS
        [Test, Explicit("Bench")]
        public unsafe void VectorGatherVsManualLoad()
        {
            var itemCount = 16 * 1024;
            using var pm = PrivateMemory<long>.Create(itemCount);
            var v = pm.GetVec();
            for (int i = 0; i < pm.Length; i++)
            {
                v.DangerousSetUnaligned(i, i + 1);
            }

            // try to load vector from new cache lines
            // in the most efficient way

            var segment = pm.Length / 4;
            const int cacheline = 8;
            var cacheLinesCount = segment / cacheline;
            var rng = new Random();
            var cacheLines = new int[cacheLinesCount];
            for (int i = 0; i < cacheLinesCount; i++)
            {
                cacheLines[i] = rng.Next(0, cacheLinesCount);
            }

            var count = 100_000_000;
            Vector256<long> gather = default;
            Vector256<long> load = default;

            for (int r = 0; r < 50; r++)
            {
                VectorGatherVsManualLoad_Gather(cacheLines, pm, cacheline, segment);

                VectorGatherVsManualLoad_Load(cacheLines, pm, cacheline, segment);
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static unsafe void VectorGatherVsManualLoad_Gather(int[] cacheLines, PrivateMemory<long> pm, int cacheline, int segment)
        {
            Vector256<long> gather;
            using (Benchmark.Run("Gather", cacheLines.Length * 1000))
            {
                for (int _ = 0; _ < 1000; _++)
                {
                    var sum = 0L;
                    var ptr = (long*)pm.Pointer;
                    for (int ii = 0; ii < cacheLines.Length; ii++)
                    {
                        var i = cacheLines[ii];
                        var idx = Vector256.Create(
                            (i * cacheline),
                            ((long)segment + i * cacheline),
                            ((long)segment * 2 + i * cacheline),
                            ((long)segment * 3 + i * cacheline)
                        );
                        gather = Avx2.GatherVector256(ptr, idx, 8);
                        sum += gather.GetElement(1);
                    }

                    if (sum < 1000)
                        throw new InvalidOperationException();
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static unsafe void VectorGatherVsManualLoad_Load(int[] cacheLines, PrivateMemory<long> pm, int cacheline, int segment)
        {
            Vector256<long> load;
            using (Benchmark.Run("Load", cacheLines.Length * 1000))
            {
                for (int _ = 0; _ < 1000; _++)
                {
                    var sum = 0L;
                    var ptr = (long*)pm.Pointer;
                    for (int ii = 0; ii < cacheLines.Length; ii++)
                    {
                        var i = cacheLines[ii];
                        load = Vector256.Create(
                            ptr[i * cacheline],
                            ptr[segment + i * cacheline],
                            ptr[segment * 2 + i * cacheline],
                            ptr[segment * 3 + i * cacheline]);
                        sum += load.GetElement(1);
                    }

                    if (sum < 1000)
                        throw new InvalidOperationException();
                }
            }
        }
#endif
        [Test]
        public void WorksOnEmpty()
        {
            var intArr = new int[0];
            var intValue = 1;
            WorksOnEmpty(intArr, intValue);
            WorksOnEmptyVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[0];
            short shortValue = 1;
            WorksOnEmpty(shortArr, shortValue);
            WorksOnEmptyVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[0];
            TestStruct customValue = 1;
            WorksOnEmpty(customArr, customValue);
            WorksOnEmptyVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new Timestamp[0];
            var tsValue = (Timestamp)1;
            WorksOnEmpty(tsArr, tsValue);
            WorksOnEmptyVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksOnEmpty<T>(T[] arr, T value)
        {
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(-1, idxI);

            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(-1, idxB);

            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxILe);

            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxILq);

            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(-1, idxIGe);

            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-1, idxIGt);

            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxBLe);

            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxBEq);

            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(-1, idxBGe);

            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-1, idxBGt);
        }

        // TODO Use Vec<T> actually
        private static void WorksOnEmptyVec<T>(Vec<T> vec, T value)
        {
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(-1, idxI);

            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(-1, idxB);

            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxILe);

            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxILq);

            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(-1, idxIGe);

            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-1, idxIGt);

            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxBLe);

            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxBEq);

            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(-1, idxBGe);

            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-1, idxBGt);
        }

        [Test]
        public void WorksOnSingle()
        {
            var intArr = new[] { 1 };
            var intValue = 1;
            WorksOnSingle(intArr, intValue);
            WorksOnSingleVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[] { 1 };
            short shortValue = 1;
            WorksOnSingle(shortArr, shortValue);
            WorksOnSingleVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[] { 1 };
            TestStruct customValue = 1;
            WorksOnSingle(customArr, customValue);
            WorksOnSingleVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new[] { (Timestamp)1 };
            Timestamp tsValue = (Timestamp)1;
            WorksOnSingle(tsArr, tsValue);
            WorksOnSingleVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksOnSingle<T>(T[] arr, T value)
        {
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(0, idxI);

            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(0, idxB);

            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxILq);

            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-2, idxIGt);

            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxBEq);

            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxBGe);

            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-2, idxBGt);
        }

        private static void WorksOnSingleVec<T>(Vec<T> vec, T value)
        {
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(0, idxI);

            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(0, idxB);

            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxILq);

            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-2, idxIGt);

            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxBEq);

            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxBGe);

            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-2, idxBGt);
        }

        [Test]
        public void WorksOnFirst()
        {
            var intArr = new[] { 1, 2 };
            var intValue = 1;
            WorksOnFirst(intArr, intValue);
            WorksOnFirstVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[] { 1, 2 };
            short shortValue = 1;
            WorksOnFirst(shortArr, shortValue);
            WorksOnFirstVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[] { 1, 2 };
            TestStruct customValue = 1;
            WorksOnFirst(customArr, customValue);
            WorksOnFirstVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new[] { (Timestamp)1, (Timestamp)2 };
            Timestamp tsValue = (Timestamp)1;
            WorksOnFirst(tsArr, tsValue);
            WorksOnFirstVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksOnFirst<T>(T[] arr, T valueIn)
        {
            var value = valueIn;
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(0, idxI);

            value = valueIn;
            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(0, idxB);

            value = valueIn;
            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            value = valueIn;
            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            value = valueIn;
            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxILq);

            value = valueIn;
            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            value = valueIn;
            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxIGt);

            value = valueIn;
            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            value = valueIn;
            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            value = valueIn;
            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxBEq);

            value = valueIn;
            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxBGe);

            value = valueIn;
            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxBGt);
        }

        private static void WorksOnFirstVec<T>(Vec<T> vec, T valueIn)
        {
            var value = valueIn;
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(0, idxI);

            value = valueIn;
            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(0, idxB);

            value = valueIn;
            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            value = valueIn;
            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            value = valueIn;
            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxILq);

            value = valueIn;
            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            value = valueIn;
            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxIGt);

            value = valueIn;
            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            value = valueIn;
            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            value = valueIn;
            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(0, idxBEq);

            value = valueIn;
            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxBGe);

            value = valueIn;
            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxBGt);
        }

        [Test]
        public void WorksOnLast()
        {
            var intArr = new[] { 1, 2 };
            var intValue = 2;
            WorksOnLast(intArr, intValue);
            WorksOnLastVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[] { 1, 2 };
            short shortValue = 2;
            WorksOnLast(shortArr, shortValue);
            WorksOnLastVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[] { 1, 2 };
            TestStruct customValue = 2;
            WorksOnLast(customArr, customValue);
            WorksOnLastVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new[] { (Timestamp)1, (Timestamp)2 };
            Timestamp tsValue = (Timestamp)2;
            WorksOnLast(tsArr, tsValue);
            WorksOnLastVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksOnLast<T>(T[] arr, T valueIn)
        {
            var value = valueIn;
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(1, idxI);

            value = valueIn;
            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(1, idxB);

            value = valueIn;
            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            value = valueIn;
            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            value = valueIn;
            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxILq);

            value = valueIn;
            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            value = valueIn;
            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxIGt);

            value = valueIn;
            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            value = valueIn;
            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            value = valueIn;
            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxBEq);

            value = valueIn;
            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            value = valueIn;
            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxBGt);
        }

        private static void WorksOnLastVec<T>(Vec<T> vec, T valueIn)
        {
            var value = valueIn;
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(1, idxI);

            value = valueIn;
            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(1, idxB);

            value = valueIn;
            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            value = valueIn;
            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            value = valueIn;
            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxILq);

            value = valueIn;
            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            value = valueIn;
            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxIGt);

            value = valueIn;
            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            value = valueIn;
            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            value = valueIn;
            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxBEq);

            value = valueIn;
            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            value = valueIn;
            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxBGt);
        }

        [Test]
        public void WorksOnExistingMiddle()
        {
            var intArr = new[] { 1, 2, 4 };
            var intValue = 2;
            WorksOnExistingMiddle(intArr, intValue);
            WorksOnExistingMiddleVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[] { 1, 2, 4 };
            short shortValue = 2;
            WorksOnExistingMiddle(shortArr, shortValue);
            WorksOnExistingMiddleVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[] { 1, 2, 4 };
            TestStruct customValue = 2;
            WorksOnExistingMiddle(customArr, customValue);
            WorksOnExistingMiddleVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new[] { (Timestamp)1, (Timestamp)2, (Timestamp)4 };
            Timestamp tsValue = (Timestamp)2;
            WorksOnExistingMiddle(tsArr, tsValue);
            WorksOnExistingMiddleVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksOnExistingMiddle<T>(T[] arr, T valueIn)
        {
            var value = valueIn;
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(1, idxI);

            value = valueIn;
            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(1, idxB);

            value = valueIn;
            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            value = valueIn;
            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            value = valueIn;
            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxILq);

            value = valueIn;
            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            value = valueIn;
            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(2, idxIGt);

            value = valueIn;
            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            value = valueIn;
            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            value = valueIn;
            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxBEq);

            value = valueIn;
            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            value = valueIn;
            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(2, idxBGt);
        }

        private static void WorksOnExistingMiddleVec<T>(Vec<T> vec, T valueIn)
        {
            var value = valueIn;
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(1, idxI);

            value = valueIn;
            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(1, idxB);

            value = valueIn;
            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            value = valueIn;
            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            value = valueIn;
            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxILq);

            value = valueIn;
            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            value = valueIn;
            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(2, idxIGt);

            value = valueIn;
            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            value = valueIn;
            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            value = valueIn;
            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(1, idxBEq);

            value = valueIn;
            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            value = valueIn;
            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(2, idxBGt);
        }

        [Test]
        public void WorksOnNonExistingMiddle()
        {
            var intArr = new[] { 1, 4 };
            var intValue = 2;
            WorksOnNonExistingMiddle(intArr, intValue);
            WorksOnNonExistingMiddleVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[] { 1, 4 };
            short shortValue = 2;
            WorksOnNonExistingMiddle(shortArr, shortValue);
            WorksOnNonExistingMiddleVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[] { 1, 4 };
            TestStruct customValue = 2;
            WorksOnNonExistingMiddle(customArr, customValue);
            WorksOnNonExistingMiddleVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new[] { (Timestamp)1, (Timestamp)4 };
            Timestamp tsValue = (Timestamp)2;
            WorksOnNonExistingMiddle(tsArr, tsValue);
            WorksOnNonExistingMiddleVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksOnNonExistingMiddle<T>(T[] arr, T valueIn)
        {
            var value = valueIn;
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(-2, idxI);

            value = valueIn;
            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(-2, idxB);

            value = valueIn;
            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            value = valueIn;
            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            value = valueIn;
            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-2, idxILq);

            value = valueIn;
            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            value = valueIn;
            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxIGt);

            value = valueIn;
            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            value = valueIn;
            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            value = valueIn;
            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-2, idxBEq);

            value = valueIn;
            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            value = valueIn;
            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxBGt);
        }

        private static void WorksOnNonExistingMiddleVec<T>(Vec<T> vec, T valueIn)
        {
            var value = valueIn;
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(-2, idxI);

            value = valueIn;
            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(-2, idxB);

            value = valueIn;
            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxILt);

            value = valueIn;
            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxILe);

            value = valueIn;
            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-2, idxILq);

            value = valueIn;
            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxIGe);

            value = valueIn;
            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxIGt);

            value = valueIn;
            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(0, idxBLt);

            value = valueIn;
            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(0, idxBLe);

            value = valueIn;
            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-2, idxBEq);

            value = valueIn;
            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(1, idxBGe);

            value = valueIn;
            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(1, idxBGt);
        }

        [Test]
        public void WorksAfterEnd()
        {
            var intArr = new[] { 0, 1 };
            var intValue = 2;
            WorksAfterEnd(intArr, intValue);
            WorksAfterEndVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[] { 0, 1 };
            short shortValue = 2;
            WorksAfterEnd(shortArr, shortValue);
            WorksAfterEndVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[] { 0, 1 };
            TestStruct customValue = 2;
            WorksAfterEnd(customArr, customValue);
            WorksAfterEndVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new[] { (Timestamp)0, (Timestamp)1 };
            var tsValue = (Timestamp)2;
            WorksAfterEnd(tsArr, tsValue);
            WorksAfterEndVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksAfterEnd<T>(T[] arr, T valueIn)
        {
            var value = valueIn;
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(-3, idxI);

            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(-3, idxB);

            value = valueIn;
            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(1, idxILt);

            value = valueIn;
            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            value = valueIn;
            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-3, idxILq);

            value = valueIn;
            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(-3, idxIGe);

            value = valueIn;
            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxIGt);

            value = valueIn;
            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(1, idxBLt);

            value = valueIn;
            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            value = valueIn;
            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-3, idxBEq);

            value = valueIn;
            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(-3, idxBGe);

            value = valueIn;
            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxBGt);
        }

        private static void WorksAfterEndVec<T>(Vec<T> vec, T valueIn)
        {
            var value = valueIn;
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(-3, idxI);

            value = valueIn;
            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(-3, idxB);

            value = valueIn;
            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(1, idxILt);

            value = valueIn;
            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxILe);

            value = valueIn;
            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-3, idxILq);

            value = valueIn;
            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(-3, idxIGe);

            value = valueIn;
            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxIGt);

            value = valueIn;
            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(1, idxBLt);

            value = valueIn;
            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(1, idxBLe);

            value = valueIn;
            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-3, idxBEq);

            value = valueIn;
            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(-3, idxBGe);

            value = valueIn;
            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-3, idxBGt);
        }

        [Test]
        public void WorksBeforeStart()
        {
            var intArr = new[] { 0, 1, 2, 3 };
            var intValue = -1;
            WorksBeforeStart(intArr, intValue);
            WorksBeforeStartVec(new Vec<int>(intArr), intValue);

            var shortArr = new short[] { 0, 1, 2, 3 };
            short shortValue = -1;
            WorksBeforeStart(shortArr, shortValue);
            WorksBeforeStartVec(new Vec<short>(shortArr), shortValue);

            var customArr = new TestStruct[] { 0, 1, 2, 3 };
            TestStruct customValue = -1;
            WorksBeforeStart(customArr, customValue);
            WorksBeforeStartVec(new Vec<TestStruct>(customArr), customValue);

            var tsArr = new[] { (Timestamp)0, (Timestamp)1, (Timestamp)2, (Timestamp)3 };
            Timestamp tsValue = (Timestamp)(long)-1;
            WorksBeforeStart(tsArr, tsValue);
            WorksBeforeStartVec(new Vec<Timestamp>(tsArr), tsValue);
        }

        private static void WorksBeforeStart<T>(T[] arr, T valueIn)
        {
            var value = valueIn;
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(-1, idxI);

            value = valueIn;
            var idxB = arr.BinarySearch(value);
            Assert.AreEqual(-1, idxB);

            value = valueIn;
            var idxILt = arr.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            value = valueIn;
            var idxILe = arr.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxILe);

            value = valueIn;
            var idxILq = arr.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxILq);

            value = valueIn;
            var idxIGe = arr.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            value = valueIn;
            var idxIGt = arr.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(0, idxIGt);

            value = valueIn;
            var idxBLt = arr.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            value = valueIn;
            var idxBLe = arr.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxBLe);

            value = valueIn;
            var idxBEq = arr.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxBEq);

            value = valueIn;
            var idxBGe = arr.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(-0, idxBGe);

            value = valueIn;
            var idxBGt = arr.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-0, idxBGt);
        }

        private static void WorksBeforeStartVec<T>(Vec<T> vec, T valueIn)
        {
            var value = valueIn;
            var idxI = vec.InterpolationSearch(value);
            Assert.AreEqual(-1, idxI);

            value = valueIn;
            var idxB = vec.BinarySearch(value);
            Assert.AreEqual(-1, idxB);

            value = valueIn;
            var idxILt = vec.InterpolationLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxILt);

            value = valueIn;
            var idxILe = vec.InterpolationLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxILe);

            value = valueIn;
            var idxILq = vec.InterpolationLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxILq);

            value = valueIn;
            var idxIGe = vec.InterpolationLookup(ref value, Lookup.GE);
            Assert.AreEqual(0, idxIGe);

            value = valueIn;
            var idxIGt = vec.InterpolationLookup(ref value, Lookup.GT);
            Assert.AreEqual(0, idxIGt);

            value = valueIn;
            var idxBLt = vec.BinaryLookup(ref value, Lookup.LT);
            Assert.AreEqual(-1, idxBLt);

            value = valueIn;
            var idxBLe = vec.BinaryLookup(ref value, Lookup.LE);
            Assert.AreEqual(-1, idxBLe);

            value = valueIn;
            var idxBEq = vec.BinaryLookup(ref value, Lookup.EQ);
            Assert.AreEqual(-1, idxBEq);

            value = valueIn;
            var idxBGe = vec.BinaryLookup(ref value, Lookup.GE);
            Assert.AreEqual(-0, idxBGe);

            value = valueIn;
            var idxBGt = vec.BinaryLookup(ref value, Lookup.GT);
            Assert.AreEqual(-0, idxBGt);
        }

        [Test]
        public void WorksWithStartLength()
        {
            var intArr = new[] { 1, 2, 3, 4, 5 };
            WorksWithStartLength<int>(intArr);

            var shortArr = new short[] { 1, 2, 3, 4, 5 };
            WorksWithStartLength<short>(shortArr);

            var customArr = new TestStruct[] { 1, 2, 3, 4, 5 };
            WorksWithStartLength<TestStruct>(customArr);

            var tsArr = new[] { (Timestamp)1, (Timestamp)2, (Timestamp)3, (Timestamp)4, (Timestamp)5 };
            WorksWithStartLength<Timestamp>(tsArr);
        }

        private static void WorksWithStartLength<T>(T[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                var idxI = arr.InterpolationSearch(i, arr.Length - i, arr[i]);
                var idxB = arr.BinarySearch(i, arr.Length - i, arr[i]);
                Assert.AreEqual(i, idxI);
                Assert.AreEqual(i, idxB);

                var lookups = new[] { Lookup.LT, Lookup.LE, Lookup.EQ, Lookup.GE, Lookup.GT };

                // search at the start of range
                foreach (var lookup in lookups)
                {
                    var val = arr[i];
                    var idxILt = arr.InterpolationLookup(i, arr.Length - i, ref val, lookup);
                    val = arr[i];
                    var idxBLt = arr.BinaryLookup(i, arr.Length - i, ref val, lookup);
                    Assert.AreEqual(idxILt, idxBLt);

                    if (i > 0 && i < arr.Length - 1)
                    {
                        if (lookup == Lookup.LT)
                        {
                            // ~i == ~start, gotcha
                            // we must check for this as if values outside the range do not exist and vec is start-based
                            Assert.AreEqual(~(i), idxBLt);
                        }
                        else if (lookup == Lookup.GT)
                        {
                            Assert.AreEqual(i + 1, idxBLt);
                        }
                        else
                        {
                            Assert.AreEqual(i, idxBLt);
                        }
                    }
                }

                // search at the end of range
                foreach (var lookup in lookups)
                {
                    var val = arr[i];
                    var idxILt = arr.InterpolationLookup(0, i + 1, ref val, lookup);
                    val = arr[i];
                    var idxBLt = arr.BinaryLookup(0, i + 1, ref val, lookup);
                    Assert.AreEqual(idxILt, idxBLt);

                    if (i > 0 && i < arr.Length - 1)
                    {
                        if (lookup == Lookup.LT)
                        {
                            Assert.AreEqual(i - 1, idxBLt);
                        }
                        else if (lookup == Lookup.GT)
                        {
                            // ~(i+1) == ~(start + length), gotcha
                            // we must check for this as if values outside the range do not exist and vec is start-based
                            Assert.AreEqual(~(i + 1), idxBLt);
                        }
                        else
                        {
                            Assert.AreEqual(i, idxBLt);
                        }
                    }
                }
            }
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SearchBench()
        {
#if DEBUG
            var count = 16 * 1024;
            var rounds = 1;
            // must be power of 2
            var lens = new[] { 16, 128, 512, 1024, count };

#else
            var count = 4L * 1024 * 1024;
            var rounds = 2;
            // must be power of 2
            var lens = new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 4 * 1024, 16 * 1024, 64 * 1024, 128 * 1024 }; // , 512 * 1024 , 1024 * 1024, 8 * 1024 * 1024

#endif
            var vec = (Enumerable.Range(0, (int)count).Select(x => (long)(x * 2)).ToArray());

            var sum = 0L;
            for (int i = 0; i < 1000; i++)
            {
                sum += VectorSearch.BinarySearch(ref vec[0], 1000, i, default);
            }

            Console.WriteLine(sum);

            for (int r = 0; r < rounds; r++)
            {
                foreach (var len in lens)
                {
                    // BS_Default(len, count, vec, r);
                    BS_Classic(len, count, vec, r);
                    BS_Classic2(len, count, vec, r);
                    // // // //
                    // BS_Avx(len, count, vec, r);
                    // BS_AvxX(len, count, vec, r);

                    // BS_Sse(len, count, vec, r);

                    // BS_NoWaste(len, count, vec, r);

                    // BS_Interpolation(len, count, vec, r);
                    // BS_InterpolationAvx(len, count, vec, r);

                    // BS_Correctness(len, count, vec, r);
                }
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static void BS_Classic(int len, long count, long[] vec, int r)
        {
            var mask = len - 1;
            using (Benchmark.Run("BS_Classic_" + len, count * 2))
            {
                for (int i = 0; i < count * 2; i++)
                {
                    var value = i & mask;
                    var idx = VectorSearch.BinarySearchClassic(ref vec[0], r, len, value);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static void BS_Classic2(int len, long count, long[] vec, int r)
        {
            var mask = len - 1;
            using (Benchmark.Run("BS_Classic2_" + len, count * 2))
            {
                for (int i = 0; i < count * 2; i++)
                {
                    var value = i & mask;

                    var idx = VectorSearch.BinarySearchHybridLoHi(ref vec[0], r, len + r - 1, value);

                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static void BS_Default(int len, long count, long[] vec, int r)
        {
            var mask = len - 1;
            using (Benchmark.Run("BS_Default_" + len, count * 2))
            {
                for (int i = 0; i < count * 2; i++)
                {
                    var value = i & mask;

                    var idx = VectorSearch.BinarySearch(ref vec[r], len, value);

                }
            }
        }

#if HAS_INTRINSICS
        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static void BS_AvxX(int len, long count, long[] vec, int r)
        {
            var mask = len - 1;
            using (Benchmark.Run("BS_Avx+_" + len, count * 2))
            {
                for (int i = 0; i < count * 2; i++)
                {
                    var value = i & mask;

                    var idx = VectorSearch.BinarySearchAvx2LoHi(ref vec[0], r, r + len - 1, value);

                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static void BS_Sse(int len, long count, long[] vec, int r)
        {
            var mask = len - 1;
            using (Benchmark.Run("BS_Sse_" + len, count * 2))
            {
                for (int i = 0; i < count * 2; i++)
                {
                    var value = i & mask;

                    var idx = VectorSearch.BinarySearchSse42LoHi(ref vec[0], r, r + len - 1, value);

                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static unsafe void BS_Correctness(int len, long count, long[] vec, int r)
        {
            // len = 16;
            // r = 6;
            var mask = len - 1;
            fixed (long* ptr = &vec[r])
            {
                using (Benchmark.Run("BS_Hybrid_check_" + len, len + 20))
                {
                    for (int i = 19; i < len + 10; i++)
                    {
                        var value = i;
                        var idx = VectorSearch.BinarySearchSse42LoHi(ref vec[0], r, len - 1, value);
                        var idx2 = VectorSearch.BinarySearchClassic(ref vec[0], r, len - r, value);

                        if (idx != idx2)
                        {
                            Assert.Fail($"len={len}, i={i}, idx {idx} != correct idx2 {idx2}");
                        }
                    }
                }
            }
        }
#endif

//         [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
//         private static void BS_InterpolationAvx(int len, long count, long[] vec, int r)
//         {
//             var mask = len - 1;
//             using (Benchmark.Run($"IP_Avx {len}", count * 2))
//             {
//                 for (int i = 0; i < count * 2; i++)
//                 {
//                     var value = i & mask;
//
//                     var idx = VectorSearch.InterpolationSearchAvx3(ref vec[0], r, len - r, (long) value);
//
//                     // if (idx > 0 && idx != value / 2)
//                     // {
//                     //     Assert.Fail($"idx {idx} != value {value}");
//                     // }
//                 }
//             }
//         }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.MethodImplAggressiveOptimization)]
        private static void BS_Interpolation(int len, long count, long[] vec, int r)
        {
            var mask = len - 1;
            using (Benchmark.Run($"IP_Class {len}", count * 2))
            {
                for (int i = 0; i < count * 2; i++)
                {
                    var value = i & mask;

                    var idx = VectorSearch.InterpolationSearch(ref vec[0], r, len, (long)value);

                    // if (idx > 0 && idx != value / 2)
                    // {
                    //     Assert.Fail($"idx {idx} != value {value}");
                    // }
                }
            }
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
                    var vec = new Spreads.Collections.Vec<Timestamp>(Enumerable.Range(0, count).Select(x => (Timestamp)x).ToArray());

                    var mult = 10_000_000 / count;

                    using (Benchmark.Run($"Bin      {lookup} {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable CS0618 // Type or member is obsolete
                                var val = (Timestamp)i;
                                var idx = vec.DangerousBinaryLookup(0, count, ref val, lookup);
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

                    using (Benchmark.Run($"Interpol {lookup} {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable CS0618 // Type or member is obsolete
                                var val = (Timestamp)i;
                                var idx = vec.DangerousInterpolationLookup(0, count, ref val,
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

#if HAS_INTRINSICS
        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void SearchIrregularBench()
        {
            Random rng;

#if DEBUG
            var rounds = 10;
            var counts = new[] { 50, 100, 256, 512, 1000, 2048, 4096, 10_000 };
#else
            var rounds = 10;
            var counts = new[] { 50, 100, 256, 512, 1000, 2048, 4096, 10_000 }; // , 100_000, 1_000_000
#endif
            for (int r = 0; r < rounds; r++)
            {
                foreach (var count in counts)
                {
                    rng = new Random(r + 100);

                    var step = rng.Next(10, 20);
                    var dev = step / rng.Next(2, 4);

                    var vec = (Enumerable.Range(0, (int)count).Select(x => (long)(x)).ToArray());

                    for (int i = 1; i < vec.Length; i++)
                    {
                        vec[i] = vec[i - 1] + step + rng.Next(-dev, dev);
                    }

                    int[] binRes = new int[vec.Length + 20];
                    int[] binAvxRes = new int[vec.Length + 20];
                    int[] interRes = new int[vec.Length + 20];
                    // int[] interAvxRes = new int[vec.Length + 20];

                    for (int i = -10; i < count + 10; i++)
                    {
                        var bs = VectorSearch.BinarySearchClassic(ref vec[0], r, count - r, (i * step));
                        var bsavx = VectorSearch.BinarySearchAvx2LoHi(ref vec[0], r, count - 1, (i * step));
                        var ints = VectorSearch.InterpolationSearch(ref vec[0], r, count - r, (i * step));
                        // var intsAvx = VectorSearch.InterpolationSearchAvx3(ref vec[0], r, count - r, (i * step));
                        if (bs != ints)
                        {
                            Console.WriteLine($"IS_AVX: [{count}] binRes {bs} != interRes {ints} at {i}");
                            ints = VectorSearch.InterpolationSearch(ref vec[0], r, count - r, (i * step));
                            Assert.Fail();
                        }

                        // if (bs != intsAvx)
                        // {
                        //     Console.WriteLine($"IS_AVX: [{count}] binRes {bs} != intsAvx {intsAvx} at {i}");
                        //     ints = VectorSearch.InterpolationSearchAvx3(ref vec[0], r, count- r, (i * step));
                        //     Assert.Fail();
                        // }

                        if (bs != bsavx)
                        {
                            Console.WriteLine($"BS_AVX: [{count}] binRes {bs} != bsavx {bsavx} at {i}");
                            bsavx = VectorSearch.BinarySearchAvx2LoHi(ref vec[0], r, count - 1, (i * step));
                            Assert.Fail();
                        }

                        if (bs >= 0)
                        {
                            // Console.WriteLine("found");
                        }
                    }
#if DEBUG
                    var mult = 1;
#else
                    var mult = 5_000_000 / count;
#endif

                    // using (Benchmark.Run($"BS_Class {count}", count * mult))
                    // {
                    //     for (int m = 0; m < mult; m++)
                    //     {
                    //         for (int i = -10; i < count + 10; i++)
                    //         {
                    //             binRes[i+10] = VectorSearch.BinarySearchClassic(ref vec[0], r, count - r, (i * step)); // vec.DangerousBinarySearch(0, count, (i * step));
                    //         }
                    //     }
                    // }
                    //
                    // using (Benchmark.Run($"BS_Avx   {count}", count * mult))
                    // {
                    //     for (int m = 0; m < mult; m++)
                    //     {
                    //         for (int i = -10; i < count + 10; i++)
                    //         {
                    //             binAvxRes[i+10] = VectorSearch.BinarySearchAvx2(ref vec[0], r, count - r, (i * step)); // vec.DangerousBinarySearch(0, count, (i * step));
                    //         }
                    //     }
                    // }

                    // using (Benchmark.Run($"IS_Avx   {count}", (count + 20) * mult))
                    // {
                    //     for (int m = 0; m < mult; m++)
                    //     {
                    //         for (int i = -10; i < count + 10; i++)
                    //         {
                    //             interAvxRes[i + 10] = VectorSearch.InterpolationSearchAvx3(ref vec[0], r, count - r, (i * step));
                    //         }
                    //     }
                    // }

                    using (Benchmark.Run($"IS_Class {count}", (count + 20) * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = -10; i < count + 10; i++)
                            {
                                interRes[i + 10] = VectorSearch.InterpolationSearch(ref vec[0], r, count - r, (i * step));
                            }
                        }
                    }

                    for (int i = 0; i < count + 20; i++)
                    {
                        // if (binRes[i] != interAvxRes[i])
                        // {
                        //     Console.WriteLine($"IS_AVX: [{count}] binRes[i] {binRes[i]} != interRes[i] {interAvxRes[i]} at {i} and count {count}");
                        //     Assert.Fail();
                        // }
                        //
                        // if (binRes[i] != interRes[i])
                        // {
                        //     Console.WriteLine($"IS_Class: [{count}] binRes[i] {binRes[i]} != interRes[i] {interRes[i]} at {i} and count {count}");
                        //     Assert.Fail();
                        // }

                        // if (interAvxRes[i] != interRes[i])
                        // {
                        //     Console.WriteLine($"IS_Avx_Class: [{count}] interAvxRes[i] {interAvxRes[i]} != interRes[i] {interRes[i]} at {i} and count {count}");
                        //     Assert.Fail();
                        // }

                        // if (binRes[i] != binAvxRes[i])
                        // {
                        //     Console.WriteLine($"BS_AVX: [{count}] binRes[i] {binRes[i]} != binAvxRes[i] {binAvxRes[i]} at {i} and count {count}");
                        //     Assert.Fail();
                        // }
                    }
                }
            }

            Benchmark.Dump();
        }
#endif

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void LookupIrregularBench()
        {
            Random rng;

#if DEBUG
            var rounds = 10;
            var counts = new[] { 50, 100, 256, 512, 1000, 2048, 4096, 10_000 };
#else
            var rounds = 10;
            var counts = new[] { 50, 100, 256, 512, 1000, 2048, 4096, 10_000, 100_000, 1_000_000 };
#endif
            for (int r = 0; r < rounds; r++)
            {
                foreach (var count in counts)
                {
                    rng = new Random(r + 2);

                    var step = rng.Next(10, 1000);
                    var dev = step / rng.Next(2, 10);

                    var vec = new Spreads.Collections.Vec<Timestamp>(Enumerable.Range(0, count)
                        .Select(i => (Timestamp)i).ToArray());

                    for (int i = 1; i < vec.Length; i++)
                    {
                        vec[i] = vec[i - 1] + (Timestamp)step + (Timestamp)rng.Next(-dev, dev); //  (Timestamp)(vec[i].Nanos * 1000 - 2 + rng.Next(0, 4)); //
                    }

                    int[] binRes = new int[vec.Length];
                    int[] interRes = new int[vec.Length];

                    for (int i = 1; i < count; i++)
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        var value = (Timestamp)(i * step);
                        var br = vec.DangerousBinaryLookup(0, count, ref value, Lookup.LE);
                        var ir = vec.DangerousInterpolationLookup(0, count, ref value, Lookup.LE);
                        if (br != ir)
                        {
                            Console.WriteLine($"[{count}] binRes {br} != interRes {ir} at {i}");
                            ir = vec.DangerousInterpolationSearch(0, count, (Timestamp)(i * step));
                            Assert.Fail();
                        }

                        if (br >= 0)
                        {
                            // Console.WriteLine("found");
                        }
#pragma warning restore CS0618 // Type or member is obsolete
                    }
#if DEBUG
                    var mult = 1;
#else
                    var mult = 5_000_000 / count;
#endif

                    using (Benchmark.Run($"Bin      {count}", count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
#pragma warning disable CS0618 // Type or member is obsolete
                                var value = (Timestamp)(i * step);
                                binRes[i] = vec.DangerousBinaryLookup(0, count, ref value, Lookup.GE);
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
#pragma warning disable CS0618 // Type or member is obsolete
                                var value = (Timestamp)(i * step);
                                interRes[i] = VectorSearch.InterpolationLookup(ref Unsafe.Add(ref vec.DangerousGetRef(0), 0), 0, count, ref value, Lookup.GE);
                                // interRes[i] = vec.DangerousInterpolationLookup(0, count, ref value, Lookup.LE);
#pragma warning restore CS0618 // Type or member is obsolete
                            }
                        }
                    }

                    for (int i = 0; i < count; i++)
                    {
                        if (binRes[i] != interRes[i])
                        {
                            Console.WriteLine($"[{count}] binRes[i] {binRes[i]}");
                            Assert.Fail();
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
                    var vec = new Spreads.Collections.Vec<Timestamp>(Enumerable.Range(0, count).Select(x => (Timestamp)x).ToArray());

                    var mult = 5_000_000 / count;

                    using (Benchmark.Run("IndexOf " + count, count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (long i = 0; i < count; i++)
                            {
                                var idx = vec.Span.Slice(0, count).IndexOf((Timestamp)i);
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
                var vec = new Spreads.Collections.Vec<long>(Enumerable.Range(0, count).Select(x => (long)x).ToArray());

                var mult = 50_000_000 / count;

                using (Benchmark.Run($"LeLookup {count}", count * mult))
                {
                    for (int m = 0; m < mult; m++)
                    {
                        for (int i = 0; i < count; i++)
                        {

                            var val = (long)i;
                            var idx = vec.DangerousInterpolationLookup(0, count, ref val, Lookup.LE);

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

        [Test]
        public void TestGithubIssue()
        {
            long[] array =
            {
                76480981, 76480982, 76480983, 76480984, 76480986, 76480987, 76480989, 76482349, 76482352, 76482354, 76482358, 76482359, 76482361, 76482364, 76483504, 76483506,
                76483529, 76483530, 76483531, 76483580, 76483581, 76485397, 76485504, 76485506, 76485803, 76485805, 76485806, 76485807, 76485809, 76485810, 76485811, 76485813,
                76485814, 76485815, 76485816, 76485817, 76485818, 76485819, 76485820, 76485823, 76485824, 128653015, 128654739, 128654742, 128654745, 130763671, 130763672,
                130763674, 130764137, 130764139, 130767397, 130767398, 130767400, 133186694, 133186695, 133186696, 133186697, 133186698, 133186699, 133186700, 133186704,
                133186706, 133186707, 133186708, 133186709, 133186710, 133186714, 133186715, 133186716, 133186717, 133186721, 135684629, 135684630, 135684631, 135684632,
                135684637, 135684639, 135684641, 135684643, 135684645, 135684647, 135684651, 135684653, 135684655, 135684657, 151015301, 151015302, 151880018, 151880023,
                154616728, 154616730, 154616732, 154616734, 154616735, 154616737, 154616738, 154616739, 154616740, 154616742, 154616743, 154616744, 154616746, 154616747,
                154616748, 154616749, 154616750, 154616751, 154616752, 154616754, 154616755, 154616756, 154616757, 154616759, 154616761, 154616763, 154616765, 154616767,
                154616768, 154616769, 154616770, 154616772, 154616774, 154616775, 154616779, 154616782, 154616784, 154616785, 174616883, 174616885, 174616891, 174616892,
                174616893, 174616895, 174616898, 174616901, 174616902, 174616904, 174616905, 174616911, 174616912, 174616914, 174616915, 174616917, 174616919, 174616921,
                174616923, 174616924, 174616925, 174616926, 174616929, 174616930, 174616931, 174616932, 174616933, 174616936, 174616937, 174616941, 174616948, 174616951,
                174885157, 174885166, 174885167, 174885169, 177953857, 177953860, 177953862, 177953866, 177953869, 177953870, 177953871, 177953872, 177953873, 177953874,
                177953876, 178086521, 178930109, 178930123, 178930124, 178930125, 178930126, 178930128, 178950402, 179310959, 179310961, 179310962, 179310966, 179310967,
                179310968, 179310969, 179310970, 179310971, 179310972, 179310973, 179310974, 179310975, 179310977, 179310978, 179310979, 179310981, 179310982, 179310983,
                179310984, 179310986, 179310987, 179310988, 179310990, 179310991, 179310994, 179310995, 179310997, 179310998, 179311000, 179311001, 179311003, 179311004,
                179311006, 179311007, 179311009, 179311011, 179311012, 179311014, 179617018, 179617050, 179617064, 179617101, 179633551, 210282201, 210282202, 210282203,
                210282204, 210282206, 210282208, 210285066, 210285068, 210285070, 210285071, 210285072, 210285073, 210285075, 210285077, 210285078, 210285079, 210285082,
                210285083, 210285084, 210285085, 210285087, 210285089, 210285090, 210285091, 210285092, 210285094, 210285095, 210285098, 210285100, 210285101, 210285102,
                210285103, 210285105, 210285108, 210285111, 210285112, 210285113, 210285115, 210285117, 210285118, 210285119, 210285120, 210285121, 243828231, 243828236
            };

            Array.Sort(array);

            foreach (long value in array)
            {
                int index = Array.BinarySearch(array, value);
                int index2 = VectorSearch.BinarySearch(ref array[0], array.Length, value);

                Assert.True(index > -1);
                if (index != index2)
                {
                    Console.WriteLine($"{value} {index} {index2}");
                    Assert.Fail();
                }
            }
        }

        [Test]
        public void VectorSearchLong()
        {
            var array = new long[1000];
            var searchArray = new long[1000];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = i;
                searchArray[i] = i + (1 - (i % 3));
            }

            foreach (var value in searchArray)
            {
                int idxCoreClrArray = Array.BinarySearch(array, value);
                int idxBinaryDefault = VectorSearch.BinarySearch(ref array[0], array.Length, value);
                int idxBinaryClassic = VectorSearch.BinarySearchClassic(ref array[0], array.Length, value);
                int idxInterpolated = VectorSearch.InterpolationSearch(ref array[0], array.Length, value);

#if HAS_INTRINSICS
                int idxAvx = VectorSearch.BinarySearchAvx2LoHi(ref array[0], 0, array.Length - 1, value);
                int idxSse = VectorSearch.BinarySearchSse42LoHi(ref array[0], 0, array.Length - 1, value);
                idxAvx.ShouldBe(idxCoreClrArray);
                idxSse.ShouldBe(idxCoreClrArray);
#endif

                idxBinaryDefault.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);

                idxInterpolated.ShouldBe(idxCoreClrArray);
            }
        }

        [Test]
        public void VectorSearchInt()
        {
            var array = new int[1000];
            var searchArray = new int[1000];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = i;
                searchArray[i] = i + (1 - (i % 3));
            }

            foreach (var value in searchArray)
            {
                int idxCoreClrArray = Array.BinarySearch(array, value);
                int idxBinaryDefault = VectorSearch.BinarySearch(ref array[0], array.Length, value);
                int idxBinaryClassic = VectorSearch.BinarySearchClassic(ref array[0], array.Length, value);
                int idxInterpolated = VectorSearch.InterpolationSearch(ref array[0], array.Length, value);

#if HAS_INTRINSICS
                int idxAvx = VectorSearch.BinarySearchAvx2LoHi(ref array[0], 0, array.Length - 1, value);
                int idxSse = VectorSearch.BinarySearchSse42LoHi(ref array[0], 0, array.Length - 1, value);
                idxAvx.ShouldBe(idxCoreClrArray);
                idxSse.ShouldBe(idxCoreClrArray);
#endif

                idxBinaryDefault.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);

                idxInterpolated.ShouldBe(idxCoreClrArray);
            }
        }

        [Test]
        public void VectorSearchShort()
        {
            var array = new short[1000];
            var searchArray = new short[1000];

            for (short i = 0; i < array.Length; i++)
            {
                array[i] = i;
                searchArray[i] = (short)(i + (1 - (i % 3)));
            }

            foreach (var value in searchArray)
            {
                int idxCoreClrArray = Array.BinarySearch(array, value);
                int idxBinaryDefault = VectorSearch.BinarySearch(ref array[0], array.Length, value);
                int idxBinaryClassic = VectorSearch.BinarySearchClassic(ref array[0], array.Length, value);
                int idxInterpolated = VectorSearch.InterpolationSearch(ref array[0], array.Length, value);

#if HAS_INTRINSICS
                int idxAvx = VectorSearch.BinarySearchAvx2LoHi(ref array[0], 0, array.Length - 1, value);
                int idxSse = VectorSearch.BinarySearchSse42LoHi(ref array[0], 0, array.Length - 1, value);
                idxAvx.ShouldBe(idxCoreClrArray);
                idxSse.ShouldBe(idxCoreClrArray);
#endif

                idxBinaryDefault.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);

                idxInterpolated.ShouldBe(idxCoreClrArray);
            }
        }

        [Test]
        public void VectorSearchSbyte()
        {
            var array = new sbyte[127];
            var searchArray = new sbyte[127];

            for (sbyte i = 0; i < array.Length; i++)
            {
                array[i] = i;
                searchArray[i] = (sbyte)(i + (1 - (i % 3)));
            }

            foreach (var value in searchArray)
            {
                int idxCoreClrArray = Array.BinarySearch(array, value);
                int idxBinaryDefault = VectorSearch.BinarySearch(ref array[0], array.Length, value);
                int idxBinaryClassic = VectorSearch.BinarySearchClassic(ref array[0], array.Length, value);
                int idxInterpolated = VectorSearch.InterpolationSearch(ref array[0], array.Length, value);

#if HAS_INTRINSICS
                int idxAvx = VectorSearch.BinarySearchAvx2LoHi(ref array[0], 0, array.Length - 1, value);
                int idxSse = VectorSearch.BinarySearchSse42LoHi(ref array[0], 0, array.Length - 1, value);
                idxAvx.ShouldBe(idxCoreClrArray);
                idxSse.ShouldBe(idxCoreClrArray);
#endif

                idxBinaryDefault.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);
                idxBinaryClassic.ShouldBe(idxCoreClrArray);

                idxInterpolated.ShouldBe(idxCoreClrArray);
            }
        }
    }
}
