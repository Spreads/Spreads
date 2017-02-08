// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Serialization;
using Newtonsoft.Json;

namespace Spreads.Extensions.Tests {


    [TestFixture]
    public class SerializationTests {


        [Test]
        public void CouldSerializeSortedMapWithJsonNet() {
            var sm = new SortedMap<DateTime, double>();
            for (int i = 0; i < 10; i++) {
                sm.Add(DateTime.UtcNow.Date.AddDays(i), i);
            }

            var str = JsonConvert.SerializeObject(sm);
            Console.WriteLine(str);
            var sm2 = JsonConvert.DeserializeObject<SortedMap<DateTime, double>>(str);
            Assert.IsTrue(sm.SequenceEqual(sm2));
        }

        [Test]
        public void CouldSerializeSortedMapWithBinary()
        {
            SortedMap<DateTime, double>.Init();
            var sm = new SortedMap<DateTime, double>();
            for (int i = 0; i < 10; i++) {
                sm.Add(DateTime.UtcNow.Date.AddDays(i), i);
            }
            MemoryStream tmp;
            var len = BinarySerializer.SizeOf(sm, out tmp);
            Console.WriteLine(len);
            var dest = BufferPool.PreserveMemory(len);
            var len2 = BinarySerializer.Write(sm, ref dest, 0, tmp);
            Assert.AreEqual(len, len2);

            SortedMap<DateTime, double> sm2 = null;
            BinarySerializer.Read<SortedMap<DateTime, double>>(dest, 0, ref sm2);

            Assert.IsTrue(sm.SequenceEqual(sm2));
        }

    }
}
