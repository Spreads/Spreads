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
    public readonly struct TestStruct : IInt64Diffable<TestStruct>
    {
        private readonly long _value;

        public TestStruct(long value)
        {
            _value = value;
        }

        public static implicit operator TestStruct(long value)
        {
            return new TestStruct(value);
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
            return new TestStruct(_value + diff);
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
        [Test]
        public void WorksOnEmpty()
        {
            var intArr = new int[0];
            var intValue = 1;
            WorksOnEmpty(intArr, intValue);
            WorksOnEmptyVec(intArr, intValue);

            var shortArr = new short[0];
            short shortValue = 1;
            WorksOnEmpty(shortArr, shortValue);
            WorksOnEmptyVec(shortArr, shortValue);

            var customArr = new TestStruct[0];
            TestStruct customValue = 1;
            WorksOnEmpty(customArr, customValue);
            WorksOnEmptyVec(customArr, customValue);

            var tsArr = new Timestamp[0];
            Timestamp tsValue = (Timestamp)1;
            WorksOnEmpty(tsArr, tsValue);
            WorksOnEmptyVec(tsArr, tsValue);
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

        private static void WorksOnEmptyVec<T>(ArrayVector<T> arr, T value)
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

        [Test]
        public void WorksOnSingle()
        {
            var intArr = new[] { 1 };
            var intValue = 1;
            WorksOnSingle(intArr, intValue);
            WorksOnSingleVec(intArr, intValue);

            var shortArr = new short[] { 1 };
            short shortValue = 1;
            WorksOnSingle(shortArr, shortValue);
            WorksOnSingleVec(shortArr, shortValue);

            var customArr = new TestStruct[] { 1 };
            TestStruct customValue = 1;
            WorksOnSingle(customArr, customValue);
            WorksOnSingleVec(customArr, customValue);

            var tsArr = new Timestamp[] { (Timestamp)1 };
            Timestamp tsValue = (Timestamp)1;
            WorksOnSingle(tsArr, tsValue);
            WorksOnSingleVec(tsArr, tsValue);
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

        private static void WorksOnSingleVec<T>(ArrayVector<T> arr, T value)
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

        [Test]
        public void WorksOnFirst()
        {
            var intArr = new[] { 1, 2 };
            var intValue = 1;
            WorksOnFirst(intArr, intValue);
            WorksOnFirstVec(intArr, intValue);

            var shortArr = new short[] { 1, 2 };
            short shortValue = 1;
            WorksOnFirst(shortArr, shortValue);
            WorksOnFirstVec(shortArr, shortValue);

            var customArr = new TestStruct[] { 1, 2 };
            TestStruct customValue = 1;
            WorksOnFirst(customArr, customValue);
            WorksOnFirstVec(customArr, customValue);

            var tsArr = new Timestamp[] { (Timestamp)1, (Timestamp)2 };
            Timestamp tsValue = (Timestamp)1;
            WorksOnFirst(tsArr, tsValue);
            WorksOnFirstVec(tsArr, tsValue);
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

        private static void WorksOnFirstVec<T>(ArrayVector<T> arr, T valueIn)
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

        [Test]
        public void WorksOnLast()
        {
            var intArr = new[] { 1, 2 };
            var intValue = 2;
            WorksOnLast(intArr, intValue);
            WorksOnLastVec(intArr, intValue);

            var shortArr = new short[] { 1, 2 };
            short shortValue = 2;
            WorksOnLast(shortArr, shortValue);
            WorksOnLastVec(shortArr, shortValue);

            var customArr = new TestStruct[] { 1, 2 };
            TestStruct customValue = 2;
            WorksOnLast(customArr, customValue);
            WorksOnLastVec(customArr, customValue);

            var tsArr = new Timestamp[] { (Timestamp)1, (Timestamp)2 };
            Timestamp tsValue = (Timestamp)2;
            WorksOnLast(tsArr, tsValue);
            WorksOnLastVec(tsArr, tsValue);
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

        private static void WorksOnLastVec<T>(ArrayVector<T> arr, T valueIn)
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

        [Test]
        public void WorksOnExistingMiddle()
        {
            var intArr = new[] { 1, 2, 4 };
            var intValue = 2;
            WorksOnExistingMiddle(intArr, intValue);
            WorksOnExistingMiddleVec(intArr, intValue);

            var shortArr = new short[] { 1, 2, 4 };
            short shortValue = 2;
            WorksOnExistingMiddle(shortArr, shortValue);
            WorksOnExistingMiddleVec(shortArr, shortValue);

            var customArr = new TestStruct[] { 1, 2, 4 };
            TestStruct customValue = 2;
            WorksOnExistingMiddle(customArr, customValue);
            WorksOnExistingMiddleVec(customArr, customValue);

            var tsArr = new Timestamp[] { (Timestamp)1, (Timestamp)2, (Timestamp)4 };
            Timestamp tsValue = (Timestamp)2;
            WorksOnExistingMiddle(tsArr, tsValue);
            WorksOnExistingMiddleVec(tsArr, tsValue);
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

        private static void WorksOnExistingMiddleVec<T>(ArrayVector<T> arr, T valueIn)
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

        [Test]
        public void WorksOnNonExistingMiddle()
        {
            var intArr = new[] { 1, 4 };
            var intValue = 2;
            WorksOnNonExistingMiddle(intArr, intValue);
            WorksOnNonExistingMiddleVec(intArr, intValue);

            var shortArr = new short[] { 1, 4 };
            short shortValue = 2;
            WorksOnNonExistingMiddle(shortArr, shortValue);
            WorksOnNonExistingMiddleVec(shortArr, shortValue);

            var customArr = new TestStruct[] { 1, 4 };
            TestStruct customValue = 2;
            WorksOnNonExistingMiddle(customArr, customValue);
            WorksOnNonExistingMiddleVec(customArr, customValue);

            var tsArr = new Timestamp[] { (Timestamp)1, (Timestamp)4 };
            Timestamp tsValue = (Timestamp)2;
            WorksOnNonExistingMiddle(tsArr, tsValue);
            WorksOnNonExistingMiddleVec(tsArr, tsValue);
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

        private static void WorksOnNonExistingMiddleVec<T>(ArrayVector<T> arr, T valueIn)
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

        [Test]
        public void WorksAfterEnd()
        {
            var intArr = new[] { 0, 1 };
            var intValue = 2;
            WorksAfterEnd(intArr, intValue);
            WorksAfterEndVec(intArr, intValue);

            var shortArr = new short[] { 0, 1 };
            short shortValue = 2;
            WorksAfterEnd(shortArr, shortValue);
            WorksAfterEndVec(shortArr, shortValue);

            var customArr = new TestStruct[] { 0, 1 };
            TestStruct customValue = 2;
            WorksAfterEnd(customArr, customValue);
            WorksAfterEndVec(customArr, customValue);

            var tsArr = new Timestamp[] { (Timestamp)0, (Timestamp)1 };
            Timestamp tsValue = (Timestamp)2;
            WorksAfterEnd(tsArr, tsValue);
            WorksAfterEndVec(tsArr, tsValue);
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

        private static void WorksAfterEndVec<T>(ArrayVector<T> arr, T valueIn)
        {
            var value = valueIn;
            var idxI = arr.InterpolationSearch(value);
            Assert.AreEqual(-3, idxI);

            value = valueIn;
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

        [Test]
        public void WorksBeforeStart()
        {
            var intArr = new[] { 0, 1, 2, 3 };
            var intValue = -1;
            WorksBeforeStart(intArr, intValue);
            WorksBeforeStartVec(intArr, intValue);

            var shortArr = new short[] { 0, 1, 2, 3 };
            short shortValue = -1;
            WorksBeforeStart(shortArr, shortValue);
            WorksBeforeStartVec(shortArr, shortValue);

            var customArr = new TestStruct[] { 0, 1, 2, 3 };
            TestStruct customValue = -1;
            WorksBeforeStart(customArr, customValue);
            WorksBeforeStartVec(customArr, customValue);

            var tsArr = new Timestamp[] { (Timestamp)0, (Timestamp)1, (Timestamp)2, (Timestamp)3 };
            Timestamp tsValue = (Timestamp)(long)-1;
            WorksBeforeStart(tsArr, tsValue);
            WorksBeforeStartVec(tsArr, tsValue);
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

        private static void WorksBeforeStartVec<T>(ArrayVector<T> arr, T valueIn)
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

        [Test]
        public void WorksWithStartLength()
        {
            var intArr = new int[] { 1, 2, 3, 4, 5 };
            WorksWithStartLength<int>(intArr);

            var shortArr = new short[] { 1, 2, 3, 4, 5 };
            WorksWithStartLength<short>(shortArr);

            var customArr = new TestStruct[] { 1, 2, 3, 4, 5 };
            WorksWithStartLength<TestStruct>(customArr);

            var tsArr = new Timestamp[] { (Timestamp)1, (Timestamp)2, (Timestamp)3, (Timestamp)4, (Timestamp)5 };
            WorksWithStartLength<Timestamp>(tsArr);
        }

        private static void WorksWithStartLength<T>(ArrayVector<T> arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                //var idxI = arr.InterpolationSearch(i, arr.Length - i, arr.Get(i));
                //var idxB = arr.BinarySearch(i, arr.Length - i, arr.Get(i));
                //Assert.AreEqual(i, idxI);
                //Assert.AreEqual(i, idxB);

                var lookups = new[] { Lookup.LT, Lookup.LE, Lookup.EQ, Lookup.GE, Lookup.GT };

                // search at the start of range
                foreach (var lookup in lookups)
                {
                    var val = arr.GetItem(i);
                    var idxILt = arr.InterpolationLookup(i, arr.Length - i, ref val, lookup);
                    val = arr.GetItem(i);
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
                    var val = arr.GetItem(i);
                    var idxILt = arr.InterpolationLookup(0, i + 1, ref val, lookup);
                    val = arr.GetItem(i);
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
        public void SearchBench()
        {
#if  DEBUG
            var count = 1024;
            var rounds = 1;
            // must be power of 2
            var lens = new[] { 16, 128, 512, 1024 };

#else
            var count = 10L * 1024 * 1024;
            var rounds = 1;
            // must be power of 2
            var lens = new[] { 16, 128, 512, 1024, 16 * 1024, 128 * 1024, 512 * 1024, 1024 * 1024, 8 * 1024 * 1024 };

#endif
            var vec = (Enumerable.Range(0, (int)count).Select(x => (Timestamp)(long)x).ToArray());

            for (int r = 0; r < rounds; r++)
            {
                foreach (var len in lens)
                {
                    var mask = len - 1;

                    using (Benchmark.Run("Binary" + len, count))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var value = i & mask;
#pragma warning disable 618
                            var idx = vec.DangerousBinarySearch(0, len, (Timestamp)value,
                                KeyComparer<Timestamp>.Default);
#pragma warning restore 618
                            if (idx != value)
                            {
                                Assert.Fail("idx != value");
                            }
                        }
                    }

                    using (Benchmark.Run($"Interpolation{len}", count))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var value = i & mask;
#pragma warning disable 618
                            var idx = vec.DangerousInterpolationSearch(0, len, (Timestamp)value,
                                KeyComparer<Timestamp>.Default);
#pragma warning restore 618
                            if (idx != value)
                            {
                                Assert.Fail("idx != value");
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

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void SearchIrregularBench()
        {
            Random rng;

#if DEBUG
            var rounds = 2;
            var counts = new[] { 50, 100, 256, 512, 1000, 2048, 4096, 10_000 };
#else
            var rounds = 10;
            var counts = new[] { 50, 100, 256, 512, 1000, 2048, 4096, 10_000, 100_000, 1_000_000 };
#endif
            for (int r = 0; r < rounds; r++)
            {
                foreach (var count in counts)
                {
                    rng = new Random(r + 1);

                    var step = rng.Next(10, 1000);
                    var dev = step / rng.Next(2, 10);

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
                        var br = vec.DangerousBinarySearch(0, count, (Timestamp)(i * step));
                        var ir = vec.DangerousInterpolationSearch(0, count, (Timestamp)(i * step));
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
                }
            }
            Benchmark.Dump();
        }

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
                    rng = new Random(r + 1);

                    var step = rng.Next(10, 1000);
                    var dev = step / rng.Next(2, 10);

                    var vec = new Vec<Timestamp>(Enumerable.Range(0, count)
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
                                binRes[i] = vec.DangerousBinaryLookup(0, count, ref value, Lookup.LE);
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
                                var value = (Timestamp)(i * step);
                                interRes[i] = vec.DangerousInterpolationLookup(0, count, ref value, Lookup.LE);
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
                            var val = (long)i;
                            var idx = vec.DangerousInterpolationLookup(0, count, ref val, Lookup.LE);
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
