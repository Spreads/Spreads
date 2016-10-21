// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
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

    }
}
