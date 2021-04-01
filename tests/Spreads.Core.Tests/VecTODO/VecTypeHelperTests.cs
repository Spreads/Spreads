// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Runtime.InteropServices;

// ReSharper disable PossibleNullReferenceException

namespace Spreads.Native.Tests
{
    [Category("CI")]
    [TestFixture]
    public class VecTypeHelperTests
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct MyStruct
        {
            private long Long;
            private short Short;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct MyStructWithRef
        {
            private string String;
            private long Long;
            private short Short;
        }

        [Test]
        public void IsRef()
        {
            Assert.AreEqual(false, VecTypeHelper.IsReferenceOrContainsReferences(typeof(MyStruct)));
            Assert.AreEqual(false, VecTypeHelper.IsReferenceOrContainsReferencesManual(typeof(MyStruct)));

            Assert.AreEqual(true, VecTypeHelper.IsReferenceOrContainsReferences(typeof(MyStructWithRef)));
            Assert.AreEqual(true, VecTypeHelper.IsReferenceOrContainsReferencesManual(typeof(MyStructWithRef)));
        }

        [Test]
        public void ArrayAdjustment()
        {
            Console.WriteLine($"ArrayOffsetAdjustment: {(byte) VecTypeHelper<int>.ArrayOffsetAdjustment}");
            Console.WriteLine($"UnsafeEx.ArrayOffsetAdjustmentOfType: {UnsafeEx.ArrayOffsetAdjustmentOfType(typeof(int))}");
            Console.WriteLine($"UnsafeEx.ArrayOffsetAdjustment: {UnsafeEx.ArrayOffsetAdjustment<int>()}");

            Assert.IsTrue((byte) VecTypeHelper<int>.ArrayOffsetAdjustment > 0);
        }

        [Test]
        public void CouldGetRuntimeVecInfo()
        {
            var types = new[] {typeof(int), typeof(decimal), typeof(MyStruct), typeof(MyStructWithRef), typeof(string), typeof(object)};

            foreach (var type in types)
            {
                var vti = VecTypeHelper.GetInfo(type);
                var vti2 = VecTypeHelper.GetInfo(vti.RuntimeTypeId);

                Assert.IsTrue(ReferenceEquals(vti.Type, vti2.Type));
                Assert.AreEqual(vti.ArrayOffsetAdjustment, vti2.ArrayOffsetAdjustment);
                Assert.AreEqual(vti.ElemSize, vti2.ElemSize);
                Assert.AreEqual(vti.UnsafeGetterPtr, vti2.UnsafeGetterPtr);
                Assert.AreEqual(vti.UnsafeSetterPtr, vti2.UnsafeSetterPtr);
                Assert.AreEqual(vti.RuntimeTypeId, vti2.RuntimeTypeId);
                Assert.AreEqual(vti.IsReferenceOrContainsReferences, vti2.IsReferenceOrContainsReferences);
                Console.WriteLine("TYPE: " + type.Name);
                Console.WriteLine("vti.RuntimeTypeId: " + vti.RuntimeTypeId);
                Console.WriteLine("vti.ElemOffset: " + vti.ArrayOffsetAdjustment);
                Console.WriteLine("vti.ElemSize: " + vti.ElemSize);
                Console.WriteLine();
            }
        }
    }
}
