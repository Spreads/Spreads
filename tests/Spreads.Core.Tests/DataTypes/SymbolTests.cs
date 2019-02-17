// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using System;

namespace Spreads.Core.Tests.DataTypes
{
    [Category("CI")]
    [TestFixture]
    public class SymbolTests
    {
        [Test]
        public void CouldRoundtripStringViaSymbol()
        {
            string str;
            string str2;

            for (int i = 0; i <= 256; i++)
            {
                str = new string('x', 256 - i);
                var sym256 = new Symbol256(str);
                str2 = sym256.ToString();
                Assert.AreEqual(str, str2);
                Assert.IsTrue(sym256 == new Symbol256(str2));
            }

            for (int i = 0; i <= 128; i++)
            {
                str = new string('x', 128 - i);
                var sym128 = new Symbol128(str);
                str2 = sym128.ToString();
                Assert.AreEqual(str, str2);
                Assert.IsTrue(sym128 == new Symbol128(str2));
            }

            for (int i = 0; i <= 64; i++)
            {
                str = new string('x', 64 - i);
                var sym64 = new Symbol64(str);
                str2 = sym64.ToString();
                Assert.AreEqual(str, str2);
                Assert.IsTrue(sym64 == new Symbol64(str2));
            }

            for (int i = 0; i <= 32; i++)
            {
                str = new string('x', 32 - i);
                var sym32 = new Symbol32(str);
                str2 = sym32.ToString();
                Assert.AreEqual(str, str2);
                Assert.IsTrue(sym32 == new Symbol32(str2));
            }

            for (int i = 0; i <= 16; i++)
            {
                str = new string('x', 16 - i);
                var sym = new Symbol(str);
                str2 = sym.ToString();
                Assert.AreEqual(str, str2);
                Assert.IsTrue(sym == new Symbol(str2));
            }
        }

        [Test]
        public void CouldRoundtripPathWithCyrillicAndSpaceViaSymbol()
        {
            var str = @"C:\Users\Имя Фамилия\AppData\Roaming\";
            var sym = new Symbol256(str);
            var str2 = sym.ToString();
            Assert.AreEqual(str, str2);
        }

        [Test]
        public void LargeStringThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new Symbol256(new string('x', 257));
            });
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new Symbol128(new string('x', 129));
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new Symbol64(new string('x', 65));
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new Symbol32(new string('x', 33));
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new Symbol(new string('x', 17));
            });
        }
    }
}
