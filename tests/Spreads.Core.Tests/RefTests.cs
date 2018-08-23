// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;

namespace Spreads.Core.Tests
{
    public readonly struct MyStruct
    {
        public readonly int value;

        public MyStruct(int val)
        {
            value = val;
        }
    }

    [TestFixture]
    public class RefTests
    {
        private MyStruct myStruct;

        [Test]
        public void RefVarIsUpdated()
        {
            // some sanity check how ref var behaves

            ref var msRef = ref myStruct;
            var ms = myStruct;

            Assert.AreEqual(0, msRef.value);
            Assert.AreEqual(0, ms.value);

            myStruct = new MyStruct(1);

            Assert.AreEqual(1, msRef.value);
            Assert.AreEqual(0, ms.value);

            myStruct = new MyStruct(2);
            Assert.AreEqual(2, msRef.value);
        }
    }
}
