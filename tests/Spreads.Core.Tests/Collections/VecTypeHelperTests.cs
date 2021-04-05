// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;
using NUnit.Framework;

// ReSharper disable PossibleNullReferenceException

namespace Spreads.Core.Tests.Collections
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
            Assert.AreEqual(false, TypeHelper<MyStruct>.IsReferenceOrContainsReferences);
            Assert.AreEqual(false, TypeHelper<MyStruct>.IsReferenceOrContainsReferencesManual(typeof(MyStruct)));

            Assert.AreEqual(true, TypeHelper<MyStructWithRef>.IsReferenceOrContainsReferences);
            Assert.AreEqual(true, TypeHelper<MyStructWithRef>.IsReferenceOrContainsReferencesManual(typeof(MyStructWithRef)));
        }

        [Test]
        public void ArrayAdjustment()
        {
            Assert.IsTrue((byte) TypeHelper<int>.ArrayOffset > 0);
        }

        [Test]
        public void CouldGetRuntimeVecInfo()
        {
            var types = new[] {typeof(int), typeof(decimal), typeof(MyStruct), typeof(MyStructWithRef), typeof(string), typeof(object)};

            foreach (var type in types)
            {
                var vti = TypeHelper.GetRuntimeTypeInfo(type);
                var vti2 = TypeHelper.GetRuntimeTypeInfo(vti.RuntimeTypeId);

                Assert.IsTrue(ReferenceEquals(vti.Type, vti2.Type));
                Assert.AreEqual(vti.ElemSize, vti2.ElemSize);
                Assert.AreEqual(vti.RuntimeTypeId, vti2.RuntimeTypeId);
                Assert.AreEqual(vti.IsReferenceOrContainsReferences, vti2.IsReferenceOrContainsReferences);
                Console.WriteLine("TYPE: " + type.Name);
                Console.WriteLine("vti.RuntimeTypeId: " + vti.RuntimeTypeId);
                Console.WriteLine("vti.ElemSize: " + vti.ElemSize);
                Console.WriteLine();
            }
        }
    }
}
