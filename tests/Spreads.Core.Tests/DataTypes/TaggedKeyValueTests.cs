// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization;
using System;
using Spreads.Serialization.Utf8Json;

namespace Spreads.Core.Tests.DataTypes
{
    [TestFixture]
    public class TaggedKeyValueTests
    {
        [Test]
        public void DateTimeDoubleIsTreatedAsBlittable()
        {
            var kv = new TaggedKeyValue<long, double>(DateTime.Now.Ticks, 123.45, 255);
            Assert.AreEqual(20, TypeHelper<TaggedKeyValue<long, double>>.Size);
            Console.WriteLine(TypeHelper<TaggedKeyValue<long, double>>.Size);

            var tgt = new byte[20 + 4];

            var written = BinarySerializer.Write(kv, ref tgt, null, SerializationFormat.Binary);
            Assert.AreEqual(20 + 4, written);

            var read = BinarySerializer.Read(tgt, out TaggedKeyValue<long, double> kv1);
            Assert.AreEqual(20 + 4, read);
            Assert.AreEqual(kv, kv1);
        }

        [Test]
        public void CouldSerializeWithCustomFormatter()
        {
            var kv = new TaggedKeyValue<long, double>(DateTime.Now.Ticks, 123.45, 255);

            var bytes = JsonSerializer.Serialize(kv);

            var kv1 = JsonSerializer.Deserialize<TaggedKeyValue<long, double>>(bytes);

            Assert.AreEqual(kv.Tag, kv1.Tag);
            Assert.AreEqual(kv.Key, kv1.Key);
            Assert.AreEqual(kv.Value, kv1.Value);
        }

    }
}