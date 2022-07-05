// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Shouldly;

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
            TypeHelper<MyStruct>.IsReferenceOrContainsReferences.ShouldBe(false);
            TypeHelper<MyStruct>.IsReferenceOrContainsReferencesManual(typeof(MyStruct)).ShouldBe(false);

            TypeHelper<MyStructWithRef>.IsReferenceOrContainsReferences.ShouldBe(true);
            TypeHelper<MyStructWithRef>.IsReferenceOrContainsReferencesManual(typeof(MyStructWithRef)).ShouldBe(true);
        }

        [Test]
        public void ArrayAdjustment()
        {
            ((byte) TypeHelper<int>.ArrayOffset > 0).ShouldBe(true);
        }

        [Test]
        public void CouldGetRuntimeVecInfo()
        {
            var types = new[] {typeof(int), typeof(decimal), typeof(MyStruct), typeof(MyStructWithRef), typeof(string), typeof(object)};

            foreach (var type in types)
            {
                var vti = TypeHelper.GetRuntimeTypeInfo(type);
                var vti2 = TypeHelper.GetRuntimeTypeInfo(vti.RuntimeTypeId);

                ReferenceEquals(vti.Type, vti2.Type).ShouldBe(true);
                vti2.ElemSize.ShouldBe(vti.ElemSize);
                vti2.RuntimeTypeId.ShouldBe(vti.RuntimeTypeId);
                vti2.IsReferenceOrContainsReferences.ShouldBe(vti.IsReferenceOrContainsReferences);
                Console.WriteLine("TYPE: " + type.Name);
                Console.WriteLine("vti.RuntimeTypeId: " + vti.RuntimeTypeId);
                Console.WriteLine("vti.ElemSize: " + vti.ElemSize);
                Console.WriteLine();
            }
        }
    }
}
