// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using System.Buffers;

namespace Spreads.Extensions.Tests {


    [TestFixture]
    public class UnsafeCastTests {
        [StructLayout(LayoutKind.Sequential)]
        public struct KVPair<K, V> {
            public K Key;
            public V Value;
        }

        public struct StructWithDefaultLayout {
            public DateTime Value;
        }

        public struct StructWithDateTime {
            public DateTime Dt;
            public double Dbl;
            public StructWithDefaultLayout Default;
        }

        [Test]
        public unsafe void DateTimeIsBlittable() {


            var dest = (OwnedBuffer<byte>)new byte[10000];
            var buffer = dest.Buffer;
            var handle = buffer.Pin();
            var ptr = (IntPtr)handle.PinnedPointer;

            Assert.IsTrue(TypeHelper<StructWithDateTime>.Size > 0);
            Assert.IsTrue(TypeHelper<KVPair<long, double>>.Size > 0);

            var str = new StructWithDateTime();
            str.Dt = DateTime.Today.AddTicks(1);
            str.Dbl = 123.4;
            str.Default.Value = DateTime.Today.AddTicks(1);


            TypeHelper<StructWithDateTime>.Write(str, ref buffer, 0);

            var str2 = default(StructWithDateTime);
            TypeHelper<StructWithDateTime>.Read(ptr, out str2);

            Assert.AreEqual(str.Dt, str2.Dt);
            Assert.AreEqual(str.Dbl, str2.Dbl);
            Assert.AreEqual(str.Default.Value, str2.Default.Value);

        }

        //[Test]
        //public void CouldCastDoubleArrayToBytesArray() {
        //    var dbls = new[] { 123.4, 567.89 };
        //    var bytes = CoreUtils.UnsafeCast<byte[]>(dbls);
        //    Assert.AreEqual(dbls.Length * 8, bytes.Length);
        //    Console.WriteLine($"Count: {bytes.Length}");

        //}


    }
}
