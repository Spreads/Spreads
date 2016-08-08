using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;

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
        public void DateTimeIsBlittable() {
            var ptr = Marshal.AllocHGlobal(10000);
            var buffer = new DirectBuffer(10000, ptr);
            Assert.IsTrue(TypeHelper<StructWithDateTime>.Size > 0);
            Assert.IsTrue(TypeHelper<KVPair<long, double>>.Size > 0);

            var str = new StructWithDateTime();
            str.Dt = DateTime.Today.AddTicks(1);
            str.Dbl = 123.4;
            str.Default.Value = DateTime.Today.AddTicks(1);


            TypeHelper<StructWithDateTime>.Write(str, ref buffer, 0);

            var str2 = default(StructWithDateTime);
            TypeHelper<StructWithDateTime>.Read(ptr, ref str2);

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
