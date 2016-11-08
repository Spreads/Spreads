// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using System;

namespace Spreads.Core.Tests {

    [TestFixture]
    public class VariantTests {

        [Test]
        public void CouldCreateAndReadWriteInlinedVariant() {
            var v = Variant.Create<double>(123);
            Assert.AreEqual(123.0, v.Get<double>());
            Assert.Throws<InvalidOperationException>(() => {
                v.Set(456); // no implicit conversion
            });
            v.Set(456.0);
            Assert.AreEqual(456.0, v.Get<double>());
        }
    }
}
