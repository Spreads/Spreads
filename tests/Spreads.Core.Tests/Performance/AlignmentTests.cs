// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Core.Tests.Performance
{
    [TestFixture]
    public class AlignmentTests
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Struct17 : IEquatable<Struct17>
        {
            public long Field1;
            public byte Field3;
            public long Field2;

            public bool Equals(Struct17 other)
            {
                return Field1 == other.Field1 && Field2 == other.Field2 && Field3 == other.Field3;
            }
        }

        [Test]
        public unsafe void SpanUnalignedIndexOf()
        {
            var arr = new Struct17[2];
            var value = new Struct17() { Field1 = 1 };
            arr[1] = value;

            var span = arr.AsSpan();
            Assert.AreEqual(1, span.IndexOf(value));
            Assert.AreEqual(17, Unsafe.SizeOf<Struct17>());
            Assert.AreEqual(17, (long)Unsafe.AsPointer(ref arr[1]) - (long)Unsafe.AsPointer(ref arr[0]));

            var v0 = arr[0];
            var v1 = arr[1];
            Assert.AreEqual(24, (long)Unsafe.AsPointer(ref v0) - (long)Unsafe.AsPointer(ref v1));
            Assert.AreEqual(15, (long)Unsafe.AsPointer(ref v0) - (long)Unsafe.AsPointer(ref v1.Field2));
        }

        [Test]
        public void SpanUnalignedDoubleIndexOf()
        {
            var arr = new byte[17];
            var value = 1.0;
            Unsafe.WriteUnaligned(ref arr[9], value);

            var span = MemoryMarshal.Cast<byte, double>(arr.AsSpan(1));

            // this should fail on ARM
            Assert.AreEqual(1, span.IndexOf(value));
        }


        [Test]
        public unsafe void ArrayWriteAlignment()
        {
            Assert.AreEqual(17, Unsafe.SizeOf<Struct17>());

            var arr = new Struct17[2];

            fixed (Struct17* ptr = &arr[0])
            {
                *ptr = new Struct17();
                ptr[1] = new Struct17();

                byte* bptr = (byte*)ptr;

                for (int i = 0; i < 34; i++)
                {
                    Assert.AreEqual(0, bptr[i]);
                }
            }

            var x = new Struct17() { Field1 = 1 };
            arr[1] = x;
        }
    }
}
