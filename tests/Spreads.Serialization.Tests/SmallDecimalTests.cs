// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization.Utf8Json;

namespace Spreads.Core.Tests
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SmallDecimalTests
    {
        [Test]
        public void JsonSerializationWorks()
        {
            var sd = (SmallDecimal)(-123.456);
            var str = JsonSerializer.ToJsonString(sd);
            Console.WriteLine(str);
            Assert.AreEqual("-123.456", str);

            var sd1 = JsonSerializer.Deserialize<SmallDecimal>(str);
            var sd2 = JsonSerializer.Deserialize<SmallDecimal>("\"-123.456000000\"");

            var d1 = JsonSerializer.Deserialize<decimal>(str);
            var d2 = JsonSerializer.Deserialize<decimal>("\"-123.456000000\"");

            Assert.AreEqual((decimal)sd, (decimal)sd1);
            Assert.AreEqual((decimal)sd, (decimal)sd2);
            Assert.AreEqual((decimal)sd, d1);
            Assert.AreEqual((decimal)sd, d2);
        }
    }
}
